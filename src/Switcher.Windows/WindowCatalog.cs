using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Switcher.Core;

namespace Switcher.Windows;

internal sealed record MonitorData(long Handle, int Index, RectD Bounds, RectD Work, bool Primary);

internal static class WindowCatalog
{
    internal static List<MonitorData> Monitors()
    {
        var result = new List<MonitorData>();
        Native.EnumDisplayMonitors(0, 0, (nint monitor, nint dc, ref Native.Rect rectangle, nint parameter) =>
        {
            var info = new Native.MonitorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>(), Device = "" };
            if (Native.GetMonitorInfo(monitor, ref info)) result.Add(new((long)monitor, result.Count + 1, info.Monitor.ToRect(), info.Work.ToRect(), (info.Flags & 1) != 0));
            return true;
        }, 0);
        return result;
    }
    internal static long CursorMonitor()
    {
        Native.GetCursorPos(out var cursor);
        return (long)Native.MonitorFromPoint(cursor, 2);
    }
    internal static List<WindowInfo> Enumerate(int limit)
    {
        var monitors = Monitors();
        var result = new List<WindowInfo>();
        var processes = new Dictionary<uint, (string Name, ulong Time)>();
        Native.EnumWindows((hwnd, parameter) =>
        {
            // Metadata APIs do not send synchronous messages into another application.
            if (!Native.IsWindowVisible(hwnd) || hwnd == Native.GetShellWindow()) return true;
            Native.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == Environment.ProcessId) return true;
            long style = (long)Native.GetWindowLongPtr(hwnd, -20);
            if ((style & 0x40000) == 0 && (style & (0x80 | 0x08000000)) != 0) return true;
            var title = new StringBuilder(1024); Native.GetWindowText(hwnd, title, title.Capacity);
            var cls = new StringBuilder(256); Native.GetClassName(hwnd, cls, cls.Capacity);
            Native.GetWindowRect(hwnd, out var rect);
            Native.DwmGetWindowAttribute(hwnd, 14, out uint cloaked, 4);
            nint monitor = Native.MonitorFromWindow(hwnd, 2);
            if (!processes.TryGetValue(pid, out var process))
            {
                nint handle = Native.OpenProcess(0x1000, false, pid);
                string name = "Application"; ulong time = 0;
                if (handle != 0)
                {
                    try
                    {
                        var path = new StringBuilder(32768); uint length = (uint)path.Capacity;
                        if (Native.QueryFullProcessImageName(handle, 0, path, ref length)) name = Path.GetFileNameWithoutExtension(path.ToString());
                        Native.GetProcessTimes(handle, out time, out _, out _, out _);
                    }
                    finally { Native.CloseHandle(handle); }
                }
                process = (name, time); processes.Add(pid, process);
            }
            result.Add(new((long)hwnd, pid, process.Time, title.ToString(), process.Name, cls.ToString(), (long)monitor,
                monitors.Find(m => m.Handle == (long)monitor)?.Index ?? 1, rect.ToRect(), Native.IsIconic(hwnd), cloaked != 0,
                true, (style & 0x80) != 0, (style & 0x40000) != 0, (style & 0x08000000) != 0,
                (long)Native.GetWindow(hwnd, 4), Native.IsHungAppWindow(hwnd)));
            return result.Count < limit;
        }, 0);
        return result;
    }
    internal static ImageSource? Icon(WindowInfo info)
    {
        if (!Native.Matches(info) || info.Hung) return null;
        // Only the icon request crosses the target's message queue, bounded to 30 ms per displayed card.
        Native.SendMessageTimeout((nint)info.Handle, 0x7f, 2, 0, 2, 30, out var result);
        nint icon = (nint)result;
        if (icon == 0) icon = Native.GetClassLongPtr((nint)info.Handle, -14);
        if (icon == 0) icon = Native.LoadIcon(0, 32512);
        if (icon == 0) return null;
        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            source.Freeze(); return source;
        }
        catch (ArgumentException) { return null; }
        // WM_GETICON / class icons are borrowed: never DestroyIcon.
    }
}

