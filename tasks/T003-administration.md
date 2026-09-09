# T003: User and application administration

- Status: COMPLETED
- Objective: Implement IAM management use cases without HTTP concerns.
- Scope: Users, applications, clients, modules, capabilities, user access, roles, role permissions, user overrides, and device administration.
- Requirements covered: Application manager and user manager remain entirely inside IAM.
- Files/components: Identity.Domain, Identity.Application, Identity.Infrastructure, unit/integration tests.
- Dependencies: T002.
- Risks/assumptions: Individual Deny overrides take precedence over Allow and role grants.

## Implementation Steps

Add commands/queries, typed validators/results, authorization-version invalidation, stores, and tests for allowed/rejected transitions.

## Acceptance Criteria

Every approved management relationship can be created, read, disabled/revoked, and audited through typed application use cases.

## Required Tests and Validation

Domain/application tests plus focused SQL-backed administration tests.

## Validation Results

- Added the application-owned typed dispatcher with no MediatR dependency.
- Added protected domain creation and lifecycle operations for applications, clients, modules, capabilities, users, application access, roles, permissions, user overrides, and devices.
- Added typed create/grant/read/status/revoke use cases, an explicit administration authorization port, EF Core store, and transaction runner.
- Added authorization-version invalidation for user role, role permission, user override, role status, and access changes.
- Added effective capability evaluation with explicit user `Deny` precedence over user `Allow` and role grants.
- Added forward-only migration `0002_administration_audit_events.sql` for explicit user, role, and role-permission audit events.
- `dotnet format Identity.slnx --no-restore`: passed.
- `dotnet build Identity.slnx --no-restore`: passed with 0 warnings and 0 errors.
- Normal test run: 10 passed, 4 SQL-only tests skipped, 0 failed.
- SQL-enabled infrastructure run on a disposable LocalDB database: 8 passed, 0 skipped, 0 failed; both migrations replayed with no pending scripts.
- Vulnerable-package audit: no known vulnerabilities in direct or transitive packages.

## Definition of Done

Implementation and validation pass, records are updated, and the focused T003 commit succeeds.
