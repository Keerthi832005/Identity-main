# T035: Bulk data HTTP API

- Status: COMPLETED
- Objective: Expose template download, export, staging, preview, correction, annotated errors, commit and discard as authorized typed endpoints.
- Scope: One bounded endpoint group serving every bulk-capable entity. No UI.
- Requirements covered: Excel template based export and import for all pages; smart paste and Excel upload converge on one server pipeline.
- Files/components: new `src/Backend/Identity.Api/Endpoints/BulkDataEndpoints.cs`, `src/Backend/Identity.Contracts/BulkData/`, `src/Backend/Identity.Api/Validation/`, `src/Backend/Tests/Identity.Api.Tests/`.
- Dependencies: T034.
- Risks/assumptions: File upload is a new attack surface on an identity service. Every endpoint requires the administration policy, enforces the existing request size limit, and validates content type and extension before the workbook is opened.

## Implementation Steps

Add the group under `/api/v1/admin/bulk`, requiring `AdministrationPolicy` exactly as the existing administration group does:

- `GET /{entity}/template` returns the empty styled workbook.
- `POST /{entity}/export` takes the caller's active filter and returns the populated, re-importable workbook.
- `POST /{entity}/staging` accepts either a multipart workbook or a JSON row array from smart paste, and returns a batch summary with counts by status.
- `GET /staging/{batchId}` returns paged staged rows with per-cell errors.
- `PUT /staging/{batchId}/rows/{rowNumber}` corrects one row and returns its revalidated result.
- `GET /staging/{batchId}/errors` returns the annotated workbook.
- `POST /staging/{batchId}/commit` commits valid rows and returns the applied result.
- `DELETE /staging/{batchId}` discards the batch.

Because paste and upload post to the same staging endpoint, one validation and preview path serves both entry points; the client never re-implements validation.

Define every payload as an immutable sealed record in `Identity.Contracts/BulkData/`. No anonymous objects, per the coding standard.

Enforce authorization on the batch itself: a batch is readable, correctable, committable and discardable only by the administrator who submitted it. Return `404`, not `403`, for another administrator's batch so batch ids are not enumerable.

Reject on content type, extension, declared size and row count before parsing. Stream request and response bodies rather than buffering whole workbooks. Set `Content-Disposition` with a sanitized, entity-and-timestamp derived file name.

Map every typed pipeline error to the existing problem-details shape with the correlation id, so the UI can surface a reference the administrator can quote.

## Acceptance Criteria

All eight endpoints behave as specified under the administration policy and are rejected without it. Cross-administrator batch access returns `404`. Oversized, wrong-type and malformed uploads are rejected before parsing. Downloads carry correct content type and file name. Commit replay is a no-op.

## Required Tests and Validation

`dotnet format`, `dotnet build`, `dotnet test` with API tests covering authorization, cross-administrator isolation, rejection paths, round-trip template and export, staging from both entry points, correction, annotated download and commit idempotency.

## Validation Results

All eight endpoints ship under `/api/v1/admin/bulk` behind `AdministrationPolicy`. An upload and a
smart paste both post to `POST /{entity}/staging`, which branches only on content type and then
converges on one `StageBulkBatchCommand`, so validation and preview cannot diverge between the two
entry points.

Entity resolution is the authorization boundary for the entity itself. `IBulkDescriptorCatalog` is
built from the descriptors, validators and committers registered in DI; an entity that is absent, or
present but missing its committer, is reported as not found rather than half-working.

Two security decisions are enforced in code rather than documented as intent. A batch belonging to
another administrator returns `404`, never `403`, so batch keys cannot be enumerated. And
`AdministrationContext.ActorUserId` is nullable for system contexts, so `RequireActor` refuses an
unattributed bulk import outright: a batch is owned by a person and must stay attributable.

Uploads are rejected before the workbook is opened, on declared size and on extension, and the
download file name is rebuilt from the entity key and a timestamp rather than echoing what the
caller sent. `BulkDocumentException` maps to `413` for size and row-count rejections and `400`
otherwise, both through the existing problem-details shape with the correlation id.

The annotated error workbook is rebuilt from the staged rows rather than the uploaded file, so
corrections already made in the preview are reflected and the original upload need not be retained.

Checks after implementation: `dotnet build Identity.slnx` succeeded with zero warnings and zero
errors; `dotnet format --verify-no-changes` exited 0; **149 tests passed, zero failed, zero skipped**
against real SQL Server on disposable `FIN_IAM_OrgMgmtTests_20260831_5591a910`, up from 136 in T034.
Thirteen are new API tests covering authentication and authorization on all eight routes, unknown
entity, template content type and file name, paste staging, export, non-xlsx rejection, empty-upload
rejection, filtered preview with cell errors, cross-administrator invisibility, row correction,
commit reporting and discard.

The fixture API tests use the fake dispatcher and therefore never execute the real handler. That gap
was closed with actual HTTP calls against the running API and real SQL, which is how a genuine wiring
fault was found: `GET /bulk/staging/{key}` returned `500` because `FIN_IAM_Local` was still at
migration `0010`. Applying `0011` to that existing populated database succeeded, which also
demonstrates the migration works outside a blank database. Re-verified afterwards: unknown batch
`404` with a correlation id, unregistered entity `404`, unauthenticated `401`, discard `404`.

The disposable database was dropped after verifying its exact name. `FIN_IAM`, `FIN_IAM_Dev`,
`FIN_PTS_DS4` and `FIN_PTS_QS4` were never written; `FIN_IAM_Local` is the local development
database created by this work and was migrated deliberately.

Not covered here: no entity descriptor ships yet, so every route reports not found on the running
host until T038-T040 register one. Expiry remains modelled but unswept; the sweep moves to T042 with
the rest of the operational wiring.

## Definition of Done

Endpoints shipped, authorized, bounded and tested; contracts typed and immutable; problem-details mapping verified.
