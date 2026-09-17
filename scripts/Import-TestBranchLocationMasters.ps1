param(
    [string]$Server = 'lpc:.\SQLEXPRESS2022',
    [string]$Database = 'IAM_LocalTest',
    [string]$BranchFile = 'C:\Users\keerthivasan\.codex\attachments\fbf53ecc-d762-4e0f-be1e-340f98d400ed\pasted-text.txt',
    [string]$LocationFile = 'C:\Users\keerthivasan\.codex\attachments\f912bd19-123d-4271-8bb0-e9555c7a3462\pasted-text.txt'
)

$ErrorActionPreference = 'Stop'

function SqlLiteral([string]$value) {
    return "N'" + $value.Replace("'", "''") + "'"
}

function Read-AssetColumn([string]$path, [string]$header) {
    $lines = Get-Content -LiteralPath $path
    $headerIndex = [Array]::FindIndex([string[]]$lines, [Predicate[string]]{ param($line) $line.Trim() -eq $header })
    if ($headerIndex -lt 0) { throw "Header '$header' was not found in $path." }
    return @($lines[($headerIndex + 1)..($lines.Count - 1)])
}

$branches = Read-AssetColumn $BranchFile 'Branch'
$locations = Read-AssetColumn $LocationFile 'Location'
$slotCount = [Math]::Min($branches.Count, $locations.Count)

$branchToState = [ordered]@{
    'Fujitec India Pvt Ltd' = 'Tamil Nadu Test'
    'Fujitec Factory Office' = 'Tamil Nadu Test'
    'Fujitec Uttar Pradesh' = 'Uttar Pradesh'
    'Central Service Warehouse' = 'Tamil Nadu Test'
    'Fujitec Pune-1' = 'Maharashtra'
    'Fujitec Mumbai1' = 'Maharashtra'
    'Fujitec Hyderabad' = 'Telangana'
    'Fujitec Chennai' = 'Tamil Nadu Test'
    'Fujitec Bangalore' = 'Karnataka'
    'Fujitec Mumbai3 (NVM)' = 'Maharashtra'
    'Fujitec Cochin' = 'Kerala'
    'Fujitec Haryana-1' = 'Haryana'
    'Fujitec Delhi' = 'Delhi'
    'Delivery Center' = 'Tamil Nadu Test'
    'Fujitec NIB Head Office' = 'Tamil Nadu Test'
    'Fujitec Goa' = 'Goa'
    'Fujitec Jaipur' = 'Rajasthan'
    'Fujitec Kolkata' = 'West Bengal'
    'Fujitec Punjab' = 'Punjab'
    'Fujitec Ahmedabad' = 'Gujarat'
    'Fujitec Andhra Pradesh' = 'Andhra Pradesh'
    'Fujitec Mumbai2' = 'Maharashtra'
    'Fujitec CMRL' = 'Tamil Nadu Test'
    'Fujitec Odisha' = 'Odisha'
}

$stateToRegion = [ordered]@{
    'Tamil Nadu Test' = 'South Test Region'
    'Telangana' = 'South Test Region'
    'Karnataka' = 'South Test Region'
    'Kerala' = 'South Test Region'
    'Andhra Pradesh' = 'South Test Region'
    'Uttar Pradesh' = 'North Test Region'
    'Haryana' = 'North Test Region'
    'Delhi' = 'North Test Region'
    'Punjab' = 'North Test Region'
    'Rajasthan' = 'North Test Region'
    'Maharashtra' = 'West Test Region'
    'Goa' = 'West Test Region'
    'Gujarat' = 'West Test Region'
    'West Bengal' = 'East Test Region'
    'Odisha' = 'East Test Region'
}

$branchNames = @($branches | ForEach-Object { $_.Trim() } | Where-Object { $_ } | Sort-Object -Unique)
$unmapped = @($branchNames | Where-Object { -not $branchToState.Contains($_) })
if ($unmapped.Count -gt 0) { throw "Unmapped branches: $($unmapped -join ', ')" }

$pairs = [ordered]@{}
for ($i = 0; $i -lt $slotCount; $i++) {
    $branch = $branches[$i].Trim()
    $location = $locations[$i].Trim()
    if (-not $branch -or -not $location) { continue }
    $key = "$branch`u{001F}$location"
    if (-not $pairs.Contains($key)) { $pairs[$key] = [pscustomobject]@{ Branch = $branch; Location = $location } }
}

