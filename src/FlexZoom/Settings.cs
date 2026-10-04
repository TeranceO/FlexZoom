using System;
using System.IO;
using System.Text.Json;

namespace FlexZoom;

public enum AccentColor { Purple, Blue, Teal, Rose, Amber }

public enum LensShape { Circle, Square, Rectangle }

public sealed record Settings
{
    public AccentColor Accent { get; set; } = AccentColor.Purple;
    public double Zoom { get; set; } = 2.5;
    public LensShape Shape { get; set; } = LensShape.Circle;
    public int Width { get; set; } = 320;
    public int Height { get; set; } = 220;
    public uint Modifiers { get; set; } = 3; // Ctrl + Alt
    public uint Key { get; set; } = 0x5A;
    public bool GlobalShortcutEnabled { get; set; } = true;
    public bool ZoomShortcutsEnabled { get; set; } = true;
    public uint ZoomInModifiers { get; set; } = 3;
    public uint ZoomInKey { get; set; } = 0xBB; // Plus / equals key
    public uint ZoomOutModifiers { get; set; } = 3;
    public uint ZoomOutKey { get; set; } = 0xBD; // Minus key
    public bool InvertColors { get; set; }
    public bool StartInTray { get; set; }
    public bool StartWithLensOn { get; set; }
    public bool CloseToTray { get; set; } = true;

    public void Validate()
    {
        if (!Enum.IsDefined(Accent)) Accent = AccentColor.Purple;
        Zoom = double.IsFinite(Zoom) ? Math.Clamp(Zoom, 1.25, 8) : 2.5;
        Width = Math.Clamp(Width, 160, 720);
        Height = Math.Clamp(Height, 140, 600);
        if (!Enum.IsDefined(Shape)) Shape = LensShape.Circle;
        if (!Hotkey.IsAllowed(Modifiers, Key)) { Modifiers = 3; Key = 0x5A; }
        if (!Hotkey.IsAllowed(ZoomInModifiers, ZoomInKey)) { ZoomInModifiers = 3; ZoomInKey = 0xBB; }
        if (!Hotkey.IsAllowed(ZoomOutModifiers, ZoomOutKey)) { ZoomOutModifiers = 3; ZoomOutKey = 0xBD; }
    }

    internal (uint Modifiers, uint Key) ShortcutFor(ShortcutAction action) => action switch
    {
        ShortcutAction.ZoomIn => (ZoomInModifiers, ZoomInKey),
        ShortcutAction.ZoomOut => (ZoomOutModifiers, ZoomOutKey),
        _ => (Modifiers, Key)
    };
    internal void SetShortcut(ShortcutAction action, uint modifiers, uint key)
    {
        switch (action)
        {
            case ShortcutAction.ZoomIn: ZoomInModifiers = modifiers; ZoomInKey = key; break;
            case ShortcutAction.ZoomOut: ZoomOutModifiers = modifiers; ZoomOutKey = key; break;
            default: Modifiers = modifiers; Key = key; break;
        }
    }
    internal bool ShortcutEnabled(ShortcutAction action) => action == ShortcutAction.Toggle ? GlobalShortcutEnabled : ZoomShortcutsEnabled;
}

public sealed class SettingsStore(string path)
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlexZoom", "settings.json");
    public Settings Load(out string? warning)
    {
        warning = null;
        try
        {
            if (!File.Exists(path)) return new();
            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? throw new JsonException();
            settings.Validate();
            return settings;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            warning = "Saved preferences could not be read. Defaults are active; your original file is kept until you change a setting.";
            return new();
        }
    }
    public void Save(Settings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, path, true);
    }
}
