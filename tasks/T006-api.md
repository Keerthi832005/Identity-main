# T006: Hardened IAM HTTP API

- Status: COMPLETED
- Objective: Expose the IAM use cases as a production-safe standalone API.
- Scope: Authentication/MFA/refresh/logout endpoints, administration endpoints, bootstrap CLI, policies, errors, rate limits, request bounds, health, OpenAPI, and host tests.
- Requirements covered: IAM can be independently operated and published as an application.
- Files/components: Identity.Api, Identity.AdminCli, Identity.Contracts, API tests.
- Dependencies: T003-T005.
- Risks/assumptions: No self-registration or management UI is required.

## Implementation Steps

Add minimal API groups, named request/response records, policies, middleware, startup validation, secure bootstrap, and end-to-end host tests.

## Acceptance Criteria

Public and administrative routes enforce the intended boundaries and return stable problem details without leaking security data.

## Required Tests and Validation

API authentication/authorization, rate-limit, validation, error, health, OpenAPI, and bootstrap tests.

## Validation Results

- Added typed versioned login, MFA completion, refresh, logout, administration, resource-query, discovery/JWKS, health, readiness, and OpenAPI endpoints.
- Added strict startup validation for the independent SQL connection, HTTPS issuer, RSA signing key, administration audience, 32-byte encryption/HMAC keys, and rate-limit bounds after all configuration providers are assembled.
- Added administration JWT validation with issuer, signature, lifetime, dedicated audience, numeric subject, and `iam.admin` capability enforcement at both HTTP and application authorization boundaries.
- Added data-annotation request validation, positive route-identifier validation, 64 KiB body limits, fixed-window authentication rate limiting, non-cacheable authentication responses, security headers, stable non-sensitive errors, and correlation identifiers.
- Added a one-time bootstrap CLI that preflights cryptographic configuration and an empty migrated database, reads all credentials and keys only from environment variables, creates the complete initial administration chain, and emits resource identifiers only.
- Added operating documentation covering configuration, migration order, bootstrap, endpoints, and security boundaries.
- `dotnet format Identity.slnx --no-restore --verify-no-changes`: passed.
- `dotnet build Identity.slnx --no-restore`: passed with 0 warnings and 0 errors.
- Normal test run: 27 passed, 6 SQL-only tests skipped, 0 failed.
- SQL-enabled run after a real CLI bootstrap on a disposable LocalDB database: 33 passed, 0 skipped, 0 failed; all three migrations replayed with no pending scripts.
- API host tests cover health/readiness, OpenAPI, discovery/JWKS, request validation, success/rejection/MFA mapping, valid and invalid bearer boundaries, administration actor propagation, rate limiting, body limits, security headers, correlation, and non-sensitive errors.
- CLI tests cover safe switch parsing, command-line secret rejection, pre-mutation secret validation, complete bootstrap sequencing, hashed client secrets, and secret-free output. A real bootstrap created one administration application, user, client, capability, role, and role permission in SQL Server.
- API and CLI Release publishes passed. Vulnerable-package, naming, anonymous application-payload, unfinished-work, and production-secret scans passed.

## Definition of Done

Implementation and validation pass, records are updated, and the focused T006 commit succeeds.
