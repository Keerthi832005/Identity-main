# T032–T043 Revamp Verification

Evidence for the administration UI revamp: an owned design system, smart paste and Excel
import/export on every management entity, and the five screens rebuilt on that system.

Fixture evidence and real API/SQL evidence are kept apart below, because they prove different things.

## Commands and results

Run from `IAM/`, against pristine disposable SQL Server databases.

| Check | Command | Result |
| --- | --- | --- |
| Migration apply | `dotnet run --project src/Backend/Identity.Database ... -- <conn>` | `Upgrade successful` |
| Migration replay | same command again | `No new scripts need to be executed` |
| Backend format | `dotnet format Identity.slnx --no-restore --verify-no-changes` | exit 0 |
| Backend build | `dotnet build Identity.slnx --no-restore` | 0 warnings, 0 errors |
| Backend tests | `dotnet test Identity.slnx --no-build` | **200 passed, 0 failed, 0 skipped** |
| Frontend build | `npm run build` | succeeded, no template warnings |
| Frontend tests | `npm test -- --watch=false` | **107 passed** across 20 files |
| Type checks | `npx tsc --noEmit -p tsconfig.{app,spec}.json` | both clean |
| Publish | `./scripts/Publish-Identity.ps1` | four outputs, exit 0 |

Backend test count over the revamp: 90 (T033) → 136 (T034) → 149 (T035) → 161 (T038) → 172 (T039)
→ 194 (T040) → 200 (T041). Frontend: 41 at baseline → 77 (T032) → 96 (T036) → 107 (T041).

The SQL-gated suite needs a **pristine** database per run. Re-running against a database an earlier
run has written to produces failures that are data collisions, not regressions; that was investigated
and confirmed during T038.

Several suites additionally guard on the database *name*
(`Assert.StartsWith("FIN_IAM_OrgMgmtTests_")`), a deliberate safety rail so they only touch their own
disposable database.

## Real API and SQL evidence

Exercised against the running API and `FIN_IAM_Local`, not fixtures. Fixture API tests use a fake
dispatcher and never execute the real handler, so this is the only evidence that the wiring works.

**Users.** A six-row paste staged 2 create, 1 update, 3 invalid. Each invalid row carried the right
message against the right cell: `email.invalid`, `manager.unknown` naming the missing code and how to
fix it, and `row.duplicate-in-batch` naming the earlier row. Commit wrote
`{"createdRowCount":2,"updatedRowCount":0,"remainingInvalidRowCount":1}`; replaying returned
`alreadyCommitted: true` and wrote nothing further. SQL confirmed `INDE05515` carried
`ManagerUserId = 2` — the in-batch manager reference resolved at apply time — and the audit held
`BulkImportSubmitted x3`, `BulkImportCommitted x1` and `UserCreated x3`: bulk writes emit the same
events a single write does.

**Modules.** A file listing every child *before* its parent staged 3 create, 1 invalid, and committed
in depth order. SQL: `insights` at the root with display order 10, `reports` beneath it, `daily`
beneath that.

**Organization units.** A file listing the deepest unit first and the root last, with two deliberately
illegal rows, staged 6 create and 2 invalid. The refusals named both types:
`A Team must sit under a Department, but IN is a Country.` and
`A Branch needs a parent, and its parent must be a State.` The committed branch came out the right
way up with correct materialised paths, `FUJITEC /1/` through `AMBATTUR /1/2/3/4/5/6/`, and the
address fields persisted.

**Export-only enforcement**, with a control to show the block is selective rather than blanket:

| Route | Result |
| --- | --- |
| `GET /bulk/audit-events/template` | `200`, 6,158-byte workbook |
| `POST /bulk/audit-events/export` | `200` |
| `POST /bulk/audit-events/staging` | `409` |
| `POST /bulk/sessions/staging` | `409` |
| `POST /bulk/users/staging` (control) | `201` |

## Browser evidence

Against the running application with real data. The screens render the records that were imported
through the bulk pipeline, so the UI and the pipeline are confirmed together.

