# IAM.Agent local rollout preparation

**Historical preparation and SQL recovery record.** The initial blocked state and numbered sequence below describe the earlier 31 August 2026 run; they are not the current deployment instructions. Local deployment subsequently completed in [A007](../tasks/A007-iam-api-app-deployment.md), followed by the [modern installer](../tasks/modern-agent-installer.md) and [reinstall verification](../tasks/agent-reinstall-fix.md). Use the root [deployment guide](../../DEPLOYMENT.md) and the actual target journal/current receipt before selecting a release or migration.

The agreed production design uses `https://iam.fujitecindia.com` for IAM app/API and `https://pts.fujitecindia.com` for PTS app/APIs, with separate internal IIS sites/pools. See [production hostnames and topology](../../DEPLOYMENT.md#production-hostnames-and-topology). Production/fleet rollout is not completed by these local records, and the guarded local deployer below is not a production installer.

Prepared 2026-08-31. **No live deployment, SQL migration or service installation was performed.** This continues the completed agent implementation with an IAM-only server release. It does not deploy PTS or include the parallel agent's uncommitted changes.

## Verified release

- Artifact directory: `IAM/artifacts/agent-rollout/20260831-030922-de82aaff`.
- IAM application source: committed revision `cd5a81214dd63089cd4d3088d747decdd51a9739`, extracted using `git archive` into an isolated build directory.
- Payload: IAM API, IAM frontend, signed agent feed and the package verifier/public key. `rollout.json` records every payload path and SHA-256 digest.
- Agent: existing 1.0.0 local pilot package from A004. Its signer is for laptop acceptance; it is not an organization-approved production signer.
- Isolated checks: 56 API tests and 134 frontend tests passed; API/frontend production builds passed. Existing frontend dependency warnings remain.
- Artifact checks passed in both PowerShell 7 and Windows PowerShell 5.1: valid package, modified content, extra files, missing components, wrong owner, unresolved source revision, path traversal and invalid digests.

Reproduce the package without touching the active workspace dependencies:

```powershell
./IAM/scripts/Publish-AgentRollout.ps1 `
  -AgentReleasePath './IAM/artifacts/agent-publish/1.0.0-07c4d03c' `
  -SourceRef cd5a81214dd63089cd4d3088d747decdd51a9739

./IAM/scripts/Test-AgentRollout.ps1 `
  -RolloutPath './IAM/artifacts/agent-rollout/20260831-030922-de82aaff'
```

## Pre-restart blocker and evidence

Public checks: IAM `/identity/health/live` returns 200, `/identity/health/ready` times out, and the agent download returns 404. Windows reports the SQL Express service running, but both ordinary and elevated database connections fail. The elevated deployment preflight stopped at `DatabaseConnection`; its receipt explicitly records no database, PTS or service changes.

Read-only SQL diagnostics found repeated error 701 messages about insufficient memory in resource pool `internal`, plus system-task/logon errors 17300/17189. This confirms SQL Server is reporting memory exhaustion; it does not establish the underlying cause or promise a restart will cure it. Diagnostics and public/preflight receipts are under the artifact directory; no credential values are recorded.

Windows reported approximately 22 GB of free physical memory during the check. No other application's process was stopped to reclaim memory; instance-specific diagnosis is still needed once SQL accepts connections.

Microsoft's [SQL Server memory troubleshooting guidance](https://learn.microsoft.com/en-us/troubleshoot/sql/database-engine/performance/troubleshoot-memory-issues) treats a service restart as a last resort when the instance cannot process queries. It drops active connections and clears caches. Because this SQL instance is shared by IAM, PTS and other work, a coordinated restart requires explicit user approval; no SQL restart, session termination, cache clearing, configuration change or IIS reset was attempted.

## Approved recovery — 2026-08-31 08:54 IST

The user subsequently approved restarting `SQLEXPRESS2022`. The initial stop wait timed out, but an additional graceful wait completed; no process was forcibly terminated. SQL Express restarted from PID 31112 to PID 56628. IIS and SQL Developer service/process identities were unchanged.

All three application databases are online, SQL connections succeed, and IAM/DS4/QS4 readiness returns HTTP 200. SQL process low-memory flags are false. These checks establish recovery, not a permanent fix for the earlier memory exhaustion. No memory setting, database schema, credential or device trust changed. Evidence is under `IAM/artifacts/sql-recovery/20260831-032142/`.

## Administrator sequence after database recovery

1. Confirm IAM readiness succeeds. Inspect memory pressure/configuration and remaining SQL errors; do not assume a restart alone resolves the cause.
2. Back up `FIN_IAM` using the existing database procedure and review the missing migrations before applying them. The post-restart journal ends at 0010, so both `0011_bulk_data_staging.sql` and `0012_agent_inventory.sql` are pending. Do not silently run the complete migration runner without reviewing the additional bulk-staging prerequisite. The server deployment helper deliberately does not migrate SQL or grant permissions. It refuses to switch without the agent table and migration journal entry. Do not run IAM migrations against either PTS database.
3. Run the guarded preflight in an administrator PowerShell window:

   ```powershell
   ./IAM/scripts/Deploy-AgentRolloutLocalIis.ps1 `
     -RolloutPath './IAM/artifacts/agent-rollout/20260831-030922-de82aaff' -ValidateOnly
   ```

4. After it passes and the release is approved, run the same command without `-ValidateOnly`. It acquires the existing local IIS deployment mutex, backs up IIS, copies to a new immutable directory, retains current IAM settings/keys/frontend configuration and changes only IAM-Api/IAM-Web physical paths plus `AgentDistribution__RootPath`. It verifies readiness, inventory authorization and exact downloaded bytes. If verification fails, it restores those paths and the feed setting, provided they were not changed concurrently. It never restores the whole server configuration over another task's work. The actual switch/rollback still needs the administrator pilot; only artifact checks and the blocked read-only preflight have run in this session.
5. Follow [IAM-Agent.md](IAM-Agent.md) for the separate elevated agent installation and IAM administrator sign-in. Validate the real browser discovery, first inventory, restart/reboot and update cycle on the pilot machine before fleet deployment. PTS release coordination remains separate.

The A004 temporary database cleanup is resolved. After recovery, the exact test database `IAM_AgentTests_1ef6c315746348ddb37b421cd0bbef03` was matched to its 08:30:34.953 creation time, verified to contain zero user objects and have zero active sessions, then removed without forcing connections. No pattern-based database deletion was used.
