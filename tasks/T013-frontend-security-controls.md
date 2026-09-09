# T013: Credentials, devices, and MFA administration

- Status: COMPLETED
- Objective: Deliver safe account-security administration without exposing protected material.
- Scope: Set password/PIN, enroll/verify/revoke TOTP, register/trust/revoke devices, security version state, validation, and time-bounded enrollment display.
- Requirements covered: Existing IAM credential, device, and MFA capabilities.
- Files/components: IAM security read APIs/tests and frontend security feature.
- Dependencies: T012.
- Risks/assumptions: Passwords, PINs, TOTP codes/secrets, and fingerprints are never logged or persisted; enrollment secret is visible only during the active setup flow.

## Implementation Steps

1. Add non-sensitive device/MFA/account security summaries.
2. Implement credential and device workflows.
3. Implement TOTP enrollment/verification/revocation with safe transient state.
4. Add tests and security review.

## Acceptance Criteria

- All existing security commands are usable through confirmation-driven forms.
- Sensitive inputs are cleared immediately after success, cancellation, or failure.

## Required Tests and Validation

Backend/frontend gates, security-focused unit tests, browser tests, dependency audit.

## Validation Results

- .NET format and isolated restore/build passed with 0 warnings and 0 errors.
- Full backend suite passed 37 tests with 10 configured-environment skips.
- Angular production build passed; 7 unit-test files passed 15 tests.
- Playwright passed all 3 workflows, including credential replacement and immediate sensitive-field clearing.
- Frontend formatting and production dependency audit passed with 0 vulnerabilities.

## Definition of Done

Implementation, tests, task records, and focused commit are complete.
