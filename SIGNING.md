# SignPath setup and releases

The repository is ready for SignPath Foundation integration. The application has been submitted and SignPath has confirmed receipt. Account provisioning, Foundation approval, and the first signature are still pending. The free program is discretionary; the project's current limited download history does not guarantee approval.

## One-time setup

1. Application submitted; receipt confirmed. Wait for SignPath's review rather than submitting a duplicate application. The [Foundation application](https://signpath.org/apply) references the public repository and [Code signing policy](https://github.com/TeranceO/FlexZoom/blob/main/CODE_SIGNING.md).
2. Enable MFA for the maintainer's GitHub and SignPath accounts. After approval, install the official [SignPath GitHub App](https://github.com/apps/signpath) with access limited to this repository and link the project to the GitHub.com Trusted Build System. Review and accept required permissions yourself.
3. Create a SignPath project (suggested slug: `flexzoom`), import `.signpath/artifact-configuration.xml` as an artifact configuration (suggested slug: `flexzoom`), and create a production signing policy (suggested slug: `release-signing`) using the approved Foundation certificate. Keep mandatory manual approval by TeranceO. Do not use a test certificate for public releases.
4. Allow the CI submitter to submit to this policy, and create its API token. Put the token directly into this repository's [Actions secrets](https://github.com/TeranceO/FlexZoom/settings/secrets/actions) under `SIGNPATH_API_TOKEN`. Do not put it in source, logs, or chat.
5. In [Actions variables](https://github.com/TeranceO/FlexZoom/settings/variables/actions), enter the exact values from SignPath:

   | Variable | Value |
   | --- | --- |
   | `SIGNPATH_ORGANIZATION_ID` | Organization ID assigned by SignPath |
   | `SIGNPATH_PROJECT_SLUG` | Actual project slug (`flexzoom` suggested) |
   | `SIGNPATH_SIGNING_POLICY_SLUG` | Actual production policy slug (`release-signing` suggested) |
   | `SIGNPATH_ARTIFACT_CONFIGURATION_SLUG` | Actual configuration slug (`flexzoom` suggested) |

6. Confirm GitHub-hosted build origin requirements and `main`/release-tag restrictions with SignPath. The configuration signs only `FlexZoom.exe`, with product `Flex Zoom` and the request's exact `version` parameter. The GitHub upload is a ZIP containing the EXE, so the XML uses `<zip-file>` as its root artifact element.
7. Run the first signed release below, approve its request, and verify the public result. Only then update the onboarding status in `AGENTS.md`, README, and `CODE_SIGNING.md` to say that Foundation signing is active.

## Release a new version

Run full native checks locally with `tools/build.ps1`. Its output is an unsigned development build and must not be uploaded as a public release. Keep local validation distinct from the 39 headless assertions performed by cloud CI.

Set the project version and add `releases/vVERSION.md` with a Code signing policy link. Commit and push to `main`, and wait for **Build and check**. Create a matching tag at the reviewed commit, then dispatch:

```powershell
git tag vVERSION
git push origin vVERSION
gh workflow run release.yml --ref main -f tag=vVERSION
```

The workflow checks SignPath credentials and exact tag/version correspondence, builds on a GitHub-hosted Windows runner, uploads only the project EXE, and requests signing with the declared version. Approve the concrete request in SignPath within its one-hour wait. Approval remains a human step for every release. If the wait expires, inspect the request and rerun the workflow; do not disable approval.

After receiving the signed file, the workflow requires a valid Authenticode signature, Foundation publisher, timestamp, and matching product metadata. It creates the ZIP from that returned EXE, adds SHA-256 and provenance, and publishes the draft release. Any failure leaves the release unpublished. Published assets are never overwritten by this workflow.

## Verify the public download

Download the ZIP and `SHA256SUMS.txt` from the published release into a new folder under `artifacts/`. Compare the ZIP's SHA-256 with the checksum. Extract it and run:

```powershell
.\tools\verify-signature.ps1 -Path 'artifacts\download-check\FlexZoom.exe' -Version 'VERSION'
```

Verify that the archive contains the signed EXE, README, validation notes, MIT license, and Code signing policy. Confirm the build/commit/request links in `PROVENANCE.json`. Signing status must be verified on the downloaded binary, not inferred from a green compile or source configuration.

## Sources

- [SignPath Foundation conditions](https://signpath.org/terms)
- [SignPath GitHub integration](https://docs.signpath.io/trusted-build-systems/github)
- [Artifact configuration syntax](https://docs.signpath.io/artifact-configuration/syntax)
- [Artifact metadata restrictions](https://docs.signpath.io/artifact-configuration/reference#file-metadata-restrictions)
