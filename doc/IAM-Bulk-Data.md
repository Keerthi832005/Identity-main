# IAM Bulk Data

Smart paste and Excel template import/export for the administration UI. One pipeline serves both
entry points, so a pasted block and an uploaded workbook cannot be validated differently.

## The shape of it

```
paste ──┐
        ├─► POST /api/v1/admin/bulk/{entity}/staging ─► validate ─► staged batch ─► preview ─► commit
upload ─┘                                                                              │
                                                                                       └─► discard
```

Nothing reaches the identity tables until the administrator commits. A staged batch is a separate,
expiring copy that is discarded, committed, or swept.

## Endpoints

All under `/api/v1/admin/bulk`, all requiring `AdministrationPolicy`.

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/{entity}/template` | Empty styled workbook |
| `POST` | `/{entity}/export` | Populated workbook for the rows the caller is looking at |
| `POST` | `/{entity}/staging` | Stage a workbook (multipart) or pasted rows (JSON) |
| `GET` | `/staging/{batchKey}` | Paged staged rows with per-cell errors |
| `PUT` | `/staging/{batchKey}/rows/{n}` | Correct one row and revalidate only that row |
| `GET` | `/staging/{batchKey}/errors` | Annotated workbook of the failures |
| `POST` | `/staging/{batchKey}/commit` | Write the valid rows |
| `DELETE` | `/staging/{batchKey}` | Discard the batch |

## Entities

| Entity key | Natural key | Import | Notes |
| --- | --- | --- | --- |
| `users` | employee code | yes | Profile fields only |
| `modules` | application + module code | yes | Hierarchy, depth-ordered commit |
| `capabilities` | capability code | yes | Code is unique across all applications |
| `roles` | application + role code | yes | Capability grants are managed on the role |
| `organization-units` | unit code | yes | Eight types, depth-ordered commit |
| `audit-events` | — | **no** | Export only |
| `sessions` | — | **no** | Export only |

The **application** entity is deliberately absent. Creating one fixes a token audience and token
lifetimes, which is a considered act rather than a spreadsheet row, and a client secret must never
travel through a template.

## What is never bulk-writable

Passwords, PINs, OTPs, client secrets, refresh tokens, MFA enrolment, device trust and session
revocation. None appear as a column in any template, export or staged row, and tests assert that no
column id or header can contain a credential word.

For the two read-only entities the refusal is structural, not a convention:
`BulkEntityDescriptor.AllowsImport` is false, and staging refuses at the endpoint before a command is
built. Leaving a committer unregistered would only have failed at commit time, and a future screen
could then have made an append-only record writable simply by wiring up a toolbar.

## Workbook layout

Row 1 is an instruction band, row 2 the headers, data from row 3. Error messages quote Excel's own row
numbers, so a person can find the row in their file.

Three sheets:

- `Data` — the rows.
- `Reference` — dropdown sources, bound with real Excel data validation.
- `_meta` — hidden: entity key, template version and the ordered column ids.

Columns are matched by **id from `_meta`, then by header text**, so a workbook whose columns an
administrator reordered still imports. A template version mismatch is refused with a message telling
them to download the template again.

An export is produced by the same writer as the template, so **an export is always a valid template**:
export a working set, edit it in Excel, re-import it.

## Validation order

1. Required and type checks from the descriptor. Excel stores a date as a serial number, so the
   reader normalises to invariant text before validation ever sees it.
2. Maximum length, before a value reaches SQL.
3. Enumeration membership. A boolean column is checked against its normalised value, not its
   `Yes`/`No` dropdown labels.
4. In-batch duplicates on the natural key. An incomplete key is reported as a missing required value
   rather than as a duplicate.
5. Entity checks, resolved for the whole batch in a fixed number of queries: does this record already
   exist (create versus update), and do its references resolve?

A reference to a record created **earlier in the same file** counts as resolvable, and the id is
looked up at apply time, once that row has been applied.

### Error codes

`cell.required`, `cell.malformed`, `cell.not-allowed`, `cell.too-long`, `row.duplicate-in-batch`,
`manager.unknown`, `manager.self`, `email.invalid`, `application.unknown`, `module.unknown`,
`module.parent-unknown`, `module.parent-self`, `module.parent-cycle`, `unit.type-unknown`,
`unit.parent-required`, `unit.root-has-parent`, `unit.parent-unknown`, `unit.parent-wrong-type`,
`unit.parent-self`, `unit.parent-cycle`.

Codes are stable; the message text is not, so branch on the code.

## Hierarchies

Modules and organization units form trees, and an administrator should not have to sort the file.
`IBulkEntityCommitter.OrderForCommit` is a default interface method: a flat entity keeps file order,
and only a hierarchy overrides it to return depth order. Ordering carries its own cycle guard, so a
malformed batch fails one commit rather than exhausting the stack.

For organization units the allowed parent of each type comes from `OrganizationUnit.ParentType`, so
the eight pairings keep exactly one definition and the bulk path cannot drift from the single-record
path.

## Commit

### Review grid

The staged preview uses a DevExtreme DataGrid with server paging. It starts at 25 rows per page;
the pager offers 10, 25, 50 or 100 rows, page navigation, and the filtered row count. On narrow
screens the pager switches to compact controls. Only the grid body scrolls through the loaded
page; the horizontal scrollbar and pager remain below it. Rows beyond the first 200 are reachable.

**Needs attention** resets to the first filtered page. Correcting a cell refreshes the counts and
current page; if the last filtered page disappears, the grid returns to the last available page.
The original workbook row numbers remain visible. Paging and filtering never limit the commit:
the confirmation and commit button always describe **all valid rows in the batch**.

If a correction, commit or refresh fails, the last successful preview remains visible with an
inline error and request reference. Use **Refresh rows** to check the server state before retrying.
Only an initial load failure displays **Batch unavailable**. Do not upload a duplicate workbook
simply because a response was interrupted: refresh the original batch first.

### Applying organization updates

An organization row classified as **Update** resolves the existing unit and runs
`UpdateOrganizationUnitCommand` with its current concurrency token. It does not create a second
root or child. The existing ID, parent and hierarchy path are preserved, while the name,
description and address are updated. Changing an existing unit's type or parent through an import
is rejected; use the organization management workflow for structural changes.

Within one transaction, each valid row is projected through the **existing administration command**
for that entity, so a bulk write inherits the same domain rules, authorization and audit as a
single-record write. `TransactionRunner` joins an ambient transaction rather than nesting, which is
what makes the whole batch one atomic unit.

`BatchKey` is the idempotency key. A replayed commit returns `alreadyCommitted: true` and writes
nothing further. Invalid rows are not written and remain available in the error download. The batch
is closed after commit: either fix invalid rows before committing, or import only the corrected
rows in a new batch afterwards.

### Regression verification

From the repository root, run the isolated SQL regression (Windows integrated SQL authentication):

```powershell
./IAM/scripts/Test-OrganizationBulk.ps1
# Optional local test SQL instance:
./IAM/scripts/Test-OrganizationBulk.ps1 -SqlServer 'localhost\SQLEXPRESS2022'
```

The script creates a unique `FIN_IAM_BulkTests_<guid>` database, applies and replays migrations,
tests a 102-row mixed create/update batch, replay safety, child updates and transaction rollback,
then drops only that generated database in `finally`. It never disconnects other sessions or
touches business databases. Cleanup failure is reported explicitly.

From `IAM/src/Frontend`, run:

```powershell
npm test -- --watch=false --include='**/bulk-workspace.component.spec.ts'
npx playwright test e2e/bulk-workspace.spec.ts
```

The browser tests use synthetic API responses, exercise a 253-row batch on desktop and mobile,
and save grid/error screenshots in `test-results`. They do not commit a live business batch.

## Bounds and retention

Defaults in `BulkDocumentLimits`: 8 MB, 5,000 rows, 12 sheets. Uploads are rejected on declared size
and extension **before the workbook is opened**. The download file name is rebuilt from the entity key
and a timestamp rather than echoing what the caller sent.

A batch is readable, correctable, committable and discardable only by the administrator who submitted
it, and another administrator's batch returns `404` rather than `403` so batch keys cannot be
enumerated. A bulk import must stay attributable, so an unattributed system context is refused.

Batches expire 24 hours after submission. `BulkStagingExpiryService` sweeps every 30 minutes, up to
200 batches per pass; a failed sweep is logged and retried at the next tick rather than stopping the
service.

## Smart paste

The clipboard parser prefers `text/html`, because Excel and Sheets both put a real table there and it
survives values containing tabs or newlines. TSV and CSV are fallbacks, with a real quote state
machine so a quoted comma, a quoted newline and a doubled quote all read correctly. Tabs beat commas
when both appear: a comma inside a cell is ordinary text, a tab is Excel's separator.

Paste is intercepted only when the target is not an input, textarea, select or `contenteditable`, and
only for multi-cell payloads, so pasting into a filter box behaves normally. The listener sits at
document level, because nothing is focused on a freshly loaded page.

A guessed mapping is never applied silently. The mapping screen always shows the resolved mapping
with a sample value per column, allows re-targeting and ignoring, refuses to stage while a required
column is unmapped, and stops two source columns landing on the same template column. Header
detection requires 60% of non-empty first-row cells to resolve, because guessing wrong either loses a
record or imports a header as data.

The sign-in screen has its own narrow credential paste: it fills both fields from one pasted pair but
**never submits**, because an accidental or mis-parsed paste would otherwise spend an attempt against
the lockout budget.

## Adding an entity

1. Write a `BulkEntityDescriptor` with stable column ids and a natural key.
2. Implement `IBulkEntityValidator` with batched lookups, never one query per row.
3. Implement `IBulkEntityCommitter`, dispatching the existing command rather than touching stores.
   Override `OrderForCommit` only for a hierarchy.
4. Register all three. An entity absent from the catalog reports `404` rather than half-working.
5. Set `AllowsImport: false` for anything append-only or irreversible.
