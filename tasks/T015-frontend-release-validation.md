# T015: Frontend publishing and end-to-end validation

- Status: COMPLETED
- Objective: Make the IAM UI independently publishable and validate the complete administrator journey.
- Scope: Publish script integration, runtime config replacement, reverse-proxy/cookie-path guidance, production budgets, accessibility/responsive review, full browser tests, dependency/security audit, and final reconciliation.
- Requirements covered: Independently publishable IAM app and complete module-wise validation.
- Files/components: Publish scripts, deployment docs, frontend E2E suite, task records.
- Dependencies: T009-T014.
- Risks/assumptions: TLS and a same-site `/identity` reverse-proxy route are deployment prerequisites for the secure browser refresh cookie.

## Implementation Steps

1. Add repeatable frontend publish output and runtime configuration instructions.
2. Validate all administrator workflows against a migrated disposable database.
3. Complete security, accessibility, responsive, dependency, and release review.

## Acceptance Criteria

- One publish command produces independently deployable API/database/admin CLI/frontend artifacts.
- All automated and documented manual gates pass with no unresolved required blocker.

## Required Tests and Validation

Full .NET and frontend gates, SQL replay, browser E2E, publish verification, dependency audits.

## Validation Results

- The publish entry point produces independent API, database, admin CLI, and static frontend outputs.
- Isolated .NET restore/build/test and Angular production build/unit tests passed.
- Playwright covers sign-in, application catalog, Deny precedence, credential clearing, and both theme modes across responsive shell routes.
- Formatting and production dependency audits passed with 0 known vulnerabilities.
- Deployment runtime configuration, public-client, same-origin reverse proxy, secure-cookie, and DevExtreme license boundaries are documented.

## Definition of Done

Every frontend-extension task is completed and committed, final validation passes, and the IAM task index returns to 100% COMPLETE.
