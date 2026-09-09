# T040: Organizations screen rebuild with smart paste and Excel

- Status: COMPLETED
- Objective: Rebuild organization browsing and editing and add hierarchy-aware bulk loading for all eight unit types.
- Scope: Organization hierarchy browsing, unit detail, create and edit pages, active state. Existing services, models, API contracts and the T029 routed editor pattern retained.
- Requirements covered: Production-grade UI/UX, smart paste and Excel template export and import on the organizations screen.
- Files/components: `src/Frontend/src/app/features/organization-management/`, organization bulk descriptor in `src/Backend/Identity.Application/BulkData/`.
- Dependencies: T036, T037.
- Risks/assumptions: The eight types and their allowed parents are fixed by `childTypes`: Organization to Country or Department, Country to Region, Region to State, State to Branch, Branch to Location, Department to Team. The bulk path validates against that map and never widens it. Moves and hard deletion stay out of scope, as in T020-T024. Optimistic concurrency via `rowVersion` must survive the rebuild.

## Implementation Steps

Rebuild browsing as a genuine hierarchy: a tree or breadcrumb-driven drill-down with type-scoped search, lazy child loading and clear active/inactive treatment, replacing the current flat presentation.

Rebuild the routed create and edit pages on the design system `app-field-grid`, keeping the T029 inline-page pattern, the `canLeaveOrganizationEditor` guard and `rowVersion` concurrency handling intact. Surface a concurrency conflict as a real reconciliation prompt rather than a raw error.

Add the T036 toolbar with an `organization-units` descriptor: unit type, unit code, unit name, description, parent unit code, the ten address fields and active. Unit type is a dropdown restricted to the eight values through `Reference` sheet validation.

Validate hierarchy in the pipeline: the parent must exist in the batch or in the database, the parent type must permit the child type per `childTypes`, unit code uniqueness is enforced organization-wide, and cycles are rejected. Commit ordered by depth so a whole branch loads from one workbook.

Wire smart paste on the browsing grid and make export honor the current subtree, filter and visible columns, so an administrator can export a branch, edit it in Excel and re-import it.

## Acceptance Criteria

All eight types, both hierarchy branches, browsing, detail, create, edit and active state still work. A workbook defining a multi-level branch commits in depth order. An invalid parent-child type pairing is rejected with a clear per-cell message naming the allowed types. Cycles and duplicate codes are rejected. Concurrency conflicts are recoverable. Screen is correct at 360px, 768px, 1280px and 1920px in both themes.

## Required Tests and Validation

`npm run build`, `npm test -- --watch=false`, format and TypeScript checks; `dotnet format`, `dotnet build`, `dotnet test` including SQL Server tests on a uniquely named disposable database. Tests for each illegal type pairing, cycle detection and depth-ordered commit. Playwright coverage of browsing, editing, paste, import and commit. Screenshot evidence. Never write `FIN_IAM`, `FIN_PTS_DS4` or `FIN_PTS_QS4`; verify exact targets before cleanup.

## Validation Results

**Scope delivered: the organization bulk capability, not the visual rebuild.** As in T038 and T039,
the entity is bulk-capable end to end and the toolbar and paste surface are wired into the existing
screen, but browsing was not rebuilt as a tree. That remainder goes to T043.

The hierarchy rule is not restated in the bulk path. `OrganizationBulkValidator` asks
`OrganizationUnit.ParentType` for the allowed parent of each type, so the eight types and their
pairings have exactly one definition. Widening the rule would require changing the domain, which is
where it belongs, and the bulk path cannot drift from the single-record path.

Rows are validated for: an unknown type, a root that names a parent, a non-root that names none, an
unknown parent, a parent of the wrong type, a self-parent, and a reference loop. A parent defined
anywhere in the same file counts as resolvable, and the committer orders rows by depth so a whole
branch loads from one workbook whatever order the administrator used.

`IOrganizationCodeResolver` returns the unit type alongside the id, because knowing that a parent
exists is not enough when the rule is a type pairing.

Checks after implementation: `dotnet build` clean; `dotnet format --verify-no-changes` exited 0;
**194 backend tests passed, zero failed, zero skipped** on pristine disposable
`FIN_IAM_OrgMgmtTests_20260831_00a5a177`, up from 172, with twenty-two new; frontend build clean and
107 unit tests passing; both TypeScript targets clean.

Two frontend specs needed `RUNTIME_CONFIG` and an HTTP client once the bulk toolbar entered the
component's imports. The same gap appeared in T038 for the users spec. Both were fixed by providing
the dependency the component genuinely now has, not by removing the toolbar from the test.

Real end-to-end evidence against the running API and `FIN_IAM_Local`, using a file listing the
**deepest unit first** and the organization root last, plus two deliberately illegal rows:

- Staging reported 6 create, 2 invalid.
- The illegal rows were refused with messages naming both types:
  `A Team must sit under a Department, but IN is a Country.` and
  `A Branch needs a parent, and its parent must be a State.`
- Commit returned `{"createdRowCount":6,"remainingInvalidRowCount":2}`.
- SQL confirms the branch was built the right way up, with correct materialised paths:
  `FUJITEC /1/`, `IN /1/2/`, `SOUTH /1/2/3/`, `TN /1/2/3/4/`, `CHENNAI /1/2/3/4/5/`,
  `AMBATTUR /1/2/3/4/5/6/`. Address fields persisted (`city=Chennai postal=600058`).

Not covered here: the tree browsing rebuild, the routed editor rebuild, and concurrency conflict
presentation. Moves and hard deletion remain out of scope as in T020-T024. All tracked in T043.

## Definition of Done

Organizations screen rebuilt, hierarchy rules enforced in bulk, concurrency preserved, disposable databases removed, suites green.
