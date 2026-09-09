# T009: IAM frontend foundation and browser authentication

- Status: COMPLETED
- Objective: Deliver an independently deployable Angular administration shell with secure IAM browser-session lifecycle and light/dark themes.
- Scope: Angular workspace, runtime configuration, responsive shell, sign-in/MFA, in-memory access token state, refresh-cookie recovery, logout, bearer interceptor, route/capability guards, dashboard landing, tests, and documentation.
- Requirements covered: Same Angular/DevExtreme shell language as the consuming app; auth-first delivery; typed payloads; no browser persistence of credentials or tokens; approved DevExtreme exception; free/open-source remaining dependencies.
- Files/components: `src/Frontend`, IAM README and operations documentation.
- Dependencies: T006 browser authentication endpoints and an IAM public administration client carrying `iam.admin`.
- Risks/assumptions: The deployed host proxies `/identity` to `Identity.Api` so the strict refresh cookie path remains first-party. Access tokens remain memory-only and are recovered through the HttpOnly refresh cookie.

## Implementation Steps

1. Create the Angular/DevExtreme workspace and runtime configuration contract.
2. Implement typed authentication state/service, refresh recovery, interceptor, and route guards.
3. Implement responsive accessible sign-in/MFA and administration shell with light/dark/system themes.
4. Add unit tests, build validation, dependency audit, and deployment documentation.

## Acceptance Criteria

- The app builds as an independent static artifact from `IAM/src/Frontend`.
- Login, MFA, refresh recovery, bearer attachment, logout, and unauthorized handling use typed models.
- Access tokens never enter local/session storage and browser responses reject exposed refresh tokens.
- Unauthorized users reach login; authenticated users without `iam.admin` reach access denied.
- Theme selection supports light, dark, and system and persists only the theme preference.
- Shell and authentication views are keyboard-accessible and responsive.

## Required Tests and Validation

- `npm run format:check`
- `npm run build`
- `npm test -- --watch=false`
- `npm audit --omit=dev`
- Manual review of generated output and task-owned diff.

## Validation Results

- `npm run format:check`: passed; all frontend source/configuration files match Prettier formatting.
- `npm run build`: passed; production output generated at `dist/IdentityAdministration.Web` with a 294.03 kB initial bundle and no build warnings.
- `npm test -- --watch=false`: passed; 3 test files and 7 tests cover runtime configuration, JWT-derived state, browser login, refresh concurrency, and revoked-session handling.
- `npm audit --omit=dev`: passed; 0 production dependency vulnerabilities.
- `rg -n "PTS|Pts|pts" src public package.json angular.json`: no redundant product-prefix identifiers; matches were ordinary words such as `scripts`, `accepts`, and `access` only.
- `git diff --check -- IAM`: passed with no whitespace errors.

## Definition of Done

Implementation, tests, quality checks, task records, documentation, and a focused IAM-only Git commit are complete.
