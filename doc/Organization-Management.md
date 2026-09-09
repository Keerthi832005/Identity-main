# Organization management

The module manages Organization → Country → Region → State → Branch → Location and Organization → Department → Team. Every call requires the existing `iam.admin` capability. No user assignments or inherited permissions are introduced.

## HTTP contract

| Method and route under `/api/v1/admin` | Behavior |
| --- | --- |
| GET `/organization-units` | Optional `organizationId`, named `unitType`, `parentOrganizationUnitId`, `isActive`, `search`, `skip`, `take`. Default 20, maximum 50; search maximum 100 characters. Stable name/ID order and total count. Parent filter requires its organization. |
| GET `/organizations/{organizationId}/units/{unitId}` | Scoped details, address, path, own active state, UTC timestamps, base64 RowVersion. |
| POST `/organizations` | Atomic ownership anchor + root + fields + path + audit. |
| POST `/organizations/{organizationId}/units` | Atomic common node + typed link + path + audit. Named child `unitType` and `parentOrganizationUnitId` required. |
| PUT `/organizations/{organizationId}/units/{unitId}` | Replace `unitCode`, `unitName`, nullable `description`, required `address`, and original `rowVersion`. Null clears optional fields. |
| PUT `/organizations/{organizationId}/units/{unitId}/active` | Required `isActive` and original `rowVersion`. Changes only this node, not descendants. |

Creation accepts `unitCode`, `unitName`, nullable `description` and nullable `address`. Address fields: `addressLine1`/`2`/`3`, `city`, `district`, `stateName`, `postalCode`, `countryCode`, nullable decimal `latitude`/`longitude`. Physical country code is separate from the Country business code; maximum three characters, no ISO reference-list validation. Coordinate bounds are enforced.

Relationships/paths are absent from update contracts; unknown JSON properties are rejected. Types are names, never enum ordinals. Creation returns 201 with a scoped detail Location header.

## Concurrency and errors

- Original eight-byte SQL RowVersion is required as base64. Stale writes return 409 `organization_concurrency_conflict`: reload, review and deliberately retry. Never automatically overwrite concurrent edits.
- Normalized codes are unique across all types within an organization; collision returns 409 `organization_code_conflict`. Separate organizations may reuse codes.
- Unchanged updates with current version are no-ops, without duplicate audit/new version. POST is not automatically retried after ambiguous network failure: search/read to determine whether creation completed.
- 400 invalid input; 401 missing authentication; 403 denied capability; 404 missing unit in requested organization. Missing parent uses existing 409 administration conflict; wrong/inactive/cross-organization parents are rejected. Errors do not expose SQL/secrets.
- Supported writes use transaction-owned organization locks, SQL RowVersion and unique constraints. Update/state audit contains identifiers, type and own state only, never descriptive/address data.

## Release boundaries

Deploy forward-only DbUp migration 0010 (audit allow-list extension), after 0008/0009, before this service. Applied migrations and existing SchemaVersions rows must never be edited. No business-database deployment, fixture or account provisioning is included. Tests use explicit disposable targets only.

Moves, hard deletion, user assignment, permission inheritance, new identity providers, INDE03275 provisioning, push/deploy/publish/merge and reset deletion allow-list expansion are excluded.


## Browsing

Open **Organizations** in IAM navigation. The eight type buttons select their management lists; search matches code/name/description, and Previous/Next page through 20 results at a time. Select a row for address, identity, path and UTC timestamps. **Scope to organization** retains one organization when switching types. **Browse children** lists immediate children, and ancestor links move up the hierarchy. **All organizations** clears scope and filters. Active filters apply to each node's own state, not inherited ancestor state. No complete hierarchy is downloaded.


## Creating and editing

**New organization** creates its anchor/root atomically. For a child, select an active parent and use **New Country**, **New Region**, **New State**, **New Branch**, **New Location**, **New Department** or **New Team**, as offered by that parent's type. Existing type lists and hierarchy browsing locate parents without unbounded selectors. Inactive parents must be activated first.

**Edit details** changes code/name/description and all shared address fields. Blank optional fields clear their values. Organization/type/parent/path never appear as editors. **Deactivate**/**Activate** asks for confirmation and changes only that unit.

A stale edit keeps the draft and disables Save. **Reload latest details** asks before discarding the draft; review the fetched version and save deliberately. Stale state changes offer **Reload changed unit**. Network failures retain the draft; check whether an ambiguous creation succeeded before retrying. Dialogs retain a stable target/version, prevent duplicate submission, contain keyboard focus, and keep title/actions visible on mobile.

## Reproducing connected verification

After the Release backend build, frontend `npm ci`, and Playwright browser installation, run `& ./IAM/scripts/Test-OrganizationBrowser.ps1` from the repository root in PowerShell 7. It requires Windows integrated SQL access to `lpc:HOCOM18502627\SQLEXPRESS2022` and available loopback ports 5107/4303 (overridable). The runner creates a unique disposable database, applies/replays DbUp, provisions a disposable administrator/public client, generates secrets only in process environment, starts the actual API and UI, runs both themes, checks SQL audit/integrity, and verifies its exact target before dropping it in `finally`. It never accepts a business database as a parameter. Do not run the live Playwright config independently against a business API.

See [T020–T024 verification](T020-T024-Verification.md) for exact commands/results, mocked versus actual evidence, reviewed fixes, cleanup records and separate release boundaries.
