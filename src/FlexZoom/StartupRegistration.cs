using System;
using System.IO;
using Microsoft.Win32;

namespace FlexZoom;

// The Windows entry is the source of truth. Reading preferences never enables startup.
internal sealed class StartupRegistration(string keyPath = StartupRegistration.RunKey, string valueName = "FlexZoom")
{
    internal const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal static string CurrentExecutable => Environment.ProcessPath ?? throw new IOException("The application path could not be determined.");
    internal static string CommandFor(string executable)
    {
        if (!Path.IsPathFullyQualified(executable) || executable.Contains('"') || !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Windows startup requires an absolute executable path.");
        var command = $"\"{executable}\" --startup";
        if (command.Length > 260) throw new IOException("This app's folder path is too long for Windows startup. Move it to a shorter path and try again.");
        return command;
    }
    public string? ReadCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(keyPath);
        return key?.GetValue(valueName) as string;
    }
    public void SetEnabled(bool enabled, string executable)
    {
        // Validate before creating or modifying a registry entry.
        string? command = enabled ? CommandFor(executable) : null;
        using var key = Registry.CurrentUser.CreateSubKey(keyPath, true);
        if (enabled) key.SetValue(valueName, command!, RegistryValueKind.String);
        else key.DeleteValue(valueName, false);
        if (!string.Equals(ReadCommand(), command, StringComparison.Ordinal))
            throw new IOException("Windows did not retain the requested startup setting.");
    }
}
