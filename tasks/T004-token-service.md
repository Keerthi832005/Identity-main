# T004: Authentication and token service

- Status: COMPLETED
- Objective: Issue and rotate secure IAM tokens for registered applications.
- Scope: Client authentication, password hashing, login, asymmetric JWT signing, discovery/JWKS, capability claims, refresh rotation/reuse detection, logout, and version invalidation.
- Requirements covered: Separately hosted first-party token service trusted by PTS.
- Files/components: Identity.Application, Identity.Infrastructure security, Identity.Contracts, tests.
- Dependencies: T002, T003.
- Risks/assumptions: Production signing/encryption material is deployment-provided and never generated into source control.

## Implementation Steps

Implement fixed-time credential checks, signing-key abstraction, token composition, discovery data, refresh-token family state transitions, and security tests.

## Acceptance Criteria

Valid clients/users receive bounded tokens; invalid, expired, replayed, disabled, or version-stale credentials are rejected without secret leakage.

## Required Tests and Validation

Cryptographic unit tests, login/token tests, rotation/reuse tests, claim-policy tests, and configuration validation.

## Validation Results

- Added PBKDF2-SHA256 password creation/verification with random 32-byte salts and fixed-time verification, plus password rotation and `SecurityVersion` invalidation.
- Added fixed-time SHA-256 client-secret verification and cryptographically random opaque 256-bit refresh tokens stored only as hashes.
- Added typed login, refresh, logout, password, and signing-metadata requests through the application-owned dispatcher.
- Added RS256 access-token issuance with deployment-provided RSA keys, bounded lifetimes, application/client/version claims, repeated capability claims, and public JWK metadata.
- Added refresh-token rotation, replacement linkage, expiry rejection, logout revocation, reuse-family revocation, and security/authorization version rejection.
- Added forward-compatible discovery metadata; HTTP discovery and JWKS routes remain owned by T006.
- `dotnet format Identity.slnx --no-restore`: passed.
- `dotnet build Identity.slnx --no-restore`: passed with 0 warnings and 0 errors.
- Normal test run: 12 passed, 5 SQL-only tests skipped, 0 failed.
- SQL-enabled infrastructure run on a disposable LocalDB database: 11 passed, 0 skipped, 0 failed; migrations replayed with no pending scripts.
- Tests cover password and secret hashing, invalid configuration, invalid clients/passwords, signed capability claims, refresh rotation/reuse, password and authorization version invalidation, logout, expiry, disabled users, and JWK metadata.
- Vulnerable-package audit: no known vulnerabilities in direct or transitive packages.
- IAM naming, application anonymous-payload, and production private-key/secret scans: passed.

## Definition of Done

Implementation and validation pass, records are updated, and the focused T004 commit succeeds.
