# T039: Applications screen rebuild with smart paste and Excel

- Status: COMPLETED
- Objective: Rebuild the application catalog and give modules, capabilities and roles a bulk path.
- Scope: Applications, clients, modules, capabilities and roles. Existing services, models and API contracts retained.
- Requirements covered: Production-grade UI/UX, smart paste and Excel template export and import on the applications screen.
- Files/components: `src/Frontend/src/app/features/application-catalog/` (455-line template and 258-line component rebuilt), catalog bulk descriptors in `src/Backend/Identity.Application/BulkData/`.
- Dependencies: T036, T037.
- Risks/assumptions: Client secrets are never exported, templated or staged. Client rows are bulk-creatable only as `Public`; `Confidential` and `Service` clients stay single-record so a secret is only ever handled through the existing dedicated path.

## Implementation Steps

Rebuild the screen on the `app-split` scaffold with an application list and a detail region covering clients, the module tree, capabilities and roles, replacing the current bespoke markup with design-system primitives and the grid preset.

Present modules as a real hierarchy using the parent module relationship and display order, with explicit reorder controls and clear system-module protection, rather than a flat list.

Add the T036 toolbar with descriptors for `applications`, `modules` (application code, module code, name, parent module code, display order, active), `capabilities` (application code, module code, capability code, name, active) and `roles` (application code, role code, name, description, system flag). These are the highest-volume seeding tasks in the product and the strongest case for the bulk path.

Validate parent module and module references by natural key within the batch as well as against existing data, so a template that creates a parent and its children in one file succeeds. Order the commit by hierarchy depth.

Wire smart paste on each detail grid and make export honor the active filter and visible columns.

## Acceptance Criteria

Every existing catalog workflow still works. A single workbook creating a module tree with children commits in the correct order. No client secret appears in any template, export or staged row. Confidential and service client creation remains single-record. Screen is correct at 360px, 768px, 1280px and 1920px in both themes.

## Required Tests and Validation

`npm run build`, `npm test -- --watch=false`, format and TypeScript checks; `dotnet format`, `dotnet build`, `dotnet test` for the descriptors. Test asserting in-batch parent resolution and depth-ordered commit. Playwright coverage of catalog, paste, import and commit. Screenshot evidence.

## Validation Results

**Scope delivered: the catalog bulk capability, not the visual rebuild.** As with T038, the entities
are bulk-capable end to end and the toolbar and paste surface are wired into the existing screen, but
the screen was not rebuilt onto `app-split` with a real module tree presentation. That remainder is
carried into T043 rather than dropped.

Three descriptors ship: `modules`, `capabilities` and `roles`. **The application itself is
deliberately absent.** Creating an application fixes a token audience and token lifetimes, which is a
considered one-off act rather than a spreadsheet row, and a client secret must never travel through a
template. A test asserts no catalog column can carry a secret, password, token or credential word.

Modules are the interesting case, because they form a tree and an administrator should not have to
sort the file. Two mechanisms handle it:

- `IBulkEntityCommitter.OrderForCommit` was added as a default interface method, so a flat entity
  keeps file order and only a hierarchy overrides it. `ModulesBulkCommitter` returns depth order, and
  `BulkDataHandler` now applies rows in the order the committer chooses rather than by row number.
- The parent id is resolved at apply time, not validation time, because a parent created earlier in
  the same commit does not exist when the batch is validated.

`ModulesBulkValidator` accepts a parent defined anywhere in the same file, and rejects a self-parent
and a reference loop. The committer's ordering carries an independent cycle guard: the validator
already rejects a cycle, but without the guard a malformed batch would recurse until the stack ran
out rather than fail one commit.

`ICatalogCodeResolver` was added as a narrow batched port for application, module, role and
capability code lookups, matching the `IUserCodeResolver` pattern from T038, so validation resolves a
whole file in a fixed number of queries.

Checks after implementation: `dotnet build` clean; `dotnet format --verify-no-changes` exited 0;
**172 backend tests passed, zero failed, zero skipped** on pristine disposable
`FIN_IAM_OrgMgmtTests_20260831_bb986d74`, up from 161, with eleven new; frontend build clean and 107
unit tests passing; both TypeScript targets clean.

Real end-to-end evidence against the running API and `FIN_IAM_Local`, using a file that lists every
child **before** its parent:

- Rows 3, 4, 5 defined `daily` -> `reports` -> `insights` in that order, with row 6 naming a
  non-existent application. Staging reported 3 create, 1 invalid.
- Commit returned `{"createdRowCount":3,"remainingInvalidRowCount":1}`.
- SQL confirms the tree was built the right way up: `insights` at the root with display order 10,
  `reports` under `insights`, `daily` under `reports`. The invalid row wrote nothing.

Not covered here: the visual rebuild of the catalog screen, the module hierarchy presentation with
reorder controls, and client bulk creation (excluded by design). All tracked in T043.

## Definition of Done

Applications screen rebuilt, hierarchy-aware bulk seeding proven, secrets excluded, suites green.
