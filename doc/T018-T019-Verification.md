# T018–T019 pre-commit review and verification

Date: 2026-08-30. Scope: all pending IAM email/reporting-manager and organization-module changes. Unrelated staged root `.gitignore` changes are excluded from the feature commit. PTS source, licensing, authentication credentials, and existing migrations 0001–0007 are unchanged.

## Review findings resolved

- Profile editing now snapshots its target user when opened, so a delayed user-selection response cannot redirect the save. A regression test covers the race.
- Real DevExtreme controls render in the unit tests; Vite handles their dependencies instead of unsupported Node directory imports.
- The profile dialog has an accessible title and close label, neutral informational help in both themes, and readable white Confirm-button text on the brand background.
- Organization creation is authorized and atomic: anchor, root, canonical path, and audit commit together. Child creation commits its common unit, typed relationship, path, and audit together. Failed root/audit writes roll back.
- Hierarchy paths are application-managed. No hierarchy trigger, recursion cap, empty-string path default, or `ParentLinkId` remains. Common business/address fields are not duplicated in typed tables.
- The migration runner now uses DbUp transactions per script. Documentation was corrected: the current journal stores script names and execution times, not content checksums.

## Passing verification

| Check | Result |
| --- | --- |
| `dotnet build IAM/Identity.slnx -c Release --no-restore` | Passed; zero warnings/errors |
| `dotnet test IAM/Identity.slnx -c Release --no-build --no-restore` with an explicit disposable SQL target | 80 passed, zero failed/skipped: Application 3, Domain 19, API 21, AdminCli 10, Infrastructure 27 |
| `dotnet format IAM/Identity.slnx --no-restore --verify-no-changes --include <changed C# files>` | Passed |
| `npm test -- --watch=false` in `IAM/src/Frontend` | 23 passed across nine test files |
| `npm run build` in `IAM/src/Frontend` | Production build passed |
| `npm run test:e2e` in `IAM/src/Frontend` | Five browser tests passed, including profile flows in light/dark themes |
| Strict TypeScript check of `e2e/user-profile.spec.ts` with the existing Node type definitions | Passed |
| Prettier check of all changed frontend TS/HTML/SCSS/JSON files | Passed |
| SQL metadata review | Nine new tables, eight views, 60 documented columns; FKs/checks enabled and trusted; no cascading deletes or hierarchy triggers |
| Migration failure/retry/replay | Deliberate object-name collision in 0009 left no partial organization tables and no failed-script history entry; retry succeeded; replay did no work and retained nine history rows |
| Screenshot review | Profile dialog reviewed at 1280×720 and 390×844 in light/dark themes; mobile bounds and button text color asserted |

## Flow boundaries

| Boundary | Evidence and limitation |
| --- | --- |
| Browser → profile HTTP request → returned profile | Browser fixtures assert POST/PUT methods, payload fields, manager selection/removal, rendered results, and visible server rejection. They do **not** call the live IAM database. |
| API → typed command | API host tests verify anonymous/unauthorized rejection, request validation, actor ID, and typed profile fields; the dispatcher is a test double. |
| Application → SQL → readback | SQL-backed tests use real persistence/transactions and verify email search, no-op audit, self/missing/inactive/cyclic managers, concurrent opposite assignments, and unchanged grants/security versions. |
| Organization command → SQL | Eight SQL-backed tests cover both branches, FK/type/ownership rules, normalized uniqueness, views, paths, RowVersion, no cascading delete, and atomic authorized/audited creation. |

The browser verification skills added a visual check beyond unit tests. A non-intercepted in-app browser confirmed login rendering with no captured console errors and a working favicon. Playwright deliberately aborts `/favicon.ico` requests when request routing is enabled; fixture screenshots therefore show a broken favicon mark. This is a test-harness artifact, confirmed in the installed Playwright implementation, not a replaced or removed production image. The wordmark is explicitly checked in the browser tests. Live browser sign-in was not claimed: no deployed admin account was created for this task.

Known non-failing dependency warnings remain: DevExtreme transitive CommonJS optimization warnings, missing `rrule` source-map sources in tests, and Inferno development-mode notices. No packages, paid features, or license keys were added or removed.

## Database safety and release boundaries

- Validation used only disposable databases `FIN_IAM_ProfileTests_20260830_48b6f12c`, `FIN_IAM_ModuleTests_20260830_4b784154`, and `FIN_IAM_ReviewTests_20260830_9d412ec7`. All three were removed after verification; their fixtures are reproducible from migrations/tests.
- Business databases remain unchanged: `FIN_IAM` has zero users and seven migration-history rows; `FIN_PTS_DS4` and `FIN_PTS_QS4` retain 17 history rows each. No migration, dummy fixture, or seed user was added to them.
- **Do not truncate, delete from, drop, reseed, or manually rewrite business `dbo.SchemaVersions`.** Deploy migrations through the runner; it appends history normally.
- Organization-wide code uniqueness is retained. Reuse across different unit types within one organization still fails. A type-scoped rule requires a separate business decision before rollout.
- Every supported organization creation command creates its root atomically. Raw SQL/DbContext writes can bypass completeness; they are not supported provisioning workflows. No organization CRUD API/UI or subtree-move workflow is included.
- The existing IAM reset utility intentionally refuses the expanded 25-table schema. Its deletion scope must be separately reviewed before any extension.
- Business rollout and the requested `INDE03275` account remain pending. Initial-admin role confirmation, display name, and secure password setup are required; no permissions are inferred from a manager assignment.

Passing these checks approves this implementation commit, not a claim that the entire application has been deployed or production-accepted.
