# A006: Approved SQL Express restart and recovery checks

- Status: COMPLETED
- Objective: Execute the user's explicit `yes restart` approval for `MSSQL$SQLEXPRESS2022`, then verify IAM and PTS recovery.
- Scope: Only the approved SQL service restart, read-only application/database checks and cleanup of this task's previously stranded empty test database. No live schema migration, configuration change or application deployment.
- Dependencies: A005 database readiness blocker.
- Risks/assumptions: IAM and PTS share this SQL instance; active connections were interrupted as disclosed before approval. Do not claim the underlying memory issue is permanently resolved.

## Implementation Steps

1. Confirm exact service/process ownership and preserve the other SQL instance and IIS.
2. Stop/start SQL Express through the normal service controller and allow additional graceful shutdown time.
3. Verify a fresh SQL process, online application databases, SQL memory flags and all three public readiness endpoints.
4. Identify and safely remove the empty temporary database left by the earlier failed agent test.

## Acceptance Criteria

The approved SQL service has a new running process; IAM and both PTS readiness endpoints return 200. Other SQL/IIS services are unchanged. No production schema, credentials or device trust are changed.

## Validation Results

- Restart completed at `2026-08-31T03:24:24Z` (08:54 IST). Process changed from 31112 to 56628.
- Initial 45-second stop wait timed out; an additional graceful wait completed successfully. **No forced termination occurred.** IIS and `SQLDEVELOPER2022` retained their service state and process identity.
- `FIN_IAM`, `FIN_PTS_DS4` and `FIN_PTS_QS4` are ONLINE. Integrated SQL connection succeeded; process physical/virtual low-memory flags are false after restart.
- IAM, DS4 and QS4 `/health/ready` checks all returned HTTP 200. No credential submission or business operation was performed.
- Removed only `IAM_AgentTests_1ef6c315746348ddb37b421cd0bbef03`, identified by the exact 08:30:34.953 creation time of our failed test. Verified zero user objects and zero active sessions immediately before removal. No forced disconnection or pattern-based deletion.
- IAM's migration journal currently ends at 0010. Both bulk-staging migration 0011 and agent migration 0012 remain absent; no migration was applied during this restart request. Review those prerequisites before the prepared agent server rollout.
- Sanitized evidence: `IAM/artifacts/sql-recovery/20260831-032142/` (`restart-result.json`, `recovery-result.json`, `verification.json`, `test-cleanup.json`).

## Required Tests and Validation

Direct service/process checks, SQL connectivity and database state, memory flags, and real HTTPS readiness. No code tests were needed for this operational restart.

## Definition of Done

Approved restart completed, recovery verified, temporary cleanup closed, and only this task's documentation committed.
