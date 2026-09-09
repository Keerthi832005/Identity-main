#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory)][string]$RolloutPath)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'AgentRollout.Common.ps1')
[void](Assert-AgentRollout $RolloutPath)
Write-Output 'PASS: real rollout file list, hashes and signed agent package.'
$temp=Join-Path ([IO.Path]::GetTempPath()) ('iam-agent-rollout-test-'+[guid]::NewGuid().ToString('N'))
$fixture=Join-Path $temp '20260831-000000-12345678'
$payload=Join-Path $fixture 'payload'
[void][IO.Directory]::CreateDirectory((Join-Path $payload 'api'))
$file=Join-Path $payload 'api/test.txt';[IO.File]::WriteAllText($file,'verified fixture')
$base=@{Owner='IAM.Agent.Rollout.v1';ReleaseId='20260831-000000-12345678';SourceCommit=('a'*40);Files=@(Get-AgentRolloutFiles $payload)}
function Reject([string]$Name,$Manifest,[string]$Reason) {
    $Manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $fixture 'rollout.json') -Encoding UTF8
    $rejected=$false
    try{[void](Assert-AgentRollout $fixture)}catch{if(-not $_.Exception.Message.Contains($Reason)){throw};$rejected=$true}
    if(-not $rejected){throw "Unsafe fixture accepted: $Name"}
    Write-Output "PASS: rejected $Name."
}
try {
    Reject 'missing required components' $base 'Missing rollout component'
    [IO.File]::WriteAllText($file,'tampered fixture');Reject 'changed file bytes' $base 'content changed'
    [IO.File]::WriteAllText($file,'verified fixture')
    [IO.File]::WriteAllText((Join-Path $payload 'api/extra.txt'),'unexpected');Reject 'unexpected file' $base 'file count mismatch'
    Remove-Item -LiteralPath (Join-Path $payload 'api/extra.txt')
    $bad=$base.Clone();$bad.Owner='Other.Project';Reject 'foreign owner' $bad 'Unrecognized rollout manifest'
    $bad=$base.Clone();$bad.SourceCommit='HEAD';Reject 'unresolved source revision' $bad 'Unrecognized rollout manifest'
    $bad=$base.Clone();$bad.Files=@(@{Path='api/../../outside';Sha256=('A'*64)});Reject 'path traversal' $bad 'Unsafe or duplicate rollout file'
    $bad=$base.Clone();$bad.Files=@(@{Path='api/test.txt';Sha256='not-a-hash'});Reject 'invalid digest' $bad 'Unsafe or duplicate rollout file'
} finally {
    $resolved=[IO.Path]::GetFullPath($temp)
    if($resolved.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) -and (Split-Path $resolved -Leaf) -cmatch '^iam-agent-rollout-test-[a-f0-9]{32}$'){
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }else{throw 'Unsafe test cleanup path.'}
}
