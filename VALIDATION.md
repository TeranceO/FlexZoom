# Validation

Last local validation: 2026-10-04, Windows 11 x64, .NET SDK 9.0.301.

## Automated checks

The build script passed 86 assertions covering settings validation, JSON persistence and corrupt-file preservation, monitor-coordinate calculations, native hotkey registration/replacement/conflicts, shortcut disabling and re-enabling, Windows startup registration in an isolated test key, and native lens behavior for all three shapes.

The saved-settings round trip includes a non-default accent, custom zoom shortcuts, and disabled shortcuts. Older settings acquire default zoom shortcuts while retaining saved values. Injected keyboard events exercise Windows global hotkey delivery from a separate focused window while settings are hidden, checking live native zoom transforms, 1.25x/8x limits, inactive-lens behavior, independent toggles, conflicts, and custom shortcut capture. Recording checks cover duplicate rejection, suppression of active actions, Escape, and focus loss. Reset restores the new defaults. Lens checks read native transforms, source rectangles, dimensions, and visibility. Tracking checks cover the separate worker thread, background mouse registration, monitor caching, and suspended work when the lens is off.

Cloud CI runs 39 headless assertions through `--self-test-headless`: settings, isolated startup registry I/O, coordinate calculations, and shortcut validation. It does not instantiate the native magnifier, inject keys, or render settings. The CI publish script was also run locally and verified the executable's product name/version against the SignPath configuration.

Both GitHub workflows pass actionlint 1.7.12. Local checks confirmed that mismatched release tags and unsigned binaries are rejected. SignPath account approval, signing, certificate validation of a signed artifact, and signed public download verification remain pending; configuration and successful compilation do not establish signing.

## Visual and manual coverage

Generated normal, compact, and scrolled compact settings renders were inspected. Teal and amber renders confirmed matching slider and checkbox accents. Earlier local manual checks covered visible magnification, click-through interaction, custom shortcut capture and persistence, tray reopening, inversion, startup settings, Close-button behavior, and staged executable replacement/rollback.

Generated renders do not prove physical keyboard or mouse behavior. Zoom shortcuts were checked with injected Windows keyboard events and native magnifier reads; a separate physical-keyboard pass and non-US keyboard layouts remain unverified.

## Not verified

Physical end-to-end input latency, mixed-DPI monitor transitions, hot-plug, Windows 10, remote desktop, sign-out/reboot startup, sleep/lock transitions, protected content, and exclusive-fullscreen games have not been comprehensively tested. Simulated monitor-coordinate checks are not physical multi-monitor tests. Native rendering can vary by graphics driver.

## Reproduce

Run `tools/build.ps1` on an interactive Windows desktop. Results and images are written under the Git-ignored `artifacts/` directory. Launch the app with `--test-surface` to open a separate click-through counter. Use `--diagnostics` for aggregate follow-loop timing; these measurements exclude display presentation latency.
