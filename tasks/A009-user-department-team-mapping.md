# A009 — User department and team mapping

Status: COMPLETED — deployed to local IAM at 10:36 IST on 2026-08-31.

Request: Add department/team mapping to the IAM user editor using the existing organization masters. Continue the default personnel seed and commit only files owned by this task.

Implemented optional selectors on create/edit, team filtering and clearing on department change, searchable bounded lists, retained selections on fetch errors, retries, stale-response protection, profile summary names, and a responsive layout. Server-side validation and SQL constraints enforce the hierarchy. Older clients and bulk imports preserve existing mappings when they omit the explicit replacement object. Existing inactive mappings can be retained or removed. No permission grants or credential changes are performed.

No department/team assignments or master names were supplied, so all 164 seeded personnel remain unassigned. Seed completion is recorded separately in A008 and commit `30bd3d3`.

Verification (2026-08-31):

- Full backend suite: 195 passed; 38 environment-dependent SQL/agent tests skipped without those configurations.
- Isolated SQL target: five mapping/profile/schema tests passed, including persisted names in search/access/security responses, audit replay, no grants, old-client preservation, inactive units/ancestors, invalid pairs and direct SQL constraint rejection.
- Full frontend suite: 147 passed; focused profile/mapping/service tests: 15 passed.
- Browser regression: light and dark create/edit/clear/error workflows passed, including desktop/mobile screenshots and no page errors. These tests use synthetic API fixtures; they do not write real employee assignments.
- Production API/frontend builds passed. Existing third-party source-map/CommonJS warnings remain.

Deployment scope: only FIN_IAM migration 0013 and IAM API/frontend. Preserve PTS, protected connection/runtime configuration, agent package, credentials, and other agents' uncommitted work. Use a committed source archive, verified copy-only SQL backup, exact pending-migration check, shared deployment lock, and existing scoped IIS deployment checks.

See [user mapping contract](../doc/User-Department-And-Team.md).

## Local rollout result

- Implementation commit: `38fd9b109e5efd70e4a0d43b7572d1156b958424`. Built from an isolated committed archive; no uncommitted work was included. The archived build repeated all 56 API and 147 frontend tests successfully.
- Release: `IAM/artifacts/agent-rollout/20260831-050317-dec61bfd`. Package hash checks and seven rejection checks passed. Database runner SHA-256: `623345BCB171BC8F86151F064314DD79764ABC418A7139DCF9BE0798B2B5D3D3`.
- Verified exact local SQL target `HOCOM18502627\SQLEXPRESS2022 / FIN_IAM`; journal contained 0001–0012, with only 0013 pending. Held the shared deployment lock while backing up, migrating and deploying.
- Created a copy-only backup with checksum and successfully ran RESTORE VERIFYONLY WITH CHECKSUM: `C:\Program Files\Microsoft SQL Server\MSSQL16.SQLEXPRESS2022\MSSQL\Backup\FIN_IAM_before_0013_20260831-050317-dec61bfd_c6a7be4b86654e628c917cbb09cca129.bak`.
- Migration 0013 applied once. Verified both nullable columns and enabled/trusted foreign keys/check constraint. User count remained 164 and credential count remained 4. Departments, teams and mapped users are all zero: no invented hierarchy or assignments.
- IAM API/frontend switched to `C:\inetpub\FIN_PTS\Releases\20260831-050317-dec61bfd-iam-agent\IAM`. Existing protected settings and frontend configuration were retained; only the IAM API pool was recycled. PTS configuration, databases, terminal deployment and agent service state were unchanged. The previous IAM release remains available.
- Deployment receipt `mapping-rollout-result.json` reports success at `2026-08-31T05:06:27Z`. API readiness and the PTS terminal/proxied IAM readiness returned HTTP 200; protected unauthenticated administration returned 401 as expected. Public `chunk-Dqu70TbC.js` matches the reviewed mapping bundle byte-for-byte.
- Live browser loaded the IAM sign-in screen with no console errors. It had no administrator session, so authenticated mapping interactions were verified by the isolated browser regression and SQL tests, not by changing a real employee in the live UI.
- Post-deployment seed preview reports all 164 personnel codes, no missing users and no name differences. New accounts still require normal credentials/access provisioning. The owned disposable SQL test database was removed after tests; no live database was removed.
