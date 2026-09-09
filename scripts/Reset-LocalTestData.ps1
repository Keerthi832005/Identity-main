[CmdletBinding()]
param(
    [switch] $Apply,
    [string] $ConfirmDatabase = ''
)

# Check before opening a connection or creating any backup.
if ($Apply -and $ConfirmDatabase -cne 'FIN_IAM') {
    throw "Full reset removes ALL IAM users, credentials, configuration, and audit data. Specify -Apply -ConfirmDatabase FIN_IAM only after explicit reset approval."
}

# Standalone IAM utility, deliberately restricted to the local FIN_IAM test database.
# Without -Apply this performs inspection only. Backups are retained, never overwritten.
$ErrorActionPreference = 'Stop'
$connection = [System.Data.SqlClient.SqlConnection]::new(
    'Server=lpc:HOCOM18502627\SQLEXPRESS2022;Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5')
$transaction = $null
$databases = @('FIN_IAM')
$expectedTables = @(
    'Identity.Application', 'Identity.ApplicationClient', 'Identity.ApplicationModule',
    'Identity.AuthenticationAudit', 'Identity.Device', 'Identity.MfaChallenge',
    'Identity.ModuleCapability', 'Identity.RefreshToken', 'Identity.Role',
    'Identity.RolePermission', 'Identity.UserAccount', 'Identity.UserApplication',
    'Identity.UserCredential', 'Identity.UserMfaMethod', 'Identity.UserPermissionOverride',
    'Identity.UserRole'
)

function Read-SqlRows([string] $Sql) {
    $command = $connection.CreateCommand()
    $command.Transaction = $transaction
    $command.CommandTimeout = 30
    $command.CommandText = $Sql
    $reader = $null
    try {
        $reader = $command.ExecuteReader()
        while ($reader.Read()) {
            $row = [ordered]@{}
            for ($index = 0; $index -lt $reader.FieldCount; $index++) {
                $row[$reader.GetName($index)] = if ($reader.IsDBNull($index)) { $null } else { $reader.GetValue($index) }
            }
            [pscustomobject]$row
        }
    }
    finally {
        if ($reader) { $reader.Dispose() }
        $command.Dispose()
    }
}

function Invoke-SqlStatement([string] $Sql) {
    $command = $connection.CreateCommand()
    $command.Transaction = $transaction
    $command.CommandTimeout = 60
    $command.CommandText = $Sql
    try { [void]$command.ExecuteNonQuery() } finally { $command.Dispose() }
}

$foreignKeyQuery = @'
SELECT fk.name AS ConstraintName,
    QUOTENAME(ps.name) + '.' + QUOTENAME(pt.name) AS ParentTable,
    QUOTENAME(rs.name) + '.' + QUOTENAME(rt.name) AS ReferencedTable,
    fk.is_disabled AS IsDisabled, fk.is_not_trusted AS IsNotTrusted,
    fk.is_not_for_replication AS IsNotForReplication,
    'ALTER TABLE ' + QUOTENAME(ps.name) + '.' + QUOTENAME(pt.name)
        + ' DROP CONSTRAINT ' + QUOTENAME(fk.name) + ';' AS DropSql,
    'ALTER TABLE ' + QUOTENAME(ps.name) + '.' + QUOTENAME(pt.name)
        + ' WITH CHECK ADD CONSTRAINT ' + QUOTENAME(fk.name)
        + ' FOREIGN KEY (' + (
            SELECT STRING_AGG(CONVERT(nvarchar(max), QUOTENAME(c.name)), ',')
                WITHIN GROUP (ORDER BY fc.constraint_column_id)
            FROM sys.foreign_key_columns fc
            JOIN sys.columns c ON c.object_id=fc.parent_object_id AND c.column_id=fc.parent_column_id
            WHERE fc.constraint_object_id=fk.object_id)
        + ') REFERENCES ' + QUOTENAME(rs.name) + '.' + QUOTENAME(rt.name) + ' (' + (
            SELECT STRING_AGG(CONVERT(nvarchar(max), QUOTENAME(c.name)), ',')
                WITHIN GROUP (ORDER BY fc.constraint_column_id)
            FROM sys.foreign_key_columns fc
            JOIN sys.columns c ON c.object_id=fc.referenced_object_id AND c.column_id=fc.referenced_column_id
            WHERE fc.constraint_object_id=fk.object_id)
        + ') ON DELETE ' + REPLACE(fk.delete_referential_action_desc COLLATE DATABASE_DEFAULT, '_', ' ')
        + ' ON UPDATE ' + REPLACE(fk.update_referential_action_desc COLLATE DATABASE_DEFAULT, '_', ' ') + ';' AS CreateSql
