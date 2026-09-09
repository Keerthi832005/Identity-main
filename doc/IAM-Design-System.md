# IAM Design System

The owned presentation layer for the Identity Administration UI. Every rebuilt screen renders
against it; no screen invents its own spacing, type, states or grid configuration.

## What this extends, and what it does not

`src/styles.scss` already carried a token contract before T032: the Fujitec accent, the 14px root,
the locked single-line editor geometry, and the `--dx-color-*` fallback pattern. PTS mirrors that
contract, so T026-T028 parity depends on it. **T032 extends that contract and replaces none of it.**

Added in T032: a spacing scale, a type ramp with three roles, `--app-shadow-md`, a z-index ladder,
named motion tokens, and a global `prefers-reduced-motion` rule.

Unchanged and not to be edited casually: `--app-primary`, the `--app-editor-*` geometry, the 14px
root, the light/dark override mechanism keyed off `.dx-swatch-iam-dark` on `<body>`.

## Tokens

### Spacing

`--app-space-1` … `--app-space-8` — `0.25rem`, `0.5rem`, `0.75rem`, `1rem`, `1.5rem`, `2rem`,
`3rem`, `4rem`.

Lay out sibling groups with `gap`, not per-element margins. A component that needs a value between
two steps is a signal the scale is wrong, not the component.

### Type

| Token | Face | Used for |
| --- | --- | --- |
| `--app-font-display` | Barlow Condensed | Page titles, section headings, uppercase labels |
| `--app-font-body` | Barlow | Everything read as prose, all control labels |
| `--app-font-mono` | IBM Plex Mono | Employee codes, unit paths, batch ids, row numbers, counts |

Sizes: `--app-font-size-title` `1.714rem`, `--app-font-size-heading` `1.286rem`,
`--app-font-size-subheading` `1.071rem`, `--app-font-size-body` `1rem`,
`--app-font-size-supporting` `0.929rem`, `--app-font-size-caption` `0.857rem`.

Condensed keeps headings narrow in a dense console. Mono is reserved for values a person compares
character by character, and always with `font-variant-numeric: tabular-nums` so columns align — the
`.app-cell-mono` class applies both.

### Elevation, layering and motion

Elevation: `--app-shadow-sm` for resting surfaces, `--app-shadow-md` for popovers and sticky bars,
`--app-shadow-lg` for drawers and dialogs.

Layering: `--app-z-sticky` `10`, `--app-z-overlay` `20`, `--app-z-popover` `30`, `--app-z-drawer`
`40`, `--app-z-toast` `50`. Nothing in the product declares a raw `z-index`.

Motion: `--app-duration-fast` `120ms`, `--app-duration-base` `200ms`, `--app-duration-slow` `320ms`,
with `--app-ease-standard` and `--app-ease-emphasis`. Naming them lets the global
`prefers-reduced-motion` rule in `styles.scss` neutralise every animation from one place.

## Primitives

Import through `DesignSystemModule` (`src/app/shared/ui/design-system/`).

### Layout

| Selector | Purpose | Projection slots |
| --- | --- | --- |
| `app-page` | Page scaffold: eyebrow, title, subtitle, sticky toolbar | `[page-actions]`, `[page-toolbar]` |
| `app-section` | Bordered surface with a heading bar | `[section-actions]` |
| `app-toolbar` | Horizontal action bar | `[toolbar-trail]` |
| `app-split` | List and detail, stacking below 1100px | `[split-list]`, `[split-detail]` |
| `app-field-grid` | Responsive form grid, single column below 720px | children marked `field-span` go full width |

`app-section` takes `flush` for content that draws its own insets, such as a grid.

### State

| Selector | Replaces | Notes |
| --- | --- | --- |
| `app-empty-state` | Bare "No records" paragraphs | Project a recovery action; a message alone is not enough |
| `app-error-state` | Raw error text | The correlation id is always shown so a report traces to a request |
| `app-skeleton` | Spinner-only loading | `role="status"`, `aria-busy`; uneven bar widths read as content |
| `app-inline-alert` | Ad-hoc coloured divs | Four tones; `danger` announces assertively, the rest politely |
| `app-busy-overlay` | Full-screen load panels | Keeps layout, marks covered content `inert` |

### Grid

`appGridPreset` on a `dx-data-grid` fixes header style, row lines, hover, filter row, column
chooser, keyboard navigation and empty text. It takes `density` (`comfortable` or `compact`).

Defaults are assigned in the directive constructor, which runs **before** Angular applies the
element's own input bindings — so anything a screen sets explicitly still wins. Behavioural options
live in the directive; only what DevExtreme renders in its own markup is styled in `styles.scss`.

## Rules a screen rebuild must follow

1. Compose from the primitives. Do not hand-roll a page header, a panel, a split layout or a state.
2. Use the spacing scale through `gap`. No bespoke margins between siblings.
3. Every asynchronous surface has all four states: loading, empty, error and content. An error state
   carries the correlation id.
4. Every destructive action confirms, and the confirmation names the exact subject and effect.
5. Both themes and four widths — 360px, 768px, 1280px, 1920px — before a screen is done.
6. Wide content scrolls inside its own container. The page body never scrolls sideways.
7. Every interactive element has a visible focus ring and a keyboard path.
