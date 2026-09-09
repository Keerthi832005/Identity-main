# A005: Prepare IAM.Agent server rollout

- Status: COMPLETED
- Objective: Prepare a reproducible IAM-only release and guarded administrator deployment for the next local pilot step.
- Scope: Committed-source build isolation, artifact hashes, read-only health checks, IAM-only IIS switch/rollback and deployment receipt. Preserve PTS and concurrent work.
- Requirements covered: User requested continuing after IAM.Agent implementation and synthetic acceptance.
- Dependencies: A001–A004.
- Risks/assumptions: Current non-elevated session cannot inspect protected IIS state or install services. Current IAM readiness requests time out; deployment must refuse an unhealthy database. The existing local acceptance signer is for the laptop pilot only, not production approval.

## Implementation Steps

1. Inspect public health, current scripts, permissions and unchanged concurrent files.
2. Build IAM from a fixed committed source archive, never from another agent's dirty files.
3. Package an IAM-only administrator deployment with artifact verification, database preflight, scoped backups and rollback; preserve all PTS sites and secrets.
4. Run artifact/security checks and record the concrete next administrator action.

## Acceptance Criteria

Release identifies its application source commit and file hashes. Deployment refuses tampered packages, unknown IIS ownership, missing migration or unhealthy database. No blanket IIS reset, credential reset or unrelated changes occur. Live installation is never claimed from preparation alone.

## Required Tests and Validation

PowerShell parser and adversarial artifact checks; isolated IAM API/frontend build and tests; read-only live readiness/download checks. Administrator-only checks remain explicitly pending if elevation is unavailable.

## Validation Results

- Isolated application release built from commit `cd5a81214dd63089cd4d3088d747decdd51a9739`; API tests 56 passed, frontend tests 134 passed and both production builds passed. Artifact: `artifacts/agent-rollout/20260831-030922-de82aaff`.
- Valid artifact and seven rejection checks passed in PowerShell 7 and Windows PowerShell 5.1. Fixed a PowerShell 5.1 connection-string builder property adaptation issue found by the real elevated preflight.
- Administrator read-only preflight verified IIS ownership then stopped at `DatabaseConnection`. Existing readiness times out; download remains 404. Receipt records no live database/PTS/service changes. Actual IIS switch and rollback remain unexecuted pilot checks.
- SQL error-log diagnostics show repeated error 701 (insufficient memory in internal resource pool), plus logon/system-task failures. The service remains running but unavailable. A coordinated SQL restart requires explicit approval because other tasks/apps share it.
- Runbook and recovery boundary: `doc/IAM-Agent-Rollout.md`. Only task-owned files are included in the focused commit.

## Definition of Done

Verified rollout package, documented preflight and blockers, and a focused commit containing only this task's files.
