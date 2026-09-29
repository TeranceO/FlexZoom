using System;
using System.ComponentModel;
using System.IO;
using System.Security;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace FlexZoom;

public partial class MainWindow : Window
{
    private readonly SettingsStore store = new(SettingsStore.DefaultPath);
    private readonly StartupRegistration startup = new();
    public Settings Preferences { get; private set; }
    private Lens? lens;
    private Hotkey? hotkey;
    private Forms.NotifyIcon? tray;
    private Forms.ToolStripMenuItem? trayToggle;
    private System.Drawing.Icon? trayIcon;
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private bool ready, quitting, dirty, recording;
    private readonly bool designTest;

    public MainWindow() : this(false) { }
    internal MainWindow(bool test)
    {
        designTest = test;
        Preferences = test ? new() : store.Load(out _);
        InitializeComponent();
        Populate(); ready = true;
        saveTimer.Tick += (_, _) => Save();
        if (!test)
        {
            Preferences = store.Load(out var warning); Populate();
            if (warning != null) SetNotice(warning);
            RefreshStartup();
            Activated += (_, _) => RefreshStartup();
            SourceInitialized += InitializeServices;
            Closing += OnClosing;
            Closed += (_, _) => { if (quitting) Application.Current.Shutdown(); };
            StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) Hide(); };
            SystemEvents.SessionSwitch += SessionChanged;
            SystemEvents.PowerModeChanged += PowerChanged;
        }
    }
    private void InitializeServices(object? sender, EventArgs e)
    {
        hotkey = new Hotkey(new WindowInteropHelper(this).Handle);
        hotkey.Pressed += Toggle;
        hotkey.CurrentShortcutRecorded += StopRecording;
        if (Preferences.GlobalShortcutEnabled && !hotkey.TrySet(Preferences.Modifiers, Preferences.Key))
            SetNotice("Your toggle shortcut is already used by another app. Click the shortcut button to choose a different one.");
        StopRecording();
        try { lens = new Lens(); lens.Apply(Preferences); lens.Failed += message => { SetNotice(message); UpdateStatus(); }; }
        catch (Exception ex) when (ex is Win32Exception or DllNotFoundException)
        { SetNotice("Magnifier unavailable: " + ex.Message); ToggleButton.IsEnabled = false; }
        using var iconStream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/FlexZoom.ico")).Stream;
        trayIcon = new System.Drawing.Icon(iconStream);
        tray = new Forms.NotifyIcon { Icon = trayIcon, Text = "Flex Zoom — lens off", Visible = true };
        var menu = new Forms.ContextMenuStrip();
        trayToggle = new Forms.ToolStripMenuItem("Turn lens on", null, (_, _) => Dispatcher.Invoke(Toggle));
        menu.Items.Add(trayToggle);
        menu.Items.Add("Open settings", null, (_, _) => Dispatcher.Invoke(ShowSettings));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Quit Flex Zoom", null, (_, _) => Dispatcher.Invoke(Quit));
        tray.ContextMenuStrip = menu;
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowSettings);
        lens?.SetEnabled(Preferences.StartWithLensOn);
        UpdateStatus();
    }
    private void Populate()
    {
        bool previous = ready; ready = false;
        ((System.Windows.Controls.RadioButton)FindName(Preferences.Accent + "Accent")).IsChecked = true;
        ApplyAccent();
        ZoomSlider.Value = Preferences.Zoom;
        WidthSlider.Value = Preferences.Width;
        HeightSlider.Value = Preferences.Height;
        CircleButton.IsChecked = Preferences.Shape == LensShape.Circle;
        SquareButton.IsChecked = Preferences.Shape == LensShape.Square;
        RectangleButton.IsChecked = Preferences.Shape == LensShape.Rectangle;
        InvertCheck.IsChecked = Preferences.InvertColors; TrayCheck.IsChecked = Preferences.StartInTray;
        StartupLensCheck.IsChecked = Preferences.StartWithLensOn;
        CloseToTrayCheck.IsChecked = Preferences.CloseToTray;
        HotkeyEnabledCheck.IsChecked = Preferences.GlobalShortcutEnabled;
        StopRecording();
        UpdateLabels(); ready = previous;
    }
    private void ApplyAccent()
    {
        Resources["Accent"] = new SolidColorBrush(AccentPalette.ColorFor(Preferences.Accent));
        Resources["AccentSurface"] = new SolidColorBrush(AccentPalette.SurfaceFor(Preferences.Accent));
    }
    private void AccentChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        Preferences.Accent = Enum.Parse<AccentColor>((string)((System.Windows.Controls.RadioButton)sender).Tag);
        ApplyAccent();
        UpdateLabels();
        QueueSave();
    }
    private void SettingsChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        Preferences.Zoom = ZoomSlider.Value;
        Preferences.Width = (int)WidthSlider.Value;
        Preferences.Height = (int)HeightSlider.Value;
        Preferences.Shape = RectangleButton.IsChecked == true ? LensShape.Rectangle : SquareButton.IsChecked == true ? LensShape.Square : LensShape.Circle;
        Preferences.InvertColors = InvertCheck.IsChecked == true;
        Preferences.StartInTray = TrayCheck.IsChecked == true;
        Preferences.StartWithLensOn = StartupLensCheck.IsChecked == true;
        Preferences.CloseToTray = CloseToTrayCheck.IsChecked == true;
        UpdateLabels();
        try { lens?.Apply(Preferences); }
        catch (Win32Exception ex) { lens?.SetEnabled(false); SetNotice(ex.Message); UpdateStatus(); }
        QueueSave();
    }
    private void UpdateLabels()
    {
        ZoomLabel.Text = $"{Preferences.Zoom:0.##}×";
        WidthLabel.Text = $"{Preferences.Width} dp"; HeightLabel.Text = $"{Preferences.Height} dp";
        SizeCaption.Text = Preferences.Shape == LensShape.Circle ? "Diameter" : Preferences.Shape == LensShape.Square ? "Side length" : "Width";
        HeightPanel.Visibility = Preferences.Shape == LensShape.Rectangle ? Visibility.Visible : Visibility.Collapsed;
        Preview.Settings = Preferences with { }; Preview.InvalidateVisual();
    }
    private void RefreshStartup()
    {
        bool previous = ready; ready = false;
        try
        {
            var command = startup.ReadCommand();
            WindowsStartupCheck.IsChecked = !string.IsNullOrWhiteSpace(command);
            WindowsStartupCheck.IsEnabled = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        { WindowsStartupCheck.IsEnabled = false; SetNotice("Windows startup could not be read: " + ex.Message); }
        finally { ready = previous; }
    }
    private void WindowsStartupChanged(object sender, RoutedEventArgs e)
    {
        if (!ready || designTest) return;
        try { startup.SetEnabled(WindowsStartupCheck.IsChecked == true, StartupRegistration.CurrentExecutable); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        { SetNotice("Windows startup could not be changed: " + ex.Message); }
        RefreshStartup();
    }
    private void QueueSave() { if (designTest) return; dirty = true; SaveLabel.Text = "Saving preferences…"; saveTimer.Stop(); saveTimer.Start(); }
    private void Save()
    {
        saveTimer.Stop(); if (!dirty) return;
        try { store.Save(Preferences); dirty = false; SaveLabel.Text = "Preferences saved automatically"; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { SaveLabel.Text = "Preferences could not be saved"; SetNotice("Your changes work for this session, but could not be saved: " + ex.Message); }
    }
    private void ToggleLens(object sender, RoutedEventArgs e) => Toggle();
    private void Toggle()
    {
        if (lens == null) return;
        bool wasEnabled = lens.Enabled;
        if (wasEnabled) WriteFollowDiagnostics();
        lens.SetEnabled(!wasEnabled); UpdateStatus();
    }
    private void WriteFollowDiagnostics()
    {
        if (!App.FollowDiagnostics || lens == null) return;
        try
        {
            string directory = Path.Combine(Environment.CurrentDirectory, "artifacts");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "live-follow.json"), JsonSerializer.Serialize(lens.Diagnostics, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { SetNotice("Could not write follow diagnostics: " + ex.Message); }
    }
    private void UpdateStatus()
    {
        bool on = lens?.Enabled == true;
        StatusLabel.Text = on ? "Lens on" : "Lens off";
        StatusDot.Fill = new SolidColorBrush(on ? Color.FromRgb(106, 224, 177) : Color.FromRgb(138, 149, 174));
        ToggleButton.Content = on ? "Turn lens off" : "Turn lens on";
        if (tray != null) tray.Text = $"Flex Zoom — lens {(on ? "on" : "off")}";
        if (trayToggle != null) { trayToggle.Text = on ? "Turn lens off" : "Turn lens on"; trayToggle.Enabled = lens != null; }
    }
    private void HotkeyEnabledChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        Preferences.GlobalShortcutEnabled = HotkeyEnabledCheck.IsChecked == true;
        if (!Preferences.GlobalShortcutEnabled) hotkey?.Disable();
        else hotkey?.TrySet(Preferences.Modifiers, Preferences.Key);
        StopRecording();
        QueueSave();
    }
    private void RecordHotkey(object sender, RoutedEventArgs e)
    {
        if (!Preferences.GlobalShortcutEnabled) return;
        recording = true; if (hotkey != null) hotkey.Recording = true;
        HotkeyButton.Content = "Press your shortcut…";
        HotkeyHint.Text = "Use Ctrl, Alt, or Shift + a key, or F1–F24. Esc cancels.";
    }
    private void CaptureHotkey(object sender, KeyEventArgs e)
    {
        if (!recording) return;
        e.Handled = true;
        var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
        if (key == System.Windows.Input.Key.Escape) { StopRecording(); return; }
        if (key is System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl or System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt or System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift or System.Windows.Input.Key.LWin or System.Windows.Input.Key.RWin) return;
        var mods = Keyboard.Modifiers;
        uint flags = (mods.HasFlag(ModifierKeys.Control) ? 2u : 0) | (mods.HasFlag(ModifierKeys.Alt) ? 1u : 0) | (mods.HasFlag(ModifierKeys.Shift) ? 4u : 0);
        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (mods.HasFlag(ModifierKeys.Windows) || !Hotkey.IsAllowed(flags, vk)) { HotkeyHint.Text = "Add Ctrl, Alt, or Shift, or use a function key."; return; }
        if (hotkey?.TrySet(flags, vk) != true) { HotkeyHint.Text = "That shortcut is unavailable. Try another combination."; return; }
        Preferences.Modifiers = flags; Preferences.Key = vk;
        StopRecording(); Notice.Visibility = Visibility.Collapsed; QueueSave();
    }
    private void EndRecording(object sender, KeyboardFocusChangedEventArgs e) { if (recording) StopRecording(); }
    private void StopRecording()
    {
        recording = false; if (hotkey != null) hotkey.Recording = false;
        HotkeyButton.Content = Hotkey.Label(Preferences.Modifiers, Preferences.Key);
        HotkeyButton.IsEnabled = Preferences.GlobalShortcutEnabled;
        HotkeyHint.Text = !Preferences.GlobalShortcutEnabled ? "" : hotkey?.Registered == false ? "Shortcut unavailable. Click to choose another." : "Click to record a different shortcut.";
    }
    private void ResetDefaults(object sender, RoutedEventArgs e)
    {
        if (!designTest)
        {
            try { startup.SetEnabled(false, StartupRegistration.CurrentExecutable); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
            { SetNotice("Lens defaults restored, but Windows startup could not be disabled: " + ex.Message); }
            RefreshStartup();
        }
        var defaults = new Settings();
        if (hotkey != null && !hotkey.TrySet(defaults.Modifiers, defaults.Key))
        { defaults.Modifiers = Preferences.Modifiers; defaults.Key = Preferences.Key; SetNotice("Lens defaults restored. Your current shortcut was kept because Ctrl + Alt + Z is in use."); }
        if (hotkey != null && !hotkey.Registered)
            defaults.GlobalShortcutEnabled = Preferences.GlobalShortcutEnabled;
        Preferences = defaults; Populate();
        try { lens?.Apply(Preferences); } catch (Win32Exception ex) { SetNotice(ex.Message); }
        QueueSave();
    }
    private void SetNotice(string message) { Notice.Text = message; Notice.Visibility = Visibility.Visible; }
    private void HideToTray(object sender, RoutedEventArgs e) { Save(); Hide(); }
    public void ShowSettings() { Show(); WindowState = WindowState.Normal; Activate(); }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (quitting) return;
        if (Preferences.CloseToTray) { e.Cancel = true; Save(); Hide(); }
        else PrepareToQuit(); // Allow this close to finish; Closed shuts down the application.
    }
    private void SessionChanged(object sender, SessionSwitchEventArgs e)
    { if (e.Reason == SessionSwitchReason.SessionLock) Dispatcher.BeginInvoke(() => { lens?.SetEnabled(false); UpdateStatus(); }); }
    private void PowerChanged(object sender, PowerModeChangedEventArgs e)
    { if (e.Mode == PowerModes.Suspend) Dispatcher.BeginInvoke(() => { lens?.SetEnabled(false); UpdateStatus(); }); }
    internal void QuitForUpdate() => Quit();
    private void Quit()
    {
        if (quitting) return;
        PrepareToQuit();
        Close();
    }
    private void PrepareToQuit()
    {
        quitting = true; Save();
        WriteFollowDiagnostics();
        SystemEvents.SessionSwitch -= SessionChanged; SystemEvents.PowerModeChanged -= PowerChanged;
        hotkey?.Dispose(); lens?.Dispose();
        if (tray != null) { tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); }
        trayIcon?.Dispose();
    }
}
