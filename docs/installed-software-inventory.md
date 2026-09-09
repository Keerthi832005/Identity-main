# Installed software inventory

The Machines & agents software list uses **DevExtreme DataGrid** with IAM's shared `appGridPreset`. It supports global search across all reported rows, column filter rows, searchable header filters, the filter builder/panel, sorting, column selection and horizontal scrolling. Paging defaults to 25 rows; users can choose 10, 25, 50 or 100. Clear software filters resets the filters and returns to the first page. Selecting another machine starts a fresh grid.

## Reported fields

| Column | Signed inventory field | Source / meaning |
| --- | --- | --- |
| Application | `name` | Machine-wide uninstall registration `DisplayName` |
| Version | `version` | `DisplayVersion` |
| Publisher | `publisher` | `Publisher` |
| Installed / serviced date | `installedOn` | Strictly parsed `InstallDate` (`yyyyMMdd`), transported as nullable date-only `yyyy-MM-dd` |
| Estimated size (MiB) | `estimatedSizeBytes` | Registry `EstimatedSize` DWORD in KiB, converted to bytes and displayed numerically in MiB |
| Registry view | `registryView` | `32-bit` or `64-bit` registration source; not a guarantee of application architecture |

Windows does not supply a time-of-day in this uninstall `InstallDate` value. For MSI applications it can represent the most recent patch or repair rather than the original installation date. No midnight/UTC timestamp, filesystem timestamp, or inventory capture timestamp is substituted for the missing installation time. The separate **Inventory captured** field remains the report's actual date/time. See [Microsoft's uninstall registry documentation](https://learn.microsoft.com/en-us/windows/win32/msi/uninstall-registry-key).

Missing or malformed installation dates and unsupported size values remain null. The grid displays **Not reported**, never an invented date or zero size. A reported zero size remains numeric zero. Per-user and portable applications remain outside the machine-wide collector's scope; no `Win32_Product` query or installer repair is triggered.

## Compatibility and rollout

The three metadata fields are optional, so historical JSON and older agent reports still deserialize. Existing signature, freshness, inventory-count and payload-size guards remain in place; the API also validates the optional size and registry-view values. The existing `AgentInstallation.InventoryJson` persistence carries these fields without a new SQL migration. `dbo.SchemaVersions` is not modified by this feature.

These source changes do **not** update the installed Windows service or IIS automatically. Deploy the new IAM API/UI and publish/activate an agent build containing the collector changes. New values appear after that agent sends a successful signed inventory report (normally every 30 minutes). Older agents and earlier reports show **Not reported** for the new fields. Never fabricate or backfill installation dates from the report timestamp.

## Verification

- Agent tests: 21 passed; one real updater integration test skipped by its existing configuration. Coverage includes strict date parsing, leap days, malformed/missing values, unsigned DWORD conversion, signed report round-trip, metadata validation and legacy JSON.
- IAM UI: 180 unit tests passed; production build passed.
- IAM API: 77 tests passed.
- Existing agent diagnostics browser tests: light/dark passed.
- New software-grid browser tests: light/dark passed using 62 fixture records. Covered multi-page navigation, global search finding a record beyond the current page, publisher column filtering, clear filters, changing page size, empty results, old reports, machine switching and 390px layout. No page errors; screenshots reviewed.
- All 9 SQL-backed agent inventory/control/model tests passed against a generated disposable database, including metadata persistence and retrieval. All 14 migrations and migration replay passed; the temporary database was removed. The live IAM database was not used for these tests.

Browser fixtures intercept IAM requests and do not create accounts, enroll devices, modify live inventory or submit credentials. Generated evidence is under `IAM/artifacts/software-grid-tests/` and is not source-controlled.
