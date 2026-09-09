# T036: Smart paste engine and shared bulk data workspace

- Status: COMPLETED
- Objective: Build the one reusable frontend surface that gives every management page smart paste plus template export and import.
- Scope: Clipboard parsing, column auto-mapping, the staged preview workspace, template and export actions. No per-screen wiring; screens adopt it in T038-T041.
- Requirements covered: Smart paste on all pages; Excel template based export and import on all pages, from a single implementation rather than per-screen copies.
- Files/components: new `src/Frontend/src/app/shared/bulk-data/` (clipboard parser, column matcher, workspace component, service, models), `src/Frontend/src/app/shared/ui/design-system/`.
- Dependencies: T032, T035.
- Risks/assumptions: A global paste handler must never hijack a paste into a focused editor or a text selection. It activates only when the page-level grid or drop surface holds focus and the payload is multi-cell. The client never validates rows itself; it posts them to the T035 staging endpoint so browser and server can never disagree.

## Implementation Steps

Implement the clipboard parser: prefer `text/html` and read its first table, because that handles quoted values and newlines inside cells that plain TSV loses; fall back to TSV, then CSV with quote handling. Detect whether the first row is a header by matching it against the descriptor's column names.

Implement the column matcher: normalize header text (case, spacing, punctuation, common synonyms such as `Employee Code` / `EmpCode` / `Employee ID`) and match to descriptor columns; fall back to positional order when no header is present. Always show the resolved mapping in an editable panel with per-column remap and ignore, and never commit a guessed mapping silently.

Build `app-bulk-workspace`, a routed inline page consistent with the T029-T031 inline-page pattern rather than a popup: a summary band with counts by status, a status filter, a preview grid with per-cell error highlighting and hover detail, inline row editing that calls the correction endpoint, download of the annotated workbook, discard, and a commit action that states exactly how many rows will be written and how many will be left behind.

Add `app-bulk-actions`, the toolbar control a screen drops in to get "Download template", "Export" (current filter, current columns), "Import" (file picker plus drag-and-drop) and a paste affordance, with a keyboard path for every action.

Wire the paste path: on a qualifying paste, parse, map, post to staging, and route to the workspace with the returned batch id. Upload follows the identical path, so both entry points land on the same preview.

Handle the real-world states properly: large paste truncation warning, unsaved-batch navigation guard reusing `canLeaveInlineForm`, progress for long uploads, and a clear error state carrying the correlation id.

## Acceptance Criteria

Pasting a block copied from Excel produces a correct mapping and a staged preview. Pasting into a focused text editor still behaves normally. Mapping is always visible and editable before commit. Error cells are individually identifiable and correctable inline. Template, export, import, annotated download, discard and commit all work from the keyboard. Nothing is written before commit.

## Required Tests and Validation

`npm run build`, `npm test -- --watch=false`, `npm run format:check`, TypeScript checks. Unit tests for the parser against Excel, Google Sheets and CSV clipboard payloads including quoted and multi-line cells, and for the matcher against reordered, renamed, partial and headerless input. Playwright coverage of paste, upload, correct, commit and discard.

## Validation Results

Shipped under `src/app/shared/bulk-data/`: clipboard parser, column matcher, HTTP service, paste
directive, toolbar, and the two routed pages (`/bulk/map` and `/bulk/:batchKey`). Both hang off the
shell, so they inherit navigation, theming and the authentication guard.

The parser prefers `text/html` because Excel and Sheets both put a real table there, which survives
values containing tabs or newlines; TSV and CSV are fallbacks. The CSV path is a proper state machine
rather than a split, so a quoted comma, a quoted newline and a doubled quote all read correctly. Tabs
win over commas when both appear on the first line: a comma inside a spreadsheet cell is ordinary
text, whereas a tab is almost always Excel's own separator.

Paste interception is deliberately narrow. `BulkPasteDirective` ignores the event when the target is
an input, textarea, select or anything `contenteditable`, and when the payload is a single cell, so
pasting into a filter box or an editor still behaves normally.

Header detection requires at least 60 per cent of non-empty first-row cells to resolve to template
columns. Guessing wrong in either direction loses a record or imports a header as data, so the bar is
deliberately high, and when detection fails the mapping screen says so rather than proceeding
quietly.

A guessed mapping is never applied silently. The mapping screen always shows the resolved mapping
with a sample value per column, allows re-targeting and ignoring, refuses to stage while a required
column is unmapped, and prevents two source columns from landing on the same template column.
Synonyms cover the spellings real HR and catalogue exports use: `Emp Code`, `Employee ID`,
`Full Name`, `Official Mail ID`, `Reports To` and others.

Row numbering is Excel's own, offset for a detected header row, so an error message names a row the
administrator can actually find in their file.

The workspace is the single preview for both entry points. Counts band, needs-attention filter,
per-cell error highlighting with a focusable tooltip, annotated workbook download, discard, and a
commit bar that states exactly how many rows will be written and how many will be left behind.
Commit and discard both confirm first, naming the numbers.

Checks after implementation: production build succeeded and emitted both lazy chunks
(`bulk-workspace-component` 18.68 kB, `bulk-mapping-component` 8.97 kB); **96 unit tests passed
across 19 files**, up from 77, with nineteen new; `tsconfig.app.json` and `tsconfig.spec.json` both
type-check clean; Prettier formatted every new file.

Browser evidence against the running application: `/bulk/map` redirected to
`/login?returnUrl=%2Fbulk%2Fmap`, confirming the guard covers the new routes, and after signing in
the page rendered on the T032 `app-page` primitive with its eyebrow, title, subtitle and action slot.

Known pre-existing issue, unchanged: `npm run format:check` still reports `src/_mini-controls.scss`,
which this task does not own.

Not covered here: no screen wires the toolbar or the paste directive yet, and no entity descriptor is
registered, so the flow cannot be exercised end to end until T038. Inline row correction is served by
the API and the service but is not yet an editable cell in the preview grid; it lands with the first
screen adoption, where a real descriptor makes the editor meaningful.

## Definition of Done

One reusable workspace and toolbar shipped, both entry points converging on the server pipeline, paste isolation proven, suites green.
