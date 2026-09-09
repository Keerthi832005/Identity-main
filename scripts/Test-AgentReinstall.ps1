#requires -Version 5.1
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'Install-IAMAgent.ps1'),[ref]$tokens,[ref]$errors)
if($errors.Count){throw $errors[0].Message}
# Load only pure/fixture-scoped helpers. Never execute the real installer or service actions.
foreach($name in @('WriteJson','Test-IamAgentCurrentInstall')){
    $function=$ast.Find({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq $name},$true)
    if(-not $function){throw 'Installer helper missing.'}
    . ([scriptblock]::Create($function.Extent.Text))
}
function Assert([bool]$Condition,[string]$Message){if(-not $Condition){throw $Message}}
$temp=Join-Path ([IO.Path]::GetTempPath()) ('iam-reinstall-test-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($temp)
try {
    $file=Join-Path $temp 'agent.json'
    WriteJson $file @{terminalId=2;mode='original'}
    Assert ((Get-Content -LiteralPath $file -Raw|ConvertFrom-Json).mode -eq 'original') 'First install JSON write failed.'
    # Reproduce the original bug in the actual supported Windows PowerShell runtime.
    if($PSVersionTable.PSEdition -eq 'Desktop'){
        $oldStage=Join-Path $temp 'old-replace.tmp';[IO.File]::WriteAllText($oldStage,'fixture')
        $failed=$false
        try{[IO.File]::Replace($oldStage,$file,$null)}catch{$failed=$_.Exception.InnerException -is [ArgumentException]}
        Assert $failed 'Expected Windows PowerShell null-to-empty-path regression was not reproduced.'
        [IO.File]::Delete($oldStage)
    }
    $before=Get-Content -LiteralPath $file -Raw|ConvertFrom-Json
    WriteJson $file @{terminalId=2;mode='replacement'}
    Assert ((Get-Content -LiteralPath $file -Raw|ConvertFrom-Json).mode -eq 'replacement') 'Reinstall replacement failed.'
    WriteJson $file $before
    Assert ((Get-Content -LiteralPath $file -Raw|ConvertFrom-Json).mode -eq 'original') 'Rollback write failed.'
    Write-Output 'PASS: first write, reproduced legacy replacement bug, repeated replacement and rollback.'
    $beforeHash=(Get-FileHash -LiteralPath $file).Hash
    $lock=[IO.File]::Open($file,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None)
    $failed=$false
    try{try{WriteJson $file @{mode='blocked'}}catch{$failed=$true}}finally{$lock.Dispose()}
    Assert ($failed -and (Get-FileHash -LiteralPath $file).Hash -ceq $beforeHash) 'Failed replacement damaged committed JSON.'
    Assert (@(Get-ChildItem -LiteralPath $temp -Filter '*.tmp').Count -eq 0) 'Staging file leaked.'
    Write-Output 'PASS: locked-file failure preserves saved JSON and removes only its staging file.'
    $install=Join-Path $temp 'install';[void][IO.Directory]::CreateDirectory((Join-Path $install 'supervisor'))
    $supervisor=Join-Path $temp 'IAM.Agent.Service.exe';[IO.File]::WriteAllText($supervisor,'synthetic executable bytes; never run')
    Copy-Item -LiteralPath $supervisor -Destination (Join-Path $install 'supervisor/IAM.Agent.Service.exe')
    $key=Join-Path $temp 'public.pem';[IO.File]::WriteAllText($key,'synthetic public key')
    $profile=[pscustomobject]@{identityBaseUrl='https://iam.test/identity/';siteOrigin='https://pts.test';updateFeed='https://iam.test/identity/api/v1/agents/releases/'}
    $existing=[pscustomobject]@{installationId='fixture-install';terminalId=2;identityBaseUrl=$profile.identityBaseUrl;siteOrigin=$profile.siteOrigin;updateFeed=$profile.updateFeed;updatePublicKey='synthetic public key';updateIntervalMinutes=60}
    $service=[pscustomobject]@{Status='Running'};$state=[pscustomobject]@{current='1.0.0'};$release=[pscustomobject]@{version='1.0.0'}
    $enrollment=[pscustomobject]@{terminalId=2};$identity=[pscustomobject]@{installationId='fixture-install'}
    $script:fixtureTerminal=2;$script:fixtureOffline=$false
    function Invoke-RestMethod {
        param($Uri,$Headers,$TimeoutSec)
        Assert ($Uri -eq 'http://127.0.0.1:43127/v1/identity' -and $Headers.Origin -eq 'https://pts.test' -and $Headers['X-IAM-Agent'] -eq '1') 'Unexpected identity request.'
        if($script:fixtureOffline){throw 'Synthetic connection failure'}
        [pscustomobject]@{siteOrigin='https://pts.test';hostname=[Environment]::MachineName;terminalId=$script:fixtureTerminal;agentVersion='1.0.0'}
    }
    function Current {Test-IamAgentCurrentInstall $service $existing $state $enrollment $identity $release $profile $install $supervisor $key}
    Assert (Current) 'Healthy identical installation not recognized.'
    $script:fixtureTerminal=9;Assert (-not (Current)) 'Wrong local identity accepted.';$script:fixtureTerminal=2
    $script:fixtureOffline=$true;Assert (-not (Current)) 'Unhealthy installation accepted.';$script:fixtureOffline=$false
    $service.Status='Stopped';Assert (-not (Current)) 'Stopped service accepted.';$service.Status='Running'
    $release.version='1.0.1';Assert (-not (Current)) 'Upgrade incorrectly skipped.';$release.version='1.0.0'
    $existing.updateFeed='https://other.test/';Assert (-not (Current)) 'Changed configuration ignored.';$existing.updateFeed=$profile.updateFeed
    [IO.File]::WriteAllText((Join-Path $install 'supervisor/IAM.Agent.Service.exe'),'tampered')
    Assert (-not (Current)) 'Different supervisor accepted.'
    Write-Output 'PASS: healthy repeat install recognized; wrong identity, offline/stopped service, upgrade, config and binary changes require repair.'
}finally{
    $resolved=[IO.Path]::GetFullPath($temp)
    $parent=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if(-not $resolved.StartsWith($parent,[StringComparison]::OrdinalIgnoreCase) -or (Split-Path $resolved -Leaf) -cnotmatch '^iam-reinstall-test-[a-f0-9]{32}$'){throw 'Unsafe fixture cleanup path.'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
