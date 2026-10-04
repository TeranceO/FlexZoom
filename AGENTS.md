# Flex Zoom contributor and release instructions

Flex Zoom is a Windows x64 .NET 9 WPF application. Preserve existing preferences, native magnification, click-through behavior, startup registration, tray controls, and shortcut conflict handling.

## Validation

- Use `tools/build.ps1` on an interactive Windows desktop before releasing behavior changes. It runs the full native self-test, renders settings at normal and compact sizes, and packages a local development build. Inspect the renders for UI changes. Report actual results rather than treating compilation as runtime proof.
- GitHub Actions runs `tools/ci-build.ps1` and `--self-test-headless`. These checks do not establish physical keyboard, native magnifier, or monitor behavior; retain the local interactive check.
- Never commit generated `artifacts/`, `dist/`, `bin/`, `obj/`, private settings, credentials, or signing keys. Preserve unrelated user edits.

## Mandatory signing for public releases

- All future public Windows releases must use **SignPath Foundation**, through `.github/workflows/release.yml` (the **Signed release** workflow). Do not upload local development EXEs/ZIPs as public release assets, and do not substitute another certificate or publish unsigned when SignPath is unavailable.
- SignPath verifies build origin. Build the release on GitHub-hosted Windows runners from an existing `vMAJOR.MINOR.PATCH` tag in `main` history, matching `<Version>` in `src/FlexZoom/FlexZoom.csproj`. Do not submit local builds or use self-hosted runners for Foundation signing.
- Follow [SIGNING.md](SIGNING.md) for onboarding and future releases. Required GitHub secret: `SIGNPATH_API_TOKEN`. Required variables: `SIGNPATH_ORGANIZATION_ID`, `SIGNPATH_PROJECT_SLUG`, `SIGNPATH_SIGNING_POLICY_SLUG`, `SIGNPATH_ARTIFACT_CONFIGURATION_SLUG`. Never print, commit, or ask the user to paste the API token into chat; they can enter it in GitHub Secrets.
- `.signpath/artifact-configuration.xml` restricts signing to the project-owned `FlexZoom.exe`, product `Flex Zoom`, and the requested product version. Upload this configuration to SignPath; changes must also be applied to the SignPath project. Do not sign bundled upstream binaries under the Foundation certificate.
- Every release requires manual approval by the designated maintainer in SignPath. Never remove or bypass this requirement. The agent can prepare and dispatch the release, then ask the human approver to approve the concrete signing request.
- `tools/verify-signature.ps1` must succeed after signing: valid Windows Authenticode chain, SignPath Foundation publisher, timestamp, and matching executable metadata. Package the returned signed EXE, calculate checksums after signing, and publish only after verification. Do not alter the EXE after signing.
- Keep [CODE_SIGNING.md](CODE_SIGNING.md), README, checked-in release notes in `releases/`, and validation evidence accurate. Each release page must link to the Code signing policy. Do not claim Foundation approval or successful signing before it actually occurs.
- After publishing, download the public ZIP and checksum file into `artifacts/`, verify SHA-256, inspect the archive, and verify the downloaded EXE's signature/version. Keep published assets immutable; use a new version for changes.

## First-time onboarding state

As of 2026-10-04, the SignPath Foundation application has been submitted and its receipt confirmed. Foundation review and approval are pending; an application acknowledgment is not approval to sign. The integration is prepared, not yet provisioned by SignPath. Version 1.2.0 is the earlier unsigned public release. Version 1.3.0 must remain a draft until Foundation approval, configuration, credentials, and a verified signed build are available. Update this paragraph when onboarding is completed.

## Standard update procedure

1. Implement the change, bump the project version, add `releases/vVERSION.md`, run the full local validation, and inspect relevant UI renders.
2. Commit and push the source to `main`; wait for **Build and check** to pass.
3. Create and push a new matching tag. Run `gh workflow run release.yml --ref main -f tag=vVERSION`.
4. Have the human maintainer approve the resulting SignPath request; wait for **Signed release** to finish.
5. Verify the public download as described above and report its URL and verified signing status.
