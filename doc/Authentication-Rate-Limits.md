# Authentication rate limits and retry countdowns

IAM applies independent, per-client-IP, one-minute fixed-window limits. Requests consume a permit before verification, regardless of success. Browser and non-browser aliases use the same budget within each policy; changing client IDs, users or endpoint aliases cannot obtain a fresh budget. Trusted forwarded headers are processed before rate limiting.

| Setting under `Identity:RateLimit` | Default requests/minute/IP | Routes under `/api/v1/auth` |
| --- | --- | --- |
| `AuthenticationPermitLimit` | 10 | `/login`, `/browser/login`, `/mfa/complete`, `/browser/mfa/complete`, `/terminal/verify` |
| `SessionRefreshPermitLimit` | 30 | `/refresh`, `/browser/refresh` |
| `SessionLogoutPermitLimit` | 30 | `/logout`, `/browser/logout` |

All limits must be integers from 1 to 1000. Invalid values fail startup. The existing `AuthenticationPermitLimit` setting remains compatible and retains its default of 10; it is not raised to solve refresh traffic. Refresh and logout cannot consume the credential-verification budget or each other's budget. Password/PIN account lockout and MFA challenge protections are unchanged.

## Configuration

Defaults are explicit in `src/Backend/Identity.Api/appsettings.json`:

```json
{
  "Identity": {
    "RateLimit": {
      "AuthenticationPermitLimit": 10,
      "SessionRefreshPermitLimit": 30,
      "SessionLogoutPermitLimit": 30
    }
  }
}
```

For IIS, override using the IAM API application pool's environment variables if required:

- `Identity__RateLimit__AuthenticationPermitLimit`
- `Identity__RateLimit__SessionRefreshPermitLimit`
- `Identity__RateLimit__SessionLogoutPermitLimit`

These are IAM API settings, not PTS API or frontend settings. Apply approved changes and recycle the IAM pool through the normal deployment process. Never disable rate limiting or account lockout to bypass a failed sign-in. Users behind the same client IP still share each budget; these are process-local limits, not a distributed quota across IIS workers/servers. Configure only actual reverse proxies as trusted, never arbitrary forwarded addresses.

## HTTP contract

A rejected request returns HTTP 429 with the existing typed `ApiErrorResponse` (`code: rate_limit_exceeded`), correlation ID, `Cache-Control: no-store`, and `Retry-After` as whole seconds, rounded up with a minimum of one second. The server uses its rejected lease's retry metadata; if unavailable, it returns 60 seconds. The public same-origin IIS proxy must preserve this header. Do not automatically replay passwords or MFA codes.

## IAM and PTS sign-in behavior

Both credential and MFA forms display an accessible live countdown after a 429 response. The countdown reads `Retry-After` seconds or an HTTP date, with a 60-second fallback for absent/invalid headers and a one-second minimum for an elapsed date or zero. It uses an absolute deadline so suspended/background-tab timers catch up when resumed.

Submit buttons, Enter-key/programmatic form submits, and PTS development quick-sign-in are guarded while waiting. Duplicate in-flight submissions are also blocked. Back navigation within the form does not clear the cooldown. Fields can still be edited. When time expires, the user may submit again manually; no automatic credential or MFA submission occurs. Timers are destroyed with the component. Cooldowns are in memory only and do not store credentials or share browser storage across tabs; the server remains authoritative after reloads or in another tab.

Session-refresh handling retains its existing single-flight behavior and does not auto-retry a 429. The visible countdown applies to credential/MFA form requests; a refresh-only throttle does not block a fresh login under the separate credential budget.

## Verification and rollout

Regression tests cover budget isolation, all credential routes, browser/API alias sharing, trusted forwarded client IPs, setting validation, retry headers, countdown parsing/expiry/disposal, duplicate submits, MFA back navigation, both themes and no automatic retries. Frontend browser tests use intercepted fixture responses, not real credentials or business database writes.

Deploy the updated IAM API and both frontends to activate the complete change. A frontend-only deploy supplies a fallback countdown but cannot separate the old backend budgets. No database migration is required; **SchemaVersions must not be changed** for this feature.

### Local verification — 2026-08-30

- IAM API: 40 tests passed, including 14 rate-limit regression cases; fake dispatcher and readiness dependencies, no business database calls.
- IAM frontend: 70 unit tests passed. All 13 browser scenarios passed across the suite and a focused rerun; the security-controls test now waits for the protected-route redirect before its unchanged autofill assertions.
- PTS frontend: 85 unit tests passed. All credential/MFA cooldown scenarios passed in light and dark. The session-recovery regression now awaits completed refresh and authenticated UI after reload instead of asserting before the asynchronous request completes.
- TypeScript app/spec checks, scoped formatting and both production frontend builds passed. Builds retain dependency CommonJS warnings; no new dependencies were added.
- The broader PTS browser suite passed 20 of 21 scenarios at the default timeout. The separate two-client live-board scenario exceeded its 30-second total budget, then passed an isolated diagnostic run with `--timeout=60000` (53.3 seconds including server startup). Its assertions and repository timeout were left unchanged; default-timeout suite reliability remains a separate follow-up, so this is not a passing full release gate.

Browser screenshots, diagnostic traces and build outputs are local ignored artifacts under `artifacts/rate-limit-verification` at the repository root. Live IIS, accounts, databases and SAP were not changed during this verification. Publish the IAM API and both frontends through the normal approved IIS deployment workflow before checking the live URLs.
