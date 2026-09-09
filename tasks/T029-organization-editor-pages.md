# T029: Organization create and edit pages

- Status: COMPLETED
- Objective: Replace the organization-management popup with a full page.
- Scope: Routed creation/editing for all eight organization unit types, preserving existing APIs, typed parent constraints, validation, draft/error handling and RowVersion conflict recovery.
- Requirements covered: Normal page scrolling, back/cancel navigation, guarded unsaved drafts, preserved browsing context, direct-link loading/error states, light/dark and mobile presentation.
- Files/components: Organization browser/editor, routes and shell route highlighting, existing unit/browser checks, task records.
- Dependencies: T028; branch codex/iam-pts-template at 46c6883, clean baseline.
- Risks/assumptions: Other agents are working in the main checkout. Change and commit only owned IAM files in this worktree; do not merge, deploy or touch business databases. Preview on port 4317 remains the user's updated frontend.

## Implementation Steps

Extract the existing editor into a routed child page; preserve the parent browser while editing; add safe direct-link initialization and navigation; adapt and extend existing tests; review and commit.

## Acceptance Criteria

Create/edit use full pages with no backdrop, dialog semantics or focus trap. All fields and mutation contracts remain. Save returns to refreshed details; cancel restores the browsing context. Browser Back and other navigation confirm dirty drafts, saving prevents duplicate requests/navigation, stale conflicts retain drafts and require explicit reload. Invalid/failed parent or target loads show a retryable error without allowing writes. Both themes and mobile widths remain usable.

## Required Tests and Validation

Frontend production build, full unit suite, app/spec/browser TypeScript, formatting, fixture browser suite, and updated disposable API/SQL browser tests where feasible. Never use FIN_IAM/FIN_PTS_DS4/FIN_PTS_QS4 for verification.

## Validation Results

- `npm run build`: passed (documented DevExtreme CommonJS warnings only).
- `npm test -- --watch=false`: 39 passed across 11 files, including two new route/draft-guard tests; existing behavior coverage retained.
- `npx tsc --noEmit -p tsconfig.app.json` and `npx tsc --noEmit -p tsconfig.spec.json`: passed.
- Browser TypeScript command from T020–T024 report: passed.
- `npm run format:check`: passed.
- `npm run test:e2e -- --workers=1`: nine fixture browser tests passed (1.1m), including all eight organization types, both themes, responsive controls, refresh/login return to child creation, Back confirmation and conflict recovery. Initial refresh check raced the route transition; corrected by awaiting the form URL before refreshing.
- `dotnet build IAM/Identity.slnx -c Release`: passed, zero warnings/errors. Initial `--no-restore` lacked this worktree's assets; normal restore/build corrected it.
- `& ./IAM/scripts/Test-OrganizationBrowser.ps1`: two actual API/SQL browser tests passed (24.7s). Disposable `FIN_IAM_OrgBrowserTests_20260830_0bab5f4d` had 58 units, ten migration records, two root/56 child creates, 20 updates and four state audit events; exact-target cleanup passed. Previous run `470deef9` exposed a test race (the concurrent writer ran before the routed editor loaded); awaited editor readiness and reran. Both disposable targets were removed. Business databases were never used.
- Screenshots use normal page scrolling; mobile Save is reached by scrolling, not a fixed modal footer. Screenshot transitions disabled to capture settled responsive layout.
- `git diff --check`: passed. Reviewed route guards, target validation, preserved parent state, mutation contracts and explicit commit paths. No backend or license changes.
- Focused implementation commit: the commit containing this record; exact hash is recorded in T031 after verification.

## Definition of Done

Implemented, tested, reviewed and documented; focused commit verified; clean task-owned working tree. No push, merge or deployment.
