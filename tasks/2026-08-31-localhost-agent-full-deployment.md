# IAM and PTS deployment with localhost agent access

Completed: **31 August 2026, 14:33:22 IST** (`2026-08-31T09:03:22Z`).

## Published components

The user authorized publication/deployment of both IAM and PTS, then explicitly
requested HTTP localhost and 127.0.0.1 origins on any port. Fixed implementation
source: `dcdaeb41f038ff584f89b818a1fcccabd4e7f75f`, including
`93bc35f` (opt-in loopback development origins). Earlier directory, inventory,
security, session-name and sidebar commits are included.

| Component | Version |
| --- | --- |
| IAM API | 1.0.3 |
| IAM administration app | 0.1.4 |
| PTS DS4 / QS4 APIs | 1.0.3 |
| PTS Portal | 0.0.3 |
| PTS connected Terminal | 0.1.6 |
| IAM.Agent worker and downloadable setup | 1.0.4 |

The installed supervisor remains **1.0.1**; its binary was not replaced. It
verified and activated worker 1.0.4 through the existing signed feed.

## Running sites

- IAM: <https://iam.local.fujitecindia.com/>
- PTS Portal: <https://ptsapp.local.fujitecindia.com/>
- Terminal: <https://ptsapp.local.fujitecindia.com/terminal/>
- Local QS4 development terminal: <http://127.0.0.1:4303/terminal/connect>

| IIS component | Final physical root |
| --- | --- |
| IAM API | `C:/inetpub/FIN_PTS/Releases/20260831-085511-ad40d7d5-iam-agent/IAM/Api` |
| IAM app | `C:/inetpub/FIN_PTS/Releases/20260831-085511-ad40d7d5-iam-agent/IAM/Frontend` |
| PTS DS4 API | `C:/inetpub/FIN_PTS/Releases/20260831-085433-c77866eb/PTS/DS4/Api` |
| PTS QS4 API | `C:/inetpub/FIN_PTS/Releases/20260831-085433-c77866eb/PTS/QS4/Api` |
| PTS Portal / Terminal | `C:/inetpub/FIN_PTS/Releases/20260831-085433-c77866eb-web/PTS/Frontend` |

## Localhost policy

`AgentSettings.AllowLoopbackDevelopmentOrigins` defaults to false, preserving
existing installations' exact HTTPS-origin policy. When explicitly enabled,
`/v1/identity` also allows `http://localhost` and `http://127.0.0.1` with any valid
port. The complete origin must match: lookalike domains, other addresses,
alternative IP spellings, URL credentials, paths and malformed origins are
rejected. CORS returns the approved origin, not `*`. The identity response uses
that same approved origin so Terminal's existing origin validation remains in
force. The local API remains read-only and loopback-bound; its Host guard,
custom-header requirement, supervisor health token and credential-free requests
remain unchanged.

Enabled `allowLoopbackDevelopmentOrigins: true` only on this development PC.
This permits identity discovery by any application served on these local hosts.
The registered HTTPS origin, update feed, signer, Terminal 2 registration,
enrollment and device key were retained. A protected configuration backup is at
`C:/ProgramData/IAM.Agent/agent-before-loopback-20260831-085433-c77866eb.json`.
Set the flag to false and restart IAM.Agent to disable development access.

IAM permissions, device trust checks and Secure/HttpOnly cookies were not
weakened. Agent discovery was verified without submitting credentials; a full
authenticated HTTP localhost login/refresh lifecycle was not tested.

## Verification and preservation

- Agent tests: 23 passed, 2 existing opt-in/process tests skipped. New HTTP tests
  exercise allowed and rejected origins with the setting both enabled and
  disabled, including preflight, identity responses, Host checks and writes.
- IAM API: 77 passed. IAM frontend: 180 passed. Signed-package verification and
  artifact tampering/path-traversal fixtures passed.
- PTS backend: 346 passed, 81 existing SQL/live-environment tests skipped.
  Portal: 85 passed. Terminal: 308 passed. All production builds passed.
- Read-only migration comparison: IAM 14/14, DS4 36/36, QS4 36/36; no pending or
  unknown scripts. No migration, seed, credential reset or business-data write
  was performed by deployment scripts.
- All three readiness endpoints, trusted HTTPS, issuer/JWKS, protected-API
  rejection, SPA routing and fail-closed service routes passed. **102 public
  HTTP file responses** matched the release hashes.
- Browser checks showed IAM and Portal sign-in screens, published Terminal
  0.1.6, and successful discovery of `HOCOM18502627 / Terminal 2 / IAM.Agent 1.0.4`
  on HTTPS and both localhost forms. No credentials or operational actions
  were submitted.
- Live preflights accepted localhost, 127.0.0.1 and the registered HTTPS origin;
  unrelated/lookalike origins returned 403. Signed Windows session/profile
  metadata from worker 1.0.4 was confirmed in IAM after the service restart.
- Database connections, IIS bindings/pool settings, application settings and
  existing API-hosted worker modes were preserved. No separate PTS worker or
  additional SAP posting mode was enabled. Windows was not restarted.

## Recovery and evidence

The initial build was superseded when localhost support was requested. Reserved
versions were retained instead of reused. The replacement build's final staging
guard detected the desktop app's automatically staged version files. A separate
review confirmed that only the nine allocated version files changed, comparing
their content with the fixed source archive after normalizing only version
values; the successful artifacts were then sealed normally.

All IIS switches succeeded. Final byte-preservation verification identified
formatting differences in the DS4/QS4 profile JSON files copied into both PTS
APIs. A scoped recovery compared every JSON value, checked the original IIS
backup and current release ownership, and restored the exact original file
bytes. The original failure receipt remains intact. Recovery then verified all
releases, enabled the requested agent setting, restarted the service and
confirmed its signed worker/inventory update.

Authoritative success receipt:
`PTS/artifacts/full-rollout/20260831-085433-c77866eb/deployment-recovered.json`.
This ignored directory also retains source/archive metadata, test/build logs,
artifact seals, the original receipts, settings-recovery scripts and live origin
checks. Component artifacts are under:

- `IAM/artifacts/agent-publish/1.0.4-69b3d484/`
- `IAM/artifacts/agent-rollout/20260831-085511-ad40d7d5/`
- `PTS/artifacts/publish/20260831-085433-c77866eb/`
- `PTS/artifacts/terminal-publish/20260831-085917-5b695630/`

Previous immutable releases and IIS backups remain available. Any rollback must
check the current component path and restore only that component; do not restore
an entire IIS backup over later work. Agent configuration backups remain
protected and are not committed.
