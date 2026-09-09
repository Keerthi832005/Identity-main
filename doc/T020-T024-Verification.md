# T020–T024 organization management verification

Date: 2026-08-30. Bounded repository scope is implemented and verified. Deployment, business data rollout and account provisioning are not approved by this report.

## Scope and commits

Initial worktree: clean detached HEAD `6077f1ac37a0dae5b5b55be04e821f6915a5497d`; work branch `codex/iam-organization-management`. All task files T020–T024 existed before implementation. Work proceeded sequentially, with checks and review before each focused commit.

| Task | Verified implementation commit | Result |
| --- | --- | --- |
| T020 | `3886d26` | Canonical queries, update/state workflows, SQL tests and audit migration |
| T021 | `64a6c9b` | Authorized typed HTTP endpoints, contracts and API tests |
| T022 | `6b3a7d2` | Eight type views, paging/search/details/hierarchy navigation |
| T023 | `320a2ef` | Creation/edit/state UI, conflict recovery and themed browser tests |
| T024 | Final validation commit containing this report; hash in final handoff | Connected API/SQL browser harness, review fixes and final evidence |

T022 inadvertently included a temporary database-name marker (no credentials or data). T023 removes it. No runtime connection or secret file is tracked.

## Requirement reconciliation

| Requirement | Implementation and evidence |
| --- | --- |
| All eight types and both approved hierarchies | Existing typed tables/domain retained. Organization feature type navigation and contextual child creation; fixture and actual browser flows create all eight types. SQL/API tests verify both branches. |
| Bounded searchable lists, details and navigation | `SearchOrganizationUnitsQuery`, `OrganizationStore.Search`, `/admin/organization-units`, scoped detail API and Angular feature. Stable name/ID order, maximum 50, UI pages of 20, total counts, type/organization/parent/own-state filters. Actual browser tests verify 22 countries across two pages and searching. |
| Shared code/name/description/address editing | `UpdateOrganizationUnitCommand`, typed DTOs and editor. All common fields remain on OrganizationUnit. Tests persist and clear fields, including all address lines and coordinates. |
| Active/inactive lifecycle | Confirmed UI actions and RowVersion state command. Descendants are retained and own state does not imply inherited permissions. Inactive immediate parents reject creation. |
| Immutable relationships and canonical paths | No relationship/path fields in update contracts; unknown JSON fields rejected. Existing atomic root/typed-child commands reused. Same-organization/type/parent SQL constraints unchanged. Paths are application-managed within the transaction. |
| Organization-wide normalized codes | Existing `(OrganizationId, NormalizedUnitCode)` uniqueness retained. SQL and actual HTTP tests reject reuse between Country and Department, including normalized case/space variants; other organizations may reuse codes. |
| Authorization, typed dispatch, audit and cancellation | Existing `iam.admin` HTTP policy plus application authorizer; application-owned dispatcher; sealed typed contracts/audit records. Cancellation passed to store/transactions. SQL/API tests cover denials, rollback and cancellation. Actual non-administrator token gets 403. |
| Optimistic concurrency | Original eight-byte RowVersion required; stale writes return distinct 409 code. No-op retries preserve version and create no duplicate audit. UI preserves draft, disables stale Save, and explicitly reloads with confirmation. Actual concurrent HTTP writer exercises browser recovery. |
| Responsive themes and states | Existing Angular/DevExtreme kit and license setup retained. Loading/empty/error/retry states, snapshot-safe targets, duplicate-submit guard, mobile dialog bounds, retained title/actions, readable Save/error/state contrast. Light/dark 1280×720 and 390×844 screenshots reviewed. |
| Integrity and atomicity | Existing root/audit rollback, typed-parent mismatch, wrong organization/type, stale updates and SQL constraints tests retained; new concurrency, no-op/audit and cached-parent tests added. No delete/move workflow, trigger, ParentLinkId, empty path default, EF migration or parallel store. |

## Exact validation commands and results

Commands below run at repository root unless otherwise indicated. SQL tests were explicitly pointed to:

