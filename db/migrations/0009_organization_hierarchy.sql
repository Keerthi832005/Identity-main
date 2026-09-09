/* Organization hierarchy. Shared business/address/audit fields live only in OrganizationUnit.
   Type-specific tables carry shared keys and parent relationships; *Details views expose aliases.
   This script never clears or rewrites dbo.SchemaVersions or existing application data. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

CREATE TABLE [Identity].[Organization]
(
    [OrganizationId] BIGINT IDENTITY(1,1) NOT NULL,
    [CreatedAt] DATETIME2(3) NOT NULL CONSTRAINT [DfOrganizationCreatedAt] DEFAULT SYSUTCDATETIME(),
    [RowVersion] ROWVERSION NOT NULL,
    CONSTRAINT [PkOrganization] PRIMARY KEY CLUSTERED ([OrganizationId])
);
GO

CREATE TABLE [Identity].[OrganizationUnit]
(
    [OrganizationUnitId] BIGINT IDENTITY(1,1) NOT NULL,
    [OrganizationId] BIGINT NOT NULL,
    [ParentOrganizationUnitId] BIGINT NULL,
    [HierarchyPath] NVARCHAR(450) NULL,
    [UnitType] NVARCHAR(20) NOT NULL,
    [ParentUnitType] AS CONVERT(NVARCHAR(20), CASE [UnitType]
        WHEN N'Country' THEN N'Organization' WHEN N'Department' THEN N'Organization'
        WHEN N'Region' THEN N'Country' WHEN N'State' THEN N'Region'
        WHEN N'Branch' THEN N'State' WHEN N'Location' THEN N'Branch'
        WHEN N'Team' THEN N'Department' END) PERSISTED,
    [UnitCode] NVARCHAR(50) NOT NULL,
    [NormalizedUnitCode] AS UPPER(LTRIM(RTRIM([UnitCode]))) PERSISTED,
    [UnitName] NVARCHAR(200) NOT NULL,
    [Description] NVARCHAR(500) NULL,
    [AddressLine1] NVARCHAR(250) NULL,
    [AddressLine2] NVARCHAR(250) NULL,
    [AddressLine3] NVARCHAR(250) NULL,
    [City] NVARCHAR(100) NULL,
    [District] NVARCHAR(100) NULL,
    [StateName] NVARCHAR(100) NULL,
    [PostalCode] NVARCHAR(20) NULL,
    [CountryCode] NVARCHAR(3) NULL,
    [Latitude] DECIMAL(9,6) NULL,
    [Longitude] DECIMAL(9,6) NULL,
    [IsActive] BIT NOT NULL CONSTRAINT [DfOrganizationUnitIsActive] DEFAULT 1,
    [CreatedAt] DATETIME2(3) NOT NULL CONSTRAINT [DfOrganizationUnitCreatedAt] DEFAULT SYSUTCDATETIME(),
    [UpdatedAt] DATETIME2(3) NULL,
    [RowVersion] ROWVERSION NOT NULL,
    CONSTRAINT [PkOrganizationUnit] PRIMARY KEY CLUSTERED ([OrganizationUnitId]),
    CONSTRAINT [UqOrganizationUnitOrganizationType] UNIQUE ([OrganizationUnitId], [OrganizationId], [UnitType]),
    CONSTRAINT [UqOrganizationUnitOrganizationParent] UNIQUE ([OrganizationUnitId], [OrganizationId], [ParentOrganizationUnitId]),
    CONSTRAINT [FkOrganizationUnitOrganization] FOREIGN KEY ([OrganizationId])
        REFERENCES [Identity].[Organization] ([OrganizationId]),
    CONSTRAINT [FkOrganizationUnitParent] FOREIGN KEY ([ParentOrganizationUnitId], [OrganizationId], [ParentUnitType])
        REFERENCES [Identity].[OrganizationUnit] ([OrganizationUnitId], [OrganizationId], [UnitType]),
    CONSTRAINT [CkOrganizationUnitType] CHECK ([UnitType] IN
        (N'Organization', N'Country', N'Region', N'State', N'Branch', N'Location', N'Department', N'Team')),
    CONSTRAINT [CkOrganizationUnitParent] CHECK
        (([UnitType] = N'Organization' AND [ParentOrganizationUnitId] IS NULL)
        OR ([UnitType] <> N'Organization' AND [ParentOrganizationUnitId] IS NOT NULL)),
    CONSTRAINT [CkOrganizationUnitNotSelf] CHECK
        ([ParentOrganizationUnitId] IS NULL OR [ParentOrganizationUnitId] <> [OrganizationUnitId]),
    CONSTRAINT [CkOrganizationUnitHierarchyPath] CHECK
        ([HierarchyPath] IS NULL OR (LEN([HierarchyPath]) > 2 AND LEFT([HierarchyPath], 1) = N'/'
            AND RIGHT([HierarchyPath], 1) = N'/' AND [HierarchyPath] NOT LIKE N'%[^0-9/]%'
            AND [HierarchyPath] NOT LIKE N'%//%')),
    CONSTRAINT [CkOrganizationUnitCode] CHECK (LEN(LTRIM(RTRIM([UnitCode]))) BETWEEN 1 AND 50),
    CONSTRAINT [CkOrganizationUnitName] CHECK (LEN(LTRIM(RTRIM([UnitName]))) BETWEEN 1 AND 200),
    CONSTRAINT [CkOrganizationUnitLatitude] CHECK ([Latitude] BETWEEN -90 AND 90),
    CONSTRAINT [CkOrganizationUnitLongitude] CHECK ([Longitude] BETWEEN -180 AND 180)
);
GO

CREATE UNIQUE INDEX [UqOrganizationUnitCode]
    ON [Identity].[OrganizationUnit] ([OrganizationId], [NormalizedUnitCode]);
CREATE UNIQUE INDEX [UqOrganizationUnitRoot]
    ON [Identity].[OrganizationUnit] ([OrganizationId]) WHERE [ParentOrganizationUnitId] IS NULL;
CREATE UNIQUE INDEX [UqOrganizationUnitHierarchyPath]
    ON [Identity].[OrganizationUnit] ([OrganizationId], [HierarchyPath]) WHERE [HierarchyPath] IS NOT NULL;
CREATE INDEX [IxOrganizationUnitParent]
    ON [Identity].[OrganizationUnit] ([OrganizationId], [ParentOrganizationUnitId]) INCLUDE ([UnitType], [IsActive]);
GO

CREATE TABLE [Identity].[Country]
(
    [CountryId] BIGINT NOT NULL,
    [OrganizationId] BIGINT NOT NULL,
    [OrganizationUnitId] AS [CountryId] PERSISTED,
    [UnitType] NVARCHAR(20) NOT NULL CONSTRAINT [DfCountryUnitType] DEFAULT N'Country',
    CONSTRAINT [PkCountry] PRIMARY KEY CLUSTERED ([CountryId]),
    CONSTRAINT [UqCountryOrganization] UNIQUE ([CountryId], [OrganizationId]),
    CONSTRAINT [CkCountryUnitType] CHECK ([UnitType] = N'Country'),
    CONSTRAINT [FkCountryOrganizationUnit] FOREIGN KEY ([CountryId], [OrganizationId], [UnitType])
        REFERENCES [Identity].[OrganizationUnit] ([OrganizationUnitId], [OrganizationId], [UnitType])
);
GO

CREATE VIEW [Identity].[CountryDetails]
AS
SELECT node.[CountryId], node.[OrganizationId], node.[OrganizationUnitId], node.[UnitType],
    unit.[UnitCode] AS [CountryCode], unit.[UnitName] AS [CountryName],
    unit.[Description], unit.[HierarchyPath],
    unit.[IsActive], unit.[CreatedAt], unit.[UpdatedAt], unit.[RowVersion]
FROM [Identity].[Country] AS node JOIN [Identity].[OrganizationUnit] AS unit
    ON node.[CountryId] = unit.[OrganizationUnitId] AND node.[OrganizationId] = unit.[OrganizationId];
GO

CREATE TABLE [Identity].[Region]
(
    [RegionId] BIGINT NOT NULL,
    [OrganizationId] BIGINT NOT NULL,
    [OrganizationUnitId] AS [RegionId] PERSISTED,
    [UnitType] NVARCHAR(20) NOT NULL CONSTRAINT [DfRegionUnitType] DEFAULT N'Region',
    [CountryId] BIGINT NOT NULL,
    CONSTRAINT [PkRegion] PRIMARY KEY CLUSTERED ([RegionId]),
    CONSTRAINT [UqRegionOrganization] UNIQUE ([RegionId], [OrganizationId]),
    CONSTRAINT [CkRegionUnitType] CHECK ([UnitType] = N'Region'),
    CONSTRAINT [FkRegionOrganizationUnit] FOREIGN KEY ([RegionId], [OrganizationId], [UnitType])
        REFERENCES [Identity].[OrganizationUnit] ([OrganizationUnitId], [OrganizationId], [UnitType]),
    CONSTRAINT [FkRegionCountry] FOREIGN KEY ([CountryId], [OrganizationId])
        REFERENCES [Identity].[Country] ([CountryId], [OrganizationId]),
    CONSTRAINT [FkRegionParentUnit] FOREIGN KEY ([RegionId], [OrganizationId], [CountryId])
        REFERENCES [Identity].[OrganizationUnit] ([OrganizationUnitId], [OrganizationId], [ParentOrganizationUnitId])
);
GO

CREATE INDEX [IxRegionCountry] ON [Identity].[Region] ([OrganizationId], [CountryId]);
GO

CREATE VIEW [Identity].[RegionDetails]
AS
SELECT node.[RegionId], node.[OrganizationId], node.[OrganizationUnitId], node.[UnitType],
    node.[CountryId],
    unit.[UnitCode] AS [RegionCode], unit.[UnitName] AS [RegionName],
    unit.[Description], unit.[HierarchyPath],
    unit.[IsActive], unit.[CreatedAt], unit.[UpdatedAt], unit.[RowVersion]
FROM [Identity].[Region] AS node JOIN [Identity].[OrganizationUnit] AS unit
    ON node.[RegionId] = unit.[OrganizationUnitId] AND node.[OrganizationId] = unit.[OrganizationId];
GO

CREATE TABLE [Identity].[State]
(
    [StateId] BIGINT NOT NULL,
    [OrganizationId] BIGINT NOT NULL,
    [OrganizationUnitId] AS [StateId] PERSISTED,
    [UnitType] NVARCHAR(20) NOT NULL CONSTRAINT [DfStateUnitType] DEFAULT N'State',
    [RegionId] BIGINT NOT NULL,
    CONSTRAINT [PkState] PRIMARY KEY CLUSTERED ([StateId]),
    CONSTRAINT [UqStateOrganization] UNIQUE ([StateId], [OrganizationId]),
    CONSTRAINT [CkStateUnitType] CHECK ([UnitType] = N'State'),
    CONSTRAINT [FkStateOrganizationUnit] FOREIGN KEY ([StateId], [OrganizationId], [UnitType])
        REFERENCES [Identity].[OrganizationUnit] ([OrganizationUnitId], [OrganizationId], [UnitType]),
    CONSTRAINT [FkStateRegion] FOREIGN KEY ([RegionId], [OrganizationId])
        REFERENCES [Identity].[Region] ([RegionId], [OrganizationId]),
    CONSTRAINT [FkStateParentUnit] FOREIGN KEY ([StateId], [OrganizationId], [RegionId])
        REFERENCES [Identity].[OrganizationUnit] ([OrganizationUnitId], [OrganizationId], [ParentOrganizationUnitId])
);
GO

CREATE INDEX [IxStateRegion] ON [Identity].[State] ([OrganizationId], [RegionId]);
GO

CREATE VIEW [Identity].[StateDetails]
AS
SELECT node.[StateId], node.[OrganizationId], node.[OrganizationUnitId], node.[UnitType],
    node.[RegionId],
    unit.[UnitCode] AS [StateCode], unit.[UnitName] AS [StateName],
    unit.[Description], unit.[HierarchyPath],
    unit.[IsActive], unit.[CreatedAt], unit.[UpdatedAt], unit.[RowVersion]
FROM [Identity].[State] AS node JOIN [Identity].[OrganizationUnit] AS unit
    ON node.[StateId] = unit.[OrganizationUnitId] AND node.[OrganizationId] = unit.[OrganizationId];
GO

CREATE TABLE [Identity].[Branch]
(
    [BranchId] BIGINT NOT NULL,
    [OrganizationId] BIGINT NOT NULL,
    [OrganizationUnitId] AS [BranchId] PERSISTED,
    [UnitType] NVARCHAR(20) NOT NULL CONSTRAINT [DfBranchUnitType] DEFAULT N'Branch',
    [StateId] BIGINT NOT NULL,
    CONSTRAINT [PkBranch] PRIMARY KEY CLUSTERED ([BranchId]),
    CONSTRAINT [UqBranchOrganization] UNIQUE ([BranchId], [OrganizationId]),
    CONSTRAINT [CkBranchUnitType] CHECK ([UnitType] = N'Branch'),
    CONSTRAINT [FkBranchOrganizationUnit] FOREIGN KEY ([BranchId], [OrganizationId], [UnitType])
        REFERENCES [Identity].[OrganizationUnit] ([OrganizationUnitId], [OrganizationId], [UnitType]),
    CONSTRAINT [FkBranchState] FOREIGN KEY ([StateId], [OrganizationId])
        REFERENCES [Identity].[State] ([StateId], [OrganizationId]),
    CONSTRAINT [FkBranchParentUnit] FOREIGN KEY ([BranchId], [OrganizationId], [StateId])
        REFERENCES [Identity].[OrganizationUnit] ([OrganizationUnitId], [OrganizationId], [ParentOrganizationUnitId])
);
GO

CREATE INDEX [IxBranchState] ON [Identity].[Branch] ([OrganizationId], [StateId]);
GO

CREATE VIEW [Identity].[BranchDetails]
AS
SELECT node.[BranchId], node.[OrganizationId], node.[OrganizationUnitId], node.[UnitType],
    node.[StateId],
    unit.[UnitCode] AS [BranchCode], unit.[UnitName] AS [BranchName],
    unit.[Description], unit.[HierarchyPath],
    unit.[AddressLine1], unit.[AddressLine2], unit.[AddressLine3], unit.[City], unit.[District],
    unit.[StateName], unit.[PostalCode], unit.[CountryCode], unit.[Latitude], unit.[Longitude],
    unit.[IsActive], unit.[CreatedAt], unit.[UpdatedAt], unit.[RowVersion]
FROM [Identity].[Branch] AS node JOIN [Identity].[OrganizationUnit] AS unit
    ON node.[BranchId] = unit.[OrganizationUnitId] AND node.[OrganizationId] = unit.[OrganizationId];
GO

CREATE TABLE [Identity].[Location]
(
    [LocationId] BIGINT NOT NULL,
    [OrganizationId] BIGINT NOT NULL,
    [OrganizationUnitId] AS [LocationId] PERSISTED,
    [UnitType] NVARCHAR(20) NOT NULL CONSTRAINT [DfLocationUnitType] DEFAULT N'Location',
    [BranchId] BIGINT NOT NULL,
    CONSTRAINT [PkLocation] PRIMARY KEY CLUSTERED ([LocationId]),
    CONSTRAINT [UqLocationOrganization] UNIQUE ([LocationId], [OrganizationId]),
    CONSTRAINT [CkLocationUnitType] CHECK ([UnitType] = N'Location'),
    CONSTRAINT [FkLocationOrganizationUnit] FOREIGN KEY ([LocationId], [OrganizationId], [UnitType])
        REFERENCES [Identity].[OrganizationUnit] ([OrganizationUnitId], [OrganizationId], [UnitType]),
    CONSTRAINT [FkLocationBranch] FOREIGN KEY ([BranchId], [OrganizationId])
        REFERENCES [Identity].[Branch] ([BranchId], [OrganizationId]),
    CONSTRAINT [FkLocationParentUnit] FOREIGN KEY ([LocationId], [OrganizationId], [BranchId])
        REFERENCES [Identity].[OrganizationUnit] ([OrganizationUnitId], [OrganizationId], [ParentOrganizationUnitId])
);
GO

CREATE INDEX [IxLocationBranch] ON [Identity].[Location] ([OrganizationId], [BranchId]);
GO

CREATE VIEW [Identity].[LocationDetails]
AS
SELECT node.[LocationId], node.[OrganizationId], node.[OrganizationUnitId], node.[UnitType],
    node.[BranchId],
    unit.[UnitCode] AS [LocationCode], unit.[UnitName] AS [LocationName],
    unit.[Description], unit.[HierarchyPath],
    unit.[AddressLine1], unit.[AddressLine2], unit.[AddressLine3], unit.[City], unit.[District],
    unit.[StateName], unit.[PostalCode], unit.[CountryCode], unit.[Latitude], unit.[Longitude],
    unit.[IsActive], unit.[CreatedAt], unit.[UpdatedAt], unit.[RowVersion]
FROM [Identity].[Location] AS node JOIN [Identity].[OrganizationUnit] AS unit
    ON node.[LocationId] = unit.[OrganizationUnitId] AND node.[OrganizationId] = unit.[OrganizationId];
GO

CREATE TABLE [Identity].[Department]
(
    [DepartmentId] BIGINT NOT NULL,
    [OrganizationId] BIGINT NOT NULL,
    [OrganizationUnitId] AS [DepartmentId] PERSISTED,
    [UnitType] NVARCHAR(20) NOT NULL CONSTRAINT [DfDepartmentUnitType] DEFAULT N'Department',
    CONSTRAINT [PkDepartment] PRIMARY KEY CLUSTERED ([DepartmentId]),
    CONSTRAINT [UqDepartmentOrganization] UNIQUE ([DepartmentId], [OrganizationId]),
    CONSTRAINT [CkDepartmentUnitType] CHECK ([UnitType] = N'Department'),
    CONSTRAINT [FkDepartmentOrganizationUnit] FOREIGN KEY ([DepartmentId], [OrganizationId], [UnitType])
        REFERENCES [Identity].[OrganizationUnit] ([OrganizationUnitId], [OrganizationId], [UnitType])
);
GO

CREATE VIEW [Identity].[DepartmentDetails]
AS
SELECT node.[DepartmentId], node.[OrganizationId], node.[OrganizationUnitId], node.[UnitType],
    unit.[UnitCode] AS [DepartmentCode], unit.[UnitName] AS [DepartmentName],
    unit.[Description], unit.[HierarchyPath],
    unit.[IsActive], unit.[CreatedAt], unit.[UpdatedAt], unit.[RowVersion]
FROM [Identity].[Department] AS node JOIN [Identity].[OrganizationUnit] AS unit
    ON node.[DepartmentId] = unit.[OrganizationUnitId] AND node.[OrganizationId] = unit.[OrganizationId];
GO

CREATE TABLE [Identity].[Team]
(
    [TeamId] BIGINT NOT NULL,
    [OrganizationId] BIGINT NOT NULL,
    [OrganizationUnitId] AS [TeamId] PERSISTED,
    [UnitType] NVARCHAR(20) NOT NULL CONSTRAINT [DfTeamUnitType] DEFAULT N'Team',
    [DepartmentId] BIGINT NOT NULL,
    CONSTRAINT [PkTeam] PRIMARY KEY CLUSTERED ([TeamId]),
    CONSTRAINT [UqTeamOrganization] UNIQUE ([TeamId], [OrganizationId]),
    CONSTRAINT [CkTeamUnitType] CHECK ([UnitType] = N'Team'),
    CONSTRAINT [FkTeamOrganizationUnit] FOREIGN KEY ([TeamId], [OrganizationId], [UnitType])
        REFERENCES [Identity].[OrganizationUnit] ([OrganizationUnitId], [OrganizationId], [UnitType]),
    CONSTRAINT [FkTeamDepartment] FOREIGN KEY ([DepartmentId], [OrganizationId])
        REFERENCES [Identity].[Department] ([DepartmentId], [OrganizationId]),
    CONSTRAINT [FkTeamParentUnit] FOREIGN KEY ([TeamId], [OrganizationId], [DepartmentId])
        REFERENCES [Identity].[OrganizationUnit] ([OrganizationUnitId], [OrganizationId], [ParentOrganizationUnitId])
);
GO

CREATE INDEX [IxTeamDepartment] ON [Identity].[Team] ([OrganizationId], [DepartmentId]);
GO

CREATE VIEW [Identity].[TeamDetails]
AS
SELECT node.[TeamId], node.[OrganizationId], node.[OrganizationUnitId], node.[UnitType],
    node.[DepartmentId],
    unit.[UnitCode] AS [TeamCode], unit.[UnitName] AS [TeamName],
    unit.[Description], unit.[HierarchyPath],
    unit.[IsActive], unit.[CreatedAt], unit.[UpdatedAt], unit.[RowVersion]
FROM [Identity].[Team] AS node JOIN [Identity].[OrganizationUnit] AS unit
    ON node.[TeamId] = unit.[OrganizationUnitId] AND node.[OrganizationId] = unit.[OrganizationId];
GO

CREATE VIEW [Identity].[OrganizationDetails]
AS
SELECT organization.[OrganizationId], unit.[OrganizationUnitId], unit.[UnitType],
    unit.[UnitCode] AS [OrganizationCode], unit.[UnitName] AS [OrganizationName],
    unit.[Description], unit.[HierarchyPath], unit.[IsActive], unit.[CreatedAt], unit.[UpdatedAt], unit.[RowVersion]
FROM [Identity].[Organization] AS organization
JOIN [Identity].[OrganizationUnit] AS unit ON unit.[OrganizationId] = organization.[OrganizationId]
WHERE unit.[UnitType] = N'Organization';
GO

/* Preserve the existing audit allow-list while adding the atomic organization creation events. */
DECLARE @auditDefinition NVARCHAR(MAX) = (
    SELECT [definition] FROM sys.check_constraints
    WHERE [parent_object_id] = OBJECT_ID(N'Identity.AuthenticationAudit') AND [name] = N'CkAuthenticationAuditEventType'
);
IF @auditDefinition IS NULL THROW 51010, 'Authentication audit event constraint is missing.', 1;
ALTER TABLE [Identity].[AuthenticationAudit] DROP CONSTRAINT [CkAuthenticationAuditEventType];
DECLARE @auditSql NVARCHAR(MAX) = N'ALTER TABLE [Identity].[AuthenticationAudit] WITH CHECK '
    + N'ADD CONSTRAINT [CkAuthenticationAuditEventType] CHECK ((' + @auditDefinition
    + N') OR [EventType] IN (N''OrganizationCreated'', N''OrganizationUnitCreated''));';
