# T030: Inline management forms

- Status: COMPLETED
- Objective: Replace remaining IAM management popups with inline pages and Back buttons, as requested after T029 began.
- Scope: Applications, clients, modules, capabilities, users, profile editing, roles, grants and overrides. Keep the account/appearance dropdown and native action confirmations.
- Requirements covered: Normal document scrolling, preserved list context, safe cancellation, existing validation and profile-target race fix, both themes and mobile.
- Files/components: Application catalog and user access components, relevant tests, task records.
- Dependencies: T029 (`eeec61a`, verified clean commit).
- Risks/assumptions: Frontend only; existing API contracts and license setup remain. No business database writes, unrelated changes, push or deployment.

## Implementation Steps

Convert modal markup to inline forms; hide the underlying list while editing; provide Back and Cancel with draft/saving protection; verify existing actions and regression tests; review and commit owned files.

## Acceptance Criteria

Every management form displays in normal page flow without a backdrop or dialog semantics. Back restores the underlying context. Errors retain entered values, save remains bounded, user profile targets remain fixed.

## Required Tests and Validation

Frontend build, unit tests, type checks, formatting and fixture browser flows for both themes and mobile.

## Validation Results

- Inline forms retain the mounted list context and existing typed API calls. A shared navigation helper protects Back/Cancel, route changes and browser unload; duplicate catalog submissions and navigation during saves are blocked. Client secrets clear on cancel/success/destruction. Profile target snapshot and manager-search version protections remain.
- `npm test -- --watch=false`: 41 tests passed in 13 files. Includes new pending-save/duplicate-write and draft/navigation guard tests; original profile race tests retained.
- `npx tsc --noEmit -p tsconfig.app.json`, `npx tsc --noEmit -p tsconfig.spec.json`, and the browser TypeScript command from T020–T024: passed.
- `npm run format:check`: passed.
- `npm run build`: final production build passed. Existing DevExtreme CommonJS warnings remain.
- `npm run test:e2e -- --workers=1`: final 11/11 passed (1.3m). Catalog tests now run in both themes and open every catalog form; access checks open every grant/role/override form; profile tests retain create/edit/clear/error coverage and add Back/draft confirmation. Mobile screenshots for catalog/profile/organization reviewed; no modal backdrop or horizontal overflow.
- Reviewed target context visibility, error retention, cancellation/saving guards, unchanged typed mutations and profile race protection. Existing header/account controls unchanged. `git diff --check` passed; only owned IAM paths staged.
- Evidence boundary: catalog/access tests route fixture API responses; actual organization API/SQL evidence is in T029. No business database writes or deployment.
- Vitest DOM logs two unsupported `window.scrollTo` notices; browser checks verify the real scroll behavior. Existing rrule source-map and Inferno/NO_COLOR warnings remain non-failing.
- Focused implementation commit: the commit containing this task record; verified hash recorded in T031.

## Definition of Done

Implementation, checks and review pass; results documented and focused commit verified.
