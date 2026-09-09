# A004: IAM.Agent acceptance

- Status: COMPLETED
- Objective: End-to-end verification, safe rollout preflight and documented limitations.
- Scope: End-to-end verification, safe rollout preflight and documented limitations.
- Requirements covered: User's IAM-owned managed Windows agent; PTS consumes terminal identity.
- Files/components: IAM.Agent, Identity backend, IAM UI, scripts/tests/docs.
- Dependencies: A003.
- Risks/assumptions: IAM admin trust required; no credential collection or arbitrary remote command execution; preserve concurrent changes.

## Implementation Steps
Implement this slice; test security and recovery; review, document and commit.
## Acceptance Criteria
Repository tests, publication and isolated browser checks pass; administrator rollout steps and unavailable live checks are explicitly recorded without claiming fleet installation.
## Required Tests and Validation
Focused .NET/Angular tests, builds, migration/package/browser tests appropriate to this slice.
## Validation Results
- `dotnet test Identity.slnx`: 187 passed, 35 database-dependent tests skipped without SQL configuration. Agent tests ran with the published 1.0.0 and 1.0.1 executables; real upgrade and failed-candidate recovery passed.
- `scripts/Test-AgentInventory.ps1 -SqlServer ('lpc:'+$env:COMPUTERNAME+'\SQLDEVELOPER2022')`: eight schema/enrollment/report tests passed without skips; 12 migrations and replay passed; disposable database removed.
- One SQL Express rerun timed out during the existing base-schema migration and could not reconnect to drop its temporary database. An `IAM_AgentTests_*` database from about 08:30 IST on 2026-08-31 may remain. No SQL service restart or unrelated deletion was attempted. The script now prints the exact generated database identity and preserves the original failure when cleanup fails.
- IAM frontend: 134 tests passed; PTS frontend: 287 passed. Both production builds passed. Existing DevExtreme CommonJS, theme-load and rrule sourcemap warnings are recorded in the runbook.
- In-app browser: synthetic IAM machine details, hardware/software, software filtering and download URLs passed; PTS missing-agent guidance and blocked sign-in passed. Screenshots saved under ignored `artifacts/agent-acceptance/`. The fixture is owned by IAM and never deployed.
- Final setup archive signature/hash verified; PowerShell scripts parsed. Source/task commits: A001 `96c4c99`, A002 `861a651`, A003 `2823661`; this record accompanies the A004 documentation/acceptance commit.
- Runbook: `doc/IAM-Agent.md`. Actual service elevation, administrator enrollment, live HTTPS browser happy path, reboot/SCM recovery and fleet deployment remain commissioning steps, not claimed acceptance results. Production distribution must use an organization-approved signer.
## Definition of Done
Implementation, applicable checks, task record and focused Git commit.
