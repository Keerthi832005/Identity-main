#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$AgentReleasePath,
    [string]$SourceRef='HEAD',
    [ValidateSet('UAT','Live','Local')][string]$Environment,
    [string]$DatabaseServer,
    [string]$DatabaseName,
    [string]$BackupDirectory,
    [switch]$ArtifactsOnly
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'AgentRollout.Common.ps1')
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $repo 'scripts/PublishVersion.Common.ps1')
. (Join-Path $PSScriptRoot 'DatabasePublish.Common.ps1')
$databaseTarget=Resolve-IamPublishDatabaseTarget -Product IAM -Environment $Environment -DatabaseServer $DatabaseServer -DatabaseName $DatabaseName -ArtifactsOnly:$ArtifactsOnly
$id=[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8)
$root=Join-Path $repo ('IAM/artifacts/agent-rollout/'+$id)
$commit=(& git -C $repo rev-parse --verify --end-of-options ($SourceRef+'^{commit}')).Trim()
if($LASTEXITCODE -or $commit -cnotmatch '^[a-f0-9]{40}$'){throw 'Source ref must resolve to a committed revision.'}
$agent=[IO.Path]::GetFullPath($AgentReleasePath)
Assert-AgentRolloutNoLinks $agent
[void][IO.Directory]::CreateDirectory($root)
$payload=Join-Path $root 'payload';[void][IO.Directory]::CreateDirectory($payload)
function Run([string]$File,[string[]]$Arguments){& $File @Arguments;if($LASTEXITCODE){throw "Build step failed: $File"}}
Run 'git' @('-C',$repo,'archive','--format=zip',('-o'+(Join-Path $root 'source.zip')),$commit,'IAM','PTS/deploy/iis/iam-web.config')
$source=Join-Path $root 'source'
Expand-Archive -LiteralPath (Join-Path $root 'source.zip') -DestinationPath $source
$publishLock=Enter-ProductPublishLock
try {
$apiVersion=New-ProductVersionReservation $repo 'iam-api' $id -SourceRoot $source
$webVersion=New-ProductVersionReservation $repo 'iam-web' $id -SourceRoot $source
Run 'powershell.exe' @('-NoProfile','-File',(Join-Path $source 'IAM/scripts/Test-AgentBootstrap.ps1'))
Run 'powershell.exe' @('-NoProfile','-File',(Join-Path $source 'IAM/scripts/Test-AgentReinstall.ps1'))
Run 'powershell.exe' @('-NoProfile','-STA','-File',(Join-Path $source 'IAM/scripts/Test-AgentInstallerUi.ps1'),'-ScreenshotDirectory',(Join-Path $root 'installer-ui'))
Run 'dotnet' @('test',(Join-Path $source 'IAM/src/Backend/Tests/Identity.Api.Tests/Identity.Api.Tests.csproj'),'--nologo','--verbosity','minimal')
Run 'dotnet' @('publish',(Join-Path $source 'IAM/src/Backend/Identity.Api/Identity.Api.csproj'),'-c','Release',"-p:Version=$($apiVersion.Version)","-p:SourceRevisionId=$commit",'-o',(Join-Path $payload 'api'),'--nologo')
Write-ProductVersionMetadata $apiVersion (Join-Path $payload 'api') $commit
Run 'dotnet' @('publish',(Join-Path $source 'IAM/src/Backend/Identity.Database/Identity.Database.csproj'),'-c','Release',"-p:Version=$($apiVersion.Version)",'-o',(Join-Path $payload 'database'),'--nologo')
Write-ProductVersionMetadata $apiVersion (Join-Path $payload 'database') $commit
$front=Join-Path $source 'IAM/src/Frontend'
Push-Location $front
try{Run 'npm.cmd' @('ci','--no-audit','--no-fund');Run 'npm.cmd' @('test','--','--watch=false');Run 'npm.cmd' @('run','build')}
finally{Pop-Location}
Copy-Item -LiteralPath (Join-Path $front 'dist/IdentityAdministration.Web/browser') -Destination (Join-Path $payload 'frontend') -Recurse
Copy-Item -LiteralPath (Join-Path $source 'PTS/deploy/iis/iam-web.config') -Destination (Join-Path $payload 'frontend/web.config')
Write-ProductVersionMetadata $webVersion (Join-Path $payload 'frontend') $commit
# Assemble setup from this archived commit, not stale scripts in a previous worker release.
. (Join-Path $source 'IAM/scripts/AgentSetup.Common.ps1')
$feed=Join-Path $payload 'feed';[void][IO.Directory]::CreateDirectory($feed)
New-IamAgentSetupBundle $agent (Join-Path $source 'IAM/scripts') (Join-Path $root 'setup') (Join-Path $feed 'IAM.Agent.Setup.zip')
Copy-Item -LiteralPath (Join-Path $agent 'feed/latest.json') -Destination $feed
$envelope=Get-Content -LiteralPath (Join-Path $feed 'latest.json') -Raw | ConvertFrom-Json
$release=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($envelope.payload)) | ConvertFrom-Json
Copy-Item -LiteralPath (Join-Path $agent ('feed/'+$release.packageFile)) -Destination $feed
$tools=Join-Path $payload 'tools';[void][IO.Directory]::CreateDirectory($tools)
Copy-Item -LiteralPath (Join-Path $agent 'supervisor/IAM.Agent.Service.exe') -Destination $tools
Copy-Item -LiteralPath (Join-Path $agent 'setup/release-public.pem') -Destination $tools
$files=@(Get-AgentRolloutFiles $payload)
$manifest=[ordered]@{Owner='IAM.Agent.Rollout.v1';ReleaseId=$id;SourceCommit=$commit;ApiVersion=$apiVersion.Version;AppVersion=$webVersion.Version;AgentVersion=$release.version;SignerPurpose='Local pilot only; organization production approval not implied';CreatedAtUtc=[DateTime]::UtcNow.ToString('O');Files=$files}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root 'rollout.json') -Encoding utf8
[void](Assert-AgentRollout $root)
if($databaseTarget){Invoke-IamPublishedDatabaseMigration -Target $databaseTarget -DatabasePath (Join-Path $payload 'database') -ReceiptDirectory $root -BackupDirectory $BackupDirectory}
Complete-ProductVersions @($apiVersion,$webVersion)
Write-Output "Verified IAM-only rollout: $root (source $commit). IIS and agent activation remain separate."
} finally {$publishLock.ReleaseMutex();$publishLock.Dispose()}
