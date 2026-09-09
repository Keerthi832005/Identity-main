# Default administrator and UAT login routing

## Changes

- Added the requested email to roster employee `INDE03275` and an explicit,
  repeatable initial IAM administrator seed. Existing credentials and revoked
  access are preserved. No default password is embedded.
- Added a frontend-only IIS routing template and installed it on the existing
  UAT root, which had no `web.config`. This fixes direct `/login` navigation.
  No Live files, API settings or unrelated agent changes were deployed.

## Verification

- Admin CLI tests: 22 passed, 4 SQL-dependent tests skipped in the ordinary
  run. Administrator and roster SQL groups were then run separately against
  migrated disposable databases: 5 and 10 passed, respectively. Tests verify
  real administrator login, transactional rollback, repeatability and refusal
  to restore revoked access. Both databases were removed afterward.
- Verification used an isolated archive of commit `7271ece` plus this task's
  files because another agent was changing shared authentication code.
  Scoped C# whitespace verification passed.
- UAT seed preview returned `would-create` for `INDE03275`, the requested
  email and `identity-administrator`; `Applied` and `Created` were false.
- UAT `/`, `/login?returnUrl=%2F` and `/users` returned the frontend with HTTP
  200. The browser displayed the IAM sign-in form without JavaScript errors.
  Missing assets and service routes remained 404; the API settings file under
  `App_Data` was not served.

## Remaining UAT setup

The database schema is initialized, but this task has not created the UAT
administrator. Apply requires the protected initial password, client secret
and matching UAT runtime key configuration. `/identity/health/ready` remains
404 because API hosting/routing is not yet configured. The login-page fix
does not mean authenticated sign-in is ready.

Instructions: [Administrator seed](../doc/Default-Administrator-Seed.md) and
[frontend-only routing](../deploy/iis/frontend-only.md).
