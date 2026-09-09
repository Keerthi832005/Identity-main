# Bulk import workbooks — DMS_V2 → IAM

Upload-ready `.xlsx` files for the **IMPORT** button on the IAM administration screens. They follow
the template contract in [`doc/IAM-Bulk-Data.md`](../../doc/IAM-Bulk-Data.md): sheets `Data`,
`Reference` and hidden `_meta`, instruction band on row 1, headers on row 2, data from row 3.

| File | Entity | Screen | Rows |
| --- | --- | --- | --- |
| `organization-units.xlsx` | `organization-units` | Administration → Organizations | 102 |
| `users.xlsx` | `users` | Administration → Users & access | 1,032 |

Upload **organization units first**, then users.

## Regenerating

```powershell
cd IAM/data/bulk-import
python build_bulk_workbooks.py          # reads DMS_V2 via Windows auth, rewrites both .xlsx
python verify_bulk_workbooks.py         # re-runs the server's staging rules, exit 0 = clean
```

`--server` / `--database` (or `DMS_SERVER` / `DMS_DATABASE`) override the source; defaults are
`HOCOM18502627\SQLEXPRESS2022` and `DMS_V2`. `--org-code` / `--org-name` name the root unit.

## Organization units mapping

| IAM type | Count | DMS_V2 source | Code |
| --- | --- | --- | --- |
| Organization | 1 | fixed root | `FIPL` |
| Country | 1 | `countries` (the country used by `branch_locations`) | `IND` |
| Region | 4 | `branch_locations.region` | `RGN-<region>` |
| State | 16 | `branch_locations.state` | `ST-<state>` |
| Branch | 20 | `branch_locations.city` | `BR-<city>` |
| Location | 27 | `branch_locations` rows where `available = 1` | `LOC-<location_name>` |
| Department | 7 | `main_departments` where `available = 1` | `DEP-<name>` |
| Team | 26 | `sub_departments` where `available = 1` | `TM-<name>` |

Codes are uppercase slugs of the source name, so they stay readable and stable across regenerations.
The generator aborts if two rows produce the same code.

Regions and states are only present as `varchar` columns on `branch_locations`, so the two levels are
derived from the distinct values there rather than from lookup tables — DMS_V2 has none.

Each `Location` row carries `city`, `state`, `postalCode` and `countryCode`, and records its DMS id
and ERP organization id in the description so a row can be traced back.

## Users mapping

`employees` → one row each. The employee code is **`INDE` + `employee_number`** (`03275` → `INDE03275`),
which is the code IAM already signs people in with: the 164 users seeded from
[`db/seeds/default-employees.json`](../../db/seeds/default-employees.json) all use `IND[CE]#####`, and
`INDE03275` there is DMS `03275`. Leading zeros are preserved as text. `employee_name` is the display
name, `email` the contact email, and the manager column is the prefixed `employee_number` of the row
`employees.manager_id` points at. `--code-prefix` changes the prefix.

9 of the 1,032 codes already exist in IAM, so those rows update rather than create. The 67 `INDC`
contractor codes are a separate, DMS-independent sequence and are left alone.

All 1,032 employees are included, not just the 1,024 active ones: 437 active employees report to one
of the 8 inactive people, and dropping those 8 would break those manager links. The users template has
no active/inactive column — deactivate the 8 on the Users screen after import.

563 of the 1,032 rows carry a manager; the other 469 have no `manager_id` in DMS_V2.

## Seeding

`IMPORT` on each screen is the supported path: Administration → Organizations → IMPORT with
`organization-units.xlsx`, review the staged preview, commit, then the same on
Administration → Users & access with `users.xlsx`. Nothing reaches the identity tables until commit,
and a batch expires after 24 hours if it is not committed.

`Identity.AdminCli seed-default-employees` is not an alternative: it seeds only the bundled 164-row
roster and has no notion of organization units.

## Known source data issues

- `branch_locations.address` holds the region word (`South`, `West`, …), not a street address, so it
  is **not** mapped to `Address line 1`. Street addresses have to be entered in IAM or fixed in DMS_V2
  and the workbook regenerated.
- No latitude/longitude anywhere in DMS_V2; those columns are left blank.
- `district` is left blank — DMS_V2 has no equivalent.
- `branch_locations` ids 22 and 27 do not exist; 24 (`Fujitec Mumbai3 (NVM)`) shares
  `erp_organization_id` 42 with id 15 (`Fujitec Navi Mumbai`). Both become separate Location rows.

## Not covered

`modules`, `capabilities` and `roles` also accept bulk import, but DMS_V2 holds no equivalent data —
its `roles`, `privileges` and `api_controllers` are DMS's own permission model, not IAM's application
catalog. Those have to be authored against the IAM applications first.
