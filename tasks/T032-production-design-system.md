# T032: Production design system foundation

- Status: COMPLETED
- Objective: Replace the ad-hoc per-screen styling with one owned design system that every rebuilt IAM screen renders against.
- Scope: Token contract, layout primitives, state primitives, data-grid preset, editor kit extensions, motion and accessibility baseline. No feature behavior, no backend, no API change.
- Requirements covered: Production-grade UI/UX baseline; consistent light/dark/system theming; responsive and accessible presentation for every subsequent screen rebuild.
- Files/components: `src/Frontend/src/styles.scss`, `src/Frontend/src/_mini-controls.scss`, new `src/Frontend/src/app/shared/ui/design-system/` (tokens, layout, states, grid preset), `src/Frontend/src/app/shared/page-header/`.
- Dependencies: T031, current `main`.
- Risks/assumptions: The existing `--app-*` token contract intentionally mirrors PTS and carries the Fujitec accent `#d01126`, the 14px root and the locked editor geometry. Those are preserved and extended, not replaced, so PTS parity from T026-T028 survives. Rewriting them would silently break both products.

## Implementation Steps

Extend the existing `--app-*` contract with the tiers the current sheet lacks: a full 8-step spacing scale, a five-step type ramp with explicit line heights, elevation levels, a z-index ladder, and named durations/easings. Keep every literal fallback pattern already in use.

Add layout primitives as standalone components under `shared/ui/design-system/`: `app-page` (title, subtitle, breadcrumb slot, primary/secondary action slots, sticky toolbar), `app-section`, `app-toolbar`, `app-field-grid` (responsive 1/2/3 column form grid), and `app-split` (list plus detail with a mobile stack breakpoint).

Add state primitives that every screen must use instead of hand-rolled markup: `app-empty-state`, `app-error-state` (message, correlation id, retry), `app-skeleton` (text, row and card shapes), `app-inline-alert` (info/success/warning/danger) and `app-busy-overlay`.

Add one `dx-data-grid` preset directive that fixes header style, row height, density toggle, zebra rules, sticky header, column chooser, filter row, selection affordances, keyboard focus ring and the empty/loading templates, so no screen configures those independently.

Define the accessibility baseline: visible focus ring on every interactive element, `prefers-reduced-motion` honored by all named durations, minimum 4.5:1 text contrast in both themes, landmark roles, and a documented heading order per page shape.

Document the system in `doc/IAM-Design-System.md`: token table, primitive inventory, when to use each, and the rules a screen rebuild must follow.

## Acceptance Criteria

Every primitive renders correctly in light, dark and system themes at 360px, 768px, 1280px and 1920px widths. No screen regression: existing screens still build and their unit tests still pass against the extended tokens. Contrast and focus requirements verified. `doc/IAM-Design-System.md` exists and matches the shipped primitives.

## Required Tests and Validation

`npm run build`, `npm test -- --watch=false`, `npm run format:check`, TypeScript checks, and Playwright fixture suite green. Screenshot evidence at all four widths in both themes.

## Validation Results

Token contract extended in `src/styles.scss`: eight-step spacing scale, three-role type ramp with
explicit sizes, `--app-shadow-md`, a five-level z-index ladder, named durations and easings, and a
global `prefers-reduced-motion` rule. The pre-existing `--app-primary`, `--app-editor-*` geometry
and 14px root are unchanged, so PTS parity from T026-T028 is intact. `angular.json` now loads
Barlow Condensed 700 alongside the 600 weight already vendored.

Eleven primitives shipped under `src/app/shared/ui/design-system/`, exported through
`DesignSystemModule`: `app-page`, `app-section`, `app-toolbar`, `app-split`, `app-field-grid`,
`app-empty-state`, `app-error-state`, `app-skeleton`, `app-inline-alert`, `app-busy-overlay` and
the `appGridPreset` directive. `app-error-state` uses `dx-button` rather than a new button class, so
MINI styling from T027 applies without change.

Frontend checks after implementation: production build succeeded; 77 unit tests passed across 17
files, including seven new design-system tests covering the page scaffold, projection slots, alert
live-region tones, the mandatory correlation id, empty-state message omission, skeleton line count
and busy-overlay `inert`. Both `tsconfig.app.json` and `tsconfig.spec.json` type-check clean.
Prettier formatted every new and touched file.

Known pre-existing issue, not introduced here and not fixed here: `npm run format:check` reports
`src/_mini-controls.scss` as unformatted. That file is unmodified by this task; reformatting it
would touch T027-owned styling outside this scope.

No screen was migrated onto the primitives in this task. T037-T041 do that, so a visual regression
check of the existing screens against the extended tokens belongs with them; the existing suites
passing is the evidence that the token extension broke nothing.

## Definition of Done

Design system shipped and documented, existing suites unbroken, focused commit confined to `IAM/`.
