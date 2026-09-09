# T016: IAM administration login and licensed UI repair

- Status: COMPLETED
- Priority: P0
- Objective: Restore real IAM administrator login and remove licensed-UI and missing-icon defects.
- Scope: Stable administration web client provisioning, existing-user administrator assignment, PTS DevExtreme license reuse, icon-font packaging, and live verification.
- Requirements covered: Independently deployable IAM administrator access, licensed DevExtreme runtime, light/dark presentation.
- Dependencies: T015.

## Root Cause

`PTS-ADMIN` and its password were valid, but `identity-admin-web` was absent from the stable administration application, causing `InvalidClient`. IAM also did not initialize the approved PTS DevExtreme license and copied theme CSS without its referenced icon-font assets.

## Implementation

- Added an idempotent, audited `provision-administration-web` CLI command for an existing active account.
- Provisioned the stable `iam-administration` application, public client, `iam.admin` capability, administrator role, application access, and role assignment.
- Initialized IAM with the existing approved PTS DevExtreme license.
- Packaged Fluent DevExtreme icon fonts beside the dynamically loaded theme stylesheets.

## Validation Results

- Real `identity-admin-web` browser login returned HTTP 200 for `PTS-ADMIN`; an access token was issued and no refresh token was returned in the response body.
- Provisioning replay reported no new application access or role assignment.
- Browser verification found no evaluation notice, both `DXIcons` and `DXIconsFluent` loaded, and light/dark themes rendered correctly.
- .NET: 38 passed, 10 environment-dependent SQL tests skipped, 0 failed.
- Angular: 16 unit tests passed; production build passed.
- Playwright: 3 browser tests passed.

## Definition of Done

Real administrator login, licensed UI rendering, icon assets, both themes, automated tests, documentation, and a focused commit are complete.
