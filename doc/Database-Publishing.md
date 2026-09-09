# IAM database publishing

IAM owns `Identity.Database`, `scripts/DatabasePublish.Common.ps1`, and
`scripts/Invoke-DatabaseMigration.ps1`. No PTS migration code is linked or run.

For each new remote publish select **UAT or Live**. UAT maps to
`FUJITECAPP2 / Fujitec_IAM_UAT`; Live maps to `FUJITECAPP2 / Fujitec_IAM_LIVE`.
An explicit selection authorizes applying pending migrations for that publish,
including initialization of an existing empty database. Do not silently reuse
the previous environment or ask for approval again during the same publish.

Inject `IDENTITY_DATABASE_CONNECTION` from the selected environment's protected
secret source into the publishing process; never put a password in a command,
artifact, receipt, or Git. Clear the variable afterward. With it already set:

```powershell
.\IAM\scripts\Publish-Identity.ps1 -Environment UAT
```

The publisher builds API, database tool, admin CLI and frontend, checks the exact
SQL target, applies pending embedded scripts and verifies zero remain before
completing the release version. `Publish-AgentRollout.ps1 -Environment UAT`
uses the same hook and its matching archived database tool; its existing
`-AgentReleasePath` argument is still required. Neither command activates IIS.
Do not use laptop-specific deployers for the FUJITECAPP2 shares.

To run the IAM tool from a previously published release:

```powershell
.\IAM\scripts\Invoke-DatabaseMigration.ps1 -Environment UAT `
    -DatabasePath '<exact release>\database' -CheckOnly
.\IAM\scripts\Invoke-DatabaseMigration.ps1 -Environment UAT `
    -DatabasePath '<exact release>\database'
```

For agent rollouts use `<exact rollout>\payload\database`. `-Environment Local`
requires explicit `-DatabaseServer` and `-DatabaseName`. A mismatched connection
is rejected before SQL changes. `-ArtifactsOnly` on the publisher explicitly
builds without touching a database, and cannot be combined with target options.
It is also the path for preparing artifacts before credentials are available.

For upgrades of existing data, the runner creates a unique SQL Server
`COPY_ONLY` backup with checksum and runs `RESTORE VERIFYONLY WITH CHECKSUM`
before applying anything. SQL Server's default backup directory is used unless
`-BackupDirectory` supplies a **server-local** path. Missing backup permissions
or storage stop the publish. Backups are retained; they are not restored or
deleted automatically. Empty databases have no existing application data to
back up; nonempty databases without a recognized journal are refused.

Scripts run transactionally one by one under a database-scoped migration lock.
Existing journal rows must remain unchanged. Unknown journal scripts (wrong
product or older release) stop execution. A failed script rolls back, but earlier
completed scripts remain journaled. Stop activation, inspect the sanitized
receipt and failed script, and fix forward or use an explicitly approved restore.
Never modify an already applied SQL file; historical SQL checksums are not stored.

Receipts record target, pending/applied script names, backup reference and outcome,
without credentials. API health, environment keys/client provisioning and IIS
routing still require deployment verification; schema success is not site success.

Verification: `powershell -File scripts/Test-DatabaseMigrations.ps1 -Product IAM`
uses exact disposable databases on the workstation SQL instance; it never uses
the deployment connection variable. Offline publish gates are covered by
`scripts/Test-PublishVersions.ps1`. Do not run the publisher tests concurrently
with another publisher because they deliberately exercise the publish mutex.
