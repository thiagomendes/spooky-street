# CI and TestFlight setup (one-time, owner actions)

Everything here is free with an existing Apple Developer Program membership and the GitHub Free
plan. None of it needs the Alienware.

## 1. Unity account → repository secrets (Settings → Secrets and variables → Actions)

| Secret | Value |
|---|---|
| `UNITY_EMAIL` | Unity account email (account created with email + password, not a social login) |
| `UNITY_PASSWORD` | Unity account password (no authenticator 2FA on this account, or CI cannot sign in) |
| `UNITY_SERIAL` | Only for Plus/Pro |

No licence file is needed. `ci/unity/container.sh` signs in with `Unity.Licensing.Client
--activate-all --include-personal` (fallback `--activate-ulf`) inside the pinned container and
releases the seat when the job ends. Verified by the *Unity licence probe* workflow (run 2,
2026-11-21): Unity Personal entitlements including `com.unity.editor.platforms.ios` were granted
and revoked cleanly.

Unity Hub 4 on Windows only writes `UnityEntitlementLicense.xml`, which is bound to that PC's
hardware and does **not** work in CI (probe run 1). Do not store it as a secret.

## 2. Apple (requires an active Apple Developer Program membership — not purchased by this repo)

1. **Bundle ID**: developer.apple.com → Identifiers → register an explicit App ID, e.g. `com.<you>.spookystreet`.
2. **App record**: App Store Connect → Apps → **+** → New App, iOS, same bundle ID, any SKU. Leave it unreleased.
3. **Distribution certificate** (no Mac needed):
   ```bash
   openssl req -new -newkey rsa:2048 -nodes -keyout dist.key -out dist.csr -subj "/CN=Spooky Street Distribution"
   ```
   Upload `dist.csr` at Certificates → **+** → *Apple Distribution*, download `distribution.cer`, then:
   ```bash
   openssl x509 -inform DER -in distribution.cer -out dist.pem
   ```
   ```bash
   openssl pkcs12 -export -legacy -inkey dist.key -in dist.pem -out dist.p12 -name "Apple Distribution"
   ```
   (`-legacy` keeps the .p12 importable by the macOS keychain.) Delete `dist.key` after storing the .p12 password.
4. **Provisioning profile**: Profiles → **+** → *App Store Connect* → the App ID → the certificate → download `.mobileprovision`.
5. **API key**: App Store Connect → Users and Access → Integrations → App Store Connect API → **+**, role *App Manager*. Download `AuthKey_XXXX.p8` (one download only). Note Key ID and Issuer ID.
6. Optional: TestFlight → Internal Testing → create a group containing yourself so processed builds appear automatically.

## 3. GitHub environment `testflight` (Settings → Environments → New environment)

- **Deployment branches and tags**: *Selected branches* → `main` only. Dispatch the workflow from `main`.
- **Required reviewers**: yourself (optional, adds an approval click before signing).
- **Environment secrets** (base64 without line breaks: `base64 -w0 file` on Linux, `[Convert]::ToBase64String([IO.File]::ReadAllBytes("file"))` in PowerShell):

| Secret | Value |
|---|---|
| `APPLE_TEAM_ID` | 10-character Team ID (Membership page) |
| `IOS_DIST_CERT_P12_BASE64` | base64 of `dist.p12` |
| `IOS_DIST_CERT_PASSWORD` | the .p12 export password |
| `IOS_PROVISIONING_PROFILE_BASE64` | base64 of the `.mobileprovision` |
| `ASC_KEY_ID` | API key ID |
| `ASC_ISSUER_ID` | API issuer ID |
| `ASC_KEY_P8_BASE64` | base64 of `AuthKey_XXXX.p8` |

## 4. Repository variables (Settings → Secrets and variables → Actions → Variables)

| Variable | Default | Purpose |
|---|---|---|
| `IOS_BUNDLE_ID` | — (required for uploads) | Bundle ID from step 2.1 |
| `MACOS_RUNNER` | `macos-15` | Runner label for the Xcode jobs (e.g. `macos-26`) |
| `XCODE_MAJOR` | `26` | Xcode major to select; App Store Connect requires a current SDK for uploads |

## 5. Spending controls

The repository is private, so macOS minutes count at 10× against the 2,000 included minutes of
GitHub Free. One TestFlight run is roughly 20–35 macOS minutes (≈200–350 included minutes) plus
~30 Linux minutes. To guarantee no charges: Settings (account) → Billing and licensing →
Budgets and alerts → set an **Actions** budget of **$0** with *Stop usage when budget limit is reached*.
Every job has a `timeout-minutes` cap; the processing poll runs on Linux so macOS is never idle-waiting.

## 6. Running it

- **CI** runs on every PR and on `main`. Without Unity secrets it still runs static checks and the
  engine-free tests, and reports "Unity tests NOT run" instead of passing silently.
- **Build for TestFlight**: Actions → *Build for TestFlight* → *Run workflow* from `main`:
  - `export-only`: proves activation + iOS export (Unity secrets only).
  - `unsigned-xcode-check`: export + compile on macOS without Apple credentials.
  - `testflight`: export → sign → upload → poll processing. The run summary lists commit,
    version, build number (`<run number>.<attempt>`) and the App Store Connect processing state.
