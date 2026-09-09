# T022: Organization navigation and hierarchy browsing

Verified focused commit: `6b3a7d2`. Final regression evidence: [T020–T024 verification](../doc/T020-T024-Verification.md).

- Status: COMPLETED
- Objective: Organization navigation and hierarchy browsing.
- Scope: Add an IAM navigation entry and management views for all eight types with bounded searchable paged lists, detail/address/audit display, organization/type/active filters, parent breadcrumbs and child browsing. Preserve existing screens, themes and licenses.
- Requirements covered: Approved Organization → Country → Region → State → Branch → Location and Organization → Department → Team; canonical storage, immutable relationships, authorized/audited management, responsive existing UI.
- Files/components: Angular organization feature, routes and shell navigation.
- Dependencies: T021.
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

Frontend production build, unit tests, TypeScript and changed-file Prettier checks.

## Validation Results

- In `IAM/src/Frontend`: `npm run build` passed (documented DevExtreme CommonJS warnings only); `npm test -- --watch=false` passed 30 tests in 11 files, extending baseline 23 by seven.
- `npx tsc --noEmit -p tsconfig.app.json` and `npx tsc --noEmit -p tsconfig.spec.json`: passed.
- `npx prettier --check src/app/features/organization-management src/app/app.routes.ts src/app/shared/shell/shell.component.ts`: passed; `git diff --check`: passed.
- Tests render actual DevExtreme search controls and eight type selectors; verify organization/parent scope, stable page counts, false active filter, search errors and superseded list/detail responses.
- Review: all lists are server-bounded at 20 per page; ancestors are fetched from the fixed hierarchy path; no full tree or unbounded selector is downloaded. Existing profile and license files unchanged.
- Browser visual/write flows remain the explicit T023/T024 dependency, not claimed as already exercised here. No new production dependencies.
- Focused commit recorded by next task/final ledger.

## Definition of Done

Implementation and applicable checks pass; review and documentation are complete; task/index updated; focused Git commit created and verified. No required placeholder or silently skipped check remains.

- Dependency commit verified: T021 `64a6c9b`.
