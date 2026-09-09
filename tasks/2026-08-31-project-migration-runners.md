# Separate IAM and PTS migration runners

## Scope

Implement the user's requested automatic migration check/apply for selected-target
publishing, with an independent runner and publishing entry point in each project.
Preserve concurrent work and commit only migration-related files.

- IAM: `Identity.Database`, `DatabasePublish.Common.ps1`, `Invoke-DatabaseMigration.ps1`.
- PTS: `PTS.Database`, `DatabasePublish.Common.ps1`, `Invoke-DatabaseMigration.ps1`.
- Targeted IAM/PTS API publishers migrate before completing release versions.
- `-ArtifactsOnly` explicitly builds without SQL changes. IIS and worker activation
  remain separate; legacy combined local IIS builds do not migrate implicitly.
- Exact target checks, a database migration lock, verified backups for existing
  data, transactional scripts, journal verification and sanitized receipts.
- Existing PTS migration 0036 keeps its coordinated drain/permissions workflow.
- Personal and repository `iam-iis-publish` skills updated for the user's standing
  migration authorization; a new UAT/Live selection is still required each publish.

## Verification

- IAM and PTS migration projects build without warnings/errors; scoped C#
  whitespace verification passes.
- SQL integration tests passed against disposable local databases for both real
  migration assemblies: check-only, target mismatch, fresh schema, no-op replay,
  and concurrent migration refusal.
- Upgrade fixtures passed: backup failure stops apply, successful verified backup,
  preserved sentinel data/history, failed-script rollback. PTS also passed the
  schema-split coordination guard and explicit coordinated invocation.
- Offline publisher tests passed: required target/secret, separate product hooks,
  artifact-only behavior, failed migrations prevent version completion, isolated
  backend builds, and existing-release preservation.
- Existing signed-agent rollout validation and negative manifest tests passed.
- Test databases were removed by exact generated name; no business database used.

## Approved IAM UAT execution

On 31 August 2026 at 11:38 UTC, the independent IAM runner checked
`FUJITECAPP2 / Fujitec_IAM_UAT`, reported 14 pending scripts, applied all 14, then
verified zero pending scripts on replay. The existing database was empty, so the
receipt records `empty-database-initialization` rather than a backup. No database
creation, reset, business-data seed, Live migration or PTS business migration ran.
The secret was injected privately into the process environment and was not written
to Git, artifacts, arguments or receipts.

Sanitized receipts are retained locally in the ignored directory
`IAM/artifacts/migrations/uat-20260831-113610`: `uat-check.json`, `uat-apply.json`,
and `uat-replay.json`. Earlier launcher attempts stopped before SQL execution
because that child shell could not resolve .NET; direct invocation succeeded.

This verifies the **schema**, not the entire UAT site. The existing frontend root
still returns HTTP 200, while `/login` and `/identity/health/ready` return 404.
API activation, protected runtime configuration and IIS routing remain separate
unfinished deployment work. No IIS configuration or running service was changed
by this migration task.
