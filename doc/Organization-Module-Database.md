# Organization module database design

## Scope and existing conventions

The existing IAM database contained 16 business tables and no organization, geography, address, or department entities. This extension stays in the existing `Identity` schema, `Identity.Database` DbUp migration project, and `IdentityDbContext`; it does not add a database, solution, migration framework, dependency, or employee-directory copy.

Migration [`0009_organization_hierarchy.sql`](../db/migrations/0009_organization_hierarchy.sql) adds nine tables: `Organization`, `OrganizationUnit`, `Country`, `Region`, `State`, `Branch`, `Location`, `Department`, and `Team`. The existing employee email/manager extension remains separate in migration 0008. No user-to-organization assignment or organizational authorization is implied by this database-only task.

```text
Organization (ownership anchor + one root OrganizationUnit)
├── Country → Region → State → Branch → Location
└── Department → Team
```

## Avoiding duplicate fields

`OrganizationUnit` owns each node's code, name, description, address, active state, timestamps, and `RowVersion` exactly once. Type-specific tables contain only the shared key, organization ownership, fixed discriminator, and required typed parent key. A `CountryId`, for example, is the same value as its `OrganizationUnitId`, not an independent generated identity.

Eight read views (`OrganizationDetails`, `CountryDetails`, `RegionDetails`, `StateDetails`, `BranchDetails`, `LocationDetails`, `DepartmentDetails`, `TeamDetails`) expose the requested type-specific names without duplicated storage. For example, `CountryDetails.CountryCode`/`CountryName` project `UnitCode`/`UnitName`. Branch and location views project the shared address fields; every view projects the unit's active/audit/concurrency fields. EF entities access these shared values through their `Unit` navigation.

`Organization` is deliberately a minimal ownership anchor. Its business code, display name, and active state are on its root unit. `OrganizationCommandHandler` implements the approved creation workflow through the existing typed dispatcher: `CreateOrganizationCommand` authorizes the caller and creates the anchor, root, canonical path, and audit event in **one transaction**. A failed root or audit insert rolls back the anchor as well. `CreateOrganizationUnitCommand` similarly commits a common unit, typed link, path, and audit together, and rejects attempts to create a second root through the child command.

The filtered unique index prevents two roots. The **service workflow**, not an impossible immediate circular FK, guarantees that every organization it creates has its root. Direct SQL/DbContext writes can bypass workflow completeness and are not supported provisioning paths. `OrganizationDetails` lists complete organizations only; the review query below detects orphan anchors from unsupported direct writes. No public organization CRUD endpoint is exposed by this change.

## Organization field dictionary

| Field | Storage | Description |
| --- | --- | --- |
| `OrganizationId` | `BIGINT IDENTITY(1,1)` | Unique ownership boundary used by every unit and typed node. |
| `CreatedAt` | `DATETIME2(3)` | UTC time the ownership anchor was created. |
| `RowVersion` | `ROWVERSION` | SQL-generated anchor concurrency token, not a date. |
| `OrganizationCode`, `OrganizationName`, `IsActive` | `OrganizationDetails` view | Canonical root-unit code, name, and active state; not duplicate columns on the anchor. |

## OrganizationUnit field dictionary