```text
IDENTITY_TEST_SQL_CONNECTION=Server=lpc:HOCOM18502627\SQLEXPRESS2022;Database=FIN_IAM_OrgMgmtTests_20260830_6912750e;Integrated Security=true;Encrypt=true;TrustServerCertificate=true
```

This database has been removed. Recreate a uniquely named migrated disposable target with the same safe naming prefix before rerunning SQL tests; never substitute a business database.

| Command | Final result |
| --- | --- |
| `dotnet restore IAM/Identity.slnx` | Passed |
| `dotnet build IAM/Identity.slnx -c Release --no-restore` | Passed; zero warnings/errors |
| `dotnet test IAM/Identity.slnx -c Release --no-build --no-restore` | 93 passed, zero failed/skipped: Application 3, Domain 19, API 26, AdminCli 10, Infrastructure 35 |
| `dotnet format IAM/Identity.slnx --no-restore --verify-no-changes` | Passed for full solution |
| `dotnet run --project IAM/src/Backend/Identity.Database -c Release --no-build --no-restore` with `IDENTITY_DATABASE_CONNECTION` set to the disposable connection | 0001–0010 applied; replay no-op; ten history rows; audit check enabled/trusted |
| `dotnet list IAM/Identity.slnx package --vulnerable --include-transitive` | No vulnerable packages reported |
| `npm ci --no-audit --no-fund` in `IAM/src/Frontend` | Passed; no dependency versions changed |
| `npm run build` in frontend | Production build passed |
| `npm test -- --watch=false` in frontend | 37 passed in 11 files, none skipped |
| `npx tsc --noEmit -p tsconfig.app.json` and `npx tsc --noEmit -p tsconfig.spec.json` in frontend | Passed |
| `npm run format:check` in frontend | Full configured Prettier check passed |
| `npm audit --omit=dev --audit-level=high` in frontend | Zero vulnerabilities |
| `npm run test:e2e` in frontend | Seven passed; all five existing browser regressions retained |
| `& ./IAM/scripts/Test-OrganizationBrowser.ps1` | Two actual browser/API/SQL tests passed in both themes; SQL readback/audit assertions and exact-target cleanup passed |
| PowerShell AST parse of `Test-OrganizationBrowser.ps1` | No syntax errors |
| `git diff --check` and scoped baseline comparisons | Passed; existing migrations 0001–0009, profile feature/race fix, license setup, generated themes, reset utility and PTS unchanged |

All browser TypeScript files were checked using the installed TypeScript 6 compiler (PowerShell does not expand native wildcard arguments):

```powershell
$typeFiles = @(rg --files e2e e2e-live | Where-Object { $_.EndsWith('.ts') })
npx tsc --ignoreConfig --noEmit --strict --target ES2022 --module nodenext --moduleResolution nodenext --types node @typeFiles playwright.live.config.ts
```

Result: passed. Initial invocations without `--ignoreConfig` or with shell wildcard arguments were corrected; no check was skipped.

## Evidence boundaries

| Boundary | Evidence |
| --- | --- |
| Fixture browser → HTTP shape → UI | `e2e/organization-management.spec.ts` routes in-memory responses. It verifies all type creation, child editing, error states, stale draft/reload, activation and hierarchy browsing. It does **not** establish SQL persistence. |
| API host → typed dispatcher | `OrganizationApiTests` uses the existing typed fake dispatcher for route/policy/validation/mapping/error tests. |
| Application → SQL | 35 infrastructure tests, including 16 organization hierarchy/management tests, run real EF, transactions, constraints, audit and RowVersion. Existing profile tests remain included. |
| Actual browser → API → SQL → UI | `e2e-live/organization-live.spec.ts`, separate config and safe runner. No route interception, real password login, signed tokens and SQL persistence. All eight types are created through the UI; actual HTTP edits cover every shared field/type; browser editing races a real concurrent update. Actual paging, 401/403, invalid relationships, duplicate codes, audit/no-op and state recovery are asserted. |
| SQL final readback | Last passing live database contained 58 path-complete units across all eight types, no orphan anchors and ten migration records. Audit totals: 2 OrganizationCreated, 56 OrganizationUnitCreated, 20 OrganizationUnitUpdated, 4 OrganizationUnitStateChanged. |
| Non-intercepted browser | agent-browser 0.35.1 opened the dev server on 4302, observed real login controls and favicon, saved/inspected screenshot, and captured no page errors. API refresh was expected to fail before the test API existed; this was not described as successful live login. Live login is established by the separate actual suite. |

