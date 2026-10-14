using System.IO;
using System.Text.Json;
using System.Windows;
using Switcher.Core;

namespace Switcher.Windows;

internal static class Program
{
    [STAThread]
    internal static int Main(string[] args)
    {
        if (args.FirstOrDefault() == "--fixture")
        {
            var fixture = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            fixture.Startup += (_, _) => Qualification.ShowFixtures(args.Skip(1).ToArray()); fixture.Run(); return 0;
        }
        using var single = new Mutex(true, "Local\\MyWallpaper.WindowSwitcher.Configurator", out bool owner);
        if (!owner) { Console.Error.WriteLine("Un sélecteur MyWallpaper fonctionne déjà dans cette session Windows."); return 2; }
        try
        {
            var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            if (args.FirstOrDefault() == "--qualify")
            { application.Startup += async (_, _) => await Qualification.RunAsync(args.Skip(1).ToArray()); application.Run(); return Environment.ExitCode; }
            var controller = new SwitcherController(application.Dispatcher);
            if (args.FirstOrDefault() is "--standalone" or "--preview")
            {
                Settings settings = Settings.Defaults;
                if (args.Length >= 3 && args[1] == "--settings")
                { using var json = JsonDocument.Parse(File.ReadAllBytes(args[2])); settings = Settings.Parse(json.RootElement); }
                bool preview = args[0] == "--preview";
                application.Startup += async (_, _) =>
                {
                    await controller.InitializeAsync(settings, !preview);
                    controller.Begin(false, 1);
                };
                // A local tray exposes a clear stop action without adding controls to MyWallpaper's product flow.
                using var tray = new TrayLifetime(() => { controller.Dispose(); application.Shutdown(); }, () => controller.Begin(false, 1));
                application.Run(); controller.Dispose(); return 0;
            }
            var protocol = new ProtocolHost(application.Dispatcher, controller);
            application.Startup += (_, _) => _ = Task.Run(protocol.RunAsync);
            application.Run(); controller.Dispose(); return 0;
        }
        catch (Exception e) when (e is IOException or ArgumentException or JsonException)
        { Console.Error.WriteLine($"window-switcher: {e.Message}"); return 1; }
        finally { single.ReleaseMutex(); }
    }
}

