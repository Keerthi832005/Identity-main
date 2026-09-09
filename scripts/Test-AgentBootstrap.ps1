#requires -Version 5.1
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$template = Join-Path $PSScriptRoot '../src/Backend/Identity.Api/Endpoints/AgentInstallBootstrap.ps1'
$tokens = $null
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($template, [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw "Bootstrap syntax is invalid: $($errors[0].Message)" }
# Load only the pure validation/extraction function. Never download, enroll, or run setup in tests.
$function = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Expand-IamAgentSetup' }, $true)
if (-not $function) { throw 'Bootstrap extraction function was not found.' }
. ([scriptblock]::Create($function.Extent.Text))
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$temp = Join-Path ([IO.Path]::GetTempPath()) ('iam-bootstrap-test-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($temp)
$names = @('Install-IAMAgent.ps1', 'Uninstall-IAMAgent.ps1', 'Install.cmd', 'README.txt',
    'setup.json', 'latest.json', 'release-public.pem', 'IAM.Agent.Service.exe', 'IAM.Agent-1.0.0-win-x64.zip')
function New-Fixture([string]$Name, [string[]]$Entries, [switch]$Link) {
    $path = Join-Path $temp ($Name + '.zip')
    $archive = [IO.Compression.ZipFile]::Open($path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($name in $Entries) {
            $entry = $archive.CreateEntry($name)
            if ($Link -and $name -eq 'Install-IAMAgent.ps1') { $entry.ExternalAttributes = 0x400 }
            $writer = [IO.StreamWriter]::new($entry.Open())
            try { $writer.Write('synthetic fixture; never execute') } finally { $writer.Dispose() }
        }
    } finally { $archive.Dispose() }
    return $path
}
function Reject-Fixture([string]$Name, [string]$Zip, [string]$Hash, [string]$Reason) {
    $destination = Join-Path $temp ($Name + '-out')
    $rejected = $false
    try { Expand-IamAgentSetup $Zip $destination $Hash } catch {
        if (-not $_.Exception.Message.Contains($Reason)) { throw }
        $rejected = $true
    }
    if (-not $rejected -or (Test-Path -LiteralPath $destination)) { throw "Unsafe fixture accepted: $Name" }
    Write-Output "PASS: $Name rejected before extraction."
}
try {
    $valid = New-Fixture 'valid' $names
    $hash = (Get-FileHash -LiteralPath $valid -Algorithm SHA256).Hash
    $destination = Join-Path $temp 'valid-out'
    Expand-IamAgentSetup $valid $destination $hash
    if (@(Get-ChildItem -LiteralPath $destination -File).Count -ne 9) { throw 'Valid fixture extraction failed.' }
    Write-Output 'PASS: valid bundle extracted without executing the installer.'
    $modern = New-Fixture 'modern' ($names + @('AgentInstallerUi.ps1'))
    Expand-IamAgentSetup $modern (Join-Path $temp 'modern-out') (Get-FileHash -LiteralPath $modern).Hash
    if (@(Get-ChildItem -LiteralPath (Join-Path $temp 'modern-out') -File).Count -ne 10) { throw 'Modern bundle extraction failed.' }
    Write-Output 'PASS: modern UI bundle extracted without executing the installer.'
    $branded = New-Fixture 'branded' ($names + @('AgentInstallerUi.ps1', 'fujitec-logo.png', 'favicon.ico'))
    Expand-IamAgentSetup $branded (Join-Path $temp 'branded-out') (Get-FileHash -LiteralPath $branded).Hash
    if (@(Get-ChildItem -LiteralPath (Join-Path $temp 'branded-out') -File).Count -ne 12) { throw 'Branded bundle extraction failed.' }
    Write-Output 'PASS: branded UI bundle extracted without executing the installer.'
    $incomplete = New-Fixture 'incomplete-branding' ($names + @('fujitec-logo.png'))
    Reject-Fixture 'incomplete-branding' $incomplete (Get-FileHash -LiteralPath $incomplete).Hash 'Incomplete branded agent setup'
    $multiple = New-Fixture 'multiple-workers' ($names + @('IAM.Agent-2.0.0-win-x64.zip'))
    Reject-Fixture 'multiple-workers' $multiple (Get-FileHash -LiteralPath $multiple).Hash 'exactly one worker package'
    Reject-Fixture 'wrong-hash' $valid ('0' * 64) 'checksum mismatch'
    foreach ($bad in @('../escape.ps1', 'sub/Install-IAMAgent.ps1', 'extra.exe', 'README.txt')) {
        $entries = @($names)
        $entries[0] = $bad
        $name = 'bad-' + [guid]::NewGuid().ToString('N')
        $zip = New-Fixture $name $entries
        Reject-Fixture $name $zip (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash 'Unsafe or unexpected file'
    }
    $missing = New-Fixture 'missing' $names[1..8]
    Reject-Fixture 'missing' $missing (Get-FileHash -LiteralPath $missing -Algorithm SHA256).Hash 'Unexpected agent setup archive contents'
    $link = New-Fixture 'link' $names -Link
    Reject-Fixture 'link' $link (Get-FileHash -LiteralPath $link -Algorithm SHA256).Hash 'Unsafe or unexpected file'
} finally {
    $resolved = [IO.Path]::GetFullPath($temp)
    $parent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (-not $resolved.StartsWith($parent, [StringComparison]::OrdinalIgnoreCase) -or
        (Split-Path $resolved -Leaf) -cnotmatch '^iam-bootstrap-test-[a-f0-9]{32}$') { throw 'Unsafe test cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
