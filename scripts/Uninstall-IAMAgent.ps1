#requires -Version 5.1
#requires -RunAsAdministrator
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $env:ProgramFiles 'IAM.Agent'))
$marker=Join-Path $root '.iam-agent-owner'
if(-not (Test-Path -LiteralPath $marker) -or [IO.File]::ReadAllText($marker) -ne 'IAM.Agent.v1'){throw 'Not an owned IAM.Agent installation.'}
$service=Get-Service 'IAM.Agent' -ErrorAction SilentlyContinue
if($service){
    $info=Get-CimInstance Win32_Service -Filter "Name='IAM.Agent'"
    if($info.PathName -ne ('"'+(Join-Path $root 'supervisor\IAM.Agent.Service.exe')+'"')){throw 'Service path differs; refusing to stop another service.'}
    Stop-Service 'IAM.Agent';(Get-Service 'IAM.Agent').WaitForStatus('Stopped',[TimeSpan]::FromSeconds(30))
    & "$env:SystemRoot\System32\sc.exe" delete IAM.Agent
    if($LASTEXITCODE){throw 'Service removal failed.'}
}
Write-Output 'IAM.Agent service removed. Binaries and protected identity are retained for safe reinstall. Revoke the device in IAM if retiring this computer.'