| Field | SQL type / rule | Purpose |
| --- | --- | --- |
| `OrganizationUnitId` | `BIGINT IDENTITY(1,1)` | Unique identifier for a hierarchy node. |
| `OrganizationId` | `BIGINT NOT NULL` | Owning organization; must match every parent and typed relationship. |
| `ParentOrganizationUnitId` | `BIGINT NULL` | Parent node; null is allowed only for root type `Organization`. |
| `HierarchyPath` | `NVARCHAR(450) NULL` | Application-managed slash-delimited ID path, e.g. `/1/2/3/`; null only while the controlled creation transaction obtains the generated ID. |
| `UnitType` | `NVARCHAR(20)` | Organization, Country, Region, State, Branch, Location, Department, or Team. |
| `UnitCode` | `NVARCHAR(50)` | Business code, unique across all types within the organization after trim/uppercase normalization. |
| `UnitName` | `NVARCHAR(200)` | Display/business name. |
| `Description` | `NVARCHAR(500) NULL` | Unit purpose, business function, or department/team responsibilities. |
| `AddressLine1` | `NVARCHAR(250) NULL` | Primary physical address. |
| `AddressLine2` | `NVARCHAR(250) NULL` | Additional address information. |
| `AddressLine3` | `NVARCHAR(250) NULL` | Further address information. |
| `City` | `NVARCHAR(100) NULL` | Physical-address city. |
| `District` | `NVARCHAR(100) NULL` | Physical-address district. |
| `StateName` | `NVARCHAR(100) NULL` | Physical-address province/state text; not the business State node's name. |
| `PostalCode` | `NVARCHAR(20) NULL` | Postal/ZIP code. |
| `CountryCode` | `NVARCHAR(3) NULL` | ISO 3166-1 country code used for the physical address (alpha-2 such as `IN` or alpha-3 such as `IND`); distinct from the Country business `UnitCode`. No reference-list membership validation is currently performed. |
| `Latitude` | `DECIMAL(9,6) NULL`, -90…90 | Latitude in decimal degrees. |
| `Longitude` | `DECIMAL(9,6) NULL`, -180…180 | Longitude in decimal degrees. |
| `IsActive` | `BIT`, default 1 | Own active/soft-deactivation state; does not delete or change descendants. |
| `CreatedAt` | `DATETIME2(3)` | UTC creation timestamp. |
| `UpdatedAt` | `DATETIME2(3) NULL` | UTC time of the last application-managed details/state change. |
| `RowVersion` | `ROWVERSION` | SQL-generated optimistic concurrency token for shared details; EF maps it with `IsRowVersion()`. |
| `NormalizedUnitCode` | persisted computed | `UPPER(LTRIM(RTRIM(UnitCode)))`, matching existing business-code uniqueness conventions. |
| `ParentUnitType` | persisted computed | Required parent discriminator derived from `UnitType`; used by the composite parent FK. |

Every physical column also has a SQL Server `MS_Description` extended property in migration 0009.

## Typed table field dictionary

The following rules apply to each of `Country`, `Region`, `State`, `Branch`, `Location`, `Department`, and `Team`:

| Field | Purpose |
| --- | --- |
| `<Type>Id` | `BIGINT` shared primary key referencing the corresponding common node. |
| `OrganizationId` | `BIGINT` ownership ID, part of every composite FK and alternate key. |
| `OrganizationUnitId` | Persisted computed alias of `<Type>Id`; cannot diverge from the shared key. |
| `UnitType` | `NVARCHAR(20)` fixed to the table's type by a default and check constraint; must match the common node. |
| Typed parent key | `BIGINT` required for Region, State, Branch, Location, and Team; must reference the same organization and the same parent as the common tree. |
| `<Type>Code`, `<Type>Name` | Exposed by the corresponding `*Details` view from `UnitCode`/`UnitName`. |
| Description, active/audit fields, `RowVersion` | Exposed by the corresponding view from the shared node; no parallel copies to drift. |
| Branch/Location address fields | Exposed by `BranchDetails`/`LocationDetails` from the shared node. |

| Typed table | Parent key | Required parent type |
| --- | --- | --- |
| Country | Common `ParentOrganizationUnitId` | Organization |
| Region | `CountryId` | Country |
| State | `RegionId` | Region |
| Branch | `StateId` | State |
| Location | `BranchId` | Branch |
| Department | Common `ParentOrganizationUnitId` | Organization, never Branch/State/Location |
| Team | `DepartmentId` | Department |

## Integrity, lifecycle, and concurrency

