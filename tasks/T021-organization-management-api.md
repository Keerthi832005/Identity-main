# T021: Typed organization management HTTP APIs

Verified focused commit: `64a6c9b`. Final regression evidence: [T020–T024 verification](../doc/T020-T024-Verification.md).

- Status: COMPLETED
- Objective: Typed organization management HTTP APIs.
- Scope: Expose authorized list/detail/root-create/typed-child-create/details-update/state-update routes using typed contracts, validation and dispatcher. Cover all eight types, safe error contracts, denial, invalid identifiers/parent/type/organization and stale RowVersion.
- Requirements covered: Approved Organization → Country → Region → State → Branch → Location and Organization → Department → Team; canonical storage, immutable relationships, authorized/audited management, responsive existing UI.
- Files/components: API endpoints/contracts and API tests.
- Dependencies: T020.
- Risks/assumptions: Existing schema is sufficient; no new migrations anticipated. Business databases are forbidden test targets. No production deployment or account provisioning.

## Implementation Steps

1. Inspect dependency implementation and preserve existing behavior.
2. Implement the complete bounded scope described above.
3. Add meaningful regression/error-path tests, run applicable checks, review the diff.
4. Record evidence and create/verify a focused commit before continuing.

## Acceptance Criteria

- Every behavior in Scope is implemented and exercised by applicable tests.
- Parent/type/organization and canonical paths cannot be edited; code uniqueness remains organization-wide.
- No unrelated screen/license/PTS changes, parallel stores, hard deletion, EF migrations, or business-database writes.

## Required Tests and Validation

Backend build, format and full backend tests with disposable SQL; focused HTTP boundary tests.

## Validation Results

- `dotnet build IAM/Identity.slnx -c Release --no-restore`: passed, zero warnings/errors.
- `dotnet test IAM/Identity.slnx -c Release --no-build --no-restore`: 92 passed, zero failed/skipped (Application 3, Domain 19, API 26, AdminCli 10, Infrastructure 34), with the exact explicit disposable SQL connection documented in T020.
- `dotnet format IAM/Identity.slnx --no-restore --verify-no-changes` and `git diff --check`: passed.
- Five HTTP tests exercise all routes' 401/403, every child type, server actor, query/update mapping, nested bounds, version/state, immutable fields, numeric-type rejection, safe 409, missing resource and no DELETE.
- HTTP tests use typed fake dispatcher; application-to-SQL checks are real. Connected browser/API/SQL evidence belongs to T024.
- Reviewed policy, validation, cancellation, typed DTOs, UTC timestamps and errors. No new packages. Contract/retry semantics in `doc/Organization-Management.md`.
- Focused commit recorded in following task/final ledger.

## Definition of Done

Implementation and applicable checks pass; review and documentation are complete; task/index updated; focused Git commit created and verified. No required placeholder or silently skipped check remains.

- Dependency commit verified: T020 `3886d26`.
