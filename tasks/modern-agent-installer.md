# Modern IAM.Agent installer approval

Request: replace the dated Windows PowerShell credential popup with a modern installer experience.

## Implementation

- Native WPF sign-in and MFA dialogs with FUJITEC branding, visible IAM destination/computer, employee-code label, password/PIN guidance, masked secret fields, keyboard defaults, cancel and inline required-field validation. Window bounds respect the available work area; overflow can scroll on small displays.
- Windows PowerShell STA is selected by the bootstrap, Install.cmd and elevation relaunch. Authentication endpoints, MFA, trust rules, signed worker validation, TLS and script policy are unchanged. Credentials are returned in memory as PSCredential/SecureString; fields clear on close. No credentials are written to disk or process arguments.
- Setup contains AgentInstallerUi.ps1. Archive validation supports the legacy nine-file and new ten-file bundles, still rejecting unexpected files, duplicates, links and multiple worker packages.
- Publishers include the UI helper. IAM rollout rebuilds setup from archived source scripts and verifies/reuses the existing signed worker; it no longer copies stale installer scripts from a previous worker release. Worker version and signing key do not change for this UI-only update.

## Validation

- Windows PowerShell 5.1 STA tests exercise blank credentials, credential conversion, Cancel, close, masked field clearing, invalid MFA and valid MFA with a leading zero. Dialogs are rendered offscreen for visual review, using synthetic data. No installation, service change, network authentication or enrollment is performed by these tests.
- Bootstrap fixtures cover both bundle formats and reject a second worker alongside prior archive security cases. Tests remove their temporary extraction directories.
- Archived source `ffde8a27097c465311f197325eb4dc75f20e2274` passed 60 API tests and 163 frontend tests, production build, ten bootstrap fixture cases and the UI checks. Existing DevExtreme CommonJS/source-map warnings remain. Real setup extraction and exact archived-script hashes passed; the extracted UI helper repeated the synthetic dialog tests. The signed worker and rollout tamper/missing-file/path checks passed.

## Published verification - 31 August 2026

Release `IAM/artifacts/agent-rollout/20260831-053826-7d5dfe55` deployed at `2026-08-31T05:41:58Z`. Both IAM IIS physical paths point into `C:/inetpub/FIN_PTS/Releases/20260831-053826-7d5dfe55-iam-agent/IAM`, with its sibling `AgentFeed` configured. The deployment held the shared IIS mutex, required previous release `20260831-052441-877883bb-iam-agent`, preserved protected configuration and created an IIS rollback backup. Database, PTS/SAP settings, Windows agent service and device trust were unchanged.

At `2026-08-31T05:42:40Z`, the public bootstrap returned HTTP 200, `text/plain; charset=utf-8` and `Cache-Control: no-store`. Its complete body matched the archived template with the publicly verified IAM issuer and current setup digest. The direct issuer download (`https://iam.local.fujitecindia.com/api/v1/agents/download`) matched the release ZIP byte for byte; all ten entries included the new UI helper and exact archived installer scripts, with no Get-Credential call. The live IAM frontend index matched the published release too.

Setup SHA-256: `7B600C6622A9FAB4C8C6944AE44E6CCEE01A4C6546DFAC636ED93FEF4F61EE3B`. Worker remains the verified signed `1.0.0` release; this change updates the setup UI, not the agent worker.

Evidence under the release directory: `deployment-result.json`, `bootstrap-deploy-result.json`, `live-modern-installer-verification.json`, `served-install.ps1`, `served-setup.zip` and `verified-installer-ui/modern-iam-sign-in.png` / `modern-iam-mfa.png`. Dialog tests used synthetic values only; actual administrator sign-in, service installation and enrollment remain a target-PC action.

## Use

### Compact branding refinement

The follow-up replaces the Georgia text imitation with IAM Web's original `fujitec-logo.png` and uses its `favicon.ico` for the native window icon. Setup bundles both assets locally; the dialog never downloads branding at sign-in. Width is reduced from 580 to 440 device-independent pixels, with 36-pixel fields/buttons, a 21-pixel heading and tighter spacing. Both sign-in and MFA retain readable labels, keyboard handling, secret clearing and scrolling when the available work area is short.

The branded bundle has twelve files. Bootstrap also accepts the earlier nine- and ten-file formats, but rejects incomplete branding groups and all previously rejected archive cases. Existing UI fixtures verify logo/icon decoding and render at the actual dialog width. The source assets are copied unchanged; no replacement artwork or generated logo is used.

Compact source `5ecde12412bc4574c380738ee2d59cecb39c0593` was published as `20260831-054840-9db5605a`. All 60 API tests, 163 frontend tests, twelve bootstrap fixtures, dialog fixtures and rollout validation passed. The real extracted bundle repeated the dialog checks and both branding files matched their archived IAM Web originals. Screenshots are saved under `verified-installer-ui` in that artifact.

The first deployment guard stopped before any change because another task had switched IAM to `20260831-054119-868240ae-iam-agent`. Its receipt, source (`ffde8a2`) and exact live index were verified; the compact build already included all that code. With this reviewed baseline, the guarded IAM-only switch succeeded at `2026-08-31T05:53:09Z`. PTS's concurrent update, protected configuration, database, installed agent service and enrollment remained unchanged. `first-guard-result.json` retains the initial refusal.

Live verification at `2026-08-31T05:53:22Z` confirmed HTTP 200/no-store bootstrap, exact setup ZIP and frontend index, twelve setup entries, and byte-identical original logo/icon assets. Current setup SHA-256: `5765D71893AB42765A889CFCDEDF4D85DB54A4797C1051A46FC7028778F7F664`. Evidence is in `live-modern-installer-verification.json` and `deployment-result.json` under this compact release. The user's screenshots independently showed their existing agent trusted and reporting inventory; deployment did not reinstall it. Existing installations need no reinstall for this setup-only visual change.

Run Windows PowerShell as administrator, then:

```powershell
irm https://iam.local.fujitecindia.com/identity/install.ps1 | iex
```

Cancel any already-open legacy prompt first. A new download is required; files extracted by an older command do not update in place. Enrollment still requires an authorized IAM administrator on the target PC.
