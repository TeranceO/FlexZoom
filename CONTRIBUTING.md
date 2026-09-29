# Contributing

Use Windows x64 with the .NET 9 SDK. Run `tools/build.ps1` from an interactive desktop before submitting behavior changes. Native magnification and hotkey checks require Windows; a successful compile alone does not establish correct runtime behavior.

For interface changes, inspect the generated settings renders at normal and compact sizes, including the accent colors. Check keyboard navigation, slider interaction, and shortcut recording in the running app when changing their controls. Keep README and validation claims aligned with what was actually tested.

## Files to keep local

Do not commit `artifacts/`, `dist/`, `bin/`, or `obj/`. These can contain executable bundles, backups, screenshots, machine paths, diagnostic output, and saved test preferences. User settings, credentials, signing keys, and IDE state also belong outside version control. The `.gitignore` covers common cases; inspect `git status` and staged diffs before committing. Do not force-add ignored output.

## Publishing

Review the files Git will include with `git ls-files --cached --others --exclude-standard`. Use Git to publish the source; uploading the entire workspace folder bypasses ignore rules. Attach the portable ZIP to a GitHub Release instead of committing it. Inspect any screenshots or logs separately before sharing them.

This project is licensed under the MIT License. Include the LICENSE file with redistributed copies.
