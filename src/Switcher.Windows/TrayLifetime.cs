using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace Switcher.Windows;

internal sealed class TrayLifetime : IDisposable
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct NotifyData
    {
        public uint Size; public nint Window; public uint Id, Flags, Callback; public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags; public Guid Guid; public nint BalloonIcon;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern bool Shell_NotifyIcon(uint message, ref NotifyData data);
    private readonly HwndSource source;
    private NotifyData data;
    private readonly ContextMenu menu;
    private readonly Action open;
    internal TrayLifetime(Action exit, Action open)
    {
        this.open = open;
        source = new HwndSource(new HwndSourceParameters("MyWallpaper Window Switcher tray") { Width = 0, Height = 0, WindowStyle = 0, ExtendedWindowStyle = 0x80 });
        source.AddHook(Hook);
        data = new NotifyData { Size = (uint)Marshal.SizeOf<NotifyData>(), Window = source.Handle, Id = 1, Flags = 1 | 2 | 4, Callback = 0x8001,
            Icon = Native.LoadIcon(0, 32512), Tip = "Sélecteur de fenêtres MyWallpaper", Info = "", InfoTitle = "" };
        if (!Shell_NotifyIcon(0, ref data)) throw new InvalidOperationException("Impossible d’afficher la commande d’arrêt dans la zone de notification.");
        menu = new ContextMenu();
        var show = new MenuItem { Header = "Ouvrir le sélecteur" }; show.Click += (_, _) => open();
        var stop = new MenuItem { Header = "Arrêter le sélecteur" }; stop.Click += (_, _) => exit();
        menu.Items.Add(show); menu.Items.Add(new Separator()); menu.Items.Add(stop);
    }
    private nint Hook(nint window, int message, nint wparam, nint lparam, ref bool handled)
    {
        if (message != 0x8001) return 0;
        if ((int)lparam == 0x203) open();
        if ((int)lparam == 0x205) { menu.Placement = PlacementMode.MousePoint; menu.IsOpen = true; }
        return 0;
    }
    public void Dispose() { Shell_NotifyIcon(2, ref data); source.Dispose(); }
}