FROM sys.foreign_keys fk
JOIN sys.tables pt ON pt.object_id=fk.parent_object_id
JOIN sys.schemas ps ON ps.schema_id=pt.schema_id
JOIN sys.tables rt ON rt.object_id=fk.referenced_object_id
JOIN sys.schemas rs ON rs.schema_id=rt.schema_id
ORDER BY ps.name, pt.name, fk.name;
'@

try {
    $connection.Open()
    $server = Read-SqlRows "SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')) AS ServerName, CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath')) AS BackupPath;"
    if ($server.ServerName -ne 'HOCOM18502627\SQLEXPRESS2022') { throw 'Unexpected SQL Server. Reset refused.' }
    $activeHosts = @(Get-CimInstance Win32_Process | Where-Object {
        $_.Name -in @('Identity.Api.exe', 'Identity.AdminCli.exe', 'PTS.Api.exe', 'PTS.Worker.exe') -or
        ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -match '(Identity\.(Api|AdminCli)|PTS\.(Api|Worker))')
    })
    if ($Apply -and $activeHosts.Count -gt 0) {
        throw 'A local IAM/PTS host is running. Stop the hosts and provisioning jobs before reset.'
    }

    $baseline = @{}
    foreach ($database in $databases) {
        Invoke-SqlStatement "USE [$database]; SET LOCK_TIMEOUT 10000; SET XACT_ABORT ON;"
        $tables = @(Read-SqlRows "SELECT s.name + '.' + t.name AS TableName, QUOTENAME(s.name)+'.'+QUOTENAME(t.name) AS QuotedName FROM sys.tables t JOIN sys.schemas s ON s.schema_id=t.schema_id WHERE t.is_ms_shipped=0 AND NOT(s.name='dbo' AND t.name='SchemaVersions') ORDER BY s.name,t.name;")
        if (@(Compare-Object ($expectedTables | Sort-Object) ($tables.TableName | Sort-Object)).Count -ne 0) {
            throw "Unexpected tables in $database. Reset refused."
        }
        $keys = @(Read-SqlRows $foreignKeyQuery)
        if (@($keys | Where-Object { $_.IsDisabled -or $_.IsNotTrusted -or $_.IsNotForReplication }).Count -gt 0) {
            throw "Nonstandard foreign key state in $database. Reset refused."
        }
        $quotedNames = @($tables.QuotedName)
        if (@($keys | Where-Object { $_.ParentTable -notin $quotedNames -or $_.ReferencedTable -notin $quotedNames }).Count -gt 0) {
            throw 'Foreign key outside the approved table set. Reset refused.'
        }
        $rowCount = 0L
        foreach ($table in $tables) { $rowCount += (Read-SqlRows "SELECT COUNT_BIG(*) AS [RowCount] FROM $($table.QuotedName);").RowCount }
        $journal = @(Read-SqlRows 'SELECT * FROM dbo.SchemaVersions ORDER BY Id;') | ConvertTo-Json -Depth 5 -Compress
        $triggers = @(Read-SqlRows 'SELECT name, parent_id, is_disabled, OBJECT_DEFINITION(object_id) AS Definition FROM sys.triggers ORDER BY name;') | ConvertTo-Json -Depth 5 -Compress
        $baseline[$database] = @{ Tables=$tables; Keys=$keys; Journal=$journal; Triggers=$triggers; RowCount=$rowCount }
        Write-Output "INSPECT $database identityTables=$($tables.Count) rows=$rowCount foreignKeys=$($keys.Count)"
    }
    if (-not $Apply) { Write-Output 'Inspection only. No data changed.'; return }

    # The verified recovery backup must finish before any truncate is attempted.
    $backupRun = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '_' + [Guid]::NewGuid().ToString('N')
    foreach ($database in $databases) {
        $backupPath = Join-Path $server.BackupPath "${database}_before_identity_test_reset_${backupRun}.bak"
        if (Test-Path -LiteralPath $backupPath) { throw 'Backup path already exists. Reset refused.' }
        $command = $connection.CreateCommand()
        $command.CommandTimeout = 180
        $command.CommandText = "BACKUP DATABASE [$database] TO DISK = @path WITH COPY_ONLY, CHECKSUM; RESTORE VERIFYONLY FROM DISK = @path WITH CHECKSUM;"
        [void]$command.Parameters.Add('@path', [System.Data.SqlDbType]::NVarChar, 4000)
        $command.Parameters['@path'].Value = $backupPath
        try { [void]$command.ExecuteNonQuery() } finally { $command.Dispose() }
        Write-Output "BACKUP_VERIFIED $backupPath"
    }

    # One local transaction covers the IAM reset. Constraints are rebuilt before commit;
    # append-only triggers and all migration-journal rows remain unchanged.
    $transaction = $connection.BeginTransaction()
    try {
        foreach ($database in $databases) {
            Invoke-SqlStatement "USE [$database];"
            $before = $baseline[$database]
            $currentKeys = @(Read-SqlRows $foreignKeyQuery) | ConvertTo-Json -Depth 5 -Compress
            if ($currentKeys -cne ($before.Keys | ConvertTo-Json -Depth 5 -Compress)) { throw 'Schema changed during preflight. Reset refused.' }
            foreach ($key in $before.Keys) { Invoke-SqlStatement $key.DropSql }
            foreach ($table in $before.Tables) { Invoke-SqlStatement "TRUNCATE TABLE $($table.QuotedName);" }
            foreach ($key in $before.Keys) { Invoke-SqlStatement $key.CreateSql }
            $afterKeys = @(Read-SqlRows $foreignKeyQuery) | ConvertTo-Json -Depth 5 -Compress
            if ($afterKeys -cne $currentKeys) { throw "Foreign key reconstruction mismatch in $database." }
            foreach ($table in $before.Tables) {
                if ((Read-SqlRows "SELECT COUNT_BIG(*) AS [RowCount] FROM $($table.QuotedName);").RowCount -ne 0) { throw 'Table is not empty.' }
            }
            $journal = @(Read-SqlRows 'SELECT * FROM dbo.SchemaVersions ORDER BY Id;') | ConvertTo-Json -Depth 5 -Compress
            $triggers = @(Read-SqlRows 'SELECT name, parent_id, is_disabled, OBJECT_DEFINITION(object_id) AS Definition FROM sys.triggers ORDER BY name;') | ConvertTo-Json -Depth 5 -Compress
            if ($journal -cne $before.Journal -or $triggers -cne $before.Triggers) { throw 'Preserved schema or migration state changed.' }
            $identities = @(Read-SqlRows "SELECT name FROM sys.identity_columns WHERE OBJECT_SCHEMA_NAME(object_id)<>'dbo' AND last_value IS NOT NULL;")
            if ($identities.Count -ne 0) { throw 'Identity table counters were not reset.' }
        }
        $transaction.Commit()
    }
    catch {
        try { $transaction.Rollback() } catch { Write-Warning 'Verify transaction rollback using the secured SQL logs.' }
        throw
    }
    finally { $transaction.Dispose(); $transaction = $null }

    foreach ($database in $databases) {
        Write-Output "CLEARED $database tables=$($baseline[$database].Tables.Count) priorRows=$($baseline[$database].RowCount) remainingRows=0; migration history, triggers, and trusted foreign keys preserved."
    }
}
finally { $connection.Dispose() }
