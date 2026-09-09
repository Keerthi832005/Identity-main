# T033: Excel document service on DevExpress Office File API

- Status: COMPLETED
- Objective: Provide the single server-side component that generates, reads and annotates every IAM workbook.
- Scope: Template generation, populated export, workbook parsing and error annotation. No HTTP endpoints, no staging persistence, no UI.
- Requirements covered: All Excel handling goes through the DevExpress Office File API; template-driven export and import for every management entity.
- Files/components: new `src/Backend/Identity.Application/BulkData/` (descriptors, ports), new `src/Backend/Identity.Infrastructure/Documents/` (DevExpress implementation), `Directory.Packages.props`, `src/Backend/Tests/Identity.Infrastructure.Tests/`.
- Dependencies: T032 is independent; this task may run in parallel.
- Risks/assumptions: `DevExpress.Document.Processor 26.1.3` resolves from the local NuGet cache and matches the installed DevExpress 26.1 and `devextreme 26.1.4`. No DevExpress package source is registered, so any clean or CI restore fails until one is added. It is a licensed commercial redistributable and contradicts the current `doc/Identity-Coding-Standard.md` rule that production dependencies be free/open-source; T042 records the approved exception alongside the existing DevExtreme exception.

## Implementation Steps

Add `DevExpress.Document.Processor` to central package management and reference it only from `Identity.Infrastructure`, so Domain and Application stay dependency-free and the Excel engine remains swappable behind a port.

Define `BulkEntityDescriptor` in Application: entity key, template version, ordered column definitions (header text, width, data type, required flag, enum member list, lookup sheet reference, help text) and a row-to-command projection. One sealed descriptor per importable entity; no anonymous payloads, per the coding standard.

Implement `IBulkWorkbookWriter` in Infrastructure over `DevExpress.Spreadsheet.Workbook`: styled and frozen header row, per-column width and number format, `DataValidation` list validation for enum and boolean columns sourced from a `Reference` sheet, a protected hidden `_meta` sheet carrying entity key, template version and column ids, and a first-row instruction band. The same writer produces an empty template and a populated export; an export is therefore always re-importable.

Implement `IBulkWorkbookReader`: open the uploaded stream, reject a missing or mismatched `_meta` sheet with a typed error, map columns by id rather than position so a user-reordered sheet still imports, coerce each cell to the declared type, and return typed cell values with source row and column coordinates for error reporting.

Implement `IBulkWorkbookAnnotator`: take the original workbook plus per-cell validation failures, apply a red fill and a cell comment to each failed cell, append a frozen `Errors` column summarizing the row, and return the annotated stream for download.

Enforce bounded input: maximum file size, maximum row count and maximum sheet count, all configurable and all rejected with typed errors rather than exceptions.

## Acceptance Criteria

A generated template opens in Excel with working dropdowns, locked headers and a hidden `_meta` sheet. A populated export re-imports without edits. A workbook with reordered columns still imports. An annotated workbook marks exactly the failing cells. Oversized and malformed inputs return typed errors. `Identity.Application` has no DevExpress reference.

## Required Tests and Validation

`dotnet format`, `dotnet build`, `dotnet test`. Round-trip unit tests per descriptor: generate, populate, read back, assert equality. Annotation tests asserting cell coordinates. Bounds tests for size, rows and sheets.

## Validation Results

Package selection corrected during implementation. `DevExpress.Spreadsheet.Core` was tried first as
the narrower dependency, but it ships only the engine internals: the concrete `Workbook` type lives
in `DevExpress.Docs`, which only `DevExpress.Document.Processor` provides. The meta package is
therefore required, not a convenience.

Security finding fixed rather than suppressed. `DevExpress.Printing.Core` resolves
`System.Security.Cryptography.Xml 8.0.3`, which carries three high-severity advisories
(GHSA-cvvh-rhrc-wg4q, GHSA-g8r8-53c2-pm3f, GHSA-mmjf-rqrv-855v). With NuGet audit and
`TreatWarningsAsErrors` the whole solution failed to restore. Resolved by pinning
`System.Security.Cryptography.Xml` to `10.0.11` through central transitive pinning, matching the
framework packages already in use. `NU1903` was not suppressed.

Ports live in `Identity.Application/BulkData/` (fifteen files: descriptor, column definition, cell,
row, export row, cell error, read result, limits, typed error enum and exception, three interfaces).
Implementations live in `Identity.Infrastructure/Documents/`. `Identity.Application` has no
DevExpress reference, so the layering rule holds and the engine stays replaceable.

Layout is shared through `ExcelWorkbookLayout` so the writer, reader and annotator cannot drift:
instruction band on row 1, headers on row 2, data from row 3, a `Reference` sheet carrying dropdown
sources, and a hidden `_meta` sheet holding entity key, template version and column ids.

Two behaviours were corrected against the real DevExpress API rather than assumed. Excel-side list
validation now allows blanks, because required columns are enforced by the server pipeline, which is
the authority; the client-side rule is a convenience only. A file that is not the template is
rejected by the metadata check rather than by the loader, because DevExpress parses delimited text
as a workbook instead of failing - the upload is refused either way, and the message tells the
administrator to download the template.

Checks after implementation: `dotnet restore` clean with no advisories; `dotnet build Identity.slnx`
succeeded with zero warnings and zero errors; `dotnet format --verify-no-changes` passed;
`dotnet test Identity.slnx` passed 90 with zero failures, of which twelve are the new workbook tests
covering template round trip, full export round trip, Excel row numbering, boolean and date word
coercion, malformed-value marking, blank-row skipping, entity mismatch, template version mismatch,
non-template rejection, size and row bounds, and that an annotated workbook still imports.

Thirty tests remain skipped across the suite because `IDENTITY_TEST_SQL_CONNECTION` is unset. That
is the pre-existing SQL-gated set; this task adds no persistence, and all twelve new tests are
in-memory.

Not covered here: no descriptor for a real entity is registered yet, and nothing is wired into
dependency injection. T034 adds the staging pipeline and T035 the endpoints; the per-entity
descriptors land with their screens in T038-T040.

## Definition of Done

Excel engine shipped behind ports, round-trip and bounds tests green, dependency exception raised for T042 to record.
