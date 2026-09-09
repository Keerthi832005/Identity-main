# T012: Users, application access, roles, and overrides

- Status: COMPLETED
- Objective: Deliver user access administration with visible effective-capability evaluation.
- Scope: User discovery/detail/create/revoke, application grants, role assignment, role permissions, Allow/Deny user overrides, expiry/reason, and effective capabilities.
- Requirements covered: RolePermission plus additional per-user Allow/Deny override support with Deny precedence.
- Files/components: IAM read APIs/tests and frontend user-access feature.
- Dependencies: T011.
- Risks/assumptions: Every mutation must show the resulting authorization version and require explicit confirmation for access removal or Deny overrides.

## Implementation Steps

1. Add user/access/role read models.
2. Build user and application-access workflows.
3. Build role matrix and user override workflows.
4. Surface effective capabilities and authorization version.

## Acceptance Criteria

- Administrators can explain each effective capability from role grants and user overrides.
- Active Deny overrides visibly take precedence over grants and Allow overrides.

## Required Tests and Validation

Backend and frontend full quality gates plus permission precedence browser tests.

## Validation Results

- .NET format and isolated build passed with 0 warnings and 0 errors.
- Full isolated .NET suite passed 37 tests with 9 configured-SQL skips; the new read-only discovery integration test also passed against `FIN_IAM` using Windows authentication.
- Angular production build passed.
- `npm test -- --watch=false`: passed 6 files and 13 tests.
- `npm run test:e2e`: passed 2 workflows, including proof that a Deny override removes `iam.audit` even when the assigned role grants it while retaining `iam.admin`.
- Frontend formatting passed and the production dependency audit reports 0 vulnerabilities.

## Definition of Done

Implementation, tests, task records, and focused commit are complete.
