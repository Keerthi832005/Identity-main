from collections import Counter, defaultdict
from pathlib import Path
import sys

import openpyxl


def sql(value: str) -> str:
    return "N'" + value.replace("'", "''") + "'"


source = Path(sys.argv[1])
destination = Path(sys.argv[2])
sheet = openpyxl.load_workbook(source, read_only=True, data_only=True).active

branch_to_state = {
    'Fujitec India Pvt Ltd': 'Tamil Nadu',
    'Fujitec Factory Office': 'Tamil Nadu',
    'Fujitec Uttar Pradesh': 'Uttar Pradesh',
    'Central Service Warehouse': 'Tamil Nadu',
    'Fujitec Pune-1': 'Maharashtra',
    'Fujitec Mumbai1': 'Maharashtra',
    'Fujitec Hyderabad': 'Telangana',
    'Fujitec Chennai': 'Tamil Nadu',
    'Fujitec Bangalore': 'Karnataka',
    'Fujitec Mumbai3 (NVM)': 'Maharashtra',
    'Fujitec Cochin': 'Kerala',
    'Fujitec Haryana-1': 'Haryana',
    'Fujitec Delhi': 'Delhi',
    'Delivery Center': 'Tamil Nadu',
    'Fujitec NIB Head Office': 'Tamil Nadu',
    'Fujitec Goa': 'Goa',
    'Fujitec Jaipur': 'Rajasthan',
    'Fujitec Kolkata': 'West Bengal',
    'Fujitec Punjab': 'Punjab',
    'Fujitec Ahmedabad': 'Gujarat',
    'Fujitec Andhra Pradesh': 'Andhra Pradesh',
    'Fujitec Mumbai2': 'Maharashtra',
    'Fujitec CMRL': 'Tamil Nadu',
    'Fujitec Odisha': 'Odisha',
}
state_to_region = {
    'Tamil Nadu': 'South', 'Telangana': 'South', 'Karnataka': 'South',
    'Kerala': 'South', 'Andhra Pradesh': 'South',
    'Uttar Pradesh': 'North', 'Haryana': 'North', 'Delhi': 'North',
    'Punjab': 'North', 'Rajasthan': 'North',
    'Maharashtra': 'West', 'Goa': 'West', 'Gujarat': 'West',
    'West Bengal': 'East', 'Odisha': 'East',
}

variants = defaultdict(Counter)
branches = set()
for branch_value, location_value in sheet.iter_rows(min_row=2, max_col=2, values_only=True):
    branch = '' if branch_value is None else str(branch_value).strip()
    location = '' if location_value is None else str(location_value).strip()
    if branch:
        branches.add(branch)
    if branch and location:
        variants[(branch.casefold(), location.casefold())][(branch, location)] += 1

unknown = sorted(branches - branch_to_state.keys())
if unknown:
    raise ValueError(f'Branches without State mappings: {unknown}')

pairs = sorted(
    (counts.most_common(1)[0][0] for counts in variants.values()),
    key=lambda pair: (pair[0].casefold(), pair[1].casefold()),
)
branch_names = sorted(branches, key=str.casefold)
regions = sorted(set(state_to_region.values()), key=str.casefold)
states = sorted(set(branch_to_state.values()), key=str.casefold)

