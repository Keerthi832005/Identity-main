# IAM deployment migrations. Secrets stay in process environment variables.
function Resolve-IamPublishDatabaseTarget {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][ValidateSet('IAM')][string]$Product,
        [string]$Environment,
        [string]$DatabaseServer,
        [string]$DatabaseName,
        [switch]$ArtifactsOnly
    )
    if ($ArtifactsOnly) {
        if ($Environment -or $DatabaseServer -or $DatabaseName) { throw 'ArtifactsOnly cannot be combined with database targeting.' }
        return $null
    }
    if (-not $Environment) { throw 'Select -Environment for this publish, or use -ArtifactsOnly to build without database changes.' }
    if ($Environment -in @('UAT','Live')) {
        $expectedDatabase = if ($Environment -eq 'UAT') { 'Fujitec_IAM_UAT' } else { 'Fujitec_IAM_LIVE' }
        if (($DatabaseServer -and $DatabaseServer -ine 'FUJITECAPP2') -or ($DatabaseName -and $DatabaseName -ine $expectedDatabase)) {
            throw 'IAM environment does not match its approved server/database mapping.'
        }
        $DatabaseServer = 'FUJITECAPP2'
        $DatabaseName = $expectedDatabase
    }
    if (-not $DatabaseServer -or -not $DatabaseName) { throw 'Specify the selected environment DatabaseServer and DatabaseName; targets are never inferred from a secret.' }
    if ($Environment -notmatch '^[A-Za-z0-9_-]+$') { throw 'Invalid environment label.' }
    $variable = 'IDENTITY_DATABASE_CONNECTION'
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($variable,'Process'))) {
        throw "Set $variable securely before publishing. Do not supply passwords on the command line."
    }
    [pscustomobject]@{Product=$Product;Environment=$Environment;Server=$DatabaseServer;Database=$DatabaseName;ConnectionVariable=$variable}
}

function Invoke-IamPublishedDatabaseMigration {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]$Target,
        [Parameter(Mandatory)][string]$DatabasePath,
        [Parameter(Mandatory)][string]$ReceiptDirectory,
        [string]$BackupDirectory,
        [switch]$CheckOnly
    )
    if ($Target.Product -cne 'IAM') { throw 'This runner accepts only IAM targets.' }
    $tool = Join-Path $DatabasePath 'Identity.Database.dll'
    if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) { throw 'The matching published migration tool is missing.' }
    if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($Target.ConnectionVariable,'Process'))) { throw 'Migration connection is missing.' }
    [void][IO.Directory]::CreateDirectory($ReceiptDirectory)
    $receipt = Join-Path $ReceiptDirectory ("migration-$($Target.Product)-$($Target.Environment)-"+[guid]::NewGuid().ToString('N')+'.json')
    $arguments = @($tool,'--expected-server',$Target.Server,'--expected-database',$Target.Database,'--receipt',$receipt)
    if ($BackupDirectory) { $arguments += @('--backup-directory',$BackupDirectory) }
    if ($CheckOnly) { $arguments += '--check' }
    Write-Output "Checking $($Target.Product) migrations for $($Target.Environment): $($Target.Server) / $($Target.Database)"
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Database migration failed. Publishing/activation stopped. Receipt: $receipt" }
    if (-not (Test-Path -LiteralPath $receipt -PathType Leaf)) { throw 'Migration did not produce its verification receipt.' }
    $result = Get-Content -LiteralPath $receipt -Raw | ConvertFrom-Json
    if (-not $result.Success -or $result.Server -ine $Target.Server -or $result.Database -ine $Target.Database -or $result.Product -cne $Target.Product -or
        ($CheckOnly -and $result.State -ne 'checked') -or (-not $CheckOnly -and $result.State -notin @('applied','up-to-date'))) {
        throw 'Migration receipt did not confirm successful verification for the selected target.'
    }
    Write-Output "Migration verified: $($result.State). Receipt: $receipt"
}
