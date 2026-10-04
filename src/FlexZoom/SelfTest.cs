using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FlexZoom;

internal static class SelfTest
{
    [DllImport("user32.dll")] private static extern nint SendMessage(nint hwnd, int message, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, nuint extraInfo);
    private static void PressShortcut(byte key)
    {
        // Exercise actual Windows hotkey delivery with a separate focused test surface.
        try
        {
            keybd_event(0x11, 0, 0, 0); keybd_event(0x12, 0, 0, 0); keybd_event(0x10, 0, 0, 0);
            keybd_event(key, 0, 0, 0);
        }
        finally
        {
            keybd_event(key, 0, 2, 0);
            keybd_event(0x10, 0, 2, 0); keybd_event(0x12, 0, 2, 0); keybd_event(0x11, 0, 2, 0);
        }
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }
    internal static Window CreateSurface()
    {
        var content = new System.Windows.Controls.StackPanel { Margin = new Thickness(40) };
        content.Children.Add(new System.Windows.Controls.TextBlock { Text = "Magnifier test surface", FontSize = 30, FontWeight = FontWeights.SemiBold });
        content.Children.Add(new System.Windows.Controls.TextBlock { Text = "Fine print: ABCDEFG 0123456789\nMove across this text to inspect live magnification.", FontSize = 14, Margin = new Thickness(0, 20, 0, 24) });
        var button = new System.Windows.Controls.Button { Content = "Click-through count: 0", Height = 100, FontSize = 24 };
        int clicks = 0; button.Click += (_, _) => button.Content = $"Click-through count: {++clicks}";
        content.Children.Add(button);
        content.Children.Add(new System.Windows.Controls.TextBlock { Text = "This separate window has no hotkey handler.\nUse the configured global shortcut to toggle Flex Zoom.", FontSize = 14, Margin = new Thickness(0, 24, 0, 0) });
        return new Window { Title = "Flex Zoom — test surface", Width = 780, Height = 450, Background = new SolidColorBrush(Color.FromRgb(22, 27, 40)), Content = content, WindowStartupLocation = WindowStartupLocation.CenterScreen };
    }
    internal static int Run(bool includeDesktop = true)
    {
        string directory = Path.Combine(Environment.CurrentDirectory, "artifacts");
        Directory.CreateDirectory(directory);
        var report = new StringBuilder();
        int checks = 0;
        string reportPath = Path.Combine(directory, includeDesktop ? "self-test.txt" : "headless-self-test.txt");
        void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); report.AppendLine("PASS " + message); checks++; }
        try
        {
            var bad = new Settings { Zoom = double.NaN, Width = 0, Height = 9000, Shape = (LensShape)999, Key = 0, Modifiers = 99, ZoomInModifiers = 99, ZoomOutKey = 0 };
            bad.Validate();
            Check(bad.Zoom == 2.5 && bad.Width == 160 && bad.Height == 600 && bad.Shape == LensShape.Circle && bad.Key == 0x5A, "Invalid settings recover to safe bounds.");
            Check(bad.ZoomInModifiers == 3 && bad.ZoomInKey == 0xBB && bad.ZoomOutModifiers == 3 && bad.ZoomOutKey == 0xBD, "Invalid zoom shortcuts recover independently to defaults.");
            string scratch = Path.Combine(directory, "test-settings.json");
            var store = new SettingsStore(scratch);
            var expected = new Settings { Accent = AccentColor.Teal, Shape = LensShape.Rectangle, Zoom = 4.25, InvertColors = true, StartInTray = true, StartWithLensOn = true, CloseToTray = false, GlobalShortcutEnabled = false, Key = 0x76, Modifiers = 0, ZoomShortcutsEnabled = false, ZoomInModifiers = 2, ZoomInKey = 0x26, ZoomOutModifiers = 4, ZoomOutKey = 0x28 };
            store.Save(expected);
            Check(store.Load(out _) == expected, "All settings survive a save/reload round trip.");
            File.WriteAllText(scratch, "{\"Zoom\":3.5,\"StartInTray\":true}");
            var migrated = store.Load(out _);
            Check(migrated.Zoom == 3.5 && migrated.StartInTray && !migrated.StartWithLensOn && migrated.CloseToTray && migrated.GlobalShortcutEnabled, "Existing preferences retain their values, start with lens off, and keep Close-to-tray as the default.");
            Check(migrated.ZoomShortcutsEnabled && migrated.ZoomInModifiers == 3 && migrated.ZoomInKey == 0xBB && migrated.ZoomOutModifiers == 3 && migrated.ZoomOutKey == 0xBD, "Existing settings acquire enabled zoom shortcuts without changing saved zoom or toggle.");
            File.WriteAllText(scratch, "invalid json");
            Check(store.Load(out var warning) == new Settings() && warning != null && File.ReadAllText(scratch) == "invalid json", "Corrupt preferences are preserved and reported.");
            File.Delete(scratch);
            string registryPath = @"Software\FlexZoom\Tests\" + Guid.NewGuid().ToString("N");
            try
            {
                // Exercise real registry I/O in an isolated key, never the user's Run key.
                var startup = new StartupRegistration(registryPath);
                using (var key = Registry.CurrentUser.CreateSubKey(registryPath)) key.SetValue("UnrelatedApp", "preserve me");
                string executable = @"C:\A folder with spaces\FlexZoom.exe";
                Check(startup.ReadCommand() == null, "Startup is initially disabled.");
                startup.SetEnabled(true, executable);
                Check(startup.ReadCommand() == "\"C:\\A folder with spaces\\FlexZoom.exe\" --startup", "Startup registration stores a quoted executable and startup argument.");
                startup.SetEnabled(false, executable);
                Check(startup.ReadCommand() == null, "Disabling startup removes the app registration.");
                using (var key = Registry.CurrentUser.OpenSubKey(registryPath)) Check((string?)key?.GetValue("UnrelatedApp") == "preserve me", "Other startup entries are preserved.");
                bool rejected = false;
                try { startup.SetEnabled(true, "relative.exe"); } catch (IOException) { rejected = true; }
                Check(rejected && startup.ReadCommand() == null, "Invalid executable paths cannot create a startup entry.");
            }
            finally { Registry.CurrentUser.DeleteSubKeyTree(registryPath, false); }
            foreach (var bounds in new[] { new Native.Rect(0, 0, 1920, 1080), new Native.Rect(-2560, -1440, 2560, 1440), new Native.Rect(1920, 0, 3840, 2160) })
            foreach (var zoom in new[] { 1.25, 2.5, 8.0 })
            foreach (var p in new[] { new Native.Point { X = bounds.Left, Y = bounds.Top }, new Native.Point { X = bounds.Right - 1, Y = bounds.Bottom - 1 }, new Native.Point { X = bounds.Left + bounds.Width / 2, Y = bounds.Top + bounds.Height / 2 } })
            {
                var destination = new Native.Rect(Math.Clamp(p.X - 160, bounds.Left, bounds.Right - 320), Math.Clamp(p.Y - 110, bounds.Top, bounds.Bottom - 220), 320, 220);
                var source = Lens.SourceFor(p, destination, bounds, zoom, 3);
                Check(source.Left >= bounds.Left && source.Right <= bounds.Right && source.Top >= bounds.Top && source.Bottom <= bounds.Bottom && source.Width > 0, $"Source is bounded at {p.X},{p.Y}, {zoom}x.");
            }
            Check(!Hotkey.IsAllowed(0, 0x41) && Hotkey.IsAllowed(0, 0x70) && Hotkey.IsAllowed(3, 0x5A), "Hotkey validation protects ordinary typing.");
            if (!includeDesktop)
            {
                report.AppendLine($"SUCCESS: {checks} headless checks passed. Native hotkeys, magnification, and UI require the interactive self-test.");
                File.WriteAllText(reportPath, report.ToString());
                return 0;
            }
            var window = new MainWindow(true); window.Show();
            var handle = new WindowInteropHelper(window).Handle;
            using (var hotkey = new Hotkey(handle))
            {
                Check(hotkey.TrySet(7, 0x87), "Native global hotkey registration succeeds.");
                Check(Native.RegisterHotKey(handle, 99, 7, 0x86), "Test conflict is reserved.");
                Check(!hotkey.TrySet(7, 0x86) && hotkey.Registered, "Conflicting replacement preserves existing hotkey.");
                Native.UnregisterHotKey(handle, 99);
                Check(hotkey.TrySet(7, 0x85), "Replacement shortcut registers successfully.");
                hotkey.Disable();
                Check(!hotkey.Registered && Native.RegisterHotKey(handle, 99, 7, 0x85), "Disabling releases the shortcut for other apps.");
                Check(!hotkey.TrySet(7, 0x85) && !hotkey.Registered, "Re-enabling detects conflicts without claiming registration.");
                Native.UnregisterHotKey(handle, 99);
                Check(hotkey.TrySet(7, 0x85), "The saved shortcut can be enabled again.");
            }
            using (var lens = new Lens())
            {
                lens.SetEnabled(true);
                var beforeBusyUi = lens.Diagnostics;
                Thread.Sleep(140); // Deliberately block the settings dispatcher.
                var afterBusyUi = lens.Diagnostics;
                Check(afterBusyUi.WorkerThreadId != Environment.CurrentManagedThreadId && afterBusyUi.TimerRefreshes > beforeBusyUi.TimerRefreshes + 2, "Lens refresh continues while the settings dispatcher is blocked.");
                Check(afterBusyUi.RawInputActive, "Native background mouse notifications are registered while the lens is on.");
                Check(afterBusyUi.MonitorQueries < afterBusyUi.TimerRefreshes, "Repeated updates reuse monitor geometry and DPI.");
                lens.SetEnabled(false);
                var paused = lens.Diagnostics;
                Thread.Sleep(80);
                Check(!paused.RawInputActive && lens.Diagnostics.TimerRefreshes == paused.TimerRefreshes, "Turning the lens off unregisters mouse notifications and stops refresh work.");
                foreach (var shape in Enum.GetValues<LensShape>())
                {
                    lens.Apply(new Settings { Shape = shape, Zoom = 3.5, Width = 360, Height = 200, InvertColors = shape == LensShape.Rectangle });
                    lens.SetEnabled(true);
                    Check(lens.Enabled && Native.IsWindowVisible(lens.Host), $"{shape} lens can be shown.");
                    Check(Native.MagGetWindowTransform(lens.Magnifier, out var matrix) && Math.Abs(matrix.A - 3.5) < .001, $"{shape} uses actual native 3.5x transform.");
                    Check(Native.MagGetWindowSource(lens.Magnifier, out var source) && source.Width > 0, $"{shape} receives an actual native source rectangle.");
                    Native.GetWindowRect(lens.Host, out var rect);
                    Check(shape == LensShape.Rectangle ? rect.Width > rect.Height : rect.Width == rect.Height, $"{shape} has correct native dimensions.");
                    lens.SetEnabled(false);
                    Check(!Native.IsWindowVisible(lens.Host), $"{shape} hides immediately.");
                }
            }
            // Isolate shortcuts from users' saved preferences and common app bindings.
            window.Preferences.SetShortcut(ShortcutAction.Toggle, 7, 0x87);
            window.Preferences.SetShortcut(ShortcutAction.ZoomIn, 7, 0x86);
            window.Preferences.SetShortcut(ShortcutAction.ZoomOut, 7, 0x85);
            window.InitializeShortcuts(handle);
            window.InitializeLens();
            var activeLens = window.ActiveLens ?? throw new InvalidOperationException("Test lens unavailable.");
            var surface = CreateSurface(); surface.Show(); surface.Activate();
            window.Hide();
            PressShortcut(0x86);
            Check(window.Preferences.Zoom == 2.75 && window.ZoomSlider.Value == 2.75 && window.ZoomLabel.Text == "2.75×" && !activeLens.Enabled, "Global zoom in updates hidden settings and keeps an inactive lens off.");
            PressShortcut(0x87);
            Check(activeLens.Enabled, "Toggle remains independently registered alongside both zoom shortcuts.");
            PressShortcut(0x85);
            Check(window.Preferences.Zoom == 2.5 && Native.MagGetWindowTransform(activeLens.Magnifier, out var zoomMatrix) && Math.Abs(zoomMatrix.A - 2.5) < .001, "Global zoom out updates the live native magnifier from another focused window.");
            window.ZoomSlider.Value = 8; PressShortcut(0x86);
            Check(window.Preferences.Zoom == 8, "Zoom in stops at 8x.");
            window.ZoomSlider.Value = 1.25; PressShortcut(0x85);
            Check(window.Preferences.Zoom == 1.25, "Zoom out stops at 1.25x.");
            window.ZoomHotkeyEnabledCheck.IsChecked = false;
            Check(Native.RegisterHotKey(handle, 99, 7, 0x86) && Native.RegisterHotKey(handle, 100, 7, 0x85), "Disabling zoom shortcuts releases both combinations.");
            PressShortcut(0x86);
            Check(window.Preferences.Zoom == 1.25, "A disabled zoom shortcut cannot change magnification.");
            PressShortcut(0x87);
            Check(!activeLens.Enabled, "Disabling zoom shortcuts leaves the toggle functional.");
            window.ZoomHotkeyEnabledCheck.IsChecked = true;
            Check(window.ZoomInHotkeyHint.Text.Contains("unavailable") && window.ZoomOutHotkeyHint.Text.Contains("unavailable"), "Re-enabling reports conflicts for each zoom action.");
            Native.UnregisterHotKey(handle, 99); Native.UnregisterHotKey(handle, 100);
            window.ZoomHotkeyEnabledCheck.IsChecked = false; window.ZoomHotkeyEnabledCheck.IsChecked = true;
            window.HotkeyEnabledCheck.IsChecked = false;
            PressShortcut(0x86);
            Check(window.Preferences.Zoom == 1.5 && !activeLens.Enabled, "Zoom shortcuts can operate independently of the toggle shortcut.");
            store.Save(window.Preferences);
            Check(store.Load(out _) == window.Preferences, "Shortcut-adjusted zoom and disabled toggle survive persistence.");
            File.Delete(scratch);
            surface.Hide(); window.Show(); window.Activate();
            window.ZoomInHotkeyButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            var beforeRecording = window.Preferences.Zoom;
            SendMessage(handle, 0x312, 5, 0);
            Check(window.Preferences.Zoom == beforeRecording && window.ZoomInHotkeyHint.Text.Contains("another Flex Zoom action"), "Recording suppresses zoom actions and rejects another action's registered combination.");
            SendMessage(handle, 0x312, 3, 0);
            Check((string)window.ZoomInHotkeyButton.Content == Hotkey.Label(7, 0x86), "Recording the current zoom shortcut ends capture without changing zoom.");
            window.ZoomInHotkeyButton.Focus();
            window.ZoomInHotkeyButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            window.ZoomOutHotkeyButton.Focus();
            PressShortcut(0x86);
            Check(window.Preferences.Zoom == beforeRecording + 0.25, "Moving keyboard focus cancels recording and restores shortcut actions.");
            window.ZoomInHotkeyButton.Focus();
            window.ZoomInHotkeyButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            PressShortcut(0x84);
            Check(window.Preferences.ZoomInKey == 0x84 && window.Preferences.ZoomInModifiers == 7, "Keyboard capture records a custom zoom shortcut.");
            window.ZoomInHotkeyButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            var escape = new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice, HwndSource.FromHwnd(handle), Environment.TickCount, System.Windows.Input.Key.Escape) { RoutedEvent = System.Windows.Input.Keyboard.PreviewKeyDownEvent };
            window.ZoomInHotkeyButton.RaiseEvent(escape);
            Check(window.Preferences.ZoomInKey == 0x84 && (string)window.ZoomInHotkeyButton.Content == Hotkey.Label(7, 0x84), "Escape cancels recording and retains the saved zoom shortcut.");
            surface.Show(); surface.Activate(); window.Hide();
            PressShortcut(0x84);
            Check(window.Preferences.Zoom == beforeRecording + 0.5, "A newly recorded shortcut zooms globally while settings are hidden.");
            surface.Close(); window.Show();
            // In test mode ResetDefaults avoids the user's Windows startup registration.
            Check(Native.RegisterHotKey(handle, 99, 3, 0xBB), "A default zoom combination can be reserved to test reset conflicts.");
            window.ResetDefaultsButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Check(window.Preferences.ZoomInKey == 0x84 && window.Preferences.ZoomInModifiers == 7, "Reset preserves a working custom zoom shortcut when its default is unavailable.");
            Native.UnregisterHotKey(handle, 99);
            window.ResetDefaultsButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Check(window.Preferences == new Settings() && window.ZoomInHotkeyButton.IsEnabled && window.ZoomOutHotkeyButton.IsEnabled, "Reset defaults restores zoom, both shortcut combinations, and enabled controls.");
            window.Notice.Visibility = Visibility.Collapsed;
            window.UpdateLayout();
            window.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
            Render(window, Path.Combine(directory, "settings-default.png"));
            foreach (var accent in Enum.GetValues<AccentColor>())
            {
                ((System.Windows.Controls.RadioButton)window.FindName(accent + "Accent")).IsChecked = true;
                window.UpdateLayout();
                Render(window, Path.Combine(directory, $"settings-{accent}.png"));
            }
            window.PurpleAccent.IsChecked = true;
            window.Width = 800; window.Height = 620; window.UpdateLayout();
            Render(window, Path.Combine(directory, "settings-compact.png"));
            window.SettingsScroll.ScrollToEnd(); window.UpdateLayout();
            Render(window, Path.Combine(directory, "settings-compact-scrolled.png"));
            Check(window.SettingsScroll.VerticalOffset > 0 && window.ZoomOutHotkeyButton.IsVisible && window.StartupLensCheck.IsVisible, "Compact settings support scrolling to the zoom and startup controls.");
            window.Close();
            report.AppendLine($"SUCCESS: {checks} checks passed. Rendered default and compact settings. Pixel output and physical multi-monitor movement require visual verification.");
            File.WriteAllText(reportPath, report.ToString());
            return 0;
        }
        catch (Exception ex) { report.AppendLine("FAIL " + ex); File.WriteAllText(reportPath, report.ToString()); return 1; }
    }
    private static void Render(Window window, string path)
    {
        var image = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        image.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}
