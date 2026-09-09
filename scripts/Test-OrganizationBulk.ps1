#requires -Version 5.1
[CmdletBinding()]
param([string]$SqlServer=('lpc:'+$env:COMPUTERNAME+'\SQLEXPRESS2022'))
$ErrorActionPreference='Stop'
$iam=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$database='FIN_IAM_BulkTests_'+[guid]::NewGuid().ToString('N')
$connection=[Data.SqlClient.SqlConnectionStringBuilder]::new()
$connection['Data Source']=$SqlServer
$connection['Initial Catalog']=$database
$connection['Integrated Security']=$true
$connection['Encrypt']=$true
$connection['TrustServerCertificate']=$true
$connection['Connect Timeout']=10
$master=[Data.SqlClient.SqlConnectionStringBuilder]::new($connection.ConnectionString)
$master['Initial Catalog']='master'
function Sql([string]$cs,[string]$statement){$c=[Data.SqlClient.SqlConnection]::new($cs);try{$c.Open();$q=$c.CreateCommand();$q.CommandText=$statement;$q.CommandTimeout=90;[void]$q.ExecuteNonQuery()}finally{$c.Dispose()}}
$oldTest=$env:IDENTITY_TEST_SQL_CONNECTION
$oldMigration=$env:IDENTITY_DATABASE_CONNECTION
$created=$false
$testFailed=$false
try {
    if($database -notmatch '^FIN_IAM_BulkTests_[a-f0-9]{32}$'){throw 'Unsafe generated database name'}
    Write-Host "Disposable organization bulk test database: $SqlServer / $database"
    Sql $master.ConnectionString "CREATE DATABASE [$database]"
    $created=$true
    $env:IDENTITY_DATABASE_CONNECTION=$connection.ConnectionString
    $env:IDENTITY_TEST_SQL_CONNECTION=$connection.ConnectionString
    & dotnet run --project (Join-Path $iam 'src/Backend/Identity.Database/Identity.Database.csproj')
    if($LASTEXITCODE){throw 'Migration failed'}
    & dotnet run --no-build --project (Join-Path $iam 'src/Backend/Identity.Database/Identity.Database.csproj')
    if($LASTEXITCODE){throw 'Migration replay failed'}
    & dotnet test (Join-Path $iam 'src/Backend/Tests/Identity.Infrastructure.Tests/Identity.Infrastructure.Tests.csproj') --filter 'FullyQualifiedName~OrganizationBulkCommitTests' --nologo
    if($LASTEXITCODE){throw 'Organization bulk SQL tests failed'}
} catch {
    $testFailed=$true
    throw
} finally {
    $env:IDENTITY_TEST_SQL_CONNECTION=$oldTest
    $env:IDENTITY_DATABASE_CONNECTION=$oldMigration
    [Data.SqlClient.SqlConnection]::ClearAllPools()
    if($created){
        try {Sql $master.ConnectionString "DROP DATABASE [$database]"; Write-Host "Removed disposable test database: $database"}
        catch {
            Write-Warning "Could not remove disposable test database $database on $SqlServer. Only this generated database may need later cleanup; no other database or session was changed."
            if(-not $testFailed){throw}
        }
    }
}
