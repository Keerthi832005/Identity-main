# IAM.Agent one-command installation

User request: provide a short hosted PowerShell installation command like `irm <url> | iex`, rather than a long download/extract/install expression.

## Implementation

- The IAM API serves an uncached, embedded PowerShell bootstrap at `/install.ps1`. On local IIS the existing proxy exposes it at `https://iam.local.fujitecindia.com/identity/install.ps1`; no IIS route change is required.
- Bootstrap embeds the current setup ZIP SHA-256 and configured HTTPS issuer download URL. Request Host cannot redirect installation. It checks Windows x64/admin prerequisites, bounds download size/time, refuses redirects, validates the ZIP hash and rejects unsafe, duplicate, unexpected or missing archive files before extraction.
- Downloaded files are kept in a unique administrator/SYSTEM-only temporary directory. The existing installer retains signed worker validation, Windows script policy, IAM administrator login/MFA and enrollment/trust checks. No automatic device enrollment is performed by development tests or deployment.
- Machines & agents has expandable instructions, a copyable command and manual-copy fallback.
- Rollout publishing runs the PowerShell bootstrap fixture checks from the archived commit before API/UI builds.

## Validation

- Windows PowerShell 5.1: valid synthetic archive and seven rejection cases passed; the fixture installer was never executed. Temporary test directories removed; no test databases or devices created.
- API tests: 60 passed, including anonymous plain-text bootstrap, current release hash, missing configuration, and request Host isolation. Additional coverage verifies HTTPS-only configuration and missing setup behavior.
- Frontend production build passed with existing DevExtreme CommonJS warnings.
- Deployment evidence, when available, is recorded below. A source/build success alone does not make the command live.

## Deployed verification — 31 August 2026

Implementation commit `8d711b3ce63c12f2196f295f34c64fd6c2b352f2` was published as `IAM/artifacts/agent-rollout/20260831-052441-877883bb`. The archived source repeated all 60 API tests, 150 frontend tests and eight Windows PowerShell extraction/validation cases successfully. Package signatures/hashes and negative rollout fixtures passed. Other tasks' uncommitted/staged files were excluded from the source archive.

Browser verification used synthetic inventory on an isolated local fixture, not live employee/device data. Expanding the instructions displayed the command; Copy command reported success. Screenshot: `install-command-panel.png` under the release directory. The fixture did not execute the bootstrap or register a device.

The administrator deployment held the shared IIS mutex and checked that the active IAM release was still `20260831-051806-26aa9b89`, preventing an overwrite of a concurrent rollout. Scoped preflight and deployment succeeded at `2026-08-31T05:29:06Z`. Previous paths and protected settings were preserved for rollback; only IAM paths/feed and its API pool changed. PTS/SAP configuration, database schema/data and agent service/device trust were unchanged.

Live `https://iam.local.fujitecindia.com/identity/install.ps1` returned HTTP 200, `text/plain; charset=utf-8` and `Cache-Control: no-store`. Its complete body matched the embedded script with the configured issuer and setup SHA-256 `C6212E2EC2ADE39383E8EB9279CE1D0095D7D2B372264C4198921757B83DEAB4`; the served script parsed successfully without execution. Live UI chunk `chunk-Bf7dSvzs.js` matched the published installation panel. IAM/DS4/QS4 readiness, transport, discovery and authorization checks passed.

Evidence: `bootstrap-deploy-result.json`, `deployment-result.json`, `live-bootstrap-verification.json`, `served-install.ps1` and the screenshot in the release directory. **The command is live; actual installation and IAM administrator enrollment remain an action on each target PC.**

## Boundaries

The bootstrap itself is trusted via HTTPS from the IAM installation. The setup ZIP digest detects corruption or a release change; it is not an independent signer for the bootstrap. The existing pilot signer is unchanged. Installation still requires an authorized administrator on each target computer; no execution-policy or certificate-validation bypass is added.
