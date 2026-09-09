# T011: Applications, clients, modules, and capabilities

- Status: COMPLETED
- Objective: Deliver complete application-catalog administration workflows.
- Scope: Application list/detail/create/revoke, public/confidential/service clients, module hierarchy, capability catalog, validation, confirmation, and audit visibility.
- Requirements covered: Application.Module and module capability administration; secure secret handling.
- Files/components: IAM read APIs/tests and frontend application-catalog feature.
- Dependencies: T010.
- Risks/assumptions: Client secrets are write-only, displayed only from administrator input, and never returned by IAM.

## Implementation Steps

1. Add typed catalog/detail queries.
2. Build application and client workflows.
3. Build hierarchical module and capability workflows.
4. Add revoke confirmations, tests, and documentation.

## Acceptance Criteria

- Every catalog resource can be discovered and managed through bounded authorized workflows.
- Secret values are never persisted in browser storage or returned by read endpoints.

## Required Tests and Validation

Backend and frontend full quality gates plus browser workflow tests.

## Validation Results

- `dotnet format Identity.slnx --no-restore`: passed.
- Isolated `dotnet restore` and `dotnet build Identity.slnx --no-restore`: passed with 0 warnings and 0 errors.
- Isolated `dotnet test Identity.slnx --no-build`: passed 37 tests; 9 configured-SQL tests skipped because no test connection was supplied.
- `npm run format:check`, `npm run build`, and `npm audit --omit=dev`: passed; production dependency vulnerabilities: 0.
- `npm test -- --watch=false`: passed 5 test files and 11 tests.
- `npm run test:e2e`: passed the sign-in, catalog discovery, and typed public-client creation workflow. Local validation emitted the expected DevExtreme license warning because the committed runtime key is intentionally empty; deployments provide the approved license through `config.json`.
- Catalog API contract verification confirms no client secret hash field is serialized.

## Definition of Done

Implementation, tests, task records, and focused commit are complete.
