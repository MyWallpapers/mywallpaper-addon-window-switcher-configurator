using System.Windows;
using System.Windows.Threading;
using Switcher.Core;

namespace Switcher.Windows;

internal sealed class SwitcherController : IDisposable
{
    private readonly Dispatcher dispatcher;
    private readonly KeyboardRouter keyboard;
    private readonly DispatcherTimer refresh, delay;
    private readonly SwitchSession session = new();
    private readonly SwitcherWindow window;
    private Settings settings = Settings.Defaults;
    private IReadOnlyList<WindowInfo> catalogue = [];
    private HashSet<long> eligibleMonitors = [];
    private int eligibleCount;
    private bool polling, disposed;
    private readonly bool diagnostic;
    internal SwitcherWindow Window => window;
    internal bool KeyboardInstalled => keyboard.Installed;
    internal bool SessionActive => session.Active;
    internal bool PollingEnabled => refresh.IsEnabled;
    internal SwitcherController(Dispatcher dispatcher, bool diagnostic = false)
    {
        this.dispatcher = dispatcher; this.diagnostic = diagnostic;
        window = new(HandleKey, (handle, commit) => { session.Select(handle); if (commit) Commit(); else Render(); }, Close,
            query => { RefreshSession(query); Render(); }, restore => Cancel(restore));
        keyboard = new(dispatcher, HandleKey, CanIntercept);
        refresh = new(TimeSpan.FromSeconds(1), DispatcherPriority.Background, async (_, _) => await PollAsync(), dispatcher);
        refresh.Stop(); // This DispatcherTimer constructor starts immediately; initialization owns activation.
        delay = new(DispatcherPriority.Normal, dispatcher);
        delay.Tick += (_, _) => { delay.Stop(); if (session.Active) window.Open(session, settings, !diagnostic); };
    }
    internal async Task InitializeAsync(Settings configuration, bool installHook = true)
    {
        settings = configuration;
        catalogue = configuration.Bool("enabled") ? await Task.Run(() => WindowCatalog.Enumerate(configuration.Int("maxCandidates"))) : [];
        UpdateEligibility();
        if (installHook) keyboard.Configure(configuration);
        refresh.Interval = TimeSpan.FromMilliseconds(configuration.Int("refreshMs")); if (configuration.Bool("enabled")) refresh.Start();
    }
    internal void Configure(Settings configuration)
    {
        settings = configuration;
        UpdateEligibility();
        keyboard.Configure(configuration);
        refresh.Interval = TimeSpan.FromMilliseconds(configuration.Int("refreshMs"));
        if (!configuration.Bool("enabled")) { Cancel(false); refresh.Stop(); catalogue = []; UpdateEligibility(); }
        else { refresh.Start(); _ = PollAsync(); if (session.Active) { RefreshSession(); Render(); } }
    }
    private async Task PollAsync()
    {
        if (polling || disposed || diagnostic) return;
        polling = true;
        try
        {
            var configuration = settings;
            var next = await Task.Run(() => WindowCatalog.Enumerate(configuration.Int("maxCandidates")));
            if (disposed || !settings.Bool("enabled") || !ReferenceEquals(configuration, settings)) return;
            catalogue = next;
            UpdateEligibility();
            if (session.Active)
            {
                var previous = session.Windows.Select(w => (w.Handle, w.Title, w.Minimized, w.Bounds)).ToList();
                RefreshSession();
                if (!previous.SequenceEqual(session.Windows.Select(w => (w.Handle, w.Title, w.Minimized, w.Bounds)))) Render();
            }
        }
        finally { polling = false; }
    }
    private FilterContext Context(long foreground) => new(WindowCatalog.CursorMonitor(), (long)Native.MonitorFromWindow((nint)foreground, 2));
    private List<WindowInfo> Candidates(long foreground, string query = "") => WindowFilter.Apply(catalogue, settings, Context(foreground), query);
    private void UpdateEligibility()
    {
        var eligible = WindowFilter.Apply(catalogue, settings.With(("windowScreen", "all")), new(0, 0));
        eligibleCount = eligible.Count;
        eligibleMonitors = eligible.Select(w => w.Monitor).ToHashSet();
    }
    private string query = "";
    private void RefreshSession(string? text = null)
    {
        if (text is not null) query = text;
        session.Refresh(Candidates(session.PreviousForeground, query));
    }
    private bool CanIntercept()
    {
        if (disposed || !settings.Bool("enabled") || eligibleCount == 0) return false;
        nint foreground = Native.GetForegroundWindow();
        long monitor = settings.String("windowScreen") switch { "cursor" => WindowCatalog.CursorMonitor(), "focused" => (long)Native.MonitorFromWindow(foreground, 2), _ => 0 };
        if (monitor != 0 && !eligibleMonitors.Contains(monitor)) return false;
        if (!settings.Bool("fullscreenBypass")) return true;
        if (!Native.GetWindowRect(foreground, out var rect)) return true;
        var info = new Native.MonitorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>(), Device = "" };
        if (!Native.GetMonitorInfo(Native.MonitorFromWindow(foreground, 2), ref info)) return true;
        return !(rect.Left <= info.Monitor.Left && rect.Top <= info.Monitor.Top && rect.Right >= info.Monitor.Right && rect.Bottom >= info.Monitor.Bottom);
    }
    internal void HandleKey(KeyAction command)
    {
        if (disposed) return;
        switch (command)
        {
            case KeyAction.Toggle: if (session.Active) Cancel(); else Begin(false, 1); break;
            case KeyAction.BeginAltForward: Begin(true, 1); break;
            case KeyAction.BeginAltReverse: Begin(true, -1); break;
            case KeyAction.Commit: Commit(); break;
            case KeyAction.Cancel: Cancel(); break;
            case KeyAction.Close: if (session.Current is { } current) Close(current.Handle); break;
            default:
                if (!session.Active) break;
                int delta = command switch { KeyAction.Backward or KeyAction.Left => -1, KeyAction.Up => -(window.Plan?.Columns ?? 1), KeyAction.Down => window.Plan?.Columns ?? 1, _ => 1 };
                session.Move(delta, settings.Bool("wrapNavigation")); Render(); break;
        }
    }
    internal void Begin(bool holdAlt, int direction)
    {
        if (!settings.Bool("enabled")) return;
        long foreground = (long)Native.GetForegroundWindow(); query = "";
        if (!session.Begin(Candidates(foreground), foreground, holdAlt, direction)) { keyboard.EndSession(); return; }
        if (holdAlt && settings.Int("openDelayMs") > 0)
        { delay.Interval = TimeSpan.FromMilliseconds(settings.Int("openDelayMs")); delay.Start(); }
        else window.Open(session, settings, !diagnostic);
    }
    internal void OpenDiagnostic(IReadOnlyList<WindowInfo> windows, Settings configuration)
    {
        settings = configuration;
        catalogue = windows; UpdateEligibility();
        session.Begin(windows, (long)Native.GetForegroundWindow(), false, 1);
        window.Open(session, settings, false);
    }
    private void Render() { if (window.IsVisible && session.Active) window.Render(session, settings); }
    internal void Commit()
    {
        if (!session.Active) return;
        var target = session.Current;
        End();
        if (target is not null && !Native.Activate(target)) Console.Error.WriteLine("window-switcher: Windows n’a pas accordé le focus ; la fenêtre a été signalée dans la barre des tâches.");
    }
    internal void Cancel(bool restore = true)
    {
        if (!session.Active) return;
        long previous = session.PreviousForeground;
        bool hadFocus = window.IsActive;
        End();
        if (restore && hadFocus && Native.IsWindow((nint)previous)) Native.SetForegroundWindow((nint)previous);
    }
    private void End() { delay.Stop(); keyboard.EndSession(); window.Suspend(); session.End(); query = ""; }
    private void Close(long handle)
    {
        if (session.Windows.FirstOrDefault(w => w.Handle == handle) is not { } target) return;
        if (!Native.Matches(target)) { _ = PollAsync(); return; }
        if (settings.Bool("closeConfirm"))
        {
            // End hold-to-choose before the modal dialog so releasing Alt cannot commit behind it.
            keyboard.EndSession();
            session.MakeSticky();
            window.ModalInteraction = true;
            MessageBoxResult answer;
            try { answer = MessageBox.Show(window, $"Fermer « {target.Title} » ?\nL’application peut vous demander d’enregistrer.", "Fermer la fenêtre", MessageBoxButton.OKCancel, MessageBoxImage.Question); }
            finally { window.ModalInteraction = false; Render(); }
            if (answer != MessageBoxResult.OK) return;
        }
        if (!Native.Matches(target) || !Native.PostMessage((nint)target.Handle, 0x10, 0, 0))
            Console.Error.WriteLine("window-switcher: demande de fermeture refusée par Windows.");
        _ = PollAsync();
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; refresh.Stop(); delay.Stop(); keyboard.Dispose(); window.Suspend(); session.End(); window.CloseOwned();
    }
}

