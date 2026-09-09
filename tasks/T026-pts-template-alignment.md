# T026: Apply the PTS template to IAM

- Status: COMPLETED
- Objective: Match the supplied PTS screenshot and existing PTS source template while retaining IAM workflows.
- Scope: IAM frontend shell, grouped/searchable navigation, breadcrumb and menu search, profile/appearance menu, shared page headers and card geometry across the six administration screens; light/dark and mobile.
- Requirements covered: Reuse Fujitec branding, PTS red accents, neutral canvas, blue navigation icons, sidebar/header proportions, account menu and card layout. Real navigation and keyboard controls; preserve existing API contracts, authorization, profile target safety and license setup.
- Files/components: IAM frontend shell, shared page header, feature header templates/styles, browser regression tests and this record.
- Dependencies: T024 and current repository main baseline `0df5620`.
- Risks/assumptions: Fresh isolated branch `codex/iam-pts-template`; initial clean worktree. PTS is a read-only visual reference. PTS production notifications and DS4/QS4 switching do not belong to IAM and are not copied. No live data, backend, migration, deployment, push or merge changes.

## Implementation Steps

1. Inspect screenshot, PTS source template and current IAM UI.
2. Adapt the reusable PTS page header and shell styling; preserve IAM behavior.
3. Verify theme switching, search, sidebar, account controls, existing feature flows and mobile bounds.
4. Review, document exact checks and commit scoped changes.

## Acceptance Criteria

- All six IAM administration routes use the matching shell and page-header template.
- Grouped navigation, menu search, previous/next navigation, desktop collapse, mobile dismissal and account actions work with keyboard access.
- Light/dark and mobile layouts remain readable with no horizontal overflow.
- Existing organization creation/edit/conflict and user-profile regression tests still pass.
- PTS, license files, API contracts, authentication and business databases remain untouched.

## Required Tests and Validation

Frontend production build; all UI unit tests; application/spec/browser TypeScript checks; Prettier; existing and new Playwright browser flows; visual screenshots in both themes and mobile; diff review.

## Validation Results

Commands ran from `IAM/src/Frontend` unless stated otherwise:

| Command/check | Result |
| --- | --- |
| `npm ci --no-audit --no-fund` | Passed, 475 packages; lockfile/dependency versions unchanged |
| `npm run build` | Production build passed; 2.01 MB initial bundle; existing four DevExtreme CommonJS warnings only |
| `npm test -- --watch=false` | 37 tests passed in 11 files; zero failed/skipped |
| `npx tsc --noEmit -p tsconfig.app.json` | Passed |
| `npx tsc --noEmit -p tsconfig.spec.json` | Passed |
| `npm run format:check` | All matched files passed |
| `npm audit --omit=dev --audit-level=high` | Zero vulnerabilities |
| `npm run test:e2e -- --workers=1` | Nine passed in 1.0m; all seven existing browser tests retained plus two new template tests |
| `npm run test:e2e -- pts-template.spec.ts --workers=1` | Two passed after focused review fixes |
| `git diff --check` at repository root | Passed |
| Scoped Git comparisons | No changes to PTS, backend, migrations, auth/session/profile service logic, runtime config, license, generated themes or branding assets |

All browser TypeScript files also passed this installed TypeScript 6 invocation in PowerShell:

```powershell
$typeFiles = @(rg --files e2e e2e-live | Where-Object { $_.EndsWith('.ts') })
npx tsc --ignoreConfig --noEmit --strict --target ES2022 --module nodenext --moduleResolution nodenext --types node @typeFiles playwright.live.config.ts
```

Agent-browser commands: `npx --yes agent-browser --session iam-template open http://127.0.0.1:4317/login`, `snapshot -i`, `errors`, `screenshot <absolute-local-path>`, `close`. Login controls rendered with no captured page errors. Initial screenshot used a missing relative output directory; rerunning with an absolute existing path passed. The temporary dev server is stopped after verification.

Reviewed 1536×900 light/dark desktop screenshots and 390×844 light/dark mobile account-menu/navigation screenshots. Existing organization editor browser tests also retain their 1280×720 and 390×844 bounds/contrast checks. Local screenshots are ignored artifacts; they are not committed. Known existing rrule sourcemap/Inferno/NO_COLOR warnings are unchanged; one early browser run emitted DevExtreme theme-load W0004 under resource contention, with correct theme assertions and subsequent final run passing.

Final source review and explicit staged-path review precede the focused T026 commit. The containing commit ID is reported in the final handoff.

## Implementation and review

- Adapted the existing PTS page-header and account-menu styles to IAM's token names. All six administration routes retain their original headings, actions and field workflows while using the same icon tile, accent rail, eyebrow badge and optional description.
- Matched PTS's 20.5rem sidebar, 4.5rem header, centered menu search, grouped blue-icon navigation, red active item, neutral canvas, card radius and subtle shadows. Desktop navigation collapses to an icon rail; mobile navigation uses an inert background, keyboard containment, Escape dismissal and restored toggle focus.
- Menu search is local navigation, including organization-type and security synonyms, not cross-system business-data search. Previous/next respects the six IAM routes.
- My profile displays the signed-in name/code; Roles and permissions displays session capabilities; Session details displays application/authorization version. Existing user/access and audit screens remain the destinations for management. These panels do not claim to fetch an employee-directory profile or role assignment list.
- Preserved Light/Dark/System preferences; added arrow-key navigation and one tab stop for the theme radiogroup. Existing session/authentication services and profile-target logic are unchanged.
- Browser review corrected plural search terms, focus timing after removing mobile inert state, dark badge/link contrast and header search positioning. Mobile screenshots wait for the drawer to fully enter the viewport. Using a distinct `favicon.ico?brand=iam` image URL retains the original icon and avoids the browser's automatic favicon request interaction under routed tests; the browser test verifies the real image decodes at 32px. No branding asset, license or generated theme was modified.
- The user explicitly requested committing only this task's files because other agents are working. All changes stay in the isolated worktree; staging uses an explicit reviewed allow-list. No blanket staging or changes to other agents' work.

## Evidence and release boundary

The nine Playwright tests use API fixtures. They verify real Angular/DevExtreme interactions, request flows and rendering, but are not new SQL integration evidence. The agent-browser smoke check opens the non-intercepted local login page and checks controls/errors; it does not authenticate against a live database. This task has no API/SQL/schema changes, no account provisioning and no business-database reads/writes. The previously documented actual API/SQL evidence remains in T020–T024 verification and is not claimed as rerun here.

No PTS-only production notifications or DS4/QS4 switcher were added. No push, merge, publishing or deployment is authorized or performed. The deployed IAM site will not change until separately integrated and deployed.

## Definition of Done

Implementation and applicable checks pass; screenshot comparison and diff reviewed; record updated; focused commit verified; no task-owned changes uncommitted.
