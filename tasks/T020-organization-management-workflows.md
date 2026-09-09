# T020: Organization management queries and update workflows

Verified focused commit: `3886d26`. Final regression evidence: [T020–T024 verification](../doc/T020-T024-Verification.md).

- Status: COMPLETED
- Objective: Organization management queries and update workflows.
- Scope: Extend the canonical organization store and dispatcher with authorized bounded search/details and immutable-relationship updates. Reuse atomic creation; add descriptive/address creation options, normalized-code conflict handling, no-op replay, audit, cancellation and RowVersion checks.
- Requirements covered: Approved Organization → Country → Region → State → Branch → Location and Organization → Department → Team; canonical storage, immutable relationships, authorized/audited management, responsive existing UI.
- Files/components: Application/domain/infrastructure organization code and SQL-backed tests.
- Dependencies: T019.
- Risks/assumptions: Shared storage is sufficient. Inspection found the SQL audit event allow-list requires forward-only migration 0010 for the two new update/state events; no applied migration is changed. Business databases are forbidden test targets. No production deployment or account provisioning.

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

dotnet restore IAM/Identity.slnx; dotnet build IAM/Identity.slnx -c Release --no-restore; dotnet test IAM/Identity.slnx -c Release --no-build --no-restore with explicit disposable IDENTITY_TEST_SQL_CONNECTION; dotnet format IAM/Identity.slnx --no-restore --verify-no-changes.

## Validation Results

- `dotnet restore IAM/Identity.slnx`: passed.
- `dotnet build IAM/Identity.slnx -c Release --no-restore`: passed, zero warnings/errors.
- `dotnet test IAM/Identity.slnx -c Release --no-build --no-restore`: 87 passed, zero failed/skipped (Application 3, Domain 19, API 21, AdminCli 10, Infrastructure 34).
- SQL test environment: `IDENTITY_TEST_SQL_CONNECTION=Server=lpc:HOCOM18502627\SQLEXPRESS2022;Database=FIN_IAM_OrgMgmtTests_20260830_6912750e;Integrated Security=true;Encrypt=true;TrustServerCertificate=true`.
- `dotnet run --project IAM/src/Backend/Identity.Database -c Release --no-build --no-restore` with `IDENTITY_DATABASE_CONNECTION` set to that same disposable target: migrations 0001–0010 applied; replay did no work; ten history rows; audit constraint enabled/trusted.
- `dotnet format IAM/Identity.slnx --no-restore --verify-no-changes` and `git diff --check`: passed.
- Added seven SQL tests covering every type, filtering/paging, common fields, relationship immutability, normalized uniqueness across types, concurrent writers, stale/no-op updates, invalid ownership/parents/fields, audit rollback, authorization and cancellation. Existing eight organization tests retained; both classes share an xUnit collection because the atomicity test checks global counts.
- Review corrected timestamp readback to return SQL-persisted precision; no-op edits do not mutate tracked fields. No new dependencies. Migration 0010 is required solely by the deployed audit allow-list.
- Focused commit: recorded in the following task/final ledger after commit creation.

## Definition of Done

Implementation and applicable checks pass; review and documentation are complete; task/index updated; focused Git commit created and verified. No required placeholder or silently skipped check remains.
