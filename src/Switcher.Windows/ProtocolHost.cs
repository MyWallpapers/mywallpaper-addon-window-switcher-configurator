using System.IO;
using System.Text.Json;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Threading;
using Switcher.Core;

namespace Switcher.Windows;

internal sealed class ProtocolHost
{
    private readonly Dispatcher dispatcher;
    private readonly SwitcherController controller;
    private readonly Channel<object> output = Channel.CreateBounded<object>(new BoundedChannelOptions(16) { SingleReader = true, SingleWriter = true });
    private readonly CancellationTokenSource stopping = new();
    internal ProtocolHost(Dispatcher dispatcher, SwitcherController controller) { this.dispatcher = dispatcher; this.controller = controller; }
    private async Task Send(object frame) => await output.Writer.WriteAsync(frame, stopping.Token);
    internal async Task RunAsync()
    {
        var writer = Task.Run(async () =>
        {
            using Stream stream = Console.OpenStandardOutput();
            await foreach (var frame in output.Reader.ReadAllAsync(stopping.Token))
                await Wire.WriteAsync(stream, frame, stopping.Token);
        });
        _ = writer.ContinueWith(_ =>
        {
            stopping.Cancel();
            dispatcher.BeginInvoke(() => { controller.Dispose(); Application.Current.Shutdown(1); });
        }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        try
        {
            using Stream input = Console.OpenStandardInput();
            var first = await Wire.ReadAsync(input, stopping.Token) ?? throw new InvalidDataException("Entrée fermée avant init.");
            Wire.ValidateInit(first);
            var settings = Settings.Parse(first);
            await Send(new { type = "progress", v = Wire.Version });
            await dispatcher.InvokeAsync(() => controller.InitializeAsync(settings)).Task.Unwrap();
            await Send(new { type = "ready", v = Wire.Version });
            while (await Wire.ReadAsync(input, stopping.Token) is { } frame)
            {
                switch (Wire.FrameType(frame))
                {
                    case "shutdown": return;
                    case "settings":
                        try
                        {
                            Settings next = Settings.Parse(frame);
                            await dispatcher.InvokeAsync(() => controller.Configure(next));
                        }
                        catch (ArgumentException e)
                        {
                            // Full snapshots are transactional. The previous snapshot stays accepted.
                            Console.Error.WriteLine($"window-switcher: réglages refusés : {e.Message}");
                            await Send(new { type = "message", v = Wire.Version, target = "broadcast", payload = new { kind = "window-switcher.settingsRejected", reason = e.Message } });
                        }
                        break;
                    case "message":
                        if (!frame.TryGetProperty("surface", out _) || !frame.TryGetProperty("instanceId", out _) || !frame.TryGetProperty("displayIndex", out _))
                            throw new InvalidDataException("Message sans route explicite.");
                        await HandleMessage(frame.GetProperty("payload")); break;
                    default: throw new InvalidDataException("Opération process-v2 incompatible.");
                }
            }
        }
        catch (Exception e) when (e is InvalidDataException or JsonException or ArgumentException or KeyNotFoundException or InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            await Send(new { type = "error", v = Wire.Version, message = e.Message, code = "window_switcher_failed" });
        }
        finally
        {
            output.Writer.TryComplete();
            await Task.WhenAny(writer, Task.Delay(500)); stopping.Cancel();
            await dispatcher.InvokeAsync(() => { controller.Dispose(); Application.Current.Shutdown(); });
        }
    }
    private async Task HandleMessage(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty("op", out var op)) throw new InvalidDataException("Commande de sélecteur manquante.");
        switch (op.GetString())
        {
            case "open": await dispatcher.InvokeAsync(() => controller.Begin(false, 1)); break;
            case "cancel": await dispatcher.InvokeAsync(() => controller.Cancel(false)); break;
            case "probeTaskView":
                await Send(new { type = "message", v = Wire.Version, target = "broadcast", payload = new
                { kind = "window-switcher.capability", capability = "native-task-view-styling", available = false, reason = "private-shell-symbols-unavailable", contract = "private-shell-symbols.v1" } });
                break;
            case "status":
                var status = await dispatcher.InvokeAsync(() => new { keyboardInstalled = controller.KeyboardInstalled, pollingEnabled = controller.PollingEnabled, sessionOpen = controller.SessionActive, livePreviews = Thumbnail.ActiveCount });
                await Send(new { type = "message", v = Wire.Version, target = "broadcast", payload = new { kind = "window-switcher.status", status } });
                break;
            default: throw new InvalidDataException("Commande de sélecteur inconnue.");
        }
    }
}