EXEC sys.sp_executesql @auditSql;
GO

/* Field-level descriptions are available in SQL Server extended properties as well as the design guide. */
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Unique organization ownership identifier; business name and active state are held by its single root OrganizationUnit.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Organization', @level2type=N'COLUMN', @level2name=N'OrganizationId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'UTC creation time of the organization ownership anchor.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Organization', @level2type=N'COLUMN', @level2name=N'CreatedAt';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'SQL Server-generated concurrency token for this anchor; not a date or timestamp.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Organization', @level2type=N'COLUMN', @level2name=N'RowVersion';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Unique identity key of an organization hierarchy node.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'OrganizationUnitId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Owning organization. Composite parent and subtype foreign keys prevent cross-organization references.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'OrganizationId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Parent node in the same organization; NULL only for a root Organization.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'ParentOrganizationUnitId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Application-managed canonical path of slash-delimited unit IDs, initialized before the creation transaction commits. Prefix comparisons identify descendants.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'HierarchyPath';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Organization, Country, Region, State, Branch, Location, Department, or Team.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'UnitType';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Persisted expected parent type derived from UnitType; the composite foreign key enforces the required hierarchy.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'ParentUnitType';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Business code unique across all unit types within the owning organization after trim/uppercase normalization.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'UnitCode';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Persisted uppercase trimmed UnitCode used by the organization-scoped unique index.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'NormalizedUnitCode';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Display/business name, stored once for the node and exposed through typed detail views.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'UnitName';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional explanation of the unit purpose or business responsibilities.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'Description';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Primary physical address, if applicable.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'AddressLine1';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Additional address information.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'AddressLine2';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Additional address information.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'AddressLine3';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Physical-address city.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'City';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Physical-address district.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'District';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Physical-address state/province text; distinct from the business State node name.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'StateName';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Postal/ZIP code for the physical address.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'PostalCode';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'ISO 3166-1 country code used for the physical address (alpha-2 such as IN or alpha-3 such as IND); business country codes are UnitCode. Reference-list membership is not validated here.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'CountryCode';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional geographic latitude in decimal degrees, -90 through 90.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'Latitude';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Optional geographic longitude in decimal degrees, -180 through 180.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'Longitude';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Own active/soft-deactivation flag; changing it does not delete or cascade changes to children.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'IsActive';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'UTC timestamp when the hierarchy node was created.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'CreatedAt';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'UTC timestamp of the last application-managed details/active-state update.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'UpdatedAt';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'SQL Server-generated concurrency token for shared business, address, and active-state fields; not a datetime.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'OrganizationUnit', @level2type=N'COLUMN', @level2name=N'RowVersion';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Shared primary key, equal to the corresponding Country OrganizationUnitId; not a second generated identity.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Country', @level2type=N'COLUMN', @level2name=N'CountryId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Owning organization; every composite relationship must retain this same value.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Country', @level2type=N'COLUMN', @level2name=N'OrganizationId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Computed alias of CountryId identifying the corresponding common hierarchy node.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Country', @level2type=N'COLUMN', @level2name=N'OrganizationUnitId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Fixed Country discriminator; check constraint and composite FK prevent wrong-type attachments.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Country', @level2type=N'COLUMN', @level2name=N'UnitType';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Shared primary key, equal to the corresponding Region OrganizationUnitId; not a second generated identity.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Region', @level2type=N'COLUMN', @level2name=N'RegionId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Owning organization; every composite relationship must retain this same value.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Region', @level2type=N'COLUMN', @level2name=N'OrganizationId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Computed alias of RegionId identifying the corresponding common hierarchy node.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Region', @level2type=N'COLUMN', @level2name=N'OrganizationUnitId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Fixed Region discriminator; check constraint and composite FK prevent wrong-type attachments.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Region', @level2type=N'COLUMN', @level2name=N'UnitType';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Parent Country shared unit ID in the same organization; also validated against OrganizationUnit.ParentOrganizationUnitId.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Region', @level2type=N'COLUMN', @level2name=N'CountryId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Shared primary key, equal to the corresponding State OrganizationUnitId; not a second generated identity.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'State', @level2type=N'COLUMN', @level2name=N'StateId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Owning organization; every composite relationship must retain this same value.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'State', @level2type=N'COLUMN', @level2name=N'OrganizationId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Computed alias of StateId identifying the corresponding common hierarchy node.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'State', @level2type=N'COLUMN', @level2name=N'OrganizationUnitId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Fixed State discriminator; check constraint and composite FK prevent wrong-type attachments.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'State', @level2type=N'COLUMN', @level2name=N'UnitType';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Parent Region shared unit ID in the same organization; also validated against OrganizationUnit.ParentOrganizationUnitId.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'State', @level2type=N'COLUMN', @level2name=N'RegionId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Shared primary key, equal to the corresponding Branch OrganizationUnitId; not a second generated identity.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Branch', @level2type=N'COLUMN', @level2name=N'BranchId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Owning organization; every composite relationship must retain this same value.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Branch', @level2type=N'COLUMN', @level2name=N'OrganizationId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Computed alias of BranchId identifying the corresponding common hierarchy node.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Branch', @level2type=N'COLUMN', @level2name=N'OrganizationUnitId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Fixed Branch discriminator; check constraint and composite FK prevent wrong-type attachments.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Branch', @level2type=N'COLUMN', @level2name=N'UnitType';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Parent State shared unit ID in the same organization; also validated against OrganizationUnit.ParentOrganizationUnitId.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Branch', @level2type=N'COLUMN', @level2name=N'StateId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Shared primary key, equal to the corresponding Location OrganizationUnitId; not a second generated identity.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Location', @level2type=N'COLUMN', @level2name=N'LocationId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Owning organization; every composite relationship must retain this same value.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Location', @level2type=N'COLUMN', @level2name=N'OrganizationId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Computed alias of LocationId identifying the corresponding common hierarchy node.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Location', @level2type=N'COLUMN', @level2name=N'OrganizationUnitId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Fixed Location discriminator; check constraint and composite FK prevent wrong-type attachments.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Location', @level2type=N'COLUMN', @level2name=N'UnitType';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Parent Branch shared unit ID in the same organization; also validated against OrganizationUnit.ParentOrganizationUnitId.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Location', @level2type=N'COLUMN', @level2name=N'BranchId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Shared primary key, equal to the corresponding Department OrganizationUnitId; not a second generated identity.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Department', @level2type=N'COLUMN', @level2name=N'DepartmentId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Owning organization; every composite relationship must retain this same value.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Department', @level2type=N'COLUMN', @level2name=N'OrganizationId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Computed alias of DepartmentId identifying the corresponding common hierarchy node.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Department', @level2type=N'COLUMN', @level2name=N'OrganizationUnitId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Fixed Department discriminator; check constraint and composite FK prevent wrong-type attachments.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Department', @level2type=N'COLUMN', @level2name=N'UnitType';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Shared primary key, equal to the corresponding Team OrganizationUnitId; not a second generated identity.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Team', @level2type=N'COLUMN', @level2name=N'TeamId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Owning organization; every composite relationship must retain this same value.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Team', @level2type=N'COLUMN', @level2name=N'OrganizationId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Computed alias of TeamId identifying the corresponding common hierarchy node.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Team', @level2type=N'COLUMN', @level2name=N'OrganizationUnitId';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Fixed Team discriminator; check constraint and composite FK prevent wrong-type attachments.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Team', @level2type=N'COLUMN', @level2name=N'UnitType';
EXEC sys.sp_addextendedproperty @name=N'MS_Description', @value=N'Parent Department shared unit ID in the same organization; also validated against OrganizationUnit.ParentOrganizationUnitId.',
    @level0type=N'SCHEMA', @level0name=N'Identity', @level1type=N'TABLE', @level1name=N'Team', @level2type=N'COLUMN', @level2name=N'DepartmentId';
GO
