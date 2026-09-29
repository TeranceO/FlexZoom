using System;
using System.Runtime.InteropServices;

namespace FlexZoom;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect
    {
        public int Left, Top, Right, Bottom;
        public Rect(int x, int y, int w, int h) { Left = x; Top = y; Right = x + w; Bottom = y + h; }
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] internal struct RawInputDevice { public ushort UsagePage, Usage; public uint Flags; public nint Target; }
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterRawInputDevices(RawInputDevice[] devices, uint count, uint size);
    [StructLayout(LayoutKind.Sequential)] internal struct Transform
    {
        public float A, B, C, D, E, F, G, H, I;
        public Transform(float zoom) { A = E = zoom; I = 1; B = C = D = F = G = H = 0; }
    }
    [DllImport("Magnification.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool MagInitialize();
    [DllImport("Magnification.dll")] internal static extern bool MagUninitialize();
    [DllImport("Magnification.dll", SetLastError = true)] internal static extern bool MagSetWindowSource(nint hwnd, Rect rect);
    [DllImport("Magnification.dll", SetLastError = true)] internal static extern bool MagSetWindowTransform(nint hwnd, ref Transform transform);
    [DllImport("Magnification.dll", SetLastError = true)] internal static extern bool MagSetWindowFilterList(nint hwnd, uint mode, int count, nint[] handles);
    [DllImport("Magnification.dll")] internal static extern bool MagGetWindowSource(nint hwnd, out Rect rect);
    [DllImport("Magnification.dll")] internal static extern bool MagGetWindowTransform(nint hwnd, out Transform transform);
    [DllImport("Magnification.dll", SetLastError = true)] internal static extern bool MagSetColorEffect(nint hwnd, float[] matrix);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern nint CreateWindowEx(uint exStyle, string className, string title, uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll")] internal static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll")] internal static extern bool SetLayeredWindowAttributes(nint hwnd, uint color, byte alpha, uint flags);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint hwnd, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(nint hwnd, int command);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern nint MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("shcore.dll")] internal static extern int GetDpiForMonitor(nint monitor, int type, out uint x, out uint y);
    [DllImport("user32.dll")] internal static extern int SetWindowRgn(nint hwnd, nint region, bool redraw);
    [DllImport("gdi32.dll")] internal static extern nint CreateEllipticRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] internal static extern nint CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(nint obj);
    [DllImport("user32.dll")] internal static extern bool InvalidateRect(nint hwnd, nint rect, bool erase);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint hwnd);
}
