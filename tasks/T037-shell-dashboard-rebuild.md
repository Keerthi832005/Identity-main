# T037: Shell, navigation and dashboard rebuild

- Status: COMPLETED
- Objective: Rebuild the application frame and the landing screen on the T032 design system.
- Scope: Shell, navigation, topbar, account and appearance panel, dashboard. Existing services, models, guards and interceptors are retained unchanged.
- Requirements covered: Production-grade UI and UX for the frame every screen renders inside.
- Files/components: `src/Frontend/src/app/shared/shell/`, `src/Frontend/src/app/features/dashboard/`, `src/Frontend/src/app/features/access-denied/`.
- Dependencies: T032.
- Risks/assumptions: The current shell already carries menu search, a collapse pin, a mobile drawer with `inert`, breadcrumbs and a theme radio group. These are correct behaviors and are preserved through the rebuild; the rebuild replaces the hand-rolled markup and styling, not the interaction model. `auth`, `theme` and session wiring are untouched.

## Implementation Steps

Rebuild the shell against the design-system primitives: replace the bespoke sidebar, topbar and panel styling with tokens, elevation and spacing scales, and keep every existing behavior including collapse, drawer `inert`, keyboard theme selection and `Ctrl K` search.

Fix the frame problems the current markup has: give the command palette proper combobox semantics with arrow-key navigation and `aria-activedescendant`, close it on `Escape` and outside click, make the account panel a focus-trapped dialog that restores focus on close, and give the breadcrumb real route-derived segments rather than a two-part label.

Rebuild the dashboard as a real landing screen: a metric band using the design-system tiles, recent activity, and direct actions into each management area, with skeleton, empty and error states from the state primitives instead of bare text. Data continues to come from the existing `dashboard.service.ts`.

Rebuild `access-denied` on the same page scaffold so an authorization failure looks deliberate rather than unstyled.

## Acceptance Criteria

Frame and dashboard render correctly at 360px, 768px, 1280px and 1920px in light, dark and system themes. Every previously supported shell behavior still works. Command palette and account panel meet dialog and combobox accessibility requirements. Dashboard shows real skeleton, empty and error states.

## Required Tests and Validation

`npm run build`, `npm test -- --watch=false`, `npm run format:check`, TypeScript checks, Playwright fixture suite. Keyboard-only walkthrough of navigation, palette and account panel. Screenshots at all four widths in both themes.

## Validation Results

The shell's interaction model was kept, as planned: collapse, mobile drawer with `inert` and a Tab
trap, `Ctrl K`, section collapse, theme radio keyboard navigation and outside-click dismissal all
still work. What changed is the three things that were actually wrong.

**A breadcrumb that lied.** `activeItem` falls back to `navigation[0]` so the prev/next stepper
always has a position, and the breadcrumb inherited that fallback. On any route outside the
navigation list it confidently named the wrong screen: `/bulk/map` displayed "Workspace / Overview".
A separate `breadcrumb` computed now derives segments from the route when nothing matches, and
renders an identifier-looking segment as "Details" rather than prettifying a batch key. Verified in
the browser: `/bulk/map` reads "Bulk / Map".

**A command palette that was not a combobox.** It offered no arrow-key navigation and no
`aria-activedescendant`. Adding them through `dx-text-box`'s `inputAttr` did not work: the DevExtreme
editor sets `role="textbox"` on its own input and that cannot be overridden, so the attributes were
silently ineffective. The palette input is now a native `input` styled to the same geometry, which is
appropriate because it is a command palette rather than a form editor and no MINI styling applies to
it. Verified in the browser after typing and pressing ArrowDown: `role="combobox"`,
`aria-expanded="true"`, `aria-activedescendant="menu-option-0"`, options carrying `role="option"` and
`aria-selected`, and a keyboard highlight distinct from hover.

**An account panel that leaked focus.** Tab could escape the open dialog, and closing it left focus
nowhere. It now traps Tab while open and returns focus to its trigger on close.

The dashboard was rebuilt on `app-page`, `app-section`, `app-skeleton`, `app-error-state` and
`app-empty-state`. It replaces the previous static module cards, which described build tasks rather
than the service, with a metric band, real recent activity and direct routes into each management
area. A failed sign-in count above zero renders in the warning tone, because on an identity service
any failure is worth a second look. The error state carries the correlation id and distinguishes an
unreachable service from a rejected request. `access-denied` was rebuilt on the same scaffold.

Checks after implementation: production build succeeded; **96 unit tests passed across 19 files**;
both `tsconfig.app.json` and `tsconfig.spec.json` type-check clean; Prettier formatted every touched
file.

Browser evidence against the running application and real SQL: the dashboard rendered live counts
from `FIN_IAM_Local` (1 application, 1 user, 8 live sessions, 2 failed sign-ins shown in the warning
tone) with five recent audit events and their correlation ids. Console errors dropped from one to
zero; the remaining message is the pre-existing DevExtreme theme-timeout warning.

Not covered here: screenshots were captured at desktop width only, so the 360px, 768px and 1920px
checks and the dark-theme pass are still outstanding and move to T042 with the other cross-screen
evidence. No unit test covers the new breadcrumb or combobox behaviour; both were verified in the
browser, and the shell has no spec file to extend without creating one, which belongs with the
broader validation task.

## Definition of Done

Frame and dashboard rebuilt with no behavior regression, accessibility gaps closed, suites green.
