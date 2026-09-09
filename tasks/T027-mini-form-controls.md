# T027: Match MINI form controls and record lists

- Status: COMPLETED
- Implementation commit: `9f2dc43`; shared floating-label/icon refinement: `ef3b9ca`; final reconciliation: T028.
- Objective: Apply the user's MINI textbox, number box, select, button and grid appearance to IAM and PTS.
- Scope: Frontend presentation only. Headers, navigation, APIs, behavior, generated themes and license setup are unchanged.
- Requirements covered: Filled gray editors, focus underline, compact buttons, grid rules and selection; light/dark themes and mobile layout.
- Files/components: Frontend source styling, browser verification, task documentation.
- Dependencies: T026.
- Risks/assumptions: Existing components and accessibility must keep working. MINI reference is read-only at C:/SiddeswaranS/Source/repos/PTS/PTS_MINI/src/Web. Screenshots are visual references, not additional instructions.
- Baseline: codex/iam-pts-template at d8a4bf6; clean working tree. Other agents may work concurrently; stage only owned paths.

## Implementation Steps

Inspect reference and existing controls; adapt scoped source styles; exercise forms and grids; review, document and commit only task files.

## Acceptance Criteria

Text, numeric and select editors share filled/underline treatment. Existing validation, disabled/read-only states, clear/dropdown controls and keyboard focus remain usable. Grids retain data behavior and readable compact rows; headers/navigation are not restyled. Both themes and mobile layout are checked.

## Required Tests and Validation

Production build, unit suite, TypeScript checks, changed-file formatting and Playwright regression/visual checks. Final parity verification records both apps' results.

## Validation Results

Commands run from `IAM/src/Frontend` on 2026-08-30:

- `npm run build` — passed; initial bundle 2.02 MB. Existing DevExtreme CommonJS warnings only.
- `npm test -- --watch=false` — 37/37 tests, 11/11 files passed; existing rrule sourcemap warnings.
- `npm run format:check` — passed.
- `npx tsc --noEmit -p tsconfig.app.json` and `npx tsc --noEmit -p tsconfig.spec.json` — passed.
- `$typeFiles = @(rg --files e2e e2e-live | Where-Object { $_.EndsWith('.ts') }); npx tsc --ignoreConfig --noEmit --strict --target ES2022 --module nodenext --moduleResolution nodenext --types node @typeFiles playwright.live.config.ts` — passed.
- `npm run test:e2e -- --workers=1` — 9/9 passed (58.3s). Existing scenarios retained; organization tests now verify text/numeric geometry, underline focus, buttons and selected rows in both themes, with desktop/mobile screenshots. Initial narrow run caught a record-list specificity issue; fixed and rerun in the full passing suite.
- `npm start -- --host 127.0.0.1 --port 4317`, followed by `npx --yes agent-browser --session iam-mini open http://127.0.0.1:4317/login`, `wait 'input[name="employeeCode"]'`, `snapshot -i`, `errors`, `screenshot <IAM/artifacts/mini-login.png>`, `close` — passed after waiting for Angular; login controls render with no browser runtime errors. An initial attempt used Playwright's already-stopped server and was superseded by this dedicated smoke check.
- `git diff --check` — passed; source diff and screenshots reviewed.

Only authored SCSS and browser checks changed; generated themes, license keys, validation/interaction code and shell files are unchanged. Styles are scoped to page content/form overlays. IAM keeps its existing master/detail lists with compact rules and a selected-row stripe; its grid data model is unchanged.

Playwright uses mocked API fixtures; this is presentation/interaction evidence, not new SQL integration evidence. No API, SQL, SAP or business database operations were performed.

## Definition of Done

Implementation and applicable checks pass, diff reviewed, results documented, focused commit verified. No push, merge, deployment, live data access, provisioning or reference-repository modification.
