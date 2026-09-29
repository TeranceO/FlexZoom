using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace FlexZoom;

internal sealed class LensRenderer : IDisposable
{
    private readonly DispatcherTimer timer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly nint host, magnifier;
    private readonly HwndSource inputWindow;
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private DispatcherOperation? pendingInput;
    private bool rawInputActive;
    private nint cachedMonitor;
    private Native.MonitorInfo monitorInfo;
    private double monitorScale = 1;
    private long monitorRefresh, inputQueuedAt;
    private Native.Rect lastDestination;
    private Native.Point lastCursor;
    private long inputEvents, inputUpdates, refreshes, moves, monitorQueries;
    private double inputDelayTotal, maxInputDelay;
    private Settings settings = new();
    private (int W, int H, LensShape Shape) geometry;
    public bool Enabled { get; private set; }
    internal nint Host => host;
    internal nint Magnifier => magnifier;
    public event Action<string>? Failed;

    public LensRenderer()
    {
        if (!Native.MagInitialize()) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not initialize the magnifier.");
        try
        {
            // Layered + transparent: clicks reach the original screen; no activation or taskbar button.
            host = Native.CreateWindowEx(0x080800A8, "STATIC", "Flex Zoom Lens", 0x82000004, 0, 0, 320, 320, 0, 0, 0, 0);
            if (host == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!Native.SetLayeredWindowAttributes(host, 0, 255, 2)) throw new Win32Exception(Marshal.GetLastWin32Error());
            magnifier = Native.CreateWindowEx(0, "Magnifier", "Flex Zoom Magnifier", 0x50000000, 3, 3, 314, 314, host, 0, 0, 0);
            if (magnifier == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!Native.MagSetWindowFilterList(magnifier, 0, 1, [host])) throw new Win32Exception(Marshal.GetLastWin32Error());
            inputWindow = new HwndSource(new HwndSourceParameters("Flex Zoom input")
            { WindowStyle = unchecked((int)0x80000000), ExtendedWindowStyle = 0x08000080, Width = 0, Height = 0 });
            inputWindow.AddHook(InputHook);
            timer.Tick += (_, _) => { refreshes++; Update(); };
            Apply(settings);
        }
        catch { inputWindow?.Dispose(); if (host != 0) Native.DestroyWindow(host); Native.MagUninitialize(); throw; }
    }

