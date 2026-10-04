# Code signing policy

## Status

Flex Zoom's SignPath integration is prepared; SignPath Foundation application and approval are pending. The existing v1.2.0 download is unsigned. New releases will be published only after successful Foundation signing and verification. Configured automation is not evidence that a certificate has been issued.

## Provider

Upon approval: Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).

Only Flex Zoom's own executable, built from tagged source in [TeranceO/FlexZoom](https://github.com/TeranceO/FlexZoom) by GitHub-hosted Windows runners, may be signed. The artifact configuration enforces product identity and version. Bundled Microsoft runtime libraries retain their upstream provenance and are not individually re-signed as Flex Zoom.

## Roles

- Author, committer, and reviewer: [Terance Ostrander (TeranceO)](https://github.com/TeranceO).
- Release and signing approver: [Terance Ostrander (TeranceO)](https://github.com/TeranceO).

Outside contributions require maintainer review. Maintainers must use multi-factor authentication for GitHub and SignPath. Each release requires manual approval in SignPath after reviewing the source commit and build origin. The public release workflow verifies the returned signature and timestamp before packaging or publication. Missing credentials, rejected requests, and invalid signatures stop publication; they do not enable an unsigned fallback.

## Privacy policy

Flex Zoom does not transfer information to other networked systems. It has no accounts, analytics, telemetry, or network-service requirement. Preferences remain local. Optional diagnostics contain aggregate lens timing counters, not screen contents or pointer coordinates. The GitHub/SignPath build and signing services process release artifacts during development, rather than user data from the running app.

## Verification

Each new signed release includes `SHA256SUMS.txt` and `PROVENANCE.json`, identifying the source commit, GitHub build, and SignPath request. Windows File Properties > Digital Signatures should show the approved SignPath Foundation certificate. A valid signature identifies publisher and integrity; it does not guarantee immediate SmartScreen reputation.

Setup and release instructions: [SIGNING.md](SIGNING.md). Foundation requirements: [Code of Conduct](https://signpath.org/terms).
