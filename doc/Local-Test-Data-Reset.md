# Local IAM test-data reset

## Scope and consequences

`scripts/Reset-LocalTestData.ps1` is a standalone Windows-authenticated maintenance tool for **FIN_IAM only**, on the local `HOCOM18502627\SQLEXPRESS2022` instance. It has no dependency on the PTS folder and cannot be pointed at an arbitrary database.

This is a **full reset**, not selective removal of generated test rows. Apply mode clears all 16 Identity tables, including real/seeded rows if present:

- Applications, clients, modules, capabilities, roles, role grants, and user permission overrides.
- User accounts, credentials, application access, and role assignments, including all administrators.
- Devices, MFA methods/challenges, refresh tokens, and authentication audit history.

The database, schema, tables, indexes, procedures, triggers, and `dbo.SchemaVersions` remain. Business identity counters are reset. Neither PTS database is cleared by this script. No SAP requests are made.

## Do not touch dbo.SchemaVersions

**`FIN_IAM.dbo.SchemaVersions` must remain untouched during every data-clear/reset operation.** Do not truncate, delete from, drop, or reseed this table. Preserve every existing migration-history row and its identity values; it records which DbUp migrations have already run.

The reset script excludes this table and verifies its contents are unchanged before committing the database transaction. All 7 migration records were preserved by the approved reset below. Keeping migration history does not recreate cleared users or application registrations.

## Preview first

Run from the repository root:

```powershell
./IAM/scripts/Reset-LocalTestData.ps1
```

Or, from the independent IAM repository root:

```powershell
./scripts/Reset-LocalTestData.ps1
```

Without `-Apply`, the script performs read-only inspection and prints aggregate counts. It does not select or print credentials, tokens, MFA secrets, audit payloads, or personal account data. It creates no backup and changes no application data.

## Apply only after explicit approval

1. Confirm that **all IAM accounts/configuration/audit history may be removed**. Keep this out of unattended startup, migrations, and CI.
2. Stop IAM API, PTS API/workers, and provisioning/test jobs. Local process detection is a safeguard, not proof that remote hosts or every SQL connection are stopped. Arrange exclusive maintenance access and ensure no process can repopulate IAM during reset.
3. Securely retain the existing signing/encryption/HMAC keys outside source control. SQL backups do not include runtime secret-provider configuration; those keys are necessary if restoring old MFA or other protected state.
4. Confirm Windows/SQL permissions for metadata inspection, backup/verification, and the scoped reset. The local SQL certificate trust setting is not intended as production TLS guidance.
5. Execute the explicit apply command:

```powershell
# DESTRUCTIVE: removes every IAM account, application, role, session, and audit row.
./IAM/scripts/Reset-LocalTestData.ps1 -Apply -ConfirmDatabase FIN_IAM
```

`-Apply` without the exact confirmation value fails before any connection or backup. An unexpected server, table list, foreign-key boundary/state, or detected running local IAM/PTS host stops apply mode.

## Backup and transactional safeguards

- Creates a uniquely named `FIN_IAM_before_identity_test_reset_<UTC>_<GUID>.bak` in SQL Server's configured default backup directory; does not intentionally overwrite existing files.
- Uses `COPY_ONLY, CHECKSUM` and completes `RESTORE VERIFYONLY ... WITH CHECKSUM` before clearing anything. The verified backup path is printed. Verification is **not** a completed restore/recovery drill.
- Reconstructs the current enabled/trusted foreign keys inside one transaction, allowing the 16 tables to be truncated without disabling audit triggers.
- Compares foreign-key definitions, migration-journal rows, and trigger definitions/states; checks empty tables and reset identity counters before committing. A failure rolls back the reset.
- Does not delete backups, kill processes, assign replacement passwords, recreate users, or bootstrap applications automatically. Backup failure prevents the reset.

Treat the backup as sensitive: it contains password hashes, encrypted MFA state, session records, and audit/account data. Protect it with the approved filesystem access and retention policy. Restore only with explicit approval, preferably to a separate database first.

## After a full reset

All logins and application access are gone. Retaining migration history means replaying migrations alone will not recreate accounts or catalogs.

1. Before reusing numeric user/application IDs, address previously issued access tokens. Clearing SQL refresh tokens prevents refresh, but does not guarantee every consumer rejects an already-issued JWT. Keep consumers stopped until old access tokens expire, or perform coordinated development signing-key rotation and consumer/JWKS-cache invalidation. Do not silently rotate production keys.
2. Bootstrap the initial administrator using the existing `Identity.AdminCli bootstrap` workflow and secured environment values documented in [Identity Operations](Identity-Operations.md#initial-administration-bootstrap). Confirm your secret/key recovery plan before reset.
3. Provision the IAM web client with `provision-administration-web`, then provision the PTS application manifest using the newly resolved administrator ID. Do not assume old IDs still identify the same accounts/resources.
4. Recreate intended role-test users, credentials, devices, and required application/role grants. This script does not create those users.
5. Clear old browser sessions, sign in again, and validate IAM/PTS authorization before re-enabling consumers/workers.

## Validation status

Script creation alone does not authorize executing the reset. Preparation used only parser/guard checks and a read-only preview. The user subsequently explicitly authorized apply mode; its result is recorded separately below.

Preparation validation on 2026-08-30:

- PowerShell parser: passed, no syntax errors.
- Apply without confirmation and with `FIN_PTS_DS4` confirmation: both rejected before opening a connection.
- Read-only preview: 16 Identity tables, 3460 data rows, 39 enabled/trusted foreign keys; the database also retains 7 migration-history records.
- At the preparation stage no destructive apply or backup was run, and database contents remained unchanged.

## Approved reset result — 2026-08-30

- User authorization: explicit `yes apply` after the full-reset consequences were explained.
- Executed at approximately 02:08:03 UTC / 07:38:03 India time using `./IAM/scripts/Reset-LocalTestData.ps1 -Apply -ConfirmDatabase FIN_IAM`.
- Cleared 3460 rows from all 16 Identity tables; a separate read-only inspection confirmed 0 remaining data rows and 0 user accounts.
- Preserved all 7 `dbo.SchemaVersions` rows, all 39 enabled/trusted foreign keys, and the enabled `TrAuthenticationAuditAppendOnly` trigger. `DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS` reported no violations.
- Created and checksum-verified the COPY_ONLY backup before truncating. This is backup-readability verification, not a completed restore drill.
- Retained backup: `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS2022\MSSQL\Backup\FIN_IAM_before_identity_test_reset_20260830T020803Z_a58fc900bca048d680e314487502a8c8.bak`.
- No existing backup was overwritten. Neither PTS database was changed by this operation; no SAP request, account re-creation, runtime-key change, or Git commit was performed.
- Logins will remain unavailable until the approved administrator/application provisioning steps above are completed. Old runtime secrets remain external and must be retained securely if the backup needs restoring.
