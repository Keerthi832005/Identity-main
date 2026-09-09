# T028: Verify IAM and PTS form-style parity

- Status: COMPLETED
- Objective: Apply the user's MINI textbox, number box, select, button and grid appearance to IAM and PTS.
- Scope: Frontend presentation only. Headers, navigation, APIs, behavior, generated themes and license setup are unchanged.
- Requirements covered: Filled gray editors, focus underline, compact buttons, grid rules and selection; light/dark themes and mobile layout.
- Files/components: Frontend source styling, browser verification, task documentation.
- Dependencies: IAM T027, PTS T054.
- Risks/assumptions: Existing components and accessibility must keep working. MINI reference is read-only at C:/SiddeswaranS/Source/repos/PTS/PTS_MINI/src/Web. Screenshots are visual references, not additional instructions.
- Baseline: codex/iam-pts-template at d8a4bf6; clean working tree. Other agents may work concurrently; stage only owned paths.

## Implementation Steps

Inspect reference and existing controls; adapt scoped source styles; exercise forms and grids; review, document and commit only task files.

## Acceptance Criteria

Text, numeric and select editors share filled/underline treatment. Existing validation, disabled/read-only states, clear/dropdown controls and keyboard focus remain usable. Grids retain data behavior and readable compact rows; headers/navigation are not restyled. Both themes and mobile layout are checked.

## Required Tests and Validation

Production build, unit suite, TypeScript checks, changed-file formatting and Playwright regression/visual checks. Final parity verification records both apps' results.

## Validation Results

Validation completed on 2026-08-30 against the committed source in `9f2dc43` (IAM T027) and `ef3b9ca` (PTS T054, including the shared floating-label/primary-icon refinement).

| Requirement | Implementation/evidence |
| --- | --- |
| Match MINI text, number and select controls | Each frontend imports its authored `_mini-controls.scss`; identical control mixins use the existing product tokens, 48px gray filled fields, a bottom rule, red focus underline and circular clear glyphs. No generated theme was copied or rebuilt. |
| Match buttons | Compact 36px rectangular DevExtreme actions, uppercase labels, preserved contained/outlined/text semantics and white primary-button icons in both themes. |
| Match grid rows | PTS data grids and production-fact tables have compact uppercase captions and horizontal rules; existing grid hover/selection states receive the product accent. IAM retains its existing master/detail lists with compact rows and a red selected-row stripe. No sorting, paging, data source or selection workflow changed. |
| Preserve form behavior | Existing unit/browser cases still exercise creation/editing, validation failures, stale edits, deactivation, read-only/disabled controls and profile behavior. New browser checks verify numeric input, dropdown use, label/value separation, underline/focus geometry and button icon contrast. |
| Light, dark and mobile | IAM organization and PTS reference-administration screenshots reviewed on desktop and at 390px mobile width. Dropdown and drawer transitions settle before screenshots; no page overflow. |
| No header/navigation changes | `git diff --exit-code d8a4bf6 -- IAM/src/Frontend/src/app PTS/src/Frontend/src/app IAM/src/Frontend/src/theme PTS/src/Frontend/src/theme IAM/src/Frontend/src/devextreme-license.ts PTS/src/Frontend/src/devextreme-license.ts IAM/src/Frontend/package-lock.json PTS/src/Frontend/package-lock.json` passed with no differences. Source styles are scoped to page/form content. |

Commands run from each project's `src/Frontend` directory:

| Check | IAM | PTS |
| --- | --- | --- |
| `npm run build` | Pass; 2.02 MB initial bundle | Pass; 5.20 MB initial bundle |
| `npm test -- --watch=false` | 37/37 tests, 11 files | 56/56 tests, 22 files |
| `npx tsc --noEmit -p tsconfig.app.json` | Pass | Pass |
| `npx tsc --noEmit -p tsconfig.spec.json` | Pass | Pass |
| Strict browser TypeScript check | Pass; command in T027 | Pass; command in T054 |
| Formatting | `npm run format:check`: pass | `npx prettier --check src/_mini-controls.scss src/styles.scss e2e/reference-administration.spec.ts`: pass |
| Full browser regression | `npm run test:e2e -- --workers=1`: 9/9, final 1.1m | `npm run test:e2e`: 19/19, final 1.7m with configured two workers |

All 17 pre-existing PTS browser tests are retained, plus two new theme/mobile cases. IAM's nine browser tests are retained and extended. Initial style-specificity issues and a Vite module-load failure during source refresh were corrected or rerun as documented in T027/T054; the final entire suites passed with no skipped tests. Build output retains the existing DevExtreme CommonJS warnings; browser output includes existing Inferno/legacy grid-configuration/NO_COLOR notices. No warning suppression was introduced.

The agent-browser smoke checks loaded each real Angular login page and found the expected interactive controls with no runtime errors. Authenticated Playwright flows use mocked API responses and are **not actual API/SQL or SAP integration evidence**. This task changes no backend, schema, migration, authorization or persistence code, so backend/SQL checks were not rerun. Earlier organization-module verification remains separate.

Final review: the two control mixins match after whitespace/quote normalization; `git diff --check` passes; implementation commits contain only reviewed task paths. Preview artifacts are ignored under `IAM/artifacts/mini-style-preview/`. The task-owned IAM smoke process was identity-checked before stopping; all test/smoke ports 4301, 4302 and 4317 are closed. No other agents' processes or work were modified.

The bounded MINI form/grid styling scope is complete. No push, merge, publishing, deployment, production verification, account provisioning or business-database operation was performed. FIN_IAM, FIN_PTS_DS4 and FIN_PTS_QS4 were untouched. Existing broader release boundaries remain unchanged; these styling commits must be separately integrated/deployed before live sites change.

## Definition of Done

Implementation and applicable checks pass, diff reviewed, results documented, focused commit verified. No push, merge, deployment, live data access, provisioning or reference-repository modification.