lines = [
    'USE [IAM_LocalTest];',
    'GO',
    'SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET CONCAT_NULL_YIELDS_NULL ON; SET ARITHABORT ON; SET NUMERIC_ROUNDABORT OFF; SET NOCOUNT ON; SET XACT_ABORT ON;',
    "IF EXISTS(SELECT 1 FROM [Identity].[OrganizationUnit]) THROW 51000, 'IAM hierarchy is not empty; import stopped.', 1;",
    'BEGIN TRANSACTION;',
    'INSERT [Identity].[Organization](CreatedAt) VALUES(SYSUTCDATETIME());',
    'DECLARE @OrganizationId bigint=SCOPE_IDENTITY();',
    "INSERT [Identity].[OrganizationUnit](OrganizationId,ParentOrganizationUnitId,HierarchyPath,UnitType,UnitCode,UnitName,IsActive,CreatedAt,UpdatedAt) VALUES(@OrganizationId,NULL,NULL,N'Organization',N'AMS-TEST-ORG',N'AMS Test Organization',1,SYSUTCDATETIME(),SYSUTCDATETIME());",
    'DECLARE @OrganizationUnitId bigint=SCOPE_IDENTITY();',
    "UPDATE [Identity].[OrganizationUnit] SET HierarchyPath=N'/'+CONVERT(nvarchar(20),OrganizationUnitId)+N'/' WHERE OrganizationUnitId=@OrganizationUnitId;",
    "INSERT [Identity].[OrganizationUnit](OrganizationId,ParentOrganizationUnitId,HierarchyPath,UnitType,UnitCode,UnitName,CountryCode,IsActive,CreatedAt,UpdatedAt) VALUES(@OrganizationId,@OrganizationUnitId,NULL,N'Country',N'IN',N'India',N'IN',1,SYSUTCDATETIME(),SYSUTCDATETIME());",
    'DECLARE @CountryId bigint=SCOPE_IDENTITY();',
    "UPDATE [Identity].[OrganizationUnit] SET HierarchyPath=N'/'+CONVERT(nvarchar(20),@OrganizationUnitId)+N'/'+CONVERT(nvarchar(20),OrganizationUnitId)+N'/' WHERE OrganizationUnitId=@CountryId;",
    "INSERT [Identity].[Country](CountryId,OrganizationId,UnitType) VALUES(@CountryId,@OrganizationId,N'Country');",
    'DECLARE @Regions TABLE(Name nvarchar(400),Code nvarchar(100));',
]

for index, name in enumerate(regions, 1):
    lines.append(f"INSERT @Regions VALUES({sql(name)},N'REG-{index:02d}');")
lines.extend([
    "INSERT [Identity].[OrganizationUnit](OrganizationId,ParentOrganizationUnitId,HierarchyPath,UnitType,UnitCode,UnitName,IsActive,CreatedAt,UpdatedAt) SELECT @OrganizationId,@CountryId,NULL,N'Region',Code,Name,1,SYSUTCDATETIME(),SYSUTCDATETIME() FROM @Regions;",
    "UPDATE u SET HierarchyPath=p.HierarchyPath+CONVERT(nvarchar(20),u.OrganizationUnitId)+N'/' FROM [Identity].[OrganizationUnit] u JOIN [Identity].[OrganizationUnit] p ON p.OrganizationUnitId=u.ParentOrganizationUnitId WHERE u.OrganizationId=@OrganizationId AND u.UnitType=N'Region';",
    "INSERT [Identity].[Region](RegionId,OrganizationId,UnitType,CountryId) SELECT OrganizationUnitId,@OrganizationId,N'Region',@CountryId FROM [Identity].[OrganizationUnit] WHERE OrganizationId=@OrganizationId AND UnitType=N'Region';",
    'DECLARE @States TABLE(Name nvarchar(400),RegionName nvarchar(400),Code nvarchar(100));',
])
for index, name in enumerate(states, 1):
    lines.append(f"INSERT @States VALUES({sql(name)},{sql(state_to_region[name])},N'ST-{index:02d}');")
lines.extend([
    "INSERT [Identity].[OrganizationUnit](OrganizationId,ParentOrganizationUnitId,HierarchyPath,UnitType,UnitCode,UnitName,IsActive,CreatedAt,UpdatedAt) SELECT @OrganizationId,r.OrganizationUnitId,NULL,N'State',s.Code,s.Name,1,SYSUTCDATETIME(),SYSUTCDATETIME() FROM @States s JOIN [Identity].[OrganizationUnit] r ON r.OrganizationId=@OrganizationId AND r.UnitType=N'Region' AND r.UnitName=s.RegionName;",
    "UPDATE u SET HierarchyPath=p.HierarchyPath+CONVERT(nvarchar(20),u.OrganizationUnitId)+N'/' FROM [Identity].[OrganizationUnit] u JOIN [Identity].[OrganizationUnit] p ON p.OrganizationUnitId=u.ParentOrganizationUnitId WHERE u.OrganizationId=@OrganizationId AND u.UnitType=N'State';",
    "INSERT [Identity].[State](StateId,OrganizationId,UnitType,RegionId) SELECT OrganizationUnitId,@OrganizationId,N'State',ParentOrganizationUnitId FROM [Identity].[OrganizationUnit] WHERE OrganizationId=@OrganizationId AND UnitType=N'State';",
    'DECLARE @Branches TABLE(Name nvarchar(400),StateName nvarchar(400),Code nvarchar(100));',
])
for index, name in enumerate(branch_names, 1):
    lines.append(f"INSERT @Branches VALUES({sql(name)},{sql(branch_to_state[name])},N'BR-ASSET-{index:03d}');")
