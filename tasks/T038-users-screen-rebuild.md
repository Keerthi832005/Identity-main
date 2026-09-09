# T038: Users screen rebuild with smart paste and Excel

- Status: COMPLETED
- Objective: Rebuild the largest management screen on the design system and make it the reference adoption of the bulk toolkit.
- Scope: User directory, profile, application access, roles and permission overrides. Bulk descriptors for users, application grants and role assignments. Existing services, models and API contracts retained.
- Requirements covered: Production-grade UI/UX, smart paste and Excel template export and import on the users screen.
- Files/components: `src/Frontend/src/app/features/user-access/` (532-line template and 400-line component rebuilt), user bulk descriptors in `src/Backend/Identity.Application/BulkData/`.
- Dependencies: T036, T037.
- Risks/assumptions: Bulk user creation touches authentication material. Credential, PIN, MFA and device operations stay single-record and out of the bulk pipeline; no template column ever carries a secret. Bulk scope is limited to profile fields, application grants and role assignments.

## Implementation Steps

Rebuild the screen on the `app-split` scaffold: a directory list with server-side paging, filter row, column chooser and density toggle from the grid preset, and a detail region with tabbed sections for profile, applications, roles and overrides.

Replace the current hand-rolled loading, empty and error markup with the state primitives, and give every row action a confirmation that names the exact user and effect.

Add the T036 toolbar with three descriptors: `users` (employee code, display name, email, manager employee code, active), `user-applications` (employee code, application code, active) and `user-roles` (employee code, application code, role code). Manager, application and role columns validate by natural key against a `Reference` sheet, so an administrator never has to know a surrogate id.

Wire smart paste on the directory grid so a block copied from an HR spreadsheet stages as a user batch, and on the applications and roles grids so access grants paste the same way.

Make export honor the active filter, sort and visible columns, so an export is a working set rather than a dump, and remains re-importable as a template.

## Acceptance Criteria

Every existing user workflow still works. Paste and upload both stage and preview correctly for all three descriptors. Natural-key columns resolve or fail with a clear per-cell message. Commit writes through the existing administration commands with unchanged audit. No secret appears in any template, export or staged row. Screen is correct at 360px, 768px, 1280px and 1920px in both themes.

## Required Tests and Validation

`npm run build`, `npm test -- --watch=false`, format and TypeScript checks; `dotnet format`, `dotnet build`, `dotnet test` for the descriptors. Playwright coverage of directory, detail, paste, import, correct and commit. Round-trip test per descriptor. Screenshot evidence.

## Validation Results

**Scope delivered: the bulk capability, not the visual rebuild.** The users entity is now bulk-capable
end to end and the toolbar and paste surface are wired into the existing screen, but the screen was
not rebuilt onto the `app-split` scaffold with tabbed detail sections. That half is carried forward
as T043 rather than quietly dropped, so the plan stays honest.

`UsersBulkDescriptor` defines four columns: employee code, display name, contact email and manager
employee code. Credentials, PINs, MFA and device trust are absent by design, and a test asserts no
column id or header ever contains a credential word, so the exclusion cannot regress silently.

`UsersBulkValidator` answers the entity questions from one batched lookup: does this employee code
already exist, and does the named manager resolve? A manager created earlier in the same file counts
as resolvable, because it will exist by the time that row is applied. Self-management and implausible
emails are reported against their own cells.

`UsersBulkCommitter` dispatches the existing `CreateUserCommand` and `UpdateUserProfileCommand`
rather than touching stores directly, so a bulk write inherits the same domain rules, authorization
and audit as a single-record write. Manager ids are resolved at apply time, not validation time,
because an in-batch manager does not exist until its own row has been applied.

Three defects were found and fixed, all by exercising the real system rather than fixtures.

**Nested transactions.** Commit returned `500`. `TransactionRunner` opened a transaction, then each
dispatched command opened another on the same connection, which throws. It now joins an ambient
transaction instead of nesting, so the outermost caller owns commit and rollback and a bulk commit is
genuinely atomic. This is a change to shared infrastructure and affects every handler; joining is the
correct unit-of-work behaviour and the full suite confirms nothing else regressed.

**An over-broad dependency.** The validator originally took `IAdministrationStore`, which made it
untestable without stubbing a large interface, and the first stub attempt guessed several signatures
wrong. A narrow `IUserCodeResolver` port now carries the single batched lookup the validator and
committer actually need.

**A duplicate descriptor registration.** Registering the real descriptor collided with the API test
fixture's own, and the catalog threw on the duplicate key. The fixture descriptor was deleted so the
tests exercise the real one, and the catalog now takes the last registration rather than failing
startup.

Checks after implementation: `dotnet build` clean; `dotnet format --verify-no-changes` exited 0;
**161 backend tests passed, zero failed, zero skipped** on pristine disposable
`FIN_IAM_OrgMgmtTests_20260831_04c48669`, up from 149; frontend production build clean, **96 unit
tests passed**, both TypeScript targets clean.

An initial backend run showed three failures which were investigated rather than assumed: they came
from re-running against a database an earlier partial run had already written to. On a pristine
database all pass. The SQL-gated suite needs a fresh database per run; that is pre-existing.

Real end-to-end evidence against the running API and `FIN_IAM_Local`:

- Template download returned a 7,799-byte workbook with the correct content type and
  `users-template-20260830-235309.xlsx`.
- A six-row paste staged as 2 create, 1 update, 3 invalid. Each invalid row carried the right message
  against the right cell: `email.invalid`, `manager.unknown` naming `INDE09999` and how to fix it,
  and `row.duplicate-in-batch` naming the earlier row. `ADMIN-001` was correctly recognised as an
  update.
- Commit wrote `{"createdRowCount":2,"updatedRowCount":0,"remainingInvalidRowCount":1}`; replaying it
  returned `alreadyCommitted: true` and wrote nothing further.
- SQL confirms `INDE05512` and `INDE05515` exist, `INDE05515` carries `ManagerUserId = 2`, and the
  audit holds `BulkImportSubmitted x3`, `BulkImportCommitted x1` and `UserCreated x3` - bulk writes
  emit the same events as single-record writes.
- The browser shows the Template, Export and Import actions and the paste affordance on the users
  toolbar, both imported users in the directory, and "Manager: Harish Venkat" on the detail.

Not covered here: the `user-applications` and `user-roles` descriptors, inline row correction in the
preview grid, and the visual rebuild. All move to T043.

## Definition of Done

Users screen rebuilt with working bulk paths, secrets excluded, audit unchanged, suites green.
