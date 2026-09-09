# Run the IAM database tool from the matching published release without rebuilding it.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('UAT','Live','Local')][string]$Environment,
    [Parameter(Mandatory)][string]$DatabasePath,
    [string]$DatabaseServer,
    [string]$DatabaseName,
    [string]$BackupDirectory,
    [string]$ReceiptDirectory = (Join-Path $PSScriptRoot '../artifacts/migrations'),
    [switch]$CheckOnly
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'DatabasePublish.Common.ps1')
$target = Resolve-IamPublishDatabaseTarget -Product IAM -Environment $Environment -DatabaseServer $DatabaseServer -DatabaseName $DatabaseName
Invoke-IamPublishedDatabaseMigration -Target $target -DatabasePath $DatabasePath -ReceiptDirectory $ReceiptDirectory -BackupDirectory $BackupDirectory -CheckOnly:$CheckOnly
