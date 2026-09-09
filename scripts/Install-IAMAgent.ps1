#requires -Version 5.1
[CmdletBinding()]
param([PSCredential]$Credential)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$principal=[Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if(-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){
    # Installation is interactive: Windows UAC remains in control; no policy bypass.
    $process=Start-Process -FilePath 'powershell.exe' -Verb RunAs -WindowStyle Normal -ArgumentList @('-NoProfile','-STA','-File',('"'+$PSCommandPath+'"')) -PassThru -Wait
    exit $process.ExitCode
}
if(-not [Environment]::Is64BitOperatingSystem){throw 'IAM.Agent requires Windows x64.'}
. (Join-Path $PSScriptRoot 'AgentInstallerUi.ps1')
$root=Join-Path $env:ProgramFiles 'IAM.Agent'
$data=Join-Path $env:ProgramData 'IAM.Agent'
function NoLinks([string]$path){
    $item=[IO.DirectoryInfo]::new([IO.Path]::GetFullPath($path))
    while($null -ne $item){if($item.Exists -and ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Agent directories cannot contain junctions or links.'};$item=$item.Parent}
}
function OwnedDirectory([string]$path){
    NoLinks $path
    $marker=Join-Path $path '.iam-agent-owner'
    if((Test-Path -LiteralPath $path) -and @(Get-ChildItem -LiteralPath $path -Force).Count -gt 0 -and (-not (Test-Path -LiteralPath $marker) -or [IO.File]::ReadAllText($marker) -ne 'IAM.Agent.v1')){throw 'Installation directory is not owned by IAM.Agent.'}
    [void][IO.Directory]::CreateDirectory($path)
    $acl=[Security.AccessControl.DirectorySecurity]::new();$acl.SetAccessRuleProtection($true,$false)
    foreach($sid in @('S-1-5-18','S-1-5-32-544')){$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid),'FullControl','ContainerInherit,ObjectInherit','None','Allow'))}
    Set-Acl -LiteralPath $path -AclObject $acl
    [IO.File]::WriteAllText($marker,'IAM.Agent.v1')
}
function WriteJson([string]$path,$value){
    $temp=$path+'.'+[guid]::NewGuid().ToString('N')+'.tmp'
    try {
        [IO.File]::WriteAllText($temp,($value|ConvertTo-Json -Depth 12),[Text.UTF8Encoding]::new($false))
        # Windows PowerShell converts $null to an empty string for this .NET string
        # parameter. NullString supplies an actual null backup path to File.Replace.
        if(Test-Path -LiteralPath $path){[IO.File]::Replace($temp,$path,[System.Management.Automation.Language.NullString]::Value)}else{[IO.File]::Move($temp,$path)}
    } finally {
        # Only this invocation's staging file; preserve the committed destination on failure.
        if([IO.File]::Exists($temp)){[IO.File]::Delete($temp)}
    }
}
function Test-IamAgentCurrentInstall($service,$existing,$previousState,$enrollment,$identity,$release,$profile,[string]$root,[string]$supervisor,[string]$publicKeyPath){
    if(-not $service -or $service.Status -ne 'Running' -or -not $existing -or -not $previousState -or $previousState.current -ne $release.version){return $false}
    if($existing.installationId -ne $identity.installationId -or $existing.terminalId -ne $enrollment.terminalId -or
       $existing.identityBaseUrl -ne $profile.identityBaseUrl -or $existing.siteOrigin -ne $profile.siteOrigin -or
       $existing.updateFeed -ne $profile.updateFeed -or $existing.updatePublicKey -cne [IO.File]::ReadAllText($publicKeyPath) -or
       $existing.updateIntervalMinutes -ne 60){return $false}
    $installedSupervisor=Join-Path $root 'supervisor\IAM.Agent.Service.exe'
    if(-not (Test-Path -LiteralPath $installedSupervisor) -or (Get-FileHash -LiteralPath $installedSupervisor).Hash -cne (Get-FileHash -LiteralPath $supervisor).Hash){return $false}
    try {
        $local=Invoke-RestMethod -Uri 'http://127.0.0.1:43127/v1/identity' -Headers @{Origin=$profile.siteOrigin;'X-IAM-Agent'='1'} -TimeoutSec 3
        return $local.siteOrigin -eq $profile.siteOrigin -and $local.hostname -eq [Environment]::MachineName -and $local.terminalId -eq $enrollment.terminalId -and $local.agentVersion -eq $release.version
    } catch {return $false}
}
function Run([string]$exe,[string[]]$arguments){& $exe @arguments;if($LASTEXITCODE){throw "Executable failed: $([IO.Path]::GetFileName($exe))"}}
$profile=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'setup.json') -Raw|ConvertFrom-Json
$server=[uri]$profile.identityBaseUrl
$origin=[uri]$profile.siteOrigin
$feed=[uri]$profile.updateFeed
if($server.Scheme -ne 'https' -or $origin.Scheme -ne 'https' -or $feed.Scheme -ne 'https' -or $feed.Authority -ne $server.Authority -or $server.UserInfo -or $server.Query -or $server.Fragment -or -not $server.AbsolutePath.EndsWith('/')){throw 'Invalid HTTPS installation profile.'}
$supervisor=Join-Path $PSScriptRoot 'IAM.Agent.Service.exe'
$manifestPath=Join-Path $PSScriptRoot 'latest.json'
$publicKeyPath=Join-Path $PSScriptRoot 'release-public.pem'
$envelope=Get-Content -LiteralPath $manifestPath -Raw|ConvertFrom-Json
$release=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($envelope.payload))|ConvertFrom-Json
if($release.packageFile -notmatch '^IAM\.Agent-\d{1,5}\.\d{1,5}\.\d{1,5}-win-x64\.zip$'){throw 'Invalid package name.'}
$package=Join-Path $PSScriptRoot $release.packageFile
Run $supervisor @('--verify',$manifestPath,$publicKeyPath,$package)
$mutex=[Threading.Mutex]::new($false,'Global\IAM.Agent.Install')
$locked=$false;$client=$null;$session=$null;$stopped=$false;$previousState=$null;$existing=$null;$supervisorBackup=$null;$candidate=$null
try {
    $locked=$mutex.WaitOne(0);if(-not $locked){throw 'Another agent installation is running.'}
    OwnedDirectory $root;OwnedDirectory $data
    $configPath=Join-Path $data 'agent.json';$statePath=Join-Path $data 'version.json'
    if(Test-Path -LiteralPath $configPath){$existing=Get-Content -LiteralPath $configPath -Raw|ConvertFrom-Json;if($existing.identityBaseUrl -ne $server.AbsoluteUri -or $existing.siteOrigin -ne $profile.siteOrigin){throw 'Existing agent belongs to another site. Use an explicit administrator migration; installation did not change it.'}}
    if(Test-Path -LiteralPath $statePath){$previousState=Get-Content -LiteralPath $statePath -Raw|ConvertFrom-Json;if([version]$release.version -lt [version]$previousState.current){throw 'Installer downgrade refused.'}}
    $versions=Join-Path $root 'versions';[void][IO.Directory]::CreateDirectory($versions)
    $candidate=Join-Path $versions ($release.version+'.install-'+[guid]::NewGuid().ToString('N'))
    Run $supervisor @('--extract',$package,$candidate)
    $identityOutput=& (Join-Path $candidate 'IAM.Agent.exe') --create-identity $data
    if($LASTEXITCODE){throw 'Device identity creation failed.'}
    $identity=$identityOutput|ConvertFrom-Json
    $enrollmentPath=Join-Path $data 'enrollment.json'
    if(Test-Path -LiteralPath $enrollmentPath){
        $enrollment=Get-Content -LiteralPath $enrollmentPath -Raw|ConvertFrom-Json
        if($enrollment.installationId -ne $identity.installationId){throw 'Enrollment identity mismatch.'}
    }else{
        if(-not $Credential){$Credential=Show-IamAgentCredentialDialog -Server $server}
        if(-not $Credential){throw 'IAM administrator approval is required.'}
        Add-Type -AssemblyName System.Net.Http
        $handler=[Net.Http.HttpClientHandler]::new();$handler.AllowAutoRedirect=$false
        $client=[Net.Http.HttpClient]::new($handler);$client.Timeout=[TimeSpan]::FromSeconds(30)
        function Post([string]$path,$body){
            $content=[Net.Http.StringContent]::new(($body|ConvertTo-Json -Depth 6),[Text.Encoding]::UTF8,'application/json')
            try{$response=$client.PostAsync([uri]::new($server,$path),$content).GetAwaiter().GetResult();try{if(-not $response.IsSuccessStatusCode){throw "IAM request failed (HTTP $([int]$response.StatusCode)); no response credentials logged."};return ($response.Content.ReadAsStringAsync().GetAwaiter().GetResult()|ConvertFrom-Json)}finally{$response.Dispose()}}finally{$content.Dispose()}
        }
        $session=Post 'api/v1/auth/login' @{employeeCode=$Credential.UserName;password=$Credential.GetNetworkCredential().Password;clientId=$profile.identityClientId}
        if($session.mfaChallengeId){
            $code=Show-IamAgentMfaDialog -Server $server
            if(-not $code){throw 'IAM verification was cancelled. No device was enrolled.'}
            try{$session=Post 'api/v1/auth/mfa/complete' @{mfaChallengeId=$session.mfaChallengeId;code=([PSCredential]::new('mfa',$code)).GetNetworkCredential().Password}}finally{$code.Dispose();$code=$null}
        }
        if(-not $session.succeeded -or -not $session.accessToken){throw 'IAM did not approve administrator sign-in.'}
        $client.DefaultRequestHeaders.Authorization=[Net.Http.Headers.AuthenticationHeaderValue]::new('Bearer',$session.accessToken)
        $enrollment=Post 'api/v1/admin/agents/enroll' $identity
        if(-not $enrollment.trusted){throw 'This device is revoked or its trust has expired. An IAM administrator must review it; setup will not re-trust it.'}
        WriteJson $enrollmentPath $enrollment
    }
    $destination=Join-Path $versions $release.version
    if(Test-Path -LiteralPath $destination){
        if((Get-FileHash -LiteralPath (Join-Path $destination 'IAM.Agent.exe')).Hash -ne (Get-FileHash -LiteralPath (Join-Path $candidate 'IAM.Agent.exe')).Hash){throw 'A different package already uses this version; publish a new version.'}
    }else{
        # Both resolved paths must remain inside this install's versions directory.
        foreach($path in @($candidate,$destination)){if(-not [IO.Path]::GetFullPath($path).StartsWith([IO.Path]::GetFullPath($versions)+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe version move.'}}
        Move-Item -LiteralPath $candidate -Destination $destination
    }
    $service=Get-Service -Name 'IAM.Agent' -ErrorAction SilentlyContinue
    if($service){
        $serviceInfo=Get-CimInstance Win32_Service -Filter "Name='IAM.Agent'"
        if($serviceInfo.PathName -ne ('"'+(Join-Path $root 'supervisor\IAM.Agent.Service.exe')+'"')){throw 'Existing service is not this owned IAM.Agent installation.'}
        if(Test-IamAgentCurrentInstall $service $existing $previousState $enrollment $identity $release $profile $root $supervisor $publicKeyPath){
            Write-Output "IAM.Agent $($release.version) is already installed and running; IAM terminal $($enrollment.terminalId). No restart or enrollment change was needed."
            return
        }
        Stop-Service -Name 'IAM.Agent';(Get-Service 'IAM.Agent').WaitForStatus('Stopped',[TimeSpan]::FromSeconds(30));$stopped=$true
        $supervisorBackup=Join-Path $root ('supervisor\IAM.Agent.Service.exe.backup-'+[guid]::NewGuid().ToString('N'))
        Copy-Item -LiteralPath (Join-Path $root 'supervisor\IAM.Agent.Service.exe') -Destination $supervisorBackup
    }
    [void][IO.Directory]::CreateDirectory((Join-Path $root 'supervisor'))
    Copy-Item -LiteralPath $supervisor -Destination (Join-Path $root 'supervisor\IAM.Agent.Service.exe') -Force
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Uninstall-IAMAgent.ps1') -Destination $root -Force
    $config=@{siteOrigin=$profile.siteOrigin;installationId=$identity.installationId;terminalId=$enrollment.terminalId;identityBaseUrl=$server.AbsoluteUri;updateFeed=$profile.updateFeed;updatePublicKey=[IO.File]::ReadAllText($publicKeyPath);updateIntervalMinutes=60}
    WriteJson $configPath $config
    $previous=$null;if($previousState -and $previousState.current -ne $release.version){$previous=$previousState.current}
    WriteJson $statePath @{current=$release.version;previous=$previous;failed=$null}
    if(-not $service){New-Service -Name 'IAM.Agent' -DisplayName 'IAM Agent - machine inventory and terminal identity' -BinaryPathName ('"'+(Join-Path $root 'supervisor\IAM.Agent.Service.exe')+'"') -StartupType Automatic|Out-Null}
    Run "$env:SystemRoot\System32\sc.exe" @('config','IAM.Agent','start=','delayed-auto')
    Run "$env:SystemRoot\System32\sc.exe" @('failure','IAM.Agent','reset=','86400','actions=','restart/10000/restart/30000/restart/60000')
    Run "$env:SystemRoot\System32\sc.exe" @('failureflag','IAM.Agent','1')
    Start-Service -Name 'IAM.Agent'
    (Get-Service 'IAM.Agent').WaitForStatus('Running',[TimeSpan]::FromSeconds(30))
    $healthy=$false
    for($attempt=0;$attempt -lt 20;$attempt++){
        try{$local=Invoke-RestMethod -Uri 'http://127.0.0.1:43127/v1/identity' -Headers @{Origin=$profile.siteOrigin;'X-IAM-Agent'='1'} -TimeoutSec 3;if($local.terminalId -eq $enrollment.terminalId -and $local.agentVersion -eq $release.version){$healthy=$true;break}}catch{Start-Sleep -Seconds 1}
    }
    if(-not $healthy){throw 'Agent started but local identity is not ready. Inspect Windows Application event log and ProgramData\IAM.Agent status files.'}
    Write-Output "IAM.Agent installed: $env:COMPUTERNAME; IAM terminal $($enrollment.terminalId). Automatic startup, inventory and signed updates are enabled."
}catch{
    if($stopped -and $previousState){
        # Recover the old executable, configuration and committed worker together.
        Stop-Service 'IAM.Agent' -ErrorAction SilentlyContinue
        (Get-Service 'IAM.Agent').WaitForStatus('Stopped',[TimeSpan]::FromSeconds(30))
        if($supervisorBackup -and (Test-Path -LiteralPath $supervisorBackup)){Copy-Item -LiteralPath $supervisorBackup -Destination (Join-Path $root 'supervisor\IAM.Agent.Service.exe') -Force}
        if($existing){WriteJson (Join-Path $data 'agent.json') $existing}
        WriteJson (Join-Path $data 'version.json') $previousState
        Start-Service 'IAM.Agent' -ErrorAction SilentlyContinue
    }
    throw
}finally{
    if($client -and $session -and $session.refreshToken){try{[void](Post 'api/v1/auth/logout' @{refreshToken=$session.refreshToken;clientId=$profile.identityClientId})}catch{Write-Warning 'Could not revoke the setup login session; review it in IAM.'}}
    if($client){$client.Dispose()};$session=$null;$Credential=$null
    if($candidate -and (Test-Path -LiteralPath $candidate)){
        # Remove only the generated extraction directory from this invocation.
        $resolved=[IO.Path]::GetFullPath($candidate)
        $versionsRoot=[IO.Path]::GetFullPath((Join-Path $root 'versions')).TrimEnd('\')+'\'
        if($resolved.StartsWith($versionsRoot,[StringComparison]::OrdinalIgnoreCase) -and (Split-Path $resolved -Leaf) -cmatch '^\d{1,5}\.\d{1,5}\.\d{1,5}\.install-[a-f0-9]{32}$'){
            try{NoLinks $resolved;Remove-Item -LiteralPath $resolved -Recurse -Force}catch{Write-Warning 'Temporary worker extraction could not be cleaned. Review inactive .install directories.'}
        }
    }
    if($locked){$mutex.ReleaseMutex()};$mutex.Dispose()
}
