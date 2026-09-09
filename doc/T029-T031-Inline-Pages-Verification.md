# IAM inline management pages — T029–T031

Date: 2026-08-30. Worktree branch: `codex/iam-pts-template`. Initial HEAD: `46c6883`, clean. Only owned IAM paths were changed and explicitly staged; other agents' checkout, PTS, business databases and license configuration were not modified.

## Scope and implementation

| Requirement | Implementation / evidence |
| --- | --- |
| Organization creation and editing use pages instead of a popup | `eeec61a` (T029): dedicated organization editor and guarded child routes under `/organizations`; all eight types use the existing typed service. |
| Other management popups become inline pages with Back | `159446e` (T030): applications, clients, modules, capabilities, users, user profiles, application grants, roles, role assignment, role capabilities and overrides. Existing application/user routes retain their mounted list context while forms are displayed. |
| Page layout and navigation | No management modal backdrop, dialog semantics, focus trap or internal scrolling panel. Forms use normal document scrolling, Back and Cancel. Organization pages have refreshable URLs; catalog/access forms are local inline views rather than separate deep links. |
| Preserve safe workflows | Validation/error handling and service contracts retained. Unsaved drafts require confirmation before departure. In-flight saves block navigation and duplicate submissions. Organization RowVersion conflicts retain drafts and require an explicit reload. Immutable parent/type/organization targets are loaded and checked before editing. |
| Make action targets clear | Catalog forms name the selected application. Access forms name the user/application; profile forms use the captured profile target, preserving the verified race fix and email/manager behavior. |
| Theme and mobile parity | Existing MINI controls and PTS page-header components reused. Light/dark desktop and 390px mobile screenshots reviewed; sidebar settles off-screen on mobile and long forms scroll to Save. |
| Preserve appropriate transient UI | Account/appearance dropdown, select-box dropdowns and native destructive/discard confirmations remain. Security credential forms were already inline. No management `role="dialog"` or modal backdrop remains. |

Backend/domain/SQL stores, migrations, core authentication, generated themes and form-control wrappers are unchanged from the task baseline. Original organization API/integrity coverage is documented in [T020–T024 verification](T020-T024-Verification.md).

## Commands and evidence

Commands run from the repository root unless marked Frontend (`IAM/src/Frontend`). Final post-commit frontend and actual API/SQL reruns passed.

| Command | Result |
| --- | --- |
| `dotnet build IAM/Identity.slnx -c Release` | Passed, zero warnings/errors; restored missing worktree assets after the first no-restore attempt. |
| `dotnet test IAM/Identity.slnx -c Release --no-build --no-restore` | 94 passed, zero failed/skipped: Application 3, Domain 19, API 26, AdminCli 11, Infrastructure 35. Real SQL connection explicitly set to the disposable target below. |
| `dotnet format IAM/Identity.slnx --no-restore --verify-no-changes` | Passed. |
| Frontend `npm run build` | Passed; existing DevExtreme CommonJS warnings only. |
| Frontend `npm test -- --watch=false` | 41 passed across 13 files; previous 37 retained, four tests added. |
| Frontend `npm run format:check` | Passed. |
| Frontend `npx tsc --noEmit -p tsconfig.app.json` and `npx tsc --noEmit -p tsconfig.spec.json` | Passed. |
| Frontend browser TypeScript command below | Passed. |
| Frontend `npm run test:e2e -- --workers=1` | 11 fixture browser tests passed (final rerun 1.3m); previous nine retained/extended, catalog/access checks now cover both themes. |
| `& ./IAM/scripts/Test-OrganizationBrowser.ps1` | Two actual browser → API → SQL tests passed in light/dark (final rerun 29.6s); SQL readback, audits and exact-target cleanup passed. |
| `git diff --check`, scoped baseline comparisons and popup scan | Passed. No backend, auth, license, generated-theme or unrelated screen changes. |
| `Invoke-WebRequest http://127.0.0.1:4317/organizations` | HTTP 200; user's preview remains running. |

Browser type check (PowerShell):

```powershell
$typeFiles = @(rg --files e2e e2e-live | Where-Object { $_.EndsWith('.ts') })
npx tsc --ignoreConfig --noEmit --strict --target ES2022 --module nodenext --moduleResolution nodenext --types node @typeFiles playwright.live.config.ts
```

Backend SQL regression used `FIN_IAM_OrgMgmtTests_20260830_70d4615f`, migrated with `dotnet run --project IAM/src/Backend/Identity.Database -c Release --no-build --no-restore`. Both `IDENTITY_DATABASE_CONNECTION` and `IDENTITY_TEST_SQL_CONNECTION` were explicitly set in the validation process to `Server=lpc:HOCOM18502627\SQLEXPRESS2022;Database=<disposable target>;Integrated Security=true;Encrypt=true;TrustServerCertificate=true`, then restored. Exact `DB_NAME()` verification preceded cleanup; the database was removed. The local ignored runner is `IAM/artifacts/inline-pages-backend-final.ps1`.

Actual browser verification used `FIN_IAM_OrgBrowserTests_20260830_0bab5f4d` and final post-commit target `FIN_IAM_OrgBrowserTests_20260830_541ef1a1`: 58 units, all eight types, ten migration records, two root creates, 56 child creates, 20 update and four state-change audit events. The earlier `470deef9` run exposed a test race: its concurrent HTTP writer ran before the new routed editor finished loading. The test now waits for editor readiness before creating a stale RowVersion. All three browser disposable targets were removed after exact-target checks. The fixture refresh test similarly waits for the child route before refreshing.

Fixture browser tests route in-memory responses and do not prove database persistence. Actual organization tests do not intercept the API and verify SQL persistence, authorization denial, invalid parent/organization/type, uniqueness, stale versions, audit and error paths. The backend SQL suite verifies root atomicity and existing profile workflows. No business database was used for any check.

Known non-failing tooling messages: DevExtreme CommonJS dependencies, rrule missing source maps, Inferno development warning, Playwright NO_COLOR warning and routed favicon artifact. Vitest's DOM emits two unsupported `window.scrollTo` notices; real browser tests verify the scrolling behavior.

## Release boundaries

No push, merge, deployment, account provisioning or business-data change is authorized or performed. FIN_IAM, FIN_PTS_DS4 and FIN_PTS_QS4 (including SchemaVersions) remain excluded. Subtree moves, hard deletion, user-to-organization assignment, inherited permissions and new identity providers remain outside scope. Only source changes and local preview are delivered.

Final task status: T029, T030 and T031 completed. Verified implementation commits: `eeec61a`, `159446e`; final documentation is the commit containing this report. Implementation tree was clean before this documentation-only commit. No pending in-scope functionality or checks remain.
