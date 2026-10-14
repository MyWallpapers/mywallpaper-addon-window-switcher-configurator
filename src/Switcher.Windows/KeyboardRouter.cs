using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Switcher.Core;

namespace Switcher.Windows;

internal enum KeyAction { Toggle, BeginAltForward, BeginAltReverse, Forward, Backward, Left, Right, Up, Down, Commit, Cancel, Close }

internal sealed class KeyboardRouter : IDisposable
{
    private readonly Native.HookProc callback;
    private readonly Dispatcher dispatcher;
    private readonly Action<KeyAction> action;
    private readonly Func<bool> canIntercept;
    private readonly HashSet<uint> consumed = [];
    private nint hook;
    private Settings settings = Settings.Defaults;
    private bool holdAlt;
    internal bool Installed => hook != 0;
    internal KeyboardRouter(Dispatcher dispatcher, Action<KeyAction> action, Func<bool> canIntercept)
    {
        this.dispatcher = dispatcher; this.action = action; this.canIntercept = canIntercept; callback = OnKey;
    }
    internal void Configure(Settings value)
    {
        settings = value; holdAlt = false;
        if (!value.Bool("enabled")) { Dispose(); return; }
        if (hook != 0) return;
        hook = Native.SetWindowsHookEx(13, callback, Native.GetModuleHandle(null), 0);
        if (hook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "Impossible d’activer le raccourci du sélecteur.");
    }
    internal void EndSession() => holdAlt = false;
    private static bool Down(int key) => Native.GetAsyncKeyState(key) < 0;
    private void Dispatch(KeyAction key) => dispatcher.BeginInvoke(() => action(key), DispatcherPriority.Input);
    private nint OnKey(int code, nint message, nint pointer)
    {
        if (code < 0) return Native.CallNextHookEx(hook, code, message, pointer);
        var data = Marshal.PtrToStructure<Native.KeyData>(pointer);
        if ((data.Flags & 0x10) != 0) return Native.CallNextHookEx(hook, code, message, pointer);
        bool up = message == 0x101 || message == 0x105;
        bool alt = Down(0x12), ctrl = Down(0x11), shift = Down(0x10), win = Down(0x5b) || Down(0x5c);
        if (up)
        {
            if (holdAlt && data.Key is 0x12 or 0xa4 or 0xa5) { holdAlt = false; Dispatch(KeyAction.Commit); }
            if (consumed.Remove(data.Key)) return 1;
            return Native.CallNextHookEx(hook, code, message, pointer);
        }
        bool dedicated = !win && (settings.String("shortcut") switch
        {
            "ctrlAltSpace" => data.Key == 0x20 && ctrl && alt && !shift,
            "ctrlShiftSpace" => data.Key == 0x20 && ctrl && shift && !alt,
            "altBacktick" => data.Key == 0xc0 && alt && !ctrl && !shift,
            _ => false
        });
        if (dedicated)
        {
            if (consumed.Add(data.Key)) Dispatch(KeyAction.Toggle);
            return 1;
        }
        if (settings.String("activationMode") == "altTab" && data.Key == 9 && alt && !ctrl && !win)
        {
            if (!holdAlt && !canIntercept()) return Native.CallNextHookEx(hook, code, message, pointer);
            Dispatch(holdAlt ? shift ? KeyAction.Backward : KeyAction.Forward : shift ? KeyAction.BeginAltReverse : KeyAction.BeginAltForward);
            holdAlt = true; consumed.Add(data.Key); return 1;
        }
        if (holdAlt)
        {
            KeyAction? key = data.Key switch
                { 0x25 => KeyAction.Left, 0x27 => KeyAction.Right, 0x26 => KeyAction.Up, 0x28 => KeyAction.Down,
                  0x0d => KeyAction.Commit, 0x1b => KeyAction.Cancel, 0x2e => KeyAction.Close, _ => null };
            if (key is { } command) { Dispatch(command); consumed.Add(data.Key); return 1; }
        }
        return Native.CallNextHookEx(hook, code, message, pointer);
    }
    public void Dispose()
    {
        holdAlt = false; consumed.Clear();
        if (hook == 0) return;
        Native.UnhookWindowsHookEx(hook); hook = 0;
    }
}

