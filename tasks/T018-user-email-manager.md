# T018: User contact email and reporting manager

- Status: COMPLETED AND VERIFIED for the implementation scope; business-database rollout and requested seed account remain pending.
- Scope: nullable contact email and self-referencing manager on IAM users, forward-only SQL migration, domain validation, EF mapping, typed API/CLI support, profile UI, tests, and documentation.
- Dependencies: T003, T012, T017.
- No changes to authentication identifiers, roles, password handling, license configuration, or PTS files.

## Acceptance criteria

1. Existing users and callers remain compatible; both new fields default to null.
2. Create/read/search/update supports email and manager; clearing either is supported.
3. Missing/inactive managers, self-assignment, descendant loops, and concurrent opposite assignments are rejected.
4. Changes are authorized and audited without changing role grants/security versions.
5. SQL constraints preserve referential integrity with no cascading delete.
6. No existing `SchemaVersions` rows are touched; test fixtures never enter the cleared business databases.

## Validation

- IAM build: passed, zero warnings/errors.
- Full IAM backend: 80 tests passed, none skipped, including SQL-backed profile persistence, email search, no-op replay, audit, invalid relationships, direct SQL constraints, and concurrent cycle prevention.
- Migrations 0001–0009: applied successfully to an isolated test database; replay and deliberate migration-failure rollback/retry passed.
- UI production build: passed; existing DevExtreme transitive CommonJS optimization warnings remain.
- UI unit tests: 23 passed, including rendered email/manager controls, profile requests, rejection handling, and a delayed-selection regression. The test runner now resolves DevExtreme through Vite, avoiding unsupported Node directory imports; real widgets are not replaced by mocks.
- Browser suite: five tests passed, including profile create/edit/clear/error flows in both themes, desktop/mobile layout, and Confirm-button text contrast. Browser API responses are fixtures; SQL persistence is verified separately by the backend suite.
- Full pre-commit evidence and limitations: [T018–T019 verification](../doc/T018-T019-Verification.md).

## Release boundary

No live migration or account seed is included in this task. Review [User Email and Manager](../doc/User-Email-And-Manager.md) before applying migration 0008. Initial-administrator role approval, display name, and secure password setup are still required for employee `INDE03275`.
