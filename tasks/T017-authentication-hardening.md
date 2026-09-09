# T017: Authentication lockout, trusted-device expiry, and TOTP replay hardening

- Status: COMPLETED
- Objective: Apply the existing account lockout policy consistently to password and MFA login, make device trust time-bound, and reject replayed TOTP time steps.
- Scope: Password login, MFA completion, trusted-device evaluation, persisted trust expiry, persisted accepted TOTP counter, migrations, API/domain contracts, and regression tests.
- Requirements covered: Password failures cannot mint unlimited MFA challenges; locked accounts cannot complete pre-existing challenges; trusted devices stop bypassing MFA after expiry; an accepted TOTP step cannot be reused.
- Files/components: IAM domain entities, authentication/MFA handlers, persistence model, DbUp migrations, API responses, configuration, and IAM tests.
- Dependencies: T016.
- Risks/assumptions: Existing trusted devices become untrusted until explicitly trusted with the new expiry; lockout responses remain enumeration-safe at the public API boundary.

## Implementation Steps

Centralize the authentication security policy, enforce lockout in password and MFA flows, persist trust expiry and accepted TOTP steps, update migrations/contracts, and add concurrency-safe regression coverage.

## Acceptance Criteria

Five invalid password or MFA attempts lock the user, no new or existing login flow succeeds during lockout, successful full authentication resets failures, trust expires at the configured boundary, and a TOTP step succeeds at most once per method.

## Required Tests and Validation

Domain tests, application authentication/MFA tests, API tests, SQL migration/model tests, format, build, full IAM backend tests, and migration replay.

## Validation Results

- Password login now checks `LockoutEndAt`, records failed password verification, and clears the shared counter only after the full login ceremony issues a session.
- MFA completion checks account lockout before accepting a challenge, records failures across newly minted challenges, and resets the counter only after successful session issuance.
- Device trust is valid for the policy's 30-day duration and every trust decision persists `TrustedUntil`; terminal and browser trust checks use the same evaluated-time rule.
- Accepted TOTP time steps persist in `LastAcceptedTimeStep`, replay is rejected across fresh challenges, and the property is an EF concurrency token to prevent parallel double acceptance.
- Migration `0007_authentication_hardening.sql` applied to `FIN_IAM` and replay reported no pending scripts.
- `dotnet build IAM/Identity.slnx -c Release --no-restore` passed with zero warnings/errors.
- SQL-backed focused authentication/MFA tests passed (6); the full IAM backend suite passed (48).
- IAM frontend tests passed (16) and its production build completed successfully.
- Two pre-existing discovery tests were made repeatable against the shared persistent database by using unique fixture identifiers and monotonic count assertions.

## Definition of Done

Implementation, applicable tests and quality checks, task/index update, and a focused Git commit all succeed.
