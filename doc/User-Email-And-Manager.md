# User email and reporting manager

IAM owns these profile fields in `[Identity].[UserAccount]`:

| Field | SQL type | Meaning |
| --- | --- | --- |
| `Email` | `NVARCHAR(254) NULL` | Optional contact email; not a login identifier or verified recovery address. |
| `ManagerUserId` | `BIGINT NULL` | Optional self-reference to `UserAccount.UserId`; `NULL` means no reporting manager. |

Existing users retain `NULL` in both fields. Email is trimmed and validated by the domain/API and is not required to be unique. Employee code remains the unique login identifier. No email notifications, email verification, password recovery, or new permissions are introduced.

## Relationship rules

- The selected manager must be an existing active IAM user.
- A user cannot manage themselves or report to a descendant. The API checks the ancestor chain, rejects existing loops, and bounds traversal to 100 manager levels.
- Transaction-owned SQL application locks serialize profile/hierarchy mutations across API instances, including simultaneous opposite assignments.
- The database enforces the self-reference and direct self-assignment check, indexes `ManagerUserId`, and does not cascade deletion. Longer-cycle validation belongs to the administration command path; do not bypass it with direct hierarchy updates in SQL.
- Manager assignment does **not** assign roles, capabilities, application access, or approval authority. Managers later deactivated remain referenced until an administrator reassigns/removes them.
- Profile updates are audited as `UserProfileUpdated`. Repeating an unchanged update is a no-op; it does not create duplicate audit events or change security versions. Email values are not copied into audit payloads.

## API and UI

- `POST /api/v1/admin/users`: existing employee-code/display-name fields plus optional `email` and `managerUserId`.
- `PUT /api/v1/admin/users/{userId}/profile`: replace `displayName`, `email`, and `managerUserId`. Send explicit `null` to clear either optional field. Employee code and permissions are unchanged.
- User search, dashboard, access, and security summaries return `email`, `managerUserId`, and `managerDisplayName`. User search includes email.
- Both writes require IAM administration authorization. Invalid request fields return 400; rejected manager relationships return the existing administration-conflict response (409).
- In **Users & access**, use **New user** or **Edit profile**. Search managers by employee code, name, or email; up to 50 results are shown, excluding inactive users and the edited user. Choose **No manager** to clear the relationship. Existing light/dark theme styles and licensed controls are reused.

## Database rollout — not applied to FIN_IAM yet

Review [`0008_user_email_manager.sql`](../db/migrations/0008_user_email_manager.sql), then apply it with the normal `Identity.Database` runner before deploying the updated API. No existing migration file is changed and no existing user data is rewritten.

**Do not truncate, delete from, drop, reseed, or manually rewrite `dbo.SchemaVersions`.** The migration runner normally appends a new execution-history row when migration 0008 is applied. Existing history must remain intact. The reset scripts continue to exclude migration history.

Testing uses a separately created disposable database, never `FIN_IAM`, `FIN_PTS_DS4`, or `FIN_PTS_QS4`. Do not point `IDENTITY_TEST_SQL_CONNECTION` at those cleared business databases.

## Requested seed identity — pending account approval

- Employee code: `INDE03275`
- Contact email: `siddeswaran.s@fujitec.co.in`
- Manager: not supplied; leave `NULL` unless a manager is explicitly selected.
- Role and password: not inferred or assigned by this change. No account has been created.

The bootstrap CLI accepts `--email` alongside the existing identifiers. If this user is confirmed as the initial IAM administrator, supply the email through that option, use the approved display name, and supply passwords only through the existing secured environment workflow. Bootstrap requires an empty migrated IAM database and creates an administrator, so do not use it to create an ordinary user. See [Identity Operations](Identity-Operations.md#initial-administration-bootstrap).
