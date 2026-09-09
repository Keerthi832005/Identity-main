#requires -Version 5.1
# Real SQL tests, confined to exact disposable databases created by this run.
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$server = 'lpc:' + $env:COMPUTERNAME + '\SQLEXPRESS2022'
$created = [Collections.Generic.List[string]]::new()
$priorMigration = $env:IDENTITY_DATABASE_CONNECTION
$priorSeed = $env:IDENTITY_ADMIN_SEED_TEST_SQL_CONNECTION
$priorRoster = $env:IDENTITY_TEST_SQL_CONNECTION
$root = Join-Path $repo ('IAM/artifacts/admin-seed-tests/' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($root)
function Sql([string]$Database,[string]$Query) {
    $connection = [System.Data.SqlClient.SqlConnection]::new("Server=$server;Database=$Database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True")
    $connection.Open()
    try { $command=$connection.CreateCommand(); $command.CommandText=$Query; $command.CommandTimeout=120; try { $command.ExecuteScalar() } finally { $command.Dispose() } }
    finally { $connection.Dispose() }
}
try {
    & dotnet build (Join-Path $repo 'IAM/src/Backend/Identity.Database/Identity.Database.csproj') --nologo -v quiet
    if ($LASTEXITCODE) { throw 'Migration build failed.' }
    foreach ($kind in @('Administrator','Roster')) {
        $database = 'FIN_IAM_OrgMgmtTests_Seed_' + [guid]::NewGuid().ToString('N')
        $null = Sql master "CREATE DATABASE [$database]"
        $created.Add($database)
        $env:IDENTITY_DATABASE_CONNECTION = "Server=$server;Database=$database;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
        & dotnet (Join-Path $repo 'IAM/src/Backend/Identity.Database/bin/Debug/net10.0/Identity.Database.dll') --expected-server $server --expected-database $database *> (Join-Path $root "$kind-migration.log")
        if ($LASTEXITCODE) { throw 'Disposable migration failed.' }
        $env:IDENTITY_ADMIN_SEED_TEST_SQL_CONNECTION = $null
        $env:IDENTITY_TEST_SQL_CONNECTION = $null
        if ($kind -eq 'Administrator') {
            $env:IDENTITY_ADMIN_SEED_TEST_SQL_CONNECTION = $env:IDENTITY_DATABASE_CONNECTION
            $filter = 'FullyQualifiedName~DefaultAdministratorSeedTests'
        } else {
            $env:IDENTITY_TEST_SQL_CONNECTION = $env:IDENTITY_DATABASE_CONNECTION
            $filter = 'FullyQualifiedName~DefaultEmployeeSeedTests'
        }
        $previousErrorAction = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            & dotnet test (Join-Path $repo 'IAM/src/Backend/Tests/Identity.AdminCli.Tests/Identity.AdminCli.Tests.csproj') --filter $filter --nologo -v minimal *> (Join-Path $root "$kind-tests.log")
            $testExit = $LASTEXITCODE
        } finally { $ErrorActionPreference = $previousErrorAction }
        if ($testExit) { throw "Seed integration test failed. Inspect $root/$kind-tests.log" }
        Write-Output "PASS: $kind SQL integration tests."
    }
} finally {
    $env:IDENTITY_DATABASE_CONNECTION = $priorMigration
    $env:IDENTITY_ADMIN_SEED_TEST_SQL_CONNECTION = $priorSeed
    $env:IDENTITY_TEST_SQL_CONNECTION = $priorRoster
    [System.Data.SqlClient.SqlConnection]::ClearAllPools()
    foreach ($database in $created) {
        if ($database -notmatch '^FIN_IAM_OrgMgmtTests_Seed_[a-f0-9]{32}$') { throw 'Unexpected cleanup target.' }
        $null = Sql master "ALTER DATABASE [$database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$database]"
    }
}
Write-Output "PASS: disposable databases removed; business databases untouched. Logs: $root"