Fixture screenshots retain the known Playwright routed-favicon artifact. Actual browser screenshots show the working favicon/wordmark. Screenshots are local ignored artifacts under `IAM/src/Frontend/test-results`, not committed credentials or browser traces. Live traces/videos/automatic screenshots are disabled so login secrets and tokens are not captured; explicit screenshots are taken only on organization screens.

## Review corrections and tooling

- Refresh tracked parent state after acquiring the organization lock. An added regression proves a reused dispatcher scope cannot create under a parent deactivated by another scope.
- Return persisted SQL timestamp precision on updates; no-op edits do not dirty tracked fields or generate audit/version churn.
- Clear stale state-conflict context when selecting a freshly fetched unit; edit snapshots remain independent of current selection.
- Keep editor title/actions visible while address fields scroll, and use locally scoped contrasting Save/error/active-state colors in both themes.
- The full formatter initially flagged 77 files under Windows `core.autocrlf=true`. `endOfLine: auto` makes it portable. Generated vendor themes and the immutable license file are excluded from source formatting; their contents are unchanged. Three small pre-existing source-format differences (application SCSS, security template, shared editor utility) were fixed without behavioral changes. No test assertion or application constraint was weakened.
- The dedicated web-provisioning manifest differs from bootstrap's existing descriptions. The disposable runner uses the existing general `provision-application` command with an exact bootstrap-matching fixture manifest, adding only the public client. Production provisioning logic was not changed.
- Harmless existing warnings: DevExtreme CommonJS optimization, rrule missing source maps, Inferno development-mode notice; Playwright also reports NO_COLOR/FORCE_COLOR. No new production packages or license changes.

## Database safety and release boundaries

Every write performed by this task targeted a named disposable database, with Windows integrated authentication, `Encrypt=true` and `TrustServerCertificate=true`, on `lpc:HOCOM18502627\SQLEXPRESS2022`. The runner generates random credentials/keys only in process environment, restores prior environment values, binds a temporary API to loopback, verifies server/database identity, and drops only its exact generated target in `finally`. It accepts no arbitrary database parameter.

Removed targets (final read-only sys.databases check returned no rows for all six):

- `FIN_IAM_OrgMgmtTests_20260830_6912750e`
- `FIN_IAM_OrgBrowserTests_20260830_4c78f9d5` (provisioning-contract setup failure)
- `FIN_IAM_OrgBrowserTests_20260830_a6950e9b` (global capability contract setup failure)
- `FIN_IAM_OrgBrowserTests_20260830_ac50a5a7` (fixture manifest required nonempty module/role collections)
- `FIN_IAM_OrgBrowserTests_20260830_e6b6dd95` (passing connected run)
- `FIN_IAM_OrgBrowserTests_20260830_24314dc7` (final passing connected run after review fixes)

**Read-only business-database snapshot:** FIN_IAM still had zero users/seven history rows; FIN_PTS_QS4 had zero business rows/17 history rows. FIN_PTS_DS4 showed **3,951 business rows and 18 history rows**, which differs from the supplied zero/17 baseline. This task made no writes to DS4 and did not investigate or alter its data. The discrepancy was reported to the user; its cause is not established here. Do not use this report to claim all business databases retained the supplied baseline.

Migration 0010 only extends the audit event allow-list and must deploy normally after 0008/0009 before using update/state management in a business database. No business migration, seed, reset, history change or INDE03275 provisioning was performed. Normal deployment and initial-account approval remain separate.

Subtree moves, hard deletion, user organization assignment, hierarchical permission inheritance, new identity providers, reset allow-list expansion, push, merge, deployment and publishing remain excluded. No required organization-management implementation/check is left blocked. Repository completion does not imply production rollout or acceptance.
