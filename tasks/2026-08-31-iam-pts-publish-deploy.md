# IAM and PTS local publication and deployment — 31 August 2026

Status: completed at **11:20:20 IST** (`2026-08-31T05:50:20Z`).

## Scope and source

Published and deployed IAM API/administration UI/agent downloads, PTS APIs for DS4 and QS4, PTS Portal, and connected Terminal **v0.1.3**. Builds used isolated archives of commit `ffde8a27097c465311f197325eb4dc75f20e2274`, including searchable manager/department/team fields, themed confirmation dialogs, and the modern agent installer. Parallel tasks' working files were excluded. Terminal version allocation was committed separately in `dcd7541`.

## Running releases

| Component | Published artifact | Final IIS physical path |
| --- | --- | --- |
| IAM API | `IAM/artifacts/agent-rollout/20260831-054119-868240ae` | `C:/inetpub/FIN_PTS/Releases/20260831-054119-868240ae-iam-agent/IAM/Api` |
| IAM administration UI | same IAM rollout | `C:/inetpub/FIN_PTS/Releases/20260831-054119-868240ae-iam-agent/IAM/Frontend` |
| PTS DS4 API | `PTS/artifacts/publish/20260831-053856-4ea205f4` | `C:/inetpub/FIN_PTS/Releases/20260831-053856-4ea205f4/PTS/DS4/Api` |
| PTS QS4 API | same PTS publish | `C:/inetpub/FIN_PTS/Releases/20260831-053856-4ea205f4/PTS/QS4/Api` |
| PTS Portal and Terminal | Portal from the PTS publish; Terminal from `PTS/artifacts/terminal-publish/20260831-054025-1bdf1f74` | `C:/inetpub/FIN_PTS/Releases/20260831-054025-1bdf1f74-terminal/PTS/Frontend` |

Public entry points:

- IAM: https://iam.local.fujitecindia.com/
- PTS Portal: https://ptsapp.local.fujitecindia.com/
- PTS Terminal: https://ptsapp.local.fujitecindia.com/terminal/

## Data and configuration preservation

- Confirmed protected IIS SQL overrides target `HOCOM18502627\SQLEXPRESS2022`: `FIN_IAM`, `FIN_PTS_DS4`, `FIN_PTS_QS4`.
- Compared archived migrations with all three journals: IAM 13/13 and each PTS database 36/36 applied, with zero pending scripts. No migration, seed, credential reset, permission grant, or business-data write was performed for this deployment.
- PTS API base/profile configuration was compared with live settings before deployment, allowing only normalization of the already-selected worker mode. Both environments retained `BackgroundProcessing__HostMode=Api`; no separate worker was started and no new worker activation or SAP posting mode was enabled.
- IAM runtime settings, Portal runtime settings/proxy rules, TLS configuration, and protected application-pool settings were preserved. API and frontend deployers held the shared local IIS deployment mutex and retained prior immutable releases/IIS backups.
- Web publication refreshed agent setup/downloads using the existing signed 1.0.0 worker package. This rollout did not install, enroll, restart, or update an IAM.Agent Windows service on a terminal PC.

## Verification

- IAM: 60 API tests and 163 frontend tests passed; production build, installer/bootstrap fixtures and signed-package/manifest tamper checks passed. One initial invalid-rate-limit startup test returned `ObjectDisposedException` rather than its expected exception; a fresh full publication rerun passed all 60 API tests without code changes. The failed artifact was not deployed.
- PTS backend: 346 tests passed; 81 database/external-environment tests were skipped by their existing configuration. No SAP mutation tests were run against live systems.
- Portal: 85 tests passed and production build succeeded. Terminal: 308 tests passed, production build/artifact checks succeeded, and v0.1.3 was allocated.
- Windows PowerShell deployment helper checks and administrator preflight passed. Exact file contents were checked during copying and through public HTTP delivery.
- Final public checks verified 18 asset/readiness responses against the published releases, including all three healthy database readiness endpoints and the exact Terminal version/release ID. IIS checks also verified trusted HTTPS, issuer/JWKS, protected API 401 responses, rejected service routes, and SPA routing. Terminal's scoped deployer passed 13 route/config/version/asset checks.
- Browser verification showed IAM and Portal sign-in pages and Terminal's employee sign-in screen with v0.1.3. Terminal showed the existing authenticated DS4 backend/Terminal 2 context. No credentials were submitted and no production operation was started. No browser error-level entries were found; the existing DevExtreme theme timeout warning remained on IAM/Portal, and Terminal had no console entries.
- The parallel task independently repeated the final IIS/public health and browser checks after the completed rollout and confirmed the same successful result without submitting credentials or changing data.

## Deployment safeguards exercised

The first preflight refused a concurrent IAM path change. Inspection confirmed another task had deployed the identical `ffde8a2` source without changing protected settings; the baseline was reviewed before proceeding.

PowerShell 7 and Windows PowerShell 5.1 produced different aggregate directory hashes because of culture-dependent file ordering. Individual API filenames and hashes were compared and identical. The operational manifest was then calculated with the same PowerShell 5.1 runtime as the deployer; artifact contents were not changed.

IAM and PTS API switches succeeded. The first Portal switch failed its immediate served-byte check and rolled back its own path. A fresh Portal destination with a scoped PTS-Web pool recycle and bounded exact-byte checks succeeded. The first failure did not record which response mismatched, so its precise cache/propagation cause was not proven. Terminal was then deployed over that verified new Portal, preserving its files. The final combined result is successful.

## Evidence and rollback references

Ignored operational records, wrappers, fixed source archive and build logs are under:

`PTS/artifacts/full-rollout/20260831-053856-4ea205f4/`

Relevant records: `database-preflight.json`, `full-preflight.json`, `full-deployment.json`, `public-verification.json`, `portal-deployment.json`, and retained `full-first-attempt.json` / `portal-first-attempt.json`. The IAM and Terminal artifact directories contain their successful deployment receipts; the PTS publish contains `api-deployment-result.json`. Local wrappers are specific to the reviewed paths and are not a generic reusable deployment entry point.

Previous releases remain available: IAM `20260831-053826-7d5dfe55-iam-agent`, PTS APIs `20260831-005211-ac86192b`, and the original Portal/Terminal root `20260831-041928-2aa99a73-terminal`. The intermediate successful Portal root `20260831-053856-4ea205f4-portal2` also remains. Any rollback must verify the current site path and scope before switching it; never restore an entire IIS backup over unrelated concurrent work.
