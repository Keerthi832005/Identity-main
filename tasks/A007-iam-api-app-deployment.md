# A007: IAM API and app deployment

- Status: COMPLETED — deployed and verified on local IIS at 09:19 IST, 2026-08-31.
- Request: Deploy the IAM API and administration app; preserve PTS and concurrent work.
- Application source: `9338af5273810fc5374f22661ad1b2e7167cf50d`, extracted with `git archive`. No uncommitted files were included.
- Live release: `IAM/artifacts/agent-rollout/20260831-034852-b2fe5d57`, containing the identical verified payload from build `20260831-034417-3a47d618`.

## Completed preparation

- Published the API and frontend from the isolated source archive. API tests: 56 passed; frontend tests: 134 passed. Both production builds passed; existing frontend dependency optimization warnings remain.
- Verified the manifest, payload hashes and existing local-pilot agent package. Valid-package and seven rejection checks passed in PowerShell 7 and Windows PowerShell 5.1.
- Published the database runner from the same archived source, separately from the immutable deployment payload. `database/Identity.Database.dll` SHA-256: `4A868C1A679CF519592C258836FDA5BF1971353EB7B654DE7F868E0E5A0CC37C`.
- Confirmed IAM liveness and readiness return HTTP 200 after the separately approved SQL recovery. No SQL restart occurred for this request.
- Read-only SQL inspection found migrations 0001–0010 in FIN_IAM. The bulk staging tables and agent inventory table are absent.
- Administrator `ValidateOnly` preflight passed ownership/connection checks and stopped at `MigrationPreflight`, blocker `Migration0012Required`. Evidence: release `deployment-result.json`, completed `2026-08-31T03:34:10Z`.

## Reviewed database prerequisite

The initial request was paused at the documented database boundary. After disclosure of the required backup and migrations, the user directed completing the rollout and correcting IIS to serve the latest files. The bounded FIN_IAM upgrade was performed as part of that continuation; no credential, account or PTS changes were included.

- `0011_bulk_data_staging.sql`: adds `Identity.BulkImportBatch` and `Identity.BulkImportRow`, their constraints/indexes, and extends the existing authentication audit event check to accept bulk-import events. Existing allowed events are retained.
- `0012_agent_inventory.sql`: adds `Identity.AgentInstallation` with device association and inventory metadata.
- Neither migration provisions accounts, resets credentials, grants permissions, deletes existing business rows, or changes PTS schemas.
- The database runner uses the migration journal and a transaction per script. The final runner was built from the same archived source as the application. SHA-256: `AB5337B7E680CB933D516131423E2A7119D68415339BE91030E6648076556A46`.

## Deployment and verification results

- Final isolated build: **56 API tests and 139 frontend tests passed**; both production builds passed. A supplementary rendered-form paste test passed with the nine-test sign-in component suite. It dispatches a clipboard event at document level, verifies both rendered inputs and masking, and verifies no automatic authentication call.
- Fixed ordinary password paste so punctuation is not interpreted as a credential pair, including when the password is revealed. Labelled and spreadsheet credential pairs still populate both fields without submitting. Only synthetic credentials were used in tests.
- Acquired the shared local deployment mutex, checked the exact FIN_IAM target and confirmed only 0011/0012 were pending. Created a unique copy-only database backup with checksum and successfully ran `RESTORE VERIFYONLY WITH CHECKSUM` before applying the two migrations.
- Backup: `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS2022\MSSQL\Backup\FIN_IAM_before_0011_0012_20260831-034417-3a47d618_3a565f2fdda24495ad61a6cc926fa213.bak`.
- Verified all 12 migration journal entries, all three new tables, and the enabled/trusted audit check constraint. No migration was rerun during the subsequent IIS retry.
- First IIS attempt stopped before committing any site path: the Windows PowerShell COM setter rejected a wrapped `Join-Path` value. An administrator in-memory diagnostic reproduced the type mismatch and verified the explicit CLR string conversion without committing configuration. Fixed `Set-Feed` accordingly.
- Retried using a fresh immutable release ID with identical payload hashes. Successful deployment receipt: `20260831-034852-b2fe5d57/deployment-result.json`, completed `2026-08-31T03:49:20Z`. Failed staging and the previous live release remain unserved for diagnosis/rollback.
- Both IIS sites now point under `C:\inetpub\FIN_PTS\Releases\20260831-034852-b2fe5d57-iam-agent\IAM\`: API at `Api`, app at `Frontend`. Administrator verification matched **166 application files** to the manifest and verified preservation of existing frontend runtime configuration and proxy rules.
- Public HTTPS checks passed: IAM readiness, frontend index bytes, agent installer and manifest bytes through the PTS proxy, and HTTP 401 for unauthenticated agent administration. The deployment's configuration comparison verified unrelated IIS configuration unchanged.
- Live `/login` now serves `main-DMNIK24M.js` with `Cache-Control: no-cache`, replacing `main-XI454BTR.js`. The served `chunk-CYkBZOd4.js` contains the credential-paste feature. The browser loaded the new page; synthetic credential entry, password reveal/masking and screenshot capture were checked without submitting a real login. Browser clipboard dispatch is covered by the rendered component test, not claimed as an operating-system clipboard test.
- Evidence: migration/build/preflight/diagnostics under `20260831-034417-3a47d618`; successful deployment, `live-file-verification.json` and `published-login.png` under `20260831-034852-b2fe5d57`.

PTS databases/sites, Terminal, SAP workers, accounts, credentials and agent services were unchanged. The included agent package remains a local pilot package; hosting it is not service installation or production fleet approval. Protected administration workflows were not exercised against real business data during this deployment smoke check.