$sqlPath = Join-Path $PSScriptRoot 'Import-TestBranchLocationMasters.generated.sql'
$sql = [Collections.Generic.List[string]]::new()
$sql.Add("USE [$Database];")
$sql.Add('GO')
$sql.Add('SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET CONCAT_NULL_YIELDS_NULL ON; SET ARITHABORT ON; SET NUMERIC_ROUNDABORT OFF; SET NOCOUNT ON; SET XACT_ABORT ON;')
$sql.Add("IF DB_NAME() <> N'$Database' THROW 51000, 'Wrong database selected.', 1;")
$sql.Add('BEGIN TRANSACTION;')
$sql.Add("DECLARE @OrganizationId bigint = (SELECT OrganizationId FROM [Identity].[OrganizationUnit] WHERE UnitType=N'Organization' AND UnitName=N'AMS Test Organization' AND IsActive=1);")
$sql.Add("DECLARE @CountryId bigint = (SELECT OrganizationUnitId FROM [Identity].[OrganizationUnit] WHERE OrganizationId=@OrganizationId AND UnitType=N'Country' AND UnitName=N'India' AND IsActive=1);")
$sql.Add("IF @OrganizationId IS NULL OR @CountryId IS NULL THROW 51001, 'AMS Test Organization / India was not found.', 1;")
$sql.Add('DECLARE @Regions TABLE(Name nvarchar(400), Code nvarchar(100));')
$regions = @($stateToRegion.Values | Sort-Object -Unique)
for ($i=0; $i -lt $regions.Count; $i++) { $sql.Add("INSERT @Regions VALUES ($(SqlLiteral $regions[$i]), N'REG-TEST-$('{0:D2}' -f ($i+1))');") }
$sql.Add("DELETE FROM @Regions WHERE Name=N'South Test Region';")
$sql.Add("INSERT INTO [Identity].[OrganizationUnit](OrganizationId,ParentOrganizationUnitId,UnitType,UnitCode,UnitName,IsActive,CreatedAt,UpdatedAt) SELECT @OrganizationId,@CountryId,N'Region',r.Code,r.Name,1,SYSUTCDATETIME(),SYSUTCDATETIME() FROM @Regions r WHERE NOT EXISTS(SELECT 1 FROM [Identity].[OrganizationUnit] u WHERE u.OrganizationId=@OrganizationId AND u.UnitType=N'Region' AND u.UnitName=r.Name);")
$sql.Add("UPDATE u SET HierarchyPath=p.HierarchyPath+CONVERT(nvarchar(20),u.OrganizationUnitId)+N'/' FROM [Identity].[OrganizationUnit] u JOIN [Identity].[OrganizationUnit] p ON p.OrganizationUnitId=u.ParentOrganizationUnitId WHERE u.OrganizationId=@OrganizationId AND u.UnitType=N'Region' AND u.HierarchyPath IS NULL;")
$sql.Add('DECLARE @States TABLE(Name nvarchar(400), RegionName nvarchar(400), Code nvarchar(100));')
$stateIndex=1
foreach($entry in $stateToRegion.GetEnumerator()) { if($entry.Key -ne 'Tamil Nadu Test'){ $sql.Add("INSERT @States VALUES ($(SqlLiteral $entry.Key),$(SqlLiteral $entry.Value),N'ST-TEST-$('{0:D2}' -f $stateIndex)');"); $stateIndex++ } }
$sql.Add("INSERT INTO [Identity].[OrganizationUnit](OrganizationId,ParentOrganizationUnitId,UnitType,UnitCode,UnitName,IsActive,CreatedAt,UpdatedAt) SELECT @OrganizationId,r.OrganizationUnitId,N'State',s.Code,s.Name,1,SYSUTCDATETIME(),SYSUTCDATETIME() FROM @States s JOIN [Identity].[OrganizationUnit] r ON r.OrganizationId=@OrganizationId AND r.UnitType=N'Region' AND r.UnitName=s.RegionName WHERE NOT EXISTS(SELECT 1 FROM [Identity].[OrganizationUnit] u WHERE u.OrganizationId=@OrganizationId AND u.UnitType=N'State' AND u.UnitName=s.Name);")
$sql.Add("UPDATE u SET HierarchyPath=p.HierarchyPath+CONVERT(nvarchar(20),u.OrganizationUnitId)+N'/' FROM [Identity].[OrganizationUnit] u JOIN [Identity].[OrganizationUnit] p ON p.OrganizationUnitId=u.ParentOrganizationUnitId WHERE u.OrganizationId=@OrganizationId AND u.UnitType=N'State' AND u.HierarchyPath IS NULL;")
$sql.Add('DECLARE @Branches TABLE(Name nvarchar(400), StateName nvarchar(400), Code nvarchar(100));')
for($i=0;$i -lt $branchNames.Count;$i++){ $name=$branchNames[$i]; $sql.Add("INSERT @Branches VALUES ($(SqlLiteral $name),$(SqlLiteral $branchToState[$name]),N'BR-ASSET-$('{0:D3}' -f ($i+1))');") }
$sql.Add("INSERT INTO [Identity].[OrganizationUnit](OrganizationId,ParentOrganizationUnitId,UnitType,UnitCode,UnitName,IsActive,CreatedAt,UpdatedAt) SELECT @OrganizationId,s.OrganizationUnitId,N'Branch',b.Code,b.Name,1,SYSUTCDATETIME(),SYSUTCDATETIME() FROM @Branches b JOIN [Identity].[OrganizationUnit] s ON s.OrganizationId=@OrganizationId AND s.UnitType=N'State' AND s.UnitName=b.StateName WHERE NOT EXISTS(SELECT 1 FROM [Identity].[OrganizationUnit] u WHERE u.OrganizationId=@OrganizationId AND u.UnitType=N'Branch' AND u.ParentOrganizationUnitId=s.OrganizationUnitId AND u.UnitName=b.Name);")
$sql.Add("UPDATE u SET HierarchyPath=p.HierarchyPath+CONVERT(nvarchar(20),u.OrganizationUnitId)+N'/' FROM [Identity].[OrganizationUnit] u JOIN [Identity].[OrganizationUnit] p ON p.OrganizationUnitId=u.ParentOrganizationUnitId WHERE u.OrganizationId=@OrganizationId AND u.UnitType=N'Branch' AND u.HierarchyPath IS NULL;")
$sql.Add('DECLARE @Locations TABLE(BranchName nvarchar(400), Name nvarchar(400), Code nvarchar(100));')
$pairIndex=1
foreach($pair in $pairs.Values){ $sql.Add("INSERT @Locations VALUES ($(SqlLiteral $pair.Branch),$(SqlLiteral $pair.Location),N'LOC-ASSET-$('{0:D4}' -f $pairIndex)');"); $pairIndex++ }
$sql.Add("INSERT INTO [Identity].[OrganizationUnit](OrganizationId,ParentOrganizationUnitId,UnitType,UnitCode,UnitName,IsActive,CreatedAt,UpdatedAt) SELECT @OrganizationId,b.OrganizationUnitId,N'Location',l.Code,l.Name,1,SYSUTCDATETIME(),SYSUTCDATETIME() FROM @Locations l JOIN [Identity].[OrganizationUnit] b ON b.OrganizationId=@OrganizationId AND b.UnitType=N'Branch' AND b.UnitName=l.BranchName WHERE NOT EXISTS(SELECT 1 FROM [Identity].[OrganizationUnit] u WHERE u.OrganizationId=@OrganizationId AND u.UnitType=N'Location' AND u.ParentOrganizationUnitId=b.OrganizationUnitId AND u.UnitName=l.Name);")
$sql.Add("UPDATE u SET HierarchyPath=p.HierarchyPath+CONVERT(nvarchar(20),u.OrganizationUnitId)+N'/' FROM [Identity].[OrganizationUnit] u JOIN [Identity].[OrganizationUnit] p ON p.OrganizationUnitId=u.ParentOrganizationUnitId WHERE u.OrganizationId=@OrganizationId AND u.UnitType=N'Location' AND u.HierarchyPath IS NULL;")
$sql.Add("IF EXISTS(SELECT 1 FROM @Branches b LEFT JOIN [Identity].[OrganizationUnit] u ON u.OrganizationId=@OrganizationId AND u.UnitType=N'Branch' AND u.UnitName=b.Name WHERE u.OrganizationUnitId IS NULL) THROW 51002, 'Branch verification failed.', 1;")
$sql.Add("IF EXISTS(SELECT 1 FROM @Locations l JOIN [Identity].[OrganizationUnit] b ON b.OrganizationId=@OrganizationId AND b.UnitType=N'Branch' AND b.UnitName=l.BranchName LEFT JOIN [Identity].[OrganizationUnit] u ON u.OrganizationId=@OrganizationId AND u.UnitType=N'Location' AND u.ParentOrganizationUnitId=b.OrganizationUnitId AND u.UnitName=l.Name WHERE u.OrganizationUnitId IS NULL) THROW 51003, 'Location verification failed.', 1;")
$sql.Add('COMMIT;')
$sql.Add("SELECT UnitType,COUNT(*) AS ActiveCount FROM [Identity].[OrganizationUnit] WHERE OrganizationId=@OrganizationId AND IsActive=1 GROUP BY UnitType ORDER BY UnitType;")
$sql.Add("SELECT s.UnitName AS StateName,b.UnitName AS BranchName,COUNT(l.OrganizationUnitId) AS LocationCount FROM [Identity].[OrganizationUnit] b JOIN [Identity].[OrganizationUnit] s ON s.OrganizationUnitId=b.ParentOrganizationUnitId LEFT JOIN [Identity].[OrganizationUnit] l ON l.ParentOrganizationUnitId=b.OrganizationUnitId AND l.UnitType=N'Location' AND l.IsActive=1 WHERE b.OrganizationId=@OrganizationId AND b.UnitType=N'Branch' AND b.UnitName IN (SELECT Name FROM @Branches) GROUP BY s.UnitName,b.UnitName ORDER BY s.UnitName,b.UnitName;")

[IO.File]::WriteAllLines($sqlPath, $sql, [Text.UTF8Encoding]::new($false))
Write-Output "Generated $sqlPath with $($branchNames.Count) branches and $($pairs.Count) branch-location rows."
& sqlcmd -S $Server -E -C -d $Database -b -i $sqlPath
if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed with exit code $LASTEXITCODE." }
