# Rebuild setup from reviewed scripts without changing the signed worker release.
function Write-IamAgentSetupScripts([string]$SetupDirectory, [string]$ScriptsDirectory) {
    foreach ($name in @('Install-IAMAgent.ps1', 'Uninstall-IAMAgent.ps1', 'AgentInstallerUi.ps1')) {
        Copy-Item -LiteralPath (Join-Path $ScriptsDirectory $name) -Destination $SetupDirectory -Force
    }
    foreach ($name in @('fujitec-logo.png', 'favicon.ico')) {
        Copy-Item -LiteralPath (Join-Path $ScriptsDirectory ('../src/Frontend/public/' + $name)) -Destination $SetupDirectory -Force
    }
    [IO.File]::WriteAllText((Join-Path $SetupDirectory 'Install.cmd'), "@echo off`r`npowershell.exe -NoProfile -STA -File `"%~dp0Install-IAMAgent.ps1`"`r`npause`r`n")
    [IO.File]::WriteAllText((Join-Path $SetupDirectory 'README.txt'), "Extract this folder and run Install.cmd. Approve Windows administrator elevation, then use the FUJITEC IAM sign-in window with your employee code and IAM administrator password (not your terminal PIN). Enter your authenticator code when requested. Cancel closes approval without enrolling this computer. No terminal ID is requested. Windows execution policy, HTTPS, signed worker validation and IAM device trust checks remain required. Do not distribute signing private keys.`r`n")
}

function New-IamAgentSetupBundle([string]$AgentReleasePath, [string]$ScriptsDirectory, [string]$StagingDirectory, [string]$OutputZip) {
    if ((Test-Path -LiteralPath $StagingDirectory) -or (Test-Path -LiteralPath $OutputZip)) {
        throw 'Setup publication requires new staging and archive paths.'
    }
    $feed = Join-Path $AgentReleasePath 'feed'
    $manifest = Join-Path $feed 'latest.json'
    $key = Join-Path $AgentReleasePath 'setup/release-public.pem'
    $supervisor = Join-Path $AgentReleasePath 'supervisor/IAM.Agent.Service.exe'
    $envelope = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
    $release = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($envelope.payload)) | ConvertFrom-Json
    if ($release.packageFile -cnotmatch '^IAM\.Agent-\d{1,5}\.\d{1,5}\.\d{1,5}-win-x64\.zip$') { throw 'Invalid worker package name.' }
    $package = Join-Path $feed $release.packageFile
    & $supervisor --verify $manifest $key $package | Out-Null
    if ($LASTEXITCODE) { throw 'Existing signed worker validation failed.' }
    [void][IO.Directory]::CreateDirectory($StagingDirectory)
    foreach ($file in @($manifest, $key, $supervisor, $package, (Join-Path $AgentReleasePath 'setup/setup.json'))) {
        Copy-Item -LiteralPath $file -Destination $StagingDirectory
    }
    Write-IamAgentSetupScripts $StagingDirectory $ScriptsDirectory
    Compress-Archive -Path (Join-Path $StagingDirectory '*') -DestinationPath $OutputZip -CompressionLevel Optimal
}
