# T025: Bootstrap-to-web provisioning compatibility and approved local restoration

- Status: COMPLETED
- Scope: Restore explicitly approved INDE03275 access for PTS T031; fix known descriptive-text incompatibility between the bootstrap and administration-web manifests. No IAM UI revamp.

## Implementation and verification

The bootstrap ships different application, capability and role descriptions from web provisioning. The web runner now recognizes only those exact legacy descriptions and preserves them in its provisioning manifest. The general strict provisioning checks remain unchanged: unknown drift, wrong audience, inactive resources and other contract mismatches still fail.

A SQL-backed regression runs the real bootstrap, provisions the public administration client twice, verifies resource reuse and unchanged legacy text, rejects description/audience drift, and validates a real public-client JWT carrying `iam.admin`. IAM Release build and full SQL-backed suite passed: **94 tests, no failures/skips**. Solution-wide format verification passed; no dependencies changed.

## Approved local setup, 2026-08-30

- User explicitly approved INDE03275 as the initial IAM administrator and the controlled DS4 post. Employee code INDE03275, email siddeswaran.s@fujitec.co.in; manager remains unset. No invented reporting hierarchy.
- Before updating FIN_IAM, made a COPY_ONLY/CHECKSUM backup and ran RESTORE VERIFYONLY successfully. Backup: `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS2022\MSSQL\Backup\FIN_IAM_before_T031_20260830T064156Z_cf0ea774a2fe4ac081745b65b3566d9b.bak`.
- Applied DbUp 0008–0010 and replayed with no pending scripts. Compared original seven SchemaVersions rows before/after: unchanged. **SchemaVersions must never be truncated, deleted, reseeded or rewritten.** Forward DbUp appends are the only permitted migration-journal changes.
- Used AdminCli bootstrap and manifest provisioning, then authenticated administration APIs for application/role grants. No direct SQL user, credential or role inserts.
- User 1 has IAM `identity-administrator`; PTS application 2 has supervisor and integration-administrator assignments. The manifest retains its eight modules, fourteen capabilities, seven roles and forty-three role grants. No all-role grant or trusted-device/MFA bypass was added.
- Real `pts-web` password login issued an RS256 token accepted by PTS; actual browser login also passed. The terminal service client is not provisioned by this task, and PIN-terminal acceptance remains separate.

## Local runtime only

- IAM API: `https://localhost:7443` (trusted localhost development certificate), with loopback HTTP proxy at port 5000. PTS API: `http://localhost:5213`. PTS UI for this acceptance: `http://127.0.0.1:4202`; existing IAM UI occupies port 4200.
- Local browser cookies use an explicit non-Secure loopback-only development override. Checked-in production cookie and HTTPS-validation settings are unchanged. This is not approval for HTTP deployment.
- Random password, signing/encryption/HMAC keys and broker secret are DPAPI-protected in `%LOCALAPPDATA%\FIN_PTS\LocalRuntime\secrets.xml`, with directory ACL restricted to the current Windows account and SYSTEM. Do not overwrite this bundle: keys are needed to use the provisioned environment. Nothing secret is committed.
- To retrieve the generated password privately in PowerShell under the same Windows account:

```powershell
$localSecrets = Import-Clixml -LiteralPath "$env:LOCALAPPDATA\FIN_PTS\LocalRuntime\secrets.xml"
[System.Net.NetworkCredential]::new('', $localSecrets.Password).Password
Remove-Variable localSecrets
```

Do not paste the result into chat, logs, source or documentation. Production must use its approved secret provider and deployment settings. The T020–T024 no-live-write restriction describes that earlier task scope; T025's explicit approval authorizes only this bounded local restoration.
