using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Switcher.Core;

namespace Switcher.Windows;

internal static class Qualification
{
    [DllImport("user32.dll")] private static extern nint GetDC(nint window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint window, nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleBitmap(nint dc, int width, int height);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(nint destination, int x, int y, int width, int height, nint source, int sx, int sy, uint op);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);

    internal static void ShowFixtures(string[] args)
    {
        if (args.Length is < 1 or > 2) { Application.Current.Shutdown(1); return; }
        string path = Path.GetFullPath(args[0]);
        var monitor = WindowCatalog.Monitors().First(m => m.Primary);
        var windows = new List<Window>();
        string[] titles = ["Atlas — Cartographie", "Journal — Notes", "Studio — Composition"];
        string[] colors = ["#1D4D58", "#ECE1C8", "#312A50"];
        for (int i = 0; i < titles.Length; ++i)
        {
            var body = new Grid { Background = (Brush)new BrushConverter().ConvertFromString(colors[i])! };
            var content = new StackPanel { Margin = new Thickness(32) };
            content.Children.Add(new TextBlock { Text = titles[i].Split('—')[0].Trim(), FontSize = 32, Foreground = i == 1 ? Brushes.Black : Brushes.White, FontWeight = FontWeights.SemiBold });
            content.Children.Add(new TextBlock { Text = "Fenêtre de test dédiée à la qualification A09", FontSize = 13, Foreground = i == 1 ? Brushes.DarkSlateGray : Brushes.LightGray, Margin = new Thickness(0, 8, 0, 30) });
            for (int row = 0; row < 4; ++row)
            {
                var line = new Border { Height = 28 + row * 4, Margin = new Thickness(0, 0, 0, 12), CornerRadius = new CornerRadius(8),
                    Background = i == 1 ? new SolidColorBrush(Color.FromRgb(204, 189, 153)) : new SolidColorBrush(Color.FromArgb(80, 170, 225, 230)) };
                content.Children.Add(line);
            }
            body.Children.Add(content);
            var window = new Window { Title = titles[i], Width = 480 + i * 30, Height = 330 + i * 15,
                Left = monitor.Work.X + 40 + i * 45, Top = monitor.Work.Y + 50 + i * 35, Content = body, ShowActivated = false };
            window.Show(); windows.Add(window);
        }
        File.WriteAllText(path, JsonSerializer.Serialize(windows.Select(w => (long)new WindowInteropHelper(w).Handle).ToArray()));
        int lifetimeSeconds = args.Length == 2 ? Math.Clamp(int.Parse(args[1]), 40, 180) : 40;
        var lifetime = new DispatcherTimer { Interval = TimeSpan.FromSeconds(lifetimeSeconds) };
        lifetime.Tick += (_, _) => Application.Current.Shutdown(); lifetime.Start();
    }
    internal static async Task RunAsync(string[] args)
    {
        if (args.Length is < 1 or > 2) { Environment.ExitCode = 1; Application.Current.Shutdown(1); return; }
        string directory = Path.GetFullPath(args[0]); Directory.CreateDirectory(directory);
        string fixtureFile = Path.Combine(directory, "fixture-handles.json");
        File.Delete(fixtureFile);
        var results = new List<object>();
        bool soak = args.Length == 2 && args[1] == "--soak";
        Process? fixture = null;
        SwitcherController? controller = null;
        try
        {
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
            start.ArgumentList.Add("--fixture"); start.ArgumentList.Add(fixtureFile);
            if (soak) start.ArgumentList.Add("180");
            fixture = Process.Start(start) ?? throw new InvalidOperationException("Fixture non démarrée.");
            for (int attempt = 0; !File.Exists(fixtureFile) && attempt < 100; ++attempt) await Task.Delay(100);
            if (!File.Exists(fixtureFile)) throw new TimeoutException("Fixture sans fenêtres.");
            long[] handles = JsonSerializer.Deserialize<long[]>(File.ReadAllBytes(fixtureFile))!;
            var all = WindowCatalog.Enumerate(240);
            var samples = all.Where(w => handles.Contains(w.Handle)).OrderBy(w => w.Title).ToList();
            if (samples.Count != 3) throw new InvalidOperationException("Les trois fenêtres de fixture n’ont pas été observées.");
            controller = new(Application.Current.Dispatcher, true);
            var settings = Settings.Defaults.With(("reduceMotion", true), ("desktopDim", 0), ("panelWidthPct", 75), ("panelHeightPct", 75));
            foreach (string profile in args.Length == 1 ? new[] { "boreal", "midnight", "rose", "paper", "cinema", "classic", "flip" } : Array.Empty<string>())
            {
                var configured = settings.With(("profile", profile));
                controller.OpenDiagnostic(samples, configured);
                await Task.Delay(180);
                controller.Window.EndAnimations(); Native.DwmFlush();
                var dpi = VisualTreeHelper.GetDpi(controller.Window);
                string capture = Path.Combine(directory, $"profile-{profile}.png");
                CaptureOwnedWindow(controller.Window, capture);
                results.Add(new { profile, layout = configured.String("layout"), thumbnails = controller.Window.SuccessfulPreviews,
                    glass = controller.Window.GlassApplied, dpi = new[] { dpi.DpiScaleX * 96, dpi.DpiScaleY * 96 }, capture = Path.GetFileName(capture),
                    width = controller.Window.ActualWidth, height = controller.Window.ActualHeight });
                controller.Cancel(false);
                if (Thumbnail.ActiveCount != 0) throw new InvalidOperationException("Une miniature n’a pas été libérée.");
            }
            foreach (string layout in args.Length == 1 || soak ? new[] { "grid", "list", "classic", "flip", "desktop" } : new[] { args[1] })
            {
                controller.OpenDiagnostic(samples, settings.With(("profile", "midnight"), ("layout", layout)));
                await Task.Delay(120); controller.Window.EndAnimations(); Native.DwmFlush();
                CaptureOwnedWindow(controller.Window, Path.Combine(directory, $"layout-{layout}.png"));
                results.Add(new { layout, thumbnails = controller.Window.SuccessfulPreviews, cards = controller.Window.Plan?.Cards.Count });
                controller.Cancel(false);
            }
            controller.OpenDiagnostic(samples, settings.With(("profile", "midnight")));
            await Task.Delay(100);
            Native.PostMessage(new WindowInteropHelper(controller.Window).Handle, 0x112, 0xf060, 0);
            await Task.Delay(100);
            bool closeClean = !controller.SessionActive && !controller.Window.IsVisible && Thumbnail.ActiveCount == 0;
            controller.OpenDiagnostic(samples, settings.With(("profile", "midnight")));
            await Task.Delay(100);
            bool reopened = controller.SessionActive && controller.Window.IsVisible && controller.Window.SuccessfulPreviews == 3;
            if (!closeClean || !reopened) throw new InvalidOperationException("SC_CLOSE → réouverture du sélecteur a échoué.");
            results.Add(new { test = "syscommand-close-and-reopen", closeClean, reopened });
            controller.Cancel(false);
            controller.OpenDiagnostic(samples, settings.With(("profile", "custom"), ("desktopDim", 0), ("shadowOpacity", 0)));
            controller.Configure(settings.With(("profile", "custom"), ("desktopDim", 55), ("shadowOpacity", 45)));
            await Task.Delay(100);
            var changed = controller.Window.AuxiliaryState;
            if (!changed.DimmerVisible || Math.Abs(changed.DimmerOpacity - .55) > .001 || !changed.ShadowVisible)
                throw new InvalidOperationException("Réglages à chaud du fond et de l’ombre non appliqués.");
            controller.Configure(settings.With(("profile", "custom"), ("desktopDim", 0), ("shadowOpacity", 0)));
            await Task.Delay(100);
            var removed = controller.Window.AuxiliaryState;
            if (removed.DimmerVisible || removed.ShadowVisible) throw new InvalidOperationException("Fond ou ombre encore visible après désactivation.");
            controller.Configure(settings.With(("enabled", false)));
            await Task.Delay(100);
            if (controller.Window.AuxiliaryState.CachedIcons != 0 || Thumbnail.ActiveCount != 0 || controller.PollingEnabled || controller.KeyboardInstalled)
                throw new InvalidOperationException("Ressources conservées après l’arrêt du sélecteur.");
            results.Add(new { test = "live-dimmer-shadow-and-disable", applied = true, removed = true, cachedIconsAfterDisable = 0 });
            var memorySamples = new List<object>();
            using var process = Process.GetCurrentProcess();
            int cycleCount = soak ? 1500 : 150;
            for (int cycle = 0; cycle < cycleCount; ++cycle)
            {
                string layout = new[] { "grid", "list", "classic", "flip", "desktop" }[cycle % 5];
                controller.OpenDiagnostic(samples, settings.With(("profile", "midnight"), ("layout", layout), ("shadowOpacity", 0)));
                await Task.Delay(35);
                controller.Cancel(false);
                await Task.Delay(15);
                if (Thumbnail.ActiveCount != 0 || controller.Window.AuxiliaryState.CachedIcons != 0 || controller.SessionActive)
                    throw new InvalidOperationException($"Nettoyage incomplet au cycle {cycle + 1}.");
                if (cycle % 10 == 9)
                {
                    process.Refresh();
                    memorySamples.Add(new { cycle = cycle + 1, privateBytes = process.PrivateMemorySize64, handles = process.HandleCount,
                        managedBytes = GC.GetTotalMemory(false), remainingThumbnails = Thumbnail.ActiveCount, cachedIcons = controller.Window.AuxiliaryState.CachedIcons });
                }
            }
            results.Add(new { test = "open-close-layout-cycles", cycles = cycleCount, forcedGarbageCollections = 0, memorySamples });
            Native.ShowWindowAsync((nint)samples[0].Handle, 6); await Task.Delay(150);
            var reduced = WindowCatalog.Enumerate(240).First(w => w.Handle == samples[0].Handle);
            if (!reduced.Minimized) throw new InvalidOperationException("Réduction non observée.");
            controller.OpenDiagnostic([reduced], settings); await Task.Delay(100);
            results.Add(new { test = "minimized-placeholder", minimized = reduced.Minimized, liveThumbnails = controller.Window.SuccessfulPreviews });
            controller.Cancel(false);
            if (!Native.PostMessage((nint)samples[1].Handle, 0x10, 0, 0)) throw new InvalidOperationException("Fermeture refusée.");
            await Task.Delay(150);
            results.Add(new { test = "normal-close-owned-fixture", closed = !Native.IsWindow((nint)samples[1].Handle) });
            controller.Dispose(); controller = null;
            File.WriteAllText(Path.Combine(directory, "windows-result.json"), JsonSerializer.Serialize(new
            {
                dateUtc = DateTime.UtcNow, os = Environment.OSVersion.VersionString, architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                source = "Real HWND fixtures on the Windows host; no Explorer injection, no Task View modification.",
                monitors = WindowCatalog.Monitors(), results, remainingThumbnails = Thumbnail.ActiveCount
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception e)
        { File.WriteAllText(Path.Combine(directory, "failure.txt"), e.ToString()); Environment.ExitCode = 1; }
        finally
        {
            controller?.Dispose();
            if (fixture is not null) { if (!fixture.HasExited) fixture.Kill(true); fixture.Dispose(); }
            Application.Current.Shutdown(Environment.ExitCode);
        }
    }
    private static void CaptureOwnedWindow(Window window, string path)
    {
        // Capture only the known destination HWND's rectangle. Private screenshots stay outside the distributable bundle.
        nint hwnd = new WindowInteropHelper(window).Handle;
        Native.GetWindowRect(hwnd, out var rect);
        int width = rect.Right - rect.Left, height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0 || width > 8000 || height > 8000) throw new InvalidOperationException("Capture hors limites.");
        nint screen = GetDC(0), memory = CreateCompatibleDC(screen), bitmap = CreateCompatibleBitmap(screen, width, height), old = SelectObject(memory, bitmap);
        try
        {
            if (!BitBlt(memory, 0, 0, width, height, screen, rect.Left, rect.Top, 0x00cc0020)) throw new InvalidOperationException("Capture GDI refusée.");
            var image = Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using var stream = File.Create(path); encoder.Save(stream);
        }
        finally { SelectObject(memory, old); DeleteObject(bitmap); DeleteDC(memory); ReleaseDC(0, screen); }
    }
}

