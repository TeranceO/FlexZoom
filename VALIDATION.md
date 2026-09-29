# Validation

Last local validation: 2026-09-28, Windows 11 x64 (build 26200), .NET SDK 9.0.301.

## Automated checks

The build script passed 63 assertions covering settings validation, JSON persistence and corrupt-file preservation, monitor-coordinate calculations, native hotkey registration/replacement/conflicts, shortcut disabling and re-enabling, Windows startup registration in an isolated test key, and native lens behavior for all three shapes.

The saved-settings round trip includes a non-default accent and a disabled global shortcut. Lens checks read native transforms, source rectangles, dimensions, and visibility. Tracking checks cover the separate worker thread, background mouse registration, monitor caching, and suspended work when the lens is off.

## Visual and manual coverage

Generated normal and compact settings renders were inspected. Teal and amber renders confirmed matching slider and checkbox accents. Earlier local manual checks covered visible magnification, click-through interaction, custom shortcut capture and persistence, tray reopening, inversion, startup settings, Close-button behavior, and staged executable replacement/rollback.

Generated renders do not prove physical keyboard or mouse behavior. The most recent slider and checkbox styling change was compiled and visually inspected; it has not received a separate manual input pass.

## Not verified

Physical end-to-end input latency, mixed-DPI monitor transitions, hot-plug, Windows 10, remote desktop, sign-out/reboot startup, sleep/lock transitions, protected content, and exclusive-fullscreen games have not been comprehensively tested. Simulated monitor-coordinate checks are not physical multi-monitor tests. Native rendering can vary by graphics driver.

## Reproduce

Run `tools/build.ps1` on an interactive Windows desktop. Results and images are written under the Git-ignored `artifacts/` directory. Launch the app with `--test-surface` to open a separate click-through counter. Use `--diagnostics` for aggregate follow-loop timing; these measurements exclude display presentation latency.
