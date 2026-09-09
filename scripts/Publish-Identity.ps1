[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [string] $OutputRoot = '',
    [ValidateSet('UAT','Live','Local')][string] $Environment,
    [string] $DatabaseServer,
    [string] $DatabaseName,
    [string] $BackupDirectory,
    [switch] $ArtifactsOnly
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$repo = Split-Path $repositoryRoot
. (Join-Path $repo 'scripts/PublishVersion.Common.ps1')
. (Join-Path $PSScriptRoot 'DatabasePublish.Common.ps1')
$databaseTarget = Resolve-IamPublishDatabaseTarget -Product IAM -Environment $Environment -DatabaseServer $DatabaseServer -DatabaseName $DatabaseName -ArtifactsOnly:$ArtifactsOnly
$releaseId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,8)
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repositoryRoot "artifacts/publish/$releaseId"
}

$resolvedOutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $resolvedOutputRoot) {
    if (-not (Test-Path -LiteralPath $resolvedOutputRoot -PathType Container) -or @(Get-ChildItem -LiteralPath $resolvedOutputRoot -Force).Count) { throw 'Publish output must be a new or empty directory. Existing releases are never overwritten.' }
}
$publishLock = Enter-ProductPublishLock
try {
$apiVersion = New-ProductVersionReservation $repo 'iam-api' $releaseId
$webVersion = New-ProductVersionReservation $repo 'iam-web' $releaseId
$buildRoot = Join-Path $repositoryRoot "artifacts/publish-build/$releaseId"
New-Item -ItemType Directory -Force -Path $resolvedOutputRoot | Out-Null

$publishTargets = [ordered]@{
    'api' = 'src/Backend/Identity.Api/Identity.Api.csproj'
    'database' = 'src/Backend/Identity.Database/Identity.Database.csproj'
    'admin-cli' = 'src/Backend/Identity.AdminCli/Identity.AdminCli.csproj'
}

foreach ($targetName in $publishTargets.Keys) {
    $projectPath = Join-Path $repositoryRoot $publishTargets[$targetName]
    $targetOutput = Join-Path $resolvedOutputRoot $targetName
    & dotnet publish $projectPath `
        --configuration $Configuration `
        --artifacts-path $buildRoot `
        "-p:Version=$($apiVersion.Version)" `
        --output $targetOutput `
        --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Publishing target '$targetName' failed with exit code $LASTEXITCODE."
    }
    Write-ProductVersionMetadata $apiVersion $targetOutput
}

$frontendRoot = Join-Path $repositoryRoot 'src/Frontend'
$frontendOutput = Join-Path $resolvedOutputRoot 'frontend'
Push-Location $frontendRoot
# npm writes its warnings to stderr, and under $ErrorActionPreference = 'Stop' Windows PowerShell
# turns a native command's stderr into a terminating error whenever output is being captured. That
# aborted the script here after the backend had already published, leaving a stale frontend in the
# output while the run still looked like it had produced a full set. Exit codes remain the check.
$previousErrorAction = $ErrorActionPreference
$ErrorActionPreference = 'Continue'
try {
    & npm.cmd ci --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw "Restoring frontend dependencies failed with exit code $LASTEXITCODE." }
    & npm.cmd run build -- --output-path (Join-Path $buildRoot 'frontend')
    if ($LASTEXITCODE -ne 0) { throw "Publishing the frontend failed with exit code $LASTEXITCODE." }
}
finally {
    $ErrorActionPreference = $previousErrorAction
    Pop-Location
}

$builtFrontend = Join-Path $buildRoot 'frontend/browser'
if (-not (Test-Path -LiteralPath $builtFrontend -PathType Container)) {
    $builtFrontend = Join-Path $buildRoot 'frontend'
}
New-Item -ItemType Directory -Force -Path $frontendOutput | Out-Null
Copy-Item -Path (Join-Path $builtFrontend '*') -Destination $frontendOutput -Recurse -Force
Write-ProductVersionMetadata $webVersion $frontendOutput

# Fail loudly rather than hand over a set of outputs with one missing member.
foreach ($expected in @('api', 'database', 'admin-cli', 'frontend')) {
    $expectedPath = Join-Path $resolvedOutputRoot $expected
    if (-not (Get-ChildItem -LiteralPath $expectedPath -File -Recurse -ErrorAction SilentlyContinue)) {
        throw "Publish output '$expected' is empty; the publish did not complete."
    }
}

if ($databaseTarget) {
    Invoke-IamPublishedDatabaseMigration -Target $databaseTarget -DatabasePath (Join-Path $resolvedOutputRoot 'database') -ReceiptDirectory $resolvedOutputRoot -BackupDirectory $BackupDirectory
}
Complete-ProductVersions @($apiVersion, $webVersion)
Write-Output "Identity API v$($apiVersion.Version), app v$($webVersion.Version): $resolvedOutputRoot"
} finally { $publishLock.ReleaseMutex(); $publishLock.Dispose() }
