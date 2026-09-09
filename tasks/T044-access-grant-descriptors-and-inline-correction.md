# T044: Access grant descriptors and inline row correction

- Status: COMPLETED
- Objective: Close the two feature remainders recorded in T043 that carry real user value.
- Scope: `user-applications` and `user-roles` bulk descriptors, and an editable cell in the bulk preview that calls the existing correction endpoint.
- Requirements covered: Smart paste and Excel on application grants and role assignments; a staged row can be corrected without leaving the preview, which is what makes the stage-preview-commit choice usable rather than a dead-end report.
- Files/components: `src/Backend/Identity.Application/Administration/` (descriptors, validators, committers), `src/Frontend/src/app/shared/bulk-data/bulk-workspace.component.*`.
- Dependencies: T038, T042.
- Risks/assumptions: Granting an application and assigning a role change what a person can do, so both stay behind the same administration commands and audit as a single-record write. Neither descriptor carries a secret, and neither touches credentials, MFA or device trust. The correction endpoint, its authorization and the frontend service already shipped in T035 and T036; only the editor is missing, so no new server surface is required.

## Implementation Steps

Add a `user-applications` descriptor keyed by employee code and application code, and a `user-roles`
descriptor keyed by employee code, application code and role code. Every reference is a natural code,
so no surrogate id ever appears in a template.

Add validators that resolve users, applications and roles for the whole batch in a fixed number of
queries, reusing the existing `IUserCodeResolver` and `ICatalogCodeResolver` ports rather than adding
new ones. Report an unknown user, application or role against its own cell, and detect a role that
does not belong to the named application.

Add committers that dispatch `GrantUserApplicationCommand` and `AssignRoleCommand`, so a bulk grant
inherits the same authorization and audit as a single-record grant.

Make the preview grid's cells editable for a row that is still staged. Editing calls the existing
`PUT /staging/{batchKey}/rows/{n}` endpoint, which revalidates only that row, and the counts band and
commit bar update from the response. An applied or committed row is not editable.

## Acceptance Criteria

Both descriptors stage, preview and commit through the same pipeline as the existing entities. An
unknown user, application or role, and a role belonging to another application, are each reported
against the right cell. A staged row can be corrected in the preview and its state changes without a
page reload. No secret appears in either template.

## Required Tests and Validation

`dotnet format`, `dotnet build`, `dotnet test` on a pristine disposable database; `npm run build`,
`npm test -- --watch=false`, format and TypeScript checks. Validator tests per descriptor. Real API
and SQL evidence of a grant and a role assignment committed from a paste, and of a correction changing
a row from invalid to valid.

## Validation Results

Both descriptors ship and inline correction works. Every reference is a natural code, so no surrogate
id appears in either template, and a test asserts that.

`AccessGrantBulkValidator` resolves users, applications and roles once for the whole batch. Roles are
resolved **per application**, because a role code may legitimately repeat across applications, and a
role that exists elsewhere but not here is the likely mistake: the message says
"A role belongs to one application; check the application code too" rather than reporting a bare
unknown. Both committers dispatch `GrantUserApplicationCommand` and `AssignRoleCommand`, so a bulk
grant carries the same authorization, authorization-version bump and audit as a single-record grant.

Permission overrides are deliberately excluded. An override is a targeted exception with a reason and
an expiry, granted after a decision; a spreadsheet of them is a sign the roles are wrong, not a
workflow to speed up.

A narrow `IAccessGrantLookup` port carries the two existence checks, matching the `IUserCodeResolver`
and `ICatalogCodeResolver` pattern. The first attempt stubbed the whole `IAdministrationStore` in
tests and broke on signature drift, exactly as it did in T038; the narrow port makes the stub two
methods.

Preview cells are now editable for a row that is still staged. An edit calls the existing correction
endpoint, which revalidates only that row, and the workspace reloads so the counts band and commit bar
reflect the change. An applied row is not editable, and edits are disabled while the batch is busy.

Checks: `dotnet build` clean, `dotnet format --verify-no-changes` exit 0, **208 backend tests passed,
zero failed, zero skipped** on pristine disposable `FIN_IAM_OrgMgmtTests_20260831_3d7eeaee`, up from
200 with eight new; frontend build clean, 107 unit tests passing, both TypeScript targets clean.

Real API and SQL evidence:

- Four staged grants reported 2 create and 2 invalid, with `user.unknown` naming the code and telling
  the administrator to import the users file first, and `application.unknown` against its own cell.
  Commit wrote 2.
- A role batch reported 1 create and 1 invalid. **The invalid row was then corrected through the
  endpoint and came back `Create` with zero errors, and the commit wrote 2 rows rather than 1** - the
  correction changed the outcome, which is the point of the preview.
- SQL confirms both grants exist with the authorization version bumped to v2, both role assignments
  exist, and the audit holds `UserApplicationAssigned x3`, `RoleAssigned x3` and
  `BulkImportRowCorrected x1`. Bulk writes emit the same events single-record writes do.

Not covered here: the richer detail presentations named in T043 - tabbed sections, module reorder
controls, organization tree browsing, concurrency reconciliation and audit server-side paging. Those
remain open and are presentation work rather than capability.

## Definition of Done

Both descriptors shipped and proven end to end, inline correction working in the preview, suites green.
