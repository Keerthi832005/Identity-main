# Read-only artifact helpers. No credentials, IIS changes or database writes.
function Get-AgentRolloutHash([string]$Text) {
    $sha=[Security.Cryptography.SHA256]::Create()
    try { [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Text))).Replace('-','') }
    finally { $sha.Dispose() }
}
function Assert-AgentRolloutNoLinks([string]$Path) {
    $item=Get-Item -LiteralPath $Path -Force
    while($item){if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Rollout paths may not contain links.'};$item=$item.Parent}
    if(@(Get-ChildItem -LiteralPath $Path -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count){throw 'Rollout content contains links.'}
}
function Get-AgentRolloutFiles([string]$Root) {
    Assert-AgentRolloutNoLinks $Root
    @(Get-ChildItem -LiteralPath $Root -Recurse -File | Sort-Object FullName | ForEach-Object {
        [ordered]@{Path=$_.FullName.Substring($Root.Length+1).Replace('\','/');Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
    })
}
function Assert-AgentRollout([string]$Path) {
    $root=[IO.Path]::GetFullPath($Path).TrimEnd('\','/')
    Assert-AgentRolloutNoLinks $root
    $manifest=Get-Content -LiteralPath (Join-Path $root 'rollout.json') -Raw | ConvertFrom-Json
    if($manifest.Owner -cne 'IAM.Agent.Rollout.v1' -or $manifest.SourceCommit -cnotmatch '^[a-f0-9]{40}$' -or
       $manifest.ReleaseId -cnotmatch '^\d{8}-\d{6}-[a-f0-9]{8}$' -or $manifest.ReleaseId -cne (Split-Path $root -Leaf)) {throw 'Unrecognized rollout manifest.'}
    $payload=Join-Path $root 'payload'
    $actual=@(Get-AgentRolloutFiles $payload)
    $expected=@($manifest.Files)
    if($actual.Count -ne $expected.Count -or $actual.Count -eq 0){throw 'Rollout file count mismatch.'}
    $map=@{}
    foreach($entry in $expected){
        if($entry.Path -cnotmatch '^(api|frontend|feed|tools|database)/[A-Za-z0-9_. /@+-]+$' -or
           $entry.Path.Contains('..') -or $entry.Sha256 -cnotmatch '^[A-F0-9]{64}$' -or $map.ContainsKey($entry.Path)){throw 'Unsafe or duplicate rollout file.'}
        $map[$entry.Path]=$entry.Sha256
    }
    foreach($entry in $actual){if(-not $map.ContainsKey($entry.Path) -or $map[$entry.Path] -cne $entry.Sha256){throw 'Rollout content changed after verification.'}}
    foreach($required in @('api/Identity.Api.dll','api/web.config','api/appsettings.json','frontend/index.html','frontend/config.json','frontend/web.config','feed/latest.json','feed/IAM.Agent.Setup.zip','tools/IAM.Agent.Service.exe','tools/release-public.pem')){
        if(-not $map.ContainsKey($required)){throw "Missing rollout component: $required"}
    }
    $feed=Join-Path $payload 'feed'
    $envelope=Get-Content -LiteralPath (Join-Path $feed 'latest.json') -Raw | ConvertFrom-Json
    $release=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($envelope.payload)) | ConvertFrom-Json
    if($release.packageFile -cnotmatch '^IAM\.Agent-\d{1,5}\.\d{1,5}\.\d{1,5}-win-x64\.zip$'){throw 'Invalid agent package path.'}
    $feedFiles=@(Get-ChildItem -LiteralPath $feed -Recurse -File)
    if($feedFiles.Count -ne 3 -or @($feedFiles | Where-Object { $_.Directory.FullName -ne $feed -or $_.Name -cnotin @('latest.json','IAM.Agent.Setup.zip',$release.packageFile) }).Count){throw 'Feed must contain only the signed manifest, version package and setup archive.'}
    & (Join-Path $payload 'tools/IAM.Agent.Service.exe') --verify (Join-Path $feed 'latest.json') (Join-Path $payload 'tools/release-public.pem') (Join-Path $feed $release.packageFile) | Out-Null
    if($LASTEXITCODE){throw 'Agent release signature or hash failed.'}
    return $manifest
}
