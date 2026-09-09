# T034: Bulk staging persistence and validation pipeline

- Status: COMPLETED
- Objective: Persist an uploaded or pasted batch, validate every row, and commit only on explicit confirmation.
- Scope: Forward-only migration, staging entities and mappings, validation pipeline, commit and discard workflows, audit. No HTTP surface, no UI.
- Requirements covered: Stage, preview, then commit import semantics; nothing is written to identity tables until the administrator confirms.
- Files/components: `db/migrations/0011_bulk_data_staging.sql`, `src/Backend/Identity.Domain/BulkData/`, `src/Backend/Identity.Application/BulkData/`, `src/Backend/Identity.Infrastructure/Persistence/`, `src/Backend/Tests/Identity.Application.Tests/`, `src/Backend/Tests/Identity.Infrastructure.Tests/`.
- Dependencies: T033.
- Risks/assumptions: The next migration number is `0011`; `0001`-`0010` are applied and must not be edited. Staging tables hold administrator-supplied identity data, so they inherit the same audit and retention obligations as the target tables and must never hold a password, PIN, OTP or client secret.

## Implementation Steps

Add migration `0011_bulk_data_staging.sql` creating `BulkImportBatch` (batch id, entity key, template version, source `Excel` or `Paste`, uploaded file name, submitted by, submitted at, row counts by status, state, expiry) and `BulkImportRow` (batch id, source row number, raw cell payload, normalized values, per-cell error collection, row state). Extend the audit event allow-list with the batch submit, correct, commit and discard events. Forward-only, checksummed, replay-safe.

Implement validation as an ordered pipeline per descriptor so every entity gets the same guarantees without repeating logic: required and type checks from the descriptor, then format and range rules, then referential lookups (manager employee code, application code, parent organization unit, role code), then in-batch duplicate detection, then existing-record conflict detection which classifies each row as `Create`, `Update` or `Conflict`.

Every failure is a typed per-cell result carrying row number, column id, a stable error code and a human message, so the same result set drives the preview grid, the annotated workbook and the API response with no duplicate mapping.

Implement row correction: an administrator edits one staged row, only that row re-runs the pipeline, and batch counters update. This is what makes the preview usable rather than a dead-end report.

Implement commit: within one transaction, project each valid row through the descriptor into the existing administration commands so bulk writes reuse the same domain rules, authorization and audit as single-record writes. Never bypass them. The batch id is the idempotency key, so a replayed commit is a no-op rather than a duplicate insert. Rows still in error are left staged and reported.

Implement discard and expiry: an administrator discards a batch explicitly, and abandoned batches expire on a bounded schedule so staged identity data does not accumulate.

## Acceptance Criteria

A batch with mixed valid and invalid rows stages fully and writes nothing. Correcting a row clears exactly its errors. Commit writes only valid rows, emits the same audit events as single-record writes, and is idempotent on replay. Discard and expiry remove staged data. Migration applies to a blank database and replays safely.

## Required Tests and Validation

`dotnet format`, `dotnet build`, `dotnet test` including SQL Server persistence tests on a uniquely named disposable database. Migration apply-and-replay verification. Idempotency test committing the same batch twice. Never write `FIN_IAM`, `FIN_PTS_DS4` or `FIN_PTS_QS4`; verify exact targets before cleanup.

## Validation Results

Migration `0011_bulk_data_staging.sql` adds `Identity.BulkImportBatch` and `Identity.BulkImportRow`
and extends the authentication audit event allow-list with the five bulk import events. Constraints
carry the invariants rather than relying on application code: an invalid row must hold errors and a
valid row must not, a committed batch must have a commit timestamp, `ExpiresAt` must follow
`SubmittedAt`, cell payloads must be valid JSON, and only an applied row may name a resource.

Idempotency lives in the aggregate. `BulkImportBatch.MarkCommitted` returns false and changes
nothing when the batch is already committed, so a retried commit cannot write the same rows twice.
`BatchKey` is the client-facing identity and the idempotency key.

Ownership is part of the query, not a check after it. `BulkStagingStore.FindBatch` filters on the
submitting administrator, so another administrator's batch is indistinguishable from one that does
not exist and batch keys cannot be probed.

`BulkValidationPipeline` is pure and synchronous: the caller fetches entity verdicts in one batched
pass and hands them in, so the pipeline is fully unit-testable and cannot issue a query per row.
Order is descriptor checks, then in-batch duplicate detection on the natural key, then merged entity
verdicts, then a create/update/invalid classification.

Two details corrected against real behaviour. A boolean column is checked against its normalised
value rather than its dropdown labels, because the reader has already turned Yes into true and
comparing against Yes/No would have rejected every row. An incomplete natural key is reported as a
missing required value rather than as a duplicate, so the message names the real problem.

Checks after implementation: `dotnet build Identity.slnx` succeeded with zero warnings and zero
errors; `dotnet format --verify-no-changes` exited 0; migration `0011` applied to a blank database
and replayed as a no-op.

Full suite against real SQL Server: **136 passed, zero failed, zero skipped** across all five test
projects, on disposable `FIN_IAM_OrgMgmtTests_20260831_6e5c843b`. That is up from the 90 recorded in
T033, where 30 SQL-gated tests were skipped for want of a connection string.

Sixteen tests are new: eleven pipeline tests in `Identity.Application.Tests` covering create versus
update classification, required, malformed, enumeration, boolean normalisation, maximum length,
in-batch duplicates, incomplete keys and verdict merging; and five staging tests in
`Identity.Infrastructure.Tests` covering staged persistence writing no identity record, per-row
correction leaving other rows untouched, cross-administrator invisibility, commit idempotency and
the valid-row-must-not-carry-errors invariant.

`PersistenceModelTests.Model_MapsAllApprovedIdentityTables` was updated to include the two new
tables. That test asserts the exact approved table set, so it correctly failed until the addition
was declared; the guard was extended, not weakened.

Eight organization tests, one AdminCli test and one authentication test failed on a first SQL run and
were investigated rather than assumed broken. They guard on the database name via
`Assert.StartsWith("FIN_IAM_OrgMgmtTests_")`, a deliberate safety rail so they only touch their own
disposable database. Re-run against a correctly named database they all pass. No regression existed.

All three disposable databases were dropped after verifying their exact names against an explicit
allow-list. `FIN_IAM`, `FIN_IAM_Dev`, `FIN_PTS_DS4` and `FIN_PTS_QS4` were never written.

Not covered here: no HTTP surface, and expiry is modelled (`ExpiresAt`, `HasExpired`, `ListExpired`)
but no scheduled sweep runs it yet. T035 adds the endpoints; the sweep is wired with them.

## Definition of Done

Staging pipeline shipped, migration applied and replayed on a disposable database, commit proven idempotent and audited, disposable databases removed.