- Dashboard at 1280px with live counts from `FIN_IAM_Local`, a non-zero failed sign-in count in the
  warning tone, and recent audit events with correlation ids.
- Users, application catalog, organizations and audit rebuilt on the design system.
- **360px**: the rail collapses to a hamburger, the toolbar wraps, the split stacks to one column and
  both cards stay intact.
- **Dark theme at 1440px**: canvas, surfaces, borders and accent text all resolve per theme across
  the design-system primitives.
- Command palette after typing and ArrowDown: `role="combobox"`, `aria-expanded="true"`,
  `aria-activedescendant="menu-option-0"`, options carrying `role="option"` and `aria-selected`.
- Breadcrumb on `/bulk/map` reads "Bulk / Map"; before the fix it read "Workspace / Overview".
- Console errors on the dashboard dropped from one to zero. The remaining message is the pre-existing
  DevExtreme theme-timeout warning.

## Defects found and fixed during the revamp

These were found by exercising the real system, and are listed because each was a genuine fault
rather than a refactor.

1. **Nested transactions.** Bulk commit returned `500`: the handler opened a transaction and every
   dispatched command opened another. `TransactionRunner` now joins an ambient transaction, which is
   also what makes a batch atomic.
2. **Vulnerable transitive package.** `DevExpress.Printing.Core` resolves
   `System.Security.Cryptography.Xml 8.0.3` with three high-severity advisories. Pinned to `10.0.11`;
   `NU1903` is not suppressed.
3. **Silent partial publish.** `Publish-Identity.ps1` aborted in its frontend step whenever output was
   captured, because npm writes warnings to stderr and `$ErrorActionPreference = 'Stop'` turns that
   into a terminating error in Windows PowerShell. The backend had already published, so the run
   produced a full-looking output set with a **stale frontend** — exactly what a CI pipeline would
   have shipped. Fixed, and the script now fails loudly if any of the four outputs is empty.
4. **A breadcrumb that lied.** It inherited `activeItem`'s fallback to the first navigation entry and
   named the wrong screen on any unlisted route.
5. **A command palette that was not a combobox.** The ARIA attributes were set through
   `dx-text-box`'s `inputAttr` and were silently ineffective, because the DevExtreme editor sets
   `role="textbox"` on its own input.
6. **Focus that read as an error.** The in-editor reveal button took the standalone-button focus ring,
   offset outward over the field border. With a red brand accent that is indistinguishable from a
   validation failure — on a password field, actively misleading.
7. **Paste that needed a click first.** Both paste surfaces listened on their host element, which
   never receives the event while nothing inside is focused.

## Dependency exception

`DevExtreme` and `DevExpress.Document.Processor` are commercial and licensed, and are approved
exceptions to the free/open-source rule in `doc/Identity-Coding-Standard.md`. Both are recorded in
`THIRD-PARTY-NOTICES.md` and `README.md`. The Excel engine is referenced only from
`Identity.Infrastructure` and sits behind Application ports, so Domain and Application stay
engine-free.

Publish confirms `DevExpress.Docs.v26.1.dll` (4.2 MB) lands in the API output.

## Release boundaries — still open

- **A clean or CI restore will fail.** No DevExpress package source is registered; the package
  resolves only from the local NuGet cache. Adding a credentialed feed or vendoring the package is a
  deployment and procurement action and is not fixed in source.
- No deployment, push, merge or business-database change was performed. `FIN_IAM`, `FIN_IAM_Dev`,
  `FIN_PTS_DS4` and `FIN_PTS_QS4` were never written. `FIN_IAM_Local` is the local development
  database created for this work.
- The DevExtreme browser licence key is empty in `public/config.json`; supply it at deployment.
- Feature remainders recorded in T043 rather than closed: the `user-applications` and `user-roles`
  descriptors, inline row correction in the bulk preview, tabbed detail sections, module reorder
  controls, organization tree browsing, concurrency-conflict reconciliation, and audit server-side
  paging with filters.
- `npm run format:check` reports `src/_mini-controls.scss`. Pre-existing and T027-owned; not touched
  by this revamp.