lines.extend([
    "INSERT [Identity].[OrganizationUnit](OrganizationId,ParentOrganizationUnitId,HierarchyPath,UnitType,UnitCode,UnitName,IsActive,CreatedAt,UpdatedAt) SELECT @OrganizationId,s.OrganizationUnitId,NULL,N'Branch',b.Code,b.Name,1,SYSUTCDATETIME(),SYSUTCDATETIME() FROM @Branches b JOIN [Identity].[OrganizationUnit] s ON s.OrganizationId=@OrganizationId AND s.UnitType=N'State' AND s.UnitName=b.StateName;",
    "UPDATE u SET HierarchyPath=p.HierarchyPath+CONVERT(nvarchar(20),u.OrganizationUnitId)+N'/' FROM [Identity].[OrganizationUnit] u JOIN [Identity].[OrganizationUnit] p ON p.OrganizationUnitId=u.ParentOrganizationUnitId WHERE u.OrganizationId=@OrganizationId AND u.UnitType=N'Branch';",
    "INSERT [Identity].[Branch](BranchId,OrganizationId,UnitType,StateId) SELECT OrganizationUnitId,@OrganizationId,N'Branch',ParentOrganizationUnitId FROM [Identity].[OrganizationUnit] WHERE OrganizationId=@OrganizationId AND UnitType=N'Branch';",
    'DECLARE @Locations TABLE(BranchName nvarchar(400),Name nvarchar(400),Code nvarchar(100));',
])
for index, (branch, location) in enumerate(pairs, 1):
    lines.append(f"INSERT @Locations VALUES({sql(branch)},{sql(location)},N'LOC-ASSET-{index:04d}');")
lines.extend([
    "INSERT [Identity].[OrganizationUnit](OrganizationId,ParentOrganizationUnitId,HierarchyPath,UnitType,UnitCode,UnitName,IsActive,CreatedAt,UpdatedAt) SELECT @OrganizationId,b.OrganizationUnitId,NULL,N'Location',l.Code,l.Name,1,SYSUTCDATETIME(),SYSUTCDATETIME() FROM @Locations l JOIN [Identity].[OrganizationUnit] b ON b.OrganizationId=@OrganizationId AND b.UnitType=N'Branch' AND b.UnitName=l.BranchName;",
    "UPDATE u SET HierarchyPath=p.HierarchyPath+CONVERT(nvarchar(20),u.OrganizationUnitId)+N'/' FROM [Identity].[OrganizationUnit] u JOIN [Identity].[OrganizationUnit] p ON p.OrganizationUnitId=u.ParentOrganizationUnitId WHERE u.OrganizationId=@OrganizationId AND u.UnitType=N'Location';",
    "INSERT [Identity].[Location](LocationId,OrganizationId,UnitType,BranchId) SELECT OrganizationUnitId,@OrganizationId,N'Location',ParentOrganizationUnitId FROM [Identity].[OrganizationUnit] WHERE OrganizationId=@OrganizationId AND UnitType=N'Location';",
    "IF (SELECT COUNT(*) FROM [Identity].[Branch] WHERE OrganizationId=@OrganizationId)<>24 THROW 51001,'Branch count verification failed.',1;",
    "IF (SELECT COUNT(*) FROM [Identity].[Location] WHERE OrganizationId=@OrganizationId)<>581 THROW 51002,'Location count verification failed.',1;",
    'COMMIT;',
    "SELECT UnitType,COUNT(*) AS ImportedCount FROM [Identity].[OrganizationUnit] WHERE OrganizationId=@OrganizationId GROUP BY UnitType ORDER BY UnitType;",
])

destination.write_text('\n'.join(lines)+'\n', encoding='utf-8')
print(f'Generated {destination} with {len(branch_names)} Branches and {len(pairs)} Locations.')
