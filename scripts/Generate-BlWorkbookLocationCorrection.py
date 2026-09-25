from pathlib import Path
import sys
from collections import Counter, defaultdict
import openpyxl


def sql_literal(value: str) -> str:
    return "N'" + value.replace("'", "''") + "'"


source = Path(sys.argv[1])
destination = Path(sys.argv[2])
workbook = openpyxl.load_workbook(source, read_only=True, data_only=True)
sheet = workbook.active

variants = defaultdict(Counter)
for branch_value, location_value in sheet.iter_rows(min_row=2, max_col=2, values_only=True):
    branch = "" if branch_value is None else str(branch_value).strip()
    location = "" if location_value is None else str(location_value).strip()
    if branch and location:
        variants[(branch.casefold(), location.casefold())][(branch, location)] += 1

canonical_pairs = {
    counts.most_common(1)[0][0]
    for counts in variants.values()
}
ordered_pairs = sorted(canonical_pairs, key=lambda item: (item[0].casefold(), item[1].casefold(), item))
lines = [
    "USE [IAM_LocalTest];",
    "GO",
    "SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET CONCAT_NULL_YIELDS_NULL ON; SET ARITHABORT ON; SET NUMERIC_ROUNDABORT OFF; SET NOCOUNT ON; SET XACT_ABORT ON;",
    "IF DB_NAME() <> N'IAM_LocalTest' THROW 51000, 'Wrong database selected.', 1;",
    "BEGIN TRANSACTION;",
    "DECLARE @OrganizationId bigint = (SELECT OrganizationId FROM [Identity].[OrganizationUnit] WHERE UnitType=N'Organization' AND UnitName=N'AMS Test Organization' AND IsActive=1);",
    "IF @OrganizationId IS NULL THROW 51001, 'AMS Test Organization was not found.', 1;",
    "DECLARE @Locations TABLE(BranchName nvarchar(400), LocationName nvarchar(400), LocationCode nvarchar(100), PRIMARY KEY(BranchName,LocationName));",
]

for index, (branch, location) in enumerate(ordered_pairs, 1):
    lines.append(
        f"INSERT @Locations VALUES ({sql_literal(branch)},{sql_literal(location)},N'LOC-BL-{index:04d}');"
    )

lines.extend(
    [
        "IF EXISTS(SELECT 1 FROM @Locations l LEFT JOIN [Identity].[OrganizationUnit] b ON b.OrganizationId=@OrganizationId AND b.UnitType=N'Branch' AND b.UnitName=l.BranchName AND b.IsActive=1 WHERE b.OrganizationUnitId IS NULL) THROW 51002, 'A workbook Branch is missing from IAM.', 1;",
        "DELETE actual FROM [Identity].[OrganizationUnit] actual JOIN [Identity].[OrganizationUnit] b ON b.OrganizationUnitId=actual.ParentOrganizationUnitId WHERE actual.OrganizationId=@OrganizationId AND actual.UnitType=N'Location' AND actual.UnitCode LIKE N'LOC-ASSET-%' AND NOT EXISTS(SELECT 1 FROM @Locations expected WHERE expected.BranchName=b.UnitName AND expected.LocationName=actual.UnitName);",
        "UPDATE actual SET UnitName=expected.LocationName,UpdatedAt=SYSUTCDATETIME() FROM [Identity].[OrganizationUnit] actual JOIN [Identity].[OrganizationUnit] b ON b.OrganizationUnitId=actual.ParentOrganizationUnitId JOIN @Locations expected ON expected.BranchName=b.UnitName AND expected.LocationName=actual.UnitName WHERE actual.OrganizationId=@OrganizationId AND actual.UnitType=N'Location' AND actual.UnitName COLLATE Latin1_General_100_BIN2 <> expected.LocationName COLLATE Latin1_General_100_BIN2;",
        "INSERT INTO [Identity].[OrganizationUnit](OrganizationId,ParentOrganizationUnitId,UnitType,UnitCode,UnitName,IsActive,CreatedAt,UpdatedAt) SELECT @OrganizationId,b.OrganizationUnitId,N'Location',l.LocationCode,l.LocationName,1,SYSUTCDATETIME(),SYSUTCDATETIME() FROM @Locations l JOIN [Identity].[OrganizationUnit] b ON b.OrganizationId=@OrganizationId AND b.UnitType=N'Branch' AND b.UnitName=l.BranchName AND b.IsActive=1 WHERE NOT EXISTS(SELECT 1 FROM [Identity].[OrganizationUnit] existing WHERE existing.OrganizationId=@OrganizationId AND existing.UnitType=N'Location' AND existing.ParentOrganizationUnitId=b.OrganizationUnitId AND existing.UnitName=l.LocationName);",
        "UPDATE u SET HierarchyPath=p.HierarchyPath+CONVERT(nvarchar(20),u.OrganizationUnitId)+N'/' FROM [Identity].[OrganizationUnit] u JOIN [Identity].[OrganizationUnit] p ON p.OrganizationUnitId=u.ParentOrganizationUnitId WHERE u.OrganizationId=@OrganizationId AND u.UnitType=N'Location' AND u.HierarchyPath IS NULL;",
        "IF EXISTS(SELECT 1 FROM @Locations l JOIN [Identity].[OrganizationUnit] b ON b.OrganizationId=@OrganizationId AND b.UnitType=N'Branch' AND b.UnitName=l.BranchName LEFT JOIN [Identity].[OrganizationUnit] actual ON actual.OrganizationId=@OrganizationId AND actual.UnitType=N'Location' AND actual.ParentOrganizationUnitId=b.OrganizationUnitId AND actual.UnitName=l.LocationName WHERE actual.OrganizationUnitId IS NULL) THROW 51004, 'A workbook mapping is missing after import.', 1;",
        "IF EXISTS(SELECT 1 FROM [Identity].[OrganizationUnit] actual JOIN [Identity].[OrganizationUnit] b ON b.OrganizationUnitId=actual.ParentOrganizationUnitId WHERE actual.OrganizationId=@OrganizationId AND actual.UnitType=N'Location' AND actual.UnitCode LIKE N'LOC-ASSET-%' AND NOT EXISTS(SELECT 1 FROM @Locations expected WHERE expected.BranchName=b.UnitName AND expected.LocationName=actual.UnitName)) THROW 51005, 'An unexpected imported mapping remains.', 1;",
        "COMMIT;",
        "SELECT COUNT(DISTINCT b.OrganizationUnitId) AS ImportedBranches, COUNT(*) AS VerifiedWorkbookMappings, COUNT(DISTINCT expected.LocationName) AS DistinctLocationNames FROM @Locations expected JOIN [Identity].[OrganizationUnit] b ON b.OrganizationId=@OrganizationId AND b.UnitType=N'Branch' AND b.UnitName=expected.BranchName JOIN [Identity].[OrganizationUnit] actual ON actual.OrganizationId=@OrganizationId AND actual.UnitType=N'Location' AND actual.ParentOrganizationUnitId=b.OrganizationUnitId AND actual.UnitName=expected.LocationName;",
    ]
)

destination.write_text("\n".join(lines) + "\n", encoding="utf-8")
print(f"Generated {destination} with {len(ordered_pairs)} unique Branch-Location mappings.")
