# T002: IAM domain and persistence

- Status: COMPLETED
- Objective: Implement the approved IAM model and SQL-backed persistence boundary.
- Scope: Domain entities/enums/invariants, EF Core mappings, DbContext, repositories, transactions, and migration/constraint integration tests.
- Requirements covered: All 16 approved tables and forward-only DbUp ownership.
- Files/components: Identity.Domain, Identity.Application persistence ports, Identity.Infrastructure, Identity.Database, tests.
- Dependencies: T001.
- Risks/assumptions: SQL integration tests require an accessible SQL Server connection.

## Implementation Steps

Implement protected domain state, explicit EF mapping to the approved schema, persistence ports/adapters, and SQL constraint tests.

## Acceptance Criteria

All entities map without EF migrations, relational constraints are exercised, and append-only data cannot be modified.

## Required Tests and Validation

Domain tests, EF model validation, blank-database migration, second-run replay, and SQL integration tests.

## Validation Results

- Implemented all 16 approved schema entities with protected state and explicit SQL Server mappings.
- Added the EF Core context as the persistence repository/unit-of-work boundary; feature-specific query ports remain owned by the application use cases that need them, avoiding a generic repository abstraction over EF Core.
- Verified the DbUp migration against a new SQL Server LocalDB database and replayed it with no pending scripts.
- Verified every mapped table and column against the migrated database.
- Verified normalized application-code uniqueness and the append-only audit trigger against SQL Server.
- `dotnet format Identity.slnx --no-restore`: passed.
- `dotnet build Identity.slnx --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test Identity.slnx --no-build --no-restore`: 7 non-SQL tests passed; 3 SQL-only tests skipped when the connection variable was absent.
- SQL-enabled infrastructure test run: 7 passed, 0 failed, 0 skipped.

## Definition of Done

Implementation and validation pass, records are updated, and the focused T002 commit succeeds.
