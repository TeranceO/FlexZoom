# Flex Zoom

A native Windows magnifier that follows your mouse. Adjust the zoom, lens shape, size, and colors from a compact settings window, then toggle the lens from any app with a global shortcut.

## Features

- Magnification from 1.25x to 8x.
- Circle, square, and rectangle lenses, with independent rectangle width and height.
- Click-through lens with optional inverted colors.
- Configurable global shortcut, with conflict detection and an option to disable it.
- Purple, blue, teal, rose, and amber accents, including sliders and checkboxes.
- System tray controls, optional Windows sign-in launch, and separate start-in-tray and lens-on-startup settings.
- Automatically saved preferences and reset defaults.

## Run

[Download Flex Zoom for Windows x64](https://github.com/TeranceO/FlexZoom/releases/latest/download/FlexZoom-Windows-x64.zip)

Extract the ZIP and open `FlexZoom.exe`. The portable build includes the .NET runtime; no installer or administrator access is needed. The executable is not code-signed, so Windows may display a publisher warning.

To build from source, follow the instructions below. Generated executables and ZIPs are distributed through GitHub Releases rather than source control.

1. Choose a zoom level, shape, and lens size.
2. Press **Ctrl + Alt + Z** or click **Turn lens on**.
3. Click the shortcut button to record a different combination; Escape cancels. Uncheck **Enable global shortcut** to release the combination while keeping the button and tray controls available.
4. Use **Hide to tray** to keep the app in the background. Double-click the tray icon to reopen settings, or right-click it to toggle the lens or quit.

By default, closing the settings window hides it to the tray. Disable **Close button hides to tray** to make Close quit the app. Launching another copy reopens the existing settings window.

## Startup and preferences

All startup options are off by default. **Launch when I sign in to Windows** registers the current executable for your Windows account. Keep the portable folder in place; after moving it, turn this option off and on again. Windows Startup Apps must also allow it. **Start in the system tray** and **Turn the lens on at startup** are independent options applied on the next launch.

Preferences are stored in `%LOCALAPPDATA%\FlexZoom\settings.json`, outside the app folder. Invalid values are bounded. Unreadable preferences fall back to defaults and are preserved until a setting changes. Save failures appear in settings. **Reset defaults** also removes this app's Windows startup entry.

## Build and verify

Requires Windows x64 and the .NET 9 SDK. From the project directory, run:

```powershell
.\tools\build.ps1
```

The script builds, runs native integration checks, renders settings previews, and publishes a self-contained executable and ZIP into `dist/`. Logs, screenshots, and test results go into `artifacts/`. `-SkipTests` packages without running the checks.

If the published app is running, the script stages the new release, closes the running copy at this project's release path, replaces it, and restarts it with saved preferences. Other app locations are not stopped. The previous release is backed up under `artifacts/previous-release`; replacement failures attempt to restore it.

For a build without publishing or replacing the portable app:

```powershell
dotnet build src/FlexZoom -c Release
```

Native tests need an interactive Windows desktop. See [VALIDATION.md](VALIDATION.md) for coverage and limitations.

## Compatibility

Built with C# / WPF and the Windows Magnification API. Tested on Windows 11 x64. Windows 10 has the required native APIs but has not been physically tested. ARM64 and x86 packages are not provided.

Normal desktop apps are the target. Secure desktops, capture-protected content, and exclusive-fullscreen games may not magnify. Use windowed or borderless modes for games. The app does not inject input into other apps or change system display magnification.

Clicks go to the original screen location beneath the cursor. Near monitor edges, the magnified source area is clamped to the display. Display composition and refresh rate still contribute latency.

## Privacy

The app has no account, telemetry, or network-service requirement. Settings remain local. Optional `--diagnostics` mode writes aggregate follow-loop counters and timings to `artifacts/live-follow.json` when the lens is turned off; it does not record screen contents or pointer coordinates.

## Source layout

- `src/FlexZoom/`: WPF interface, preferences, global shortcut, tray integration, and native magnifier.
- `src/FlexZoom/Assets/`: application icon required by the build.
- `src/FlexZoom/SelfTest.cs`: native integration checks and a separate click-through test surface (`--test-surface`).
- `tools/build.ps1`: build, validation, portable packaging, and local update workflow.

See [CONTRIBUTING.md](CONTRIBUTING.md) for development and publication guidance.

## License

[MIT](LICENSE).
