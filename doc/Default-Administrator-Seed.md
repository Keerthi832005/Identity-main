# Initial IAM administrator

The explicit `seed-default-administrator` command initializes a migrated, empty
IAM database with this administrator:

| Field | Value |
| --- | --- |
| Employee code | `INDE03275` |
| Display name | `Siddeswaran S` |
| Email | `Siddeswaran.S@fujitec.co.in` |
| Application | `iam-administration` |
| Role | `identity-administrator` |
| Capability | `iam.admin` |

The seed also provisions the `identity-admin-web` public client. It does not
grant PTS access or configure IIS. Run it from a trusted administration process
using the selected environment's database connection, after migrations and
before importing the ordinary employee roster.

```powershell
# Preview: requires only IDENTITY_DATABASE_CONNECTION.
dotnet '<release>\admin-cli\Identity.AdminCli.dll' seed-default-administrator

# Apply: requires the selected environment's protected bootstrap configuration.
dotnet '<release>\admin-cli\Identity.AdminCli.dll' seed-default-administrator --apply
```

Use the same protected environment variables as the existing bootstrap command:
`IDENTITY_DATABASE_CONNECTION`, `IDENTITY_JWT_ISSUER`, `IDENTITY_JWT_KEY_ID`,
`IDENTITY_JWT_PRIVATE_KEY_PEM`, `IDENTITY_SECURITY_KEY_ID`,
`IDENTITY_ENCRYPTION_KEY`, `IDENTITY_CHALLENGE_KEY`,
`IDENTITY_IDENTIFIER_HASH_KEY`, `IDENTITY_BOOTSTRAP_PASSWORD`, and
`IDENTITY_BOOTSTRAP_CLIENT_SECRET`. The password must meet the existing
bootstrap requirements; there is no checked-in or automatic default password.
The encryption, challenge and identifier keys are separate 32-byte base64
values. Runtime API configuration must use the matching environment keys and
issuer. Never copy another environment's key bundle. Remove the bootstrap
password/client secret variables after use; keep all secrets out of arguments,
logs, receipts and Git. See [Identity operations](Identity-Operations.md).

Without `--apply`, the command returns `would-create` and changes no accounts.
Creation, credentials, roles and web provisioning share one SQL transaction.
A matching active administrator returns `already-seeded` without resetting
credentials or adding permissions. Inactive accounts, a different email,
revoked administrator access, or a nonempty installation without this
administrator cause refusal. Use authorized administration to resolve those
cases; the seed must not restore revoked access.

The ordinary `seed-default-employees` command now carries the supplied email
for `INDE03275`, but does not promote roster users to administrator. The other
employee with the same display name remains a separate account.

Run `powershell -NoProfile -File IAM/scripts/Test-DefaultAdministratorSeed.ps1`
from the repository root for real SQL verification. It creates and removes
exact disposable databases on the workstation's `SQLEXPRESS2022` instance;
deployment databases are never used. Coverage includes preview, rollback,
administrator login, replay, revoked access and roster preservation.
