#requires -Version 5.1
#requires -RunAsAdministrator
[CmdletBinding()]
param([Parameter(Mandatory)][string]$RolloutPath,[switch]$ValidateOnly)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'AgentRollout.Common.ps1')
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$RolloutPath=[IO.Path]::GetFullPath($RolloutPath).TrimEnd('\','/')
$manifest=Assert-AgentRollout $RolloutPath
if($RolloutPath -ne (Join-Path $repo ('IAM/artifacts/agent-rollout/'+$manifest.ReleaseId))){throw 'Use a verified rollout directly under this repository artifact directory.'}
$releaseRoot='C:\inetpub\FIN_PTS\Releases'
$stateRoot='C:\ProgramData\FIN_PTS\IIS-Local'
$destination=Join-Path $releaseRoot ($manifest.ReleaseId+'-iam-agent')
$feedPath=Join-Path $destination 'AgentFeed'
$oldPaths=@{};$newPaths=@{};$oldFeed=$null;$switched=$false;$locked=$false;$manager=$null
$mutex=[Threading.Mutex]::new($false,'Local\FIN_PTS_LocalIis_Deployment')
$receipt=[ordered]@{Owner='IAM.Agent.Rollout.v1';Success=$false;SourceCommit=$manifest.SourceCommit;Release=$destination;DatabaseChanged=$false;PtsChanged=$false;ServiceInstalled=$false;ValidateOnly=[bool]$ValidateOnly}
function Protect([string]$Path,[string]$Pool='') {
    $acl=[Security.AccessControl.DirectorySecurity]::new();$acl.SetAccessRuleProtection($true,$false)
    foreach($sid in @('S-1-5-18','S-1-5-32-544')){$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid),'FullControl','ContainerInherit,ObjectInherit','None','Allow'))}
    if($Pool){$sid=([Security.Principal.NTAccount]::new('IIS APPPOOL\'+$Pool)).Translate([Security.Principal.SecurityIdentifier]);$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new($sid,'ReadAndExecute','ContainerInherit,ObjectInherit','None','Allow'))}
    Set-Acl -LiteralPath $Path -AclObject $acl
}
function Get-PreservedIisHash {
    $xml=[xml][IO.File]::ReadAllText("$env:SystemRoot/System32/inetsrv/config/applicationHost.config")
    foreach($name in @('IAM-Api','IAM-Web')){
        $node=$xml.SelectSingleNode("/configuration/system.applicationHost/sites/site[@name='$name']/application[@path='/']/virtualDirectory[@path='/']")
        if(-not $node){throw 'Expected IAM site is missing.'};$node.SetAttribute('physicalPath','NORMALIZED-IAM-ROLLOUT-PATH')
    }
    foreach($node in @($xml.SelectNodes("/configuration/system.applicationHost/applicationPools/add[@name='IAM-Api']/environmentVariables/add[@name='AgentDistribution__RootPath']"))){[void]$node.ParentNode.RemoveChild($node)}
    Get-AgentRolloutHash $xml.OuterXml
}
function Set-Feed($Pool,[AllowNull()]$Value) {
    $variables=$Pool.GetCollection('environmentVariables')
    foreach($entry in @($variables | Where-Object { $_['name'] -eq 'AgentDistribution__RootPath' })){[void]$variables.Remove($entry)}
    # Windows PowerShell wraps Join-Path output; IIS's COM setter needs a CLR string.
    if($null -ne $Value){$entry=$variables.CreateElement('add');$entry['name']='AgentDistribution__RootPath';$entry['value']=[string]$Value;[void]$variables.Add($entry)}
}
function Http([string]$Url,[int]$Expected=200,[string]$ExpectedHash='') {
    $handler=[Net.Http.HttpClientHandler]::new();$handler.AllowAutoRedirect=$false;$handler.UseProxy=$false
    $client=[Net.Http.HttpClient]::new($handler);$client.Timeout=[TimeSpan]::FromSeconds(12)
    try {
        $response=$client.GetAsync($Url).GetAwaiter().GetResult()
        try{
            if([int]$response.StatusCode -ne $Expected){throw "IAM HTTP verification failed: expected $Expected."}
            if($ExpectedHash){$bytes=$response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();$sha=[Security.Cryptography.SHA256]::Create();try{$hash=[BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-','')}finally{$sha.Dispose()};if($hash -cne $ExpectedHash){throw 'Downloaded content differs from verified release.'}}
        }finally{$response.Dispose()}
    }finally{$client.Dispose()}
}
try {
    $receipt.Step='OwnershipPreflight'
    $locked=$mutex.WaitOne(0);if(-not $locked){throw 'Another local IIS deployment is running.'}
    Add-Type -AssemblyName System.Net.Http
    $owner=Get-Content -LiteralPath (Join-Path $stateRoot 'deployment.json') -Raw | ConvertFrom-Json
    if($owner.Owner -cne 'FIN_PTS.LocalIis.v1'){throw 'Local IIS ownership mismatch.'}
    Assert-AgentRolloutNoLinks $releaseRoot
    if(Test-Path -LiteralPath $destination){throw 'Immutable destination already exists.'}
    Add-Type -Path "$env:SystemRoot/System32/inetsrv/Microsoft.Web.Administration.dll"
    $manager=[Microsoft.Web.Administration.ServerManager]::new()
    $before=Get-PreservedIisHash
    foreach($name in @('IAM-Api','IAM-Web')){
        $site=$manager.Sites[$name];$pool=$manager.ApplicationPools[$name]
        $binding=if($name -eq 'IAM-Api'){'127.0.0.1:18100:'}else{'127.0.0.1:443:iam.local.fujitecindia.com'}
        $protocol=if($name -eq 'IAM-Api'){'http'}else{'https'}
        if(-not $site -or -not $pool -or $site.State -ne 'Started' -or $site.Applications.Count -ne 1 -or
           $site.Applications['/'].VirtualDirectories.Count -ne 1 -or $site.Applications['/'].ApplicationPoolName -ne $name -or
           $site.Bindings.Count -ne 1 -or $site.Bindings[0].BindingInformation -ne $binding -or $site.Bindings[0].Protocol -ne $protocol -or
           @($manager.Sites | Where-Object Name -ne $name | ForEach-Object Applications | Where-Object ApplicationPoolName -eq $name).Count){throw 'IAM site/pool/binding ownership drift.'}
        $suffix=if($name -eq 'IAM-Api'){'IAM\Api'}else{'IAM\Frontend'}
        $old=[IO.Path]::GetFullPath($site.Applications['/'].VirtualDirectories['/'].PhysicalPath)
        if(-not $old.StartsWith($releaseRoot+'\',[StringComparison]::OrdinalIgnoreCase) -or -not $old.EndsWith('\'+$suffix,[StringComparison]::OrdinalIgnoreCase)){throw 'IAM physical path is outside the owned layout.'}
        Assert-AgentRolloutNoLinks $old
        $oldPaths[$name]=$old;$newPaths[$name]=Join-Path $destination $suffix
    }
    $pool=$manager.ApplicationPools['IAM-Api'];$variables=$pool.GetCollection('environmentVariables')
    $setting=@($variables | Where-Object { $_['name'] -eq 'AgentDistribution__RootPath' })
    if($setting.Count -gt 1){throw 'Duplicate distribution override.'}
    if($setting.Count){$oldFeed=[string]$setting[0]['value']}
    $connections=@($variables | Where-Object { $_['name'] -eq 'ConnectionStrings__Identity' })
    if($connections.Count -ne 1){throw 'Expected existing IAM database override is missing; no credentials will be inferred.'}
    $cs=[Data.SqlClient.SqlConnectionStringBuilder]::new([string]$connections[0]['value'])
    if($cs.InitialCatalog -cne 'FIN_IAM' -or -not $cs.IntegratedSecurity){throw 'Only the existing local FIN_IAM integrated-security database is supported.'}
    if($cs.DataSource -ine ('lpc:'+$env:COMPUTERNAME+'\SQLEXPRESS2022')){throw 'Unexpected IAM SQL instance; review deployment separately.'}
    $cs['Connect Timeout']=8
    $receipt.Step='DatabaseConnection'
    $connection=[Data.SqlClient.SqlConnection]::new($cs.ConnectionString)
    try{
        $connection.Open();$query=$connection.CreateCommand();$query.CommandTimeout=8
        $receipt.Step='MigrationPreflight'
        $query.CommandText="SET LOCK_TIMEOUT 5000; SELECT CASE WHEN OBJECT_ID(N'Identity.AgentInstallation',N'U') IS NOT NULL AND OBJECT_ID(N'Identity.AgentControlState',N'U') IS NOT NULL AND OBJECT_ID(N'Identity.AgentUpdateRequest',N'U') IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.SchemaVersions WHERE ScriptName=N'Identity.Database.Migrations.0014_agent_update_requests.sql') THEN 1 ELSE 0 END"
        if([int]$query.ExecuteScalar() -ne 1){$receipt.Blocker='Migration0014Required';throw 'Agent migrations through 0014 are required. Back up FIN_IAM and run its reviewed migration before deployment. No database was changed.'}
    }finally{$connection.Dispose();$cs=$null;$connections=$null}
    $receipt.Step='ExistingHttpReadiness'
    Http 'https://iam.local.fujitecindia.com/identity/health/ready'
    $receipt.PreviousPaths=$oldPaths
    if($ValidateOnly){$receipt.Success=$true;Write-Output 'PASS: IAM-only rollout preflight. No live changes applied.';return}
    $receipt.Step='BackupAndCopy'
    $backup='IAM-Agent-'+$manifest.ReleaseId
    & "$env:SystemRoot/System32/inetsrv/appcmd.exe" add backup $backup | Out-Null
    if($LASTEXITCODE){throw 'IIS backup failed.'};$receipt.IisBackup=$backup
    [void][IO.Directory]::CreateDirectory($destination);Protect $destination
    [void][IO.Directory]::CreateDirectory((Join-Path $destination 'IAM'))
    Copy-Item -LiteralPath (Join-Path $RolloutPath 'payload/api') -Destination $newPaths['IAM-Api'] -Recurse
    Copy-Item -LiteralPath (Join-Path $RolloutPath 'payload/frontend') -Destination $newPaths['IAM-Web'] -Recurse
    Copy-Item -LiteralPath (Join-Path $RolloutPath 'payload/feed') -Destination $feedPath -Recurse
    # Preserve current application configuration, frontend URLs, license and proxy rules.
    Get-ChildItem -LiteralPath $oldPaths['IAM-Api'] -Filter 'appsettings*.json' -File | ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $newPaths['IAM-Api'] -Force}
    foreach($name in @('config.json','web.config')){Copy-Item -LiteralPath (Join-Path $oldPaths['IAM-Web'] $name) -Destination $newPaths['IAM-Web'] -Force}
    Protect $newPaths['IAM-Api'] 'IAM-Api';Protect $newPaths['IAM-Web'] 'IAM-Web';Protect $feedPath 'IAM-Api'
    [void](Assert-AgentRollout $RolloutPath)
    if((Get-PreservedIisHash) -cne $before){throw 'IIS changed during preparation; no switch applied.'}
    $receipt.Step='ScopedSwitch'
    foreach($name in $newPaths.Keys){$manager.Sites[$name].Applications['/'].VirtualDirectories['/'].PhysicalPath=$newPaths[$name]}
    Set-Feed $manager.ApplicationPools['IAM-Api'] $feedPath
    $manager.CommitChanges();$switched=$true
    [void]$manager.ApplicationPools['IAM-Api'].Recycle()
    $receipt.Step='ReleaseHttpVerification'
    $healthy=$false
    for($attempt=0;$attempt -lt 6;$attempt++){try{Http 'https://iam.local.fujitecindia.com/identity/health/ready';$healthy=$true;break}catch{Start-Sleep -Seconds 2}}
    if(-not $healthy){throw 'New IAM API did not become ready.'}
    Http 'https://iam.local.fujitecindia.com/agents' 200 (Get-FileHash -LiteralPath (Join-Path $newPaths['IAM-Web'] 'index.html')).Hash
    Http 'https://ptsapp.local.fujitecindia.com/identity/api/v1/agents/download' 200 (Get-FileHash -LiteralPath (Join-Path $feedPath 'IAM.Agent.Setup.zip')).Hash
    Http 'https://ptsapp.local.fujitecindia.com/identity/api/v1/agents/releases/latest.json' 200 (Get-FileHash -LiteralPath (Join-Path $feedPath 'latest.json')).Hash
    Http 'https://iam.local.fujitecindia.com/identity/api/v1/admin/agents' 401
    if((Get-PreservedIisHash) -cne $before){throw 'Unrelated IIS configuration changed; inspect deployment receipt.'}
    $receipt.Success=$true
    Write-Output 'PASS: IAM-only release and agent download deployed. PTS, database and device trust unchanged. Install the agent separately with administrator sign-in.'
}catch{
    $receipt.ErrorType=$_.Exception.GetType().FullName;$receipt.ErrorLine=$_.InvocationInfo.ScriptLineNumber
    if($switched){
        try{
            $manager.Dispose();$manager=[Microsoft.Web.Administration.ServerManager]::new()
            foreach($name in $newPaths.Keys){if($manager.Sites[$name].Applications['/'].VirtualDirectories['/'].PhysicalPath -ne $newPaths[$name]){throw 'IAM paths changed concurrently; automatic rollback refused.'}}
            $active=@($manager.ApplicationPools['IAM-Api'].GetCollection('environmentVariables') | Where-Object { $_['name'] -eq 'AgentDistribution__RootPath' })
            if($active.Count -ne 1 -or $active[0]['value'] -ne $feedPath){throw 'Distribution configuration changed concurrently; rollback refused.'}
            foreach($name in $oldPaths.Keys){$manager.Sites[$name].Applications['/'].VirtualDirectories['/'].PhysicalPath=$oldPaths[$name]}
            Set-Feed $manager.ApplicationPools['IAM-Api'] $oldFeed
            $manager.CommitChanges();[void]$manager.ApplicationPools['IAM-Api'].Recycle();$receipt.RolledBack=$true
        }catch{$receipt.RollbackFailed=$true}
    }
    throw
}finally{
    $receipt.CompletedAtUtc=[DateTime]::UtcNow.ToString('O')
    $receipt | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $RolloutPath 'deployment-result.json') -Encoding UTF8
    if($manager){$manager.Dispose()};if($locked){$mutex.ReleaseMutex()};$mutex.Dispose()
}
