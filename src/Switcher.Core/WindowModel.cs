namespace Switcher.Core;

public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
}

public sealed record WindowInfo(long Handle, uint ProcessId, ulong ProcessTime, string Title,
    string Application, string ClassName, long Monitor, int ScreenIndex, RectD Bounds,
    bool Minimized = false, bool Cloaked = false, bool Visible = true, bool ToolWindow = false,
    bool AppWindow = false, bool NoActivate = false, long Owner = 0, bool Hung = false);

public readonly record struct FilterContext(long CursorMonitor, long FocusedMonitor);

public static class WindowFilter
{
    private static readonly HashSet<string> ShellClasses = new(StringComparer.OrdinalIgnoreCase)
        { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "DV2ControlHost", "MultitaskingViewFrame" };

    public static List<WindowInfo> Apply(IEnumerable<WindowInfo> source, Settings settings,
        FilterContext context, string query = "")
    {
        var include = settings.Applications("includeApplications");
        var exclude = settings.Applications("excludeApplications");
        long monitor = settings.String("windowScreen") switch
            { "cursor" => context.CursorMonitor, "focused" => context.FocusedMonitor, _ => 0 };
        double minimum = settings.Number("minWindowSize");
        var windows = source.Where(w => w.Visible && !w.Cloaked && !ShellClasses.Contains(w.ClassName)
                && (w.AppWindow || (!w.ToolWindow && !w.NoActivate && w.Owner == 0)))
            .Where(w => monitor == 0 || w.Monitor == monitor)
            .Where(w => settings.String("minimized") switch { "exclude" => !w.Minimized, "only" => w.Minimized, _ => true })
            .Where(w => !settings.Bool("hideUntitled") || !string.IsNullOrWhiteSpace(w.Title))
            .Where(w => !settings.Bool("hideHung") || !w.Hung)
            .Where(w => !settings.Bool("hideGhosts") || (!w.ClassName.Equals("Ghost", StringComparison.OrdinalIgnoreCase)
                && (w.Minimized || w.Bounds.Width >= minimum || w.Bounds.Height >= minimum)))
            .Where(w => (include.Count == 0 || include.Contains(w.Application)) && !exclude.Contains(w.Application))
            .Where(w => string.IsNullOrEmpty(query) || w.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || w.Application.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .DistinctBy(w => w.Handle).Take(settings.Int("maxCandidates")).ToList();
        IEnumerable<WindowInfo> sorted = settings.String("sort") switch
        {
            "title" => windows.OrderBy(w => w.Title, StringComparer.CurrentCultureIgnoreCase),
            "application" => windows.OrderBy(w => w.Application, StringComparer.CurrentCultureIgnoreCase).ThenBy(w => w.Title),
            _ => windows
        };
        if (settings.Bool("groupApplications"))
        {
            var groupOrder = windows.Select(w => w.Application).Distinct(StringComparer.OrdinalIgnoreCase)
                .Select((app, i) => (app, i)).ToDictionary(x => x.app, x => x.i, StringComparer.OrdinalIgnoreCase);
            sorted = sorted.OrderBy(w => groupOrder[w.Application]);
        }
        return sorted.ToList();
    }
}

public sealed class SwitchSession
{
    public IReadOnlyList<WindowInfo> Windows { get; private set; } = [];
    public int Selected { get; private set; }
    public WindowInfo? Current => Windows.Count == 0 ? null : Windows[Selected];
    public bool Active { get; private set; }
    public bool HoldAlt { get; private set; }
    public long PreviousForeground { get; private set; }

    public bool Begin(IReadOnlyList<WindowInfo> windows, long foreground, bool holdAlt, int direction)
    {
        Windows = windows;
        PreviousForeground = foreground;
        HoldAlt = holdAlt;
        Active = windows.Count != 0;
        int index = windows.ToList().FindIndex(w => w.Handle == foreground);
        Selected = index < 0 ? 0 : index;
        if (holdAlt && Active) Move(direction, true);
        return Active;
    }
    public void Refresh(IReadOnlyList<WindowInfo> windows)
    {
        long handle = Current?.Handle ?? 0;
        Windows = windows;
        int retained = windows.ToList().FindIndex(w => w.Handle == handle);
        Selected = retained >= 0 ? retained : Math.Clamp(Selected, 0, Math.Max(0, windows.Count - 1));
    }
    public void Move(int delta, bool wrap)
    {
        if (!Active || Windows.Count == 0) return;
        Selected = wrap ? ((Selected + delta) % Windows.Count + Windows.Count) % Windows.Count
            : Math.Clamp(Selected + delta, 0, Windows.Count - 1);
    }
    public void Select(long handle)
    {
        int index = Windows.ToList().FindIndex(w => w.Handle == handle);
        if (index >= 0) Selected = index;
    }
    public void End() { Active = false; HoldAlt = false; Windows = []; Selected = 0; }
    public void MakeSticky() => HoldAlt = false;
}

