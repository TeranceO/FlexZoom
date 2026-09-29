using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Interop;

namespace FlexZoom;

internal sealed class Hotkey : IDisposable
{
    private readonly HwndSource source;
    private int activeId;
    private uint modifiers, key;
    public bool Registered => activeId != 0;
    public bool Recording { get; set; }
    public event Action? Pressed;
    public event Action? CurrentShortcutRecorded;
    public Hotkey(nint handle) { source = HwndSource.FromHwnd(handle)!; source.AddHook(Hook); }
    public bool TrySet(uint newModifiers, uint newKey)
    {
        if (!IsAllowed(newModifiers, newKey)) return false;
        if (Registered && modifiers == newModifiers && key == newKey) return true;
        int next = activeId == 1 ? 2 : 1;
        if (!Native.RegisterHotKey(source.Handle, next, newModifiers | 0x4000, newKey)) return false;
        if (Registered) Native.UnregisterHotKey(source.Handle, activeId);
        activeId = next; modifiers = newModifiers; key = newKey; return true;
    }
    internal static bool IsAllowed(uint mods, uint vk) => (mods & ~7u) == 0 && vk is >= 0x08 and <= 0xFE && vk is not (0x10 or 0x11 or 0x12 or 0x5B or 0x5C) && (mods != 0 || vk is >= 0x70 and <= 0x87);
    public static string Label(uint mods, uint vk)
    {
        var parts = new List<string>();
        if ((mods & 2) != 0) parts.Add("Ctrl"); if ((mods & 1) != 0) parts.Add("Alt"); if ((mods & 4) != 0) parts.Add("Shift");
        parts.Add(new KeyConverter().ConvertToString(KeyInterop.KeyFromVirtualKey((int)vk)) ?? "?");
        return string.Join(" + ", parts);
    }
    private nint Hook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    { if (Registered && msg == 0x312 && (int)wParam == activeId) { handled = true; if (Recording) CurrentShortcutRecorded?.Invoke(); else Pressed?.Invoke(); } return 0; }
    public void Disable()
    {
        if (Registered) Native.UnregisterHotKey(source.Handle, activeId);
        activeId = 0; Recording = false;
    }
    public void Dispose() { Disable(); source.RemoveHook(Hook); }
}
