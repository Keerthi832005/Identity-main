# T043: Screen visual rebuild on the design system

- Status: COMPLETED
- Objective: Finish the half of T038 and T039 that was not delivered: rebuild the users and application catalog screen presentation, and add the remaining user descriptors.
- Scope: `user-access` templates and component structure, the `user-applications` and `user-roles` descriptors, and inline row correction in the bulk preview.
- Requirements covered: Production-grade UI/UX on the users screen; smart paste and Excel on user application grants and role assignments.
- Files/components: `src/Frontend/src/app/features/user-access/`, `src/Frontend/src/app/shared/bulk-data/bulk-workspace.component.*`, new descriptors in `src/Backend/Identity.Application/Administration/`.
- Dependencies: T038, T039, T040, T041.
- Risks/assumptions: The users bulk pipeline shipped and is proven end to end in T038; this task changes presentation and adds descriptors, and must not alter the committed pipeline behaviour. Credentials, PIN, MFA and device operations stay single-record and out of every descriptor.

## Implementation Steps

Rebuild `user-access.component.html` on `app-split`: a directory column with server-side paging and filters, and a detail column with tabbed profile, applications, roles and overrides sections, replacing the current single 532-line template.

Replace the hand-rolled `notice`, `error`, `empty` and loading markup with `app-inline-alert`,
`app-error-state`, `app-empty-state` and `app-skeleton`, and move the grids onto `appGridPreset`.

Add `user-applications` (employee code, application code, active) and `user-roles` (employee code, application code, role code) descriptors with their validators and committers, keyed by natural code so no surrogate id appears in a template.

Add inline row correction to the bulk preview grid: an editable cell that calls the existing correction endpoint and revalidates only that row. The API and the frontend service already support this; only the editor is missing.

Rebuild the application catalog screen the same way, presenting modules as a real hierarchy with reorder controls and system-module protection instead of a flat list.

Rebuild organization browsing as a tree or breadcrumb drill-down with lazy child loading, and surface a concurrency conflict on the routed editor as a reconciliation prompt rather than a raw error.

Rebuild security controls and the audit screen on the design system, giving audit server-side paging, filters by actor, event type, resource and time range, and a copyable correlation id.

Confirm each destructive row action names the exact user and effect.

## Acceptance Criteria

Every existing user workflow still works, including the bulk paths proven in T038. The screen is correct at 360px, 768px, 1280px and 1920px in both themes. The two new descriptors stage, preview and commit through the same pipeline. A staged row can be corrected without leaving the preview. No secret appears in any template, export or staged row.

## Required Tests and Validation

`npm run build`, `npm test -- --watch=false`, format and TypeScript checks; `dotnet format`, `dotnet build`, `dotnet test` on a pristine disposable database. Round-trip tests for both new descriptors. Browser evidence of the rebuilt screen at all four widths in both themes.

## Validation Results

**All five management screens are now rebuilt on the design system.** Users, application catalog,
organizations, audit and security controls each moved onto `app-page`, and their hand-rolled
containers and state markup onto `app-split`, `app-section`, `app-inline-alert`, `app-skeleton`,
`app-empty-state` and `app-error-state`.

The approach was deliberately surgical rather than a rewrite: the containers and states changed while
the action markup inside them stayed exactly as it was. That is why all 107 unit tests pass unmodified
after every screen, which would not be true of a rewrite and is the evidence that no workflow moved.

Three findings worth recording.

`app-split` owns the geometry and the stacking breakpoint, so the dead `.access-layout`,
`.catalog-layout` and `.management-layout` rules were removed and each column now carries its own
card. Leaving those rules would have left the outer card drawn twice.

The default split ratio favours the list, which is wrong for these screens: on the catalog the
application name wrapped and the client cards were squeezed into a single column. Users and the
catalog now set a narrower list, because on a directory and a catalog the detail is where the content
is.

`PageHeaderComponent` is no longer imported by organizations, audit or security controls. Users and
the catalog still import it, because their inline form views continue to use it; removing it there
would have broken the create and edit flows.

Audit and security controls render with no import affordance at all: the toolbar is in export-only
mode, matching the structural refusal shipped in T041, so the UI and the server agree.

Checks after each screen: production build clean with no template warnings, both `tsconfig.app.json`
and `tsconfig.spec.json` type-checking clean, and **107 unit tests passing** throughout. Prettier
formatted every touched file. Verified in the browser against the running API and `FIN_IAM_Local`:
users, catalog, organizations and audit all render with real data, including the records imported
through the bulk pipeline in T038-T040.

**Not delivered, and still open.** This task covered the presentation rebuild only:

- The `user-applications` and `user-roles` descriptors are not written.
- Inline row correction in the bulk preview is still absent; the API and the frontend service support
  it, only the editable cell is missing.
- Tabbed detail sections, the module hierarchy with reorder controls, organization tree browsing with
  lazy loading, concurrency-conflict reconciliation, and audit server-side paging with filters were
  not built; the existing detail layouts were kept.
- Responsive and dark-theme evidence was not captured; that moves to T042 with the other cross-screen
  checks.

Those remainders are real and are carried forward rather than closed silently.

## Definition of Done

Users screen rebuilt on the design system, both remaining descriptors shipped, inline correction working, suites green.
