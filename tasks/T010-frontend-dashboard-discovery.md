# T010: Administration dashboard and resource discovery API

- Status: COMPLETED
- Objective: Add bounded read/search APIs and an operational IAM dashboard so administrators can discover resources before changing them.
- Scope: Typed dashboard counts, paged application/user summaries, health indicators, authorization checks, audit coverage, and responsive dashboard UI.
- Requirements covered: Module-wise administration; no client-side database assumptions; read-before-write workflows.
- Files/components: Backend administration queries/contracts/endpoints/tests and `src/Frontend` dashboard/data access.
- Dependencies: T009.
- Risks/assumptions: Count and search queries must remain bounded and must not expose credential, secret, token, or MFA material.

## Implementation Steps

1. Define paged query contracts and authorization-safe read models.
2. Implement persistence queries, endpoints, and tests.
3. Build dashboard cards, health state, search entry points, and empty/error states.

## Acceptance Criteria

- Administrators can see bounded resource totals and navigate from current application/user summaries.
- Read APIs require `iam.admin`, validate paging, and expose no secret-derived values.

## Required Tests and Validation

Backend format/build/tests, frontend format/build/unit tests, SQL integration tests when configured.

## Validation Results

- `dotnet format Identity.slnx --no-restore`: passed.
- Isolated `dotnet restore` and `dotnet build Identity.slnx --no-restore`: passed with 0 warnings and 0 errors while the live IAM host remained untouched.
- Isolated `dotnet test Identity.slnx --no-build`: passed 37 tests; 9 SQL-dependent tests were skipped because `IDENTITY_TEST_SQL_CONNECTION` was not configured. The administration SQL flow now includes dashboard and paged discovery assertions for configured runs.
- `npm run format:check`: passed.
- `npm run build`: passed; production output generated successfully with no warnings.
- `npm test -- --watch=false`: passed 4 test files and 9 tests.
- `npm audit --omit=dev`: passed with 0 production dependency vulnerabilities.
- `git diff --check -- IAM`: passed.

## Definition of Done

Implementation, tests, task records, and focused commit are complete.
