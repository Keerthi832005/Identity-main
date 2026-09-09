#requires -Version 7.0
[CmdletBinding()]
param(
    # Omit for an automatic patch increment. Explicit versions must be newer than all retained releases.
    [ValidatePattern('^\d{1,5}\.\d{1,5}\.\d{1,5}$')][string]$Version,
    [Parameter(Mandatory)][string]$SigningKeyPath,
    [Parameter(Mandatory)][uri]$IdentityBaseUrl,
    [Parameter(Mandatory)][uri]$PtsOrigin,
    [switch]$CreateSigningKey
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'AgentSetup.Common.ps1')
$iam=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path (Split-Path $iam) 'scripts/PublishVersion.Common.ps1')
if($IdentityBaseUrl.Scheme -ne 'https' -or -not $IdentityBaseUrl.AbsoluteUri.EndsWith('/') -or $PtsOrigin.Scheme -ne 'https'){throw 'Use HTTPS URLs and a trailing slash for IdentityBaseUrl.'}
$publishLock=Enter-ProductPublishLock
try {
$id=[DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8)
$agentVersion=New-ProductVersionReservation (Split-Path $iam) 'iam-agent' $id $Version
$Version=$agentVersion.Version
$release=Join-Path $iam ('artifacts/agent-publish/'+$Version+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
[void](New-Item -ItemType Directory -Path $release)
$SigningKeyPath=[IO.Path]::GetFullPath($SigningKeyPath)
$rsa=[Security.Cryptography.RSA]::Create(3072)
try {
    if($CreateSigningKey){
        if(Test-Path -LiteralPath $SigningKeyPath){throw 'Signing key already exists; refusing to replace it.'}
        [void](New-Item -ItemType Directory -Path (Split-Path $SigningKeyPath) -Force)
        $acl=[Security.AccessControl.DirectorySecurity]::new();$acl.SetAccessRuleProtection($true,$false)
        foreach($sid in @('S-1-5-18','S-1-5-32-544',[Security.Principal.WindowsIdentity]::GetCurrent().User.Value)){$acl.AddAccessRule([Security.AccessControl.FileSystemAccessRule]::new([Security.Principal.SecurityIdentifier]::new($sid),'FullControl','ContainerInherit,ObjectInherit','None','Allow'))}
        Set-Acl -LiteralPath (Split-Path $SigningKeyPath) -AclObject $acl
        [IO.File]::WriteAllText($SigningKeyPath,$rsa.ExportPkcs8PrivateKeyPem())
    }else{$rsa.ImportFromPem([IO.File]::ReadAllText($SigningKeyPath))}
    if($rsa.KeySize -lt 3072){throw 'RSA-3072 or stronger required.'}
    $worker=Join-Path $release 'worker';$supervisor=Join-Path $release 'supervisor';$feed=Join-Path $release 'feed';$setup=Join-Path $release 'setup'
    foreach($dir in @($feed,$setup)){[void](New-Item -ItemType Directory -Path $dir)}
    foreach($item in @(@('IAM.Agent',$worker),@('IAM.Agent.Service',$supervisor))){
        & dotnet publish (Join-Path $iam ('src/Backend/'+$item[0]+'/'+$item[0]+'.csproj')) -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:Version=$Version --artifacts-path (Join-Path $release 'build') -o $item[1] --nologo
        if($LASTEXITCODE){throw 'Agent publication failed.'}
        Write-ProductVersionMetadata $agentVersion $item[1]
    }
    $packageName="IAM.Agent-$Version-win-x64.zip";$package=Join-Path $feed $packageName
    Compress-Archive -LiteralPath (Join-Path $worker 'IAM.Agent.exe') -DestinationPath $package -CompressionLevel Optimal
    $payload=[ordered]@{product='IAM.Agent';schema=1;version=$Version;runtime='win-x64';packageFile=$packageName;size=(Get-Item -LiteralPath $package).Length;sha256=(Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash;expiresAt=[DateTimeOffset]::UtcNow.AddDays(90).ToString('O');minimumSupervisorVersion='1.0.0'}
    $bytes=[Text.Encoding]::UTF8.GetBytes(($payload|ConvertTo-Json -Compress))
    $signature=$rsa.SignData($bytes,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pss)
    [IO.File]::WriteAllText((Join-Path $feed 'latest.json'),(@{payload=[Convert]::ToBase64String($bytes);signature=[Convert]::ToBase64String($signature)}|ConvertTo-Json))
    [IO.File]::WriteAllText((Join-Path $setup 'release-public.pem'),$rsa.ExportSubjectPublicKeyInfoPem())
    Copy-Item -LiteralPath (Join-Path $supervisor 'IAM.Agent.Service.exe') -Destination $setup
    Copy-Item -LiteralPath $package -Destination $setup
    Copy-Item -LiteralPath (Join-Path $feed 'latest.json') -Destination $setup
    Write-IamAgentSetupScripts $setup $PSScriptRoot
    $profile=@{identityBaseUrl=$IdentityBaseUrl.AbsoluteUri;siteOrigin=$PtsOrigin.GetLeftPart([UriPartial]::Authority);updateFeed=([uri]::new($IdentityBaseUrl,'api/v1/agents/releases/')).AbsoluteUri;identityClientId='identity-admin-web'}
    [IO.File]::WriteAllText((Join-Path $setup 'setup.json'),($profile|ConvertTo-Json))
    & (Join-Path $supervisor 'IAM.Agent.Service.exe') --verify (Join-Path $feed 'latest.json') (Join-Path $setup 'release-public.pem') $package
    if($LASTEXITCODE){throw 'Published signature/hash verification failed.'}
    Compress-Archive -Path (Join-Path $setup '*') -DestinationPath (Join-Path $feed 'IAM.Agent.Setup.zip') -CompressionLevel Optimal
    Write-ProductVersionMetadata $agentVersion $release
    Complete-ProductVersions @($agentVersion)
    Write-Output "Published IAM.Agent ${Version}: $release"
    Write-Output "Serve the feed directory through Identity AgentDistribution:RootPath. Keep the signing private key outside it."
} finally {$rsa.Dispose()}
} finally {$publishLock.ReleaseMutex();$publishLock.Dispose()}
