# T005: MFA, devices, and security audit

- Status: COMPLETED
- Objective: Add secure TOTP MFA, trusted-device controls, and append-only typed audit.
- Scope: MFA enrollment/verification/challenges, encrypted secrets, device trust/revocation, keyed identifier hashes, typed audit payloads, and sensitive-data rejection.
- Requirements covered: UserMfaMethod, MfaChallenge, Device, AuthenticationAudit, and user-requested named record payloads.
- Files/components: Identity domain/application/infrastructure security features and tests.
- Dependencies: T002-T004.
- Risks/assumptions: V1 delivers TOTP; external email/SMS delivery is excluded.

## Implementation Steps

Implement secret protection, RFC TOTP verification, bounded challenges/attempts, device lifecycle, audit writer, and regression tests.

## Acceptance Criteria

MFA and device transitions are bounded and auditable; raw credentials, OTPs, tokens, and headers never persist in audit data.

## Required Tests and Validation

TOTP vectors, expiration/replay/attempt tests, device revocation tests, encryption tests, and append-only SQL tests.

## Validation Results

- Added TOTP enrollment and verification with 20-byte random secrets, RFC 6238-compatible SHA-1 codes, six digits, a 30-second period, and a one-step clock window.
- Protected MFA secrets with versioned AES-256-GCM envelopes and deployment-provided key identifiers; plaintext secrets are returned only for initial enrollment and never persisted.
- Added five-minute hashed login challenges with a five-attempt maximum, completion replay rejection, expiry enforcement, and fixed-time keyed challenge verification.
- Added explicit device trust and revocation transitions. Active trusted devices can bypass TOTP; untrusted, unknown, inactive, or revoked devices cannot.
- Added HMAC-SHA256 login-identifier hashes and typed allowlisted audit payload records; credentials, OTPs, client secrets, tokens, and headers are rejected from audit payload serialization.
- Added forward-only migration `0003_mfa_audit_events.sql` for explicit enrollment-verification rejection audit events.
- `dotnet format Identity.slnx --no-restore --verify-no-changes`: passed.
- `dotnet build Identity.slnx --no-restore`: passed with 0 warnings and 0 errors.
- Normal test run: 13 passed, 6 SQL-only tests skipped, 0 failed.
- SQL-enabled run on a disposable LocalDB database: 19 passed, 0 skipped, 0 failed; all three migrations applied to a blank database and replayed with no pending scripts.
- Tests cover RFC TOTP vectors, authenticated encryption and tamper rejection, keyed hashes, typed audit allowlisting, enrollment rejection, challenge attempts/expiry/replay, trusted-device bypass, device revocation, MFA revocation, encrypted database values, identifier hashes, and sensitive audit-data exclusion.
- Existing EF and SQL append-only audit tests remain green.

## Definition of Done

Implementation and validation pass, records are updated, and the focused T005 commit succeeds.