- Composite FK `(ParentOrganizationUnitId, OrganizationId, ParentUnitType)` targets `(OrganizationUnitId, OrganizationId, UnitType)`. This enforces organization isolation and legal parent-child types. The fixed type ordering also makes cycles impossible.
- Typed FKs validate type, ownership, typed parent, and common-parent agreement. Merely pointing at a same-organization but different parent is rejected.
- `UqOrganizationUnitRoot` permits only one null-parent root per organization. Non-root null parents, self-parenting, unknown types, and normalized duplicate business codes are rejected.
- There is **no hierarchy trigger, empty-string path default, `ParentLinkId`, or recursion cap**. `OrganizationUnit.InitializeHierarchyPath` uses the persisted ID and validated parent's path inside the command transaction. SQL checks path syntax; it cannot check ancestor-path agreement across rows. Direct hierarchy/path updates are unsupported because they bypass the service. No subtree-move command is exposed.
- `CreateOrganizationCommand` saves the ownership anchor, saves the root to obtain its ID, initializes `/rootId/`, appends an audit event, and commits. `CreateOrganizationUnitCommand` validates parent/type/ownership, inserts the common node with its parent already assigned, obtains the generated ID, initializes the path, inserts the typed link and audit, and commits. There is no separately committed parent/path stage.
- No FK uses cascading delete. Soft-deactivation updates only the node's `IsActive`; domain creation rejects an inactive immediate parent. This does not implement inherited authorization or automatic ancestor-state filtering.
- Update shared details with the original `RowVersion`; stale updates fail. The typed-parent consistency FK uses the actual nullable `ParentOrganizationUnitId`. It is enforced in DbUp SQL, not represented as an EF alternate key, because EF alternate keys cannot contain null root-parent values. Other ownership/type/typed-parent relationships remain mapped in EF.
- Public organization CRUD/UI and role/permission grants are not introduced by this task. Internal creation commands use the existing IAM administrator authorizer and append `OrganizationCreated`/`OrganizationUnitCreated` audit records with typed identifier metadata; existing role policy is unchanged.

## Rollout and reset safeguards

Review and deploy migrations through the existing `Identity.Database` runner before deploying the new EF model. **Do not truncate, delete from, drop, reseed, or manually rewrite `dbo.SchemaVersions`.** Normal DbUp deployment appends execution history for the new migration; existing rows stay intact.

The runner executes each migration and its history entry in one transaction. Deliberate migration failure, rollback, successful retry, and no-op replay were verified on a disposable database; see [verification evidence](T018-T019-Verification.md). Code uniqueness remains organization-wide as originally specified, not per unit type. Confirm a change separately if Country and Department must share the same code within one organization.

No migration was applied to `FIN_IAM`, `FIN_PTS_DS4`, or `FIN_PTS_QS4` during development. Validation uses separate disposable SQL databases. No real organizations, dummy data, or requested employee account are seeded into business databases.

The existing IAM reset utility intentionally allows only its previously approved 16-table schema. It will refuse the expanded 25-table schema. Its destructive scope must be reviewed separately before allowing organization master data to be cleared; do not bypass that guard.

## Review queries (read-only, after deployment)

```sql
SELECT * FROM [Identity].[OrganizationDetails];
SELECT * FROM [Identity].[CountryDetails];
SELECT * FROM [Identity].[RegionDetails];
SELECT * FROM [Identity].[StateDetails];
SELECT * FROM [Identity].[BranchDetails];
SELECT * FROM [Identity].[LocationDetails];
SELECT * FROM [Identity].[DepartmentDetails];
SELECT * FROM [Identity].[TeamDetails];

-- Must return no incomplete organization anchors.
SELECT organization.OrganizationId
FROM [Identity].[Organization] AS organization
LEFT JOIN [Identity].[OrganizationUnit] AS root
  ON root.OrganizationId = organization.OrganizationId AND root.UnitType = N'Organization'
WHERE root.OrganizationUnitId IS NULL;

-- Extended field descriptions.
SELECT t.name AS TableName, c.name AS ColumnName, CONVERT(NVARCHAR(4000), p.value) AS Description
FROM sys.tables AS t
JOIN sys.schemas AS s ON s.schema_id = t.schema_id
JOIN sys.columns AS c ON c.object_id = t.object_id
JOIN sys.extended_properties AS p
  ON p.class = 1 AND p.major_id = c.object_id AND p.minor_id = c.column_id AND p.name = N'MS_Description'
WHERE s.name = N'Identity'
ORDER BY t.name, c.column_id;
```


## Management extension (T020�T024)

The T019 API/UI exclusions above describe the original foundation. Organization management now extends the same store, dispatcher and atomic creation commands. Shared-detail and own-state updates retain immutable organization/type/parent/path; RowVersion is required and unchanged retries are no-ops. Per-organization transaction locks serialize supported creation/state/details writes; SQL uniqueness still spans all unit types. Migration `0010_organization_management_audit.sql` only adds `OrganizationUnitUpdated` and `OrganizationUnitStateChanged` to the existing audit event constraint. It must deploy through DbUp before using those operations. No business database has been migrated.
