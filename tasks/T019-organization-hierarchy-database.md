# T019: Organization hierarchy database module

- Status: COMPLETED AND VERIFIED for the implementation scope. No business-database rollout performed.
- Scope: nine normalized IAM tables, eight typed read views, authorized transactional creation commands, domain entities, EF mappings, SQL field descriptions, and tests.
- Requirements: Organization → Country → Region → State → Branch → Location; Organization → Department → Team; strict same-organization/type/parent FKs; scoped codes; single-root uniqueness; canonical paths; active state and concurrency.
- Standards: existing Identity schema and DbUp runner, PascalCase and Pk/Fk/Uq/Ck/Df/Ix names, sealed domain types, typed records, UTC DATETIME2(3), SQL ROWVERSION, no new dependencies or EF migrations.
- Design: common code/name/address/audit fields live in OrganizationUnit only; typed tables share the unit key, and typed views expose requested aliases.
- Boundaries: no organization API/UI, data seeding, permissions, cross-module redesign, or expansion of reset-script deletion scope.

## Acceptance and verification

1. No existing organization/address table is duplicated; no existing migration is edited.
2. SQL creates tables in FK dependency order and enforces wrong-type/cross-organization/parent mismatch rejection.
3. Duplicate roots and normalized business codes fail; Department remains directly under Organization.
4. Hierarchy paths are initialized by the application inside the creation transaction; no trigger or recursion limit exists and no move workflow is exposed.
5. Stale RowVersion writes fail, soft-deactivation does not delete children, and no cascading FK deletes exist.
6. Shared aliases and field-level MS_Description metadata match the design guide.
7. Full IAM build/tests, new database migration/replay, frontend regression tests, and formatting pass.

## Evidence

- Fresh migration 0001–0009 applied successfully to an isolated SQL Server database, including field descriptions and no hierarchy trigger.
- Eight SQL-backed organization integration tests passed: hierarchy/views, isolation/types, roots/codes, typed parent consistency, paths/delete/coordinates, RowVersion, atomic root/audit rollback, and authorized/audited path-complete creation.
- Full IAM backend suite: 80 tests passed, none skipped.
- Original organization-wide code uniqueness retained: `(OrganizationId, NormalizedUnitCode)`. Type-scoped reuse is not silently introduced; a business-rule change needs explicit approval before rollout.
- All 60 organization columns have SQL descriptions; constraints are enabled/trusted, eight views exist, and no hierarchy triggers or cascading deletes exist.
- DbUp now uses a transaction per migration, including its history entry. A deliberate 0009 collision rolled back all partial schema, retained eight completed history rows, then successfully retried; final replay retained nine history rows.
- Formatting, frontend production build, 23 UI unit tests, and five browser tests passed. See [T018–T019 verification](../doc/T018-T019-Verification.md) for final evidence and release boundaries.

See [Organization Module Database](../doc/Organization-Module-Database.md) for complete field descriptions, normalized storage decisions, transactional provisioning requirements, read-only review queries, and rollout safeguards.
