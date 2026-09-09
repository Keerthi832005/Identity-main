# T045: Presentation refinements and verification gaps

- Status: COMPLETED
- Objective: Close the presentation remainders recorded in T043 and the verification gaps recorded in T037 and T042.
- Scope: Tabbed user detail; module hierarchy with reorder and system protection; organization drill-down with lazy loading; concurrency reconciliation; audit paging, filters and copyable correlation id; shell unit tests; remaining responsive evidence; the outstanding formatting warning.
- Requirements covered: Production-grade UI/UX on every management screen. No new capability; every bulk path shipped in T038-T041 and T044 must behave exactly as it does today.
- Files/components: `src/Frontend/src/app/features/{user-access,application-catalog,organization-management,security-audit}/`, `src/Frontend/src/app/shared/shell/`, `src/_mini-controls.scss`.
- Dependencies: T043, T044.
- Risks/assumptions: These screens carry destructive actions and the bulk toolbars. The refactors are presentational, so every existing spec must keep passing unmodified; a spec that needs changing is a signal that behaviour moved, not that the test was wrong. `_mini-controls.scss` is T027-owned and shared in spirit with PTS, so it is reformatted only, never restyled.

## Implementation Steps

Give the user detail tabbed sections for profile, applications, roles and overrides, so the detail
column stops being one long scroll. Tabs are presentation only: every action keeps its current
handler and confirmation.

Present application modules as a real hierarchy using the parent relationship and display order, with
explicit move-up and move-down controls and clear protection for system modules.

Replace organization list browsing with a drill-down that loads children on demand and shows the path
back, so an administrator navigates the tree rather than paging a flat list.

Surface an organization concurrency conflict as a reconciliation prompt naming what changed and
offering to reload, rather than the current raw error line.

Give the audit screen server-side paging over the append-only stream, filters by actor, event type,
result and time range, and a correlation id the administrator can copy in one action.

Add a shell spec covering the breadcrumb fallback and the command palette's combobox behaviour, both
of which were only browser-verified in T037.

Capture the 768px and 1920px evidence missing from T037 and T042, and run Prettier over
`_mini-controls.scss` so `npm run format:check` is clean.

## Acceptance Criteria

Every existing workflow behaves as before and every existing spec passes unmodified. Tabs, reorder,
drill-down, reconciliation, paging and filters all work against the running API. `format:check` passes
with no warnings. Screens are correct at 360px, 768px, 1280px and 1920px in both themes.

## Required Tests and Validation

`npm run build`, `npm test -- --watch=false`, `npm run format:check`, both TypeScript targets;
`dotnet format`, `dotnet build`, `dotnet test` on a pristine disposable database. New shell specs.
Browser evidence per screen.

## Validation Results

All five presentation items ship, and the two verification gaps are closed.

**User detail** is tabbed into Profile, Access and Roles. Every action keeps its handler and its
confirmation; only the container changed.

**Module reorder needed backend work**, which the task did not anticipate: `DisplayOrder` was
settable only at creation, so there was no way to move a module at all. `ApplicationModule` gained a
plain `SetDisplayOrder`, and the rule about which modules may move is enforced in the handler,
because renumbering a sibling list has to touch every member including the ones that keep their
place. A move renumbers the whole sibling sequence rather than swapping two values: a bulk import
can leave several siblings sharing one display order, and swapping equal numbers moves nothing. A
system module holds its place whether it is the module being moved (refused) or the one that would
be displaced (a no-op, like the end of the list). The frontend builds the tree from the parent link
and display order; a module whose parent is missing from the catalog is shown at the root rather
than hidden with its subtree.

**Organization browsing** is a lazy tree. A branch is fetched the first time its row is opened and
kept, because browsing back up a hierarchy revisits the same nodes constantly. The first
implementation asked for 100 children and would have failed in a real deployment - the organization
query refuses a take above 50 - which the unit tests did not catch and reading the handler did. The
scope bar is now a browsing path where every step above the current node is a button back to it; a
segment the screen has never loaded is skipped rather than shown as an id, because a step it cannot
take is worse than a shorter trail.

**A concurrency conflict** reads the current version and names what the other administrator changed
- active state, name, code, description, address - instead of repeating the server's message as a
page error. When the current version cannot be fetched the prompt still appears and says plainly
that nothing on the screen changed.

**The audit trail** is paged 50 at a time against the append-only stream; applying a filter returns
to the first page and paging keeps the filter. Each correlation id can be copied in one action or
traced, which refilters the timeline to every event in that request. A clipboard the browser blocks
is explained in place rather than raised as a failure. The event-type filter stays free text on
purpose: administration and authentication events share one table and one column, so a select built
from either enum would silently hide the other vocabulary.

**Shell specs** were added first, and the breadcrumb spec immediately caught a real defect: `title()`
stripped separators before testing whether a segment was an identifier, so a batch key rendered as
title-cased words.

**Responsive evidence** is now captured by `e2e/responsive-evidence.spec.ts`, which visits every
screen at 768px and 1920px in both themes, asserts the page body never scrolls sideways, and
photographs each one - 20 screenshots per run under `test-results/`.

Running it exposed that **the offline browser suite had not been run since the design-system
rebuild** and was asserting on markup that no longer exists: the dashboard greeting, the organization
empty state, a navigation link that is now ambiguous, and user detail sections that are now behind
tabs. Each assertion was updated to the current markup, and the two layout wrappers the suite hooks
into got their class names back rather than the suite losing the check.

Checks: `dotnet build` clean with zero warnings, **211 backend tests passed, zero failed, zero
skipped** on pristine disposable `FIN_IAM_OrgMgmtTests_20260831_5d27a439` (up from 208, with three
new reorder assertions); frontend build clean, **134 unit tests passing** (up from 116),
`format:check` clean, both TypeScript targets clean; **13 of 15 browser tests passing**.

The `pts-template` test was repaired last, and it carried two more stale assertions: a page
description behind a toggle that a page now states outright, and a command palette addressed as a
plain textbox when it became a combobox on gaining its listbox. Its navigation check is now a list of
menu names rather than a count, because a count says nothing about which menu is missing and breaks
on every new entry without saying whether that entry was intended - which is exactly how it failed
when `Machines & agents` arrived. **All 15 browser tests pass.**

SQL evidence, taken after the instance recovered:

```
ApplicationModuleId  ModuleName  DisplayOrder  IsSystem
                 11  second                 0         0
                 10  first                  1         0
                 12  third                  2         1
```

`second` was created after `first` and both were created with display order 0, as a bulk import
leaves them; one move up put `second` first and renumbered the sequence, and the system module kept
its place. The disposable database `FIN_IAM_OrgMgmtTests_20260831_5d27a439` was then dropped, with
`FIN_IAM_Dev` and `FIN_IAM_Local` verified untouched.

Not closed here: the DevExpress package source and the empty `devExtremeLicenseKey`. Both are
procurement, not source.

## Definition of Done

All five presentation items delivered, shell behaviour covered by tests, responsive evidence complete, formatting clean, suites green.