    public void Apply(Settings value)
    {
        var transform = new Native.Transform((float)value.Zoom);
        if (!Native.MagSetWindowTransform(magnifier, ref transform)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not set the zoom level.");
        float c = value.InvertColors ? -1 : 1, offset = value.InvertColors ? 1 : 0;
        if (!Native.MagSetColorEffect(magnifier, [c,0,0,0,0, 0,c,0,0,0, 0,0,c,0,0, 0,0,0,1,0, offset,offset,offset,0,1]))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not set the lens colors.");
        settings = value with { };
        if (Enabled) Update();
    }
    public void SetEnabled(bool enabled)
    {
        if (Enabled == enabled) return;
        Enabled = enabled;
        if (enabled)
        {
            cachedMonitor = 0;
            rawInputActive = Native.RegisterRawInputDevices([new Native.RawInputDevice { UsagePage = 1, Usage = 2, Flags = 0x100, Target = inputWindow.Handle }], 1, (uint)Marshal.SizeOf<Native.RawInputDevice>());
            Update();
            if (Enabled) { Native.ShowWindow(host, 8); timer.Start(); }
        }
        else
        {
            timer.Stop(); pendingInput?.Abort(); pendingInput = null;
            if (rawInputActive)
                Native.RegisterRawInputDevices([new Native.RawInputDevice { UsagePage = 1, Usage = 2, Flags = 1, Target = 0 }], 1, (uint)Marshal.SizeOf<Native.RawInputDevice>());
            rawInputActive = false;
            Native.ShowWindow(host, 0);
        }
    }
    private nint InputHook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0xFF && Enabled) // WM_INPUT; leave unhandled for DefWindowProc cleanup.
        {
            inputEvents++;
            // Only one queued update, using the newest cursor position instead of replaying old points.
            if (pendingInput is not { Status: DispatcherOperationStatus.Pending })
            {
                inputQueuedAt = Stopwatch.GetTimestamp();
                pendingInput = dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
                {
                    pendingInput = null;
                    if (!Enabled) return;
                    double delay = Stopwatch.GetElapsedTime(inputQueuedAt).TotalMilliseconds;
                    inputDelayTotal += delay; maxInputDelay = Math.Max(maxInputDelay, delay); inputUpdates++;
                    Update(false);
                });
            }
        }
        else if (message is 0x7E or 0x2E0 or 0x1A) cachedMonitor = 0; // display, DPI, settings
        return 0;
    }
    internal LensDiagnostics Diagnostics => new(Enabled, rawInputActive, Environment.CurrentManagedThreadId, inputEvents, inputUpdates, refreshes, moves, monitorQueries,
        inputUpdates == 0 ? 0 : inputDelayTotal / inputUpdates, maxInputDelay);
    internal static Native.Rect SourceFor(Native.Point cursor, Native.Rect lens, Native.Rect monitor, double zoom, int border)
    {
        int w = Math.Max(1, (int)Math.Ceiling((lens.Width - 2 * border) / zoom));
        int h = Math.Max(1, (int)Math.Ceiling((lens.Height - 2 * border) / zoom));
        int x = (int)Math.Round(cursor.X - (cursor.X - lens.Left - border) / zoom);
        int y = (int)Math.Round(cursor.Y - (cursor.Y - lens.Top - border) / zoom);
        return new(Math.Clamp(x, monitor.Left, monitor.Right - w), Math.Clamp(y, monitor.Top, monitor.Bottom - h), w, h);
    }
    internal void Update(bool refreshContent = true)
    {
        if (!Native.GetCursorPos(out var cursor)) return;
        var monitor = Native.MonitorFromPoint(cursor, 2);
        long now = Environment.TickCount64;
        if (monitor != cachedMonitor || now - monitorRefresh >= 1000)
        {
            var freshInfo = new Native.MonitorInfo { Size = Marshal.SizeOf<Native.MonitorInfo>() };
            if (!Native.GetMonitorInfo(monitor, ref freshInfo)) return;
            monitorInfo = freshInfo;
            monitorScale = Native.GetDpiForMonitor(monitor, 0, out uint dpi, out _) == 0 ? dpi / 96.0 : 1;
            cachedMonitor = monitor; monitorRefresh = now; monitorQueries++;
        }
        var info = monitorInfo;
        double scale = monitorScale;
        int width = Math.Min((int)(settings.Width * scale), info.Monitor.Width);
        int height = Math.Min((int)((settings.Shape == LensShape.Rectangle ? settings.Height : settings.Width) * scale), info.Monitor.Height);
        if (settings.Shape != LensShape.Rectangle) width = height = Math.Min(width, height);
        const int border = 3;
        if (geometry != (width, height, settings.Shape))
        {
            Native.SetWindowPos(host, -1, 0, 0, width, height, 0x12);
            Native.SetWindowPos(magnifier, 0, border, border, width - border * 2, height - border * 2, 0x14);
            SetRegion(host, width, height); SetRegion(magnifier, width - border * 2, height - border * 2);
            geometry = (width, height, settings.Shape);
        }
        var dest = new Native.Rect(Math.Clamp(cursor.X - width / 2, info.Monitor.Left, info.Monitor.Right - width), Math.Clamp(cursor.Y - height / 2, info.Monitor.Top, info.Monitor.Bottom - height), width, height);
        bool moved = dest.Left != lastDestination.Left || dest.Top != lastDestination.Top || dest.Width != lastDestination.Width || dest.Height != lastDestination.Height;
        if (moved)
        {
            Native.SetWindowPos(host, -1, dest.Left, dest.Top, width, height, 0x10);
            lastDestination = dest; moves++;
        }
        else if (!refreshContent && cursor.X == lastCursor.X && cursor.Y == lastCursor.Y) return;
        // At an edge the window can stay still while the pointer/source changes.
        if (!Native.MagSetWindowSource(magnifier, SourceFor(cursor, dest, info.Monitor, settings.Zoom, border)))
        {
            SetEnabled(false); Failed?.Invoke("The screen could not be magnified. Try toggling the lens again on the normal desktop."); return;
        }
        Native.InvalidateRect(magnifier, 0, false);
        lastCursor = cursor;
    }
    private void SetRegion(nint window, int width, int height)
    {
        nint region = settings.Shape == LensShape.Circle ? Native.CreateEllipticRgn(0, 0, width + 1, height + 1) : Native.CreateRectRgn(0, 0, width, height);
        if (Native.SetWindowRgn(window, region, true) == 0) Native.DeleteObject(region);
    }
    public void Dispose() { SetEnabled(false); inputWindow.RemoveHook(InputHook); inputWindow.Dispose(); Native.DestroyWindow(host); Native.MagUninitialize(); }
}
