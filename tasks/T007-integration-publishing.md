# T007: PTS integration and independent publishing

- Status: COMPLETED
- Objective: Prove PTS trusts IAM-issued tokens while both applications remain independently buildable and publishable.
- Scope: Standards-compatible issuer metadata, actual-token consumer integration tests, operations documentation, publish automation, and final independent-solution checks. PTS source remained read-only because it is owned by a separate active task.
- Requirements covered: Separate deployment with standards-based JWT/JWKS integration only.
- Files/components: Identity API/contracts, IAM integration tests, IAM operations and consumer documentation, standalone publish script.
- Dependencies: T006.
- Risks/assumptions: Production hostnames, certificates, and deployment are user-owned inputs.

## Implementation Steps

Expose standards-compatible metadata, align the documented capability contract, add cross-host token validation tests, document database/API/admin CLI operation, and verify independent publish outputs.

## Acceptance Criteria

An IAM token authorizes a consumer policy configured with the PTS audience and capability contract, denied/expired tokens fail, neither production solution references the other, and IAM publish artifacts succeed.

## Required Tests and Validation

Both solution quality gates, cross-application tests, package/security audit, publish checks, and unfinished-work scan.

## Validation Results

- Added `/.well-known/openid-configuration` with the issuer, JWKS URI, and RS256 metadata required by ASP.NET Core JWT bearer `Authority` configuration while retaining the extended IAM discovery endpoint.
- Added a black-box consumer host test that obtains metadata and public keys over HTTP and validates an access token produced by IAM's production RS256 issuer. The accepted path requires the `pts-api` audience, `pts.api` capability, and security/authorization version claims.
- Verified missing capability returns 403 and wrong-audience and expired tokens return 401. The test uses no consumer-to-IAM project reference and no private key sharing.
- Documented the consumer contract, `capability` claim mapping, issuer/`AllowedHosts` requirement, offline revocation boundary, and safe signing-key rotation.
- Added a repository-owned publish script that creates separate Release outputs for the API, database runner, and administration CLI.
- `dotnet format Identity.slnx --no-restore --verify-no-changes`: passed.
- `dotnet build Identity.slnx --no-restore`: passed with 0 warnings and 0 errors.
- IAM normal test run: 29 passed, 6 SQL-only tests skipped, 0 failed.
- IAM SQL-enabled run on a fresh disposable LocalDB database: 35 passed, 0 skipped, 0 failed; 3 migrations applied, replay reported no pending scripts, all 16 Identity tables were present, and the database was removed.
- `scripts/Publish-Identity.ps1` produced and verified separate `api`, `database`, and `admin-cli` Release artifacts under a temporary output root.
- PTS read-only compatibility gate: format and build passed with 0 warnings and 0 errors; 68 tests passed, 16 SQL-only tests skipped, and 0 failed.
- IAM and PTS vulnerable-package audits reported no known vulnerabilities. Project-reference, naming, anonymous application-payload, unfinished-work, secret-literal, and whitespace scans passed for the task-owned scope.
- Production projects in IAM and PTS have no cross-solution project reference. PTS T012 is dependency-ready without a T007 change under `PTS/`.

## Definition of Done

Implementation and validation pass, records are updated, and the focused T007 commit succeeds; PTS T012 becomes dependency-ready.
