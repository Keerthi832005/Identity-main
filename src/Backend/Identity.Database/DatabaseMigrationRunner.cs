using System.Reflection;
using System.Text.Json;
using DbUp;
using Microsoft.Data.SqlClient;

namespace Identity.Database;

// Project-owned deployment runner; preserve existing DbUp migration resource names.
public static class DatabaseMigrationRunner
{
    public static int Run(string[] args, Assembly assembly, string product, string connectionVariable)
    {
        var receipt = new MigrationReceipt { Product = product };
        Options? options = null;
        try
        {
            options = Parse(args, connectionVariable);
            var settings = new SqlConnectionStringBuilder(options.ConnectionString);
            if (string.IsNullOrWhiteSpace(settings.DataSource) || string.IsNullOrWhiteSpace(settings.InitialCatalog))
                throw new InvalidOperationException("An explicit SQL server and database are required.");
            AssertTarget(settings.DataSource, settings.InitialCatalog, options);
            receipt.Server = settings.DataSource;
            receipt.Database = settings.InitialCatalog;
            receipt.CheckOnly = options.CheckOnly;

            using var connection = new SqlConnection(settings.ConnectionString);
            connection.Open();
            using (var identity = connection.CreateCommand())
            {
                identity.CommandText = "SELECT DB_NAME()";
                if (!string.Equals((string?)identity.ExecuteScalar(), settings.InitialCatalog, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Connected database differs from the selected target.");
            }

            // Database-scoped SQL lock also coordinates publishers running on different machines.
            using (var acquire = connection.CreateCommand())
            {
                acquire.CommandText = "DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=N'FIN_PTS.SchemaMigration', @LockMode=N'Exclusive', @LockOwner=N'Session', @LockTimeout=0; SELECT @result;";
                if (Convert.ToInt32(acquire.ExecuteScalar()) < 0)
                    throw new InvalidOperationException("Another migration is running for this database. Retry after it finishes.");
            }

            var engine = DeployChanges.To.SqlDatabase(settings.ConnectionString)
                .WithScriptsEmbeddedInAssembly(assembly)
                .WithTransactionPerScript()
                .WithExecutionTimeout(TimeSpan.FromMinutes(5))
                .LogToNowhere()
                .Build();
            var before = ReadJournal(connection);
            var available = assembly.GetManifestResourceNames().Where(name => name.EndsWith(".sql", StringComparison.Ordinal)).ToHashSet(StringComparer.Ordinal);
            if (before.Any(row => !available.Contains(row.ScriptName)))
                throw new InvalidOperationException("Migration history contains scripts outside this release. Wrong product or older release refused.");
            receipt.PendingBefore = engine.GetScriptsToExecute().Select(script => script.Name).ToArray();
            Console.WriteLine($"{product}: {receipt.PendingBefore.Length} pending migration(s).");
            foreach (var name in receipt.PendingBefore) Console.WriteLine(name);
            if (options.CheckOnly || receipt.PendingBefore.Length == 0)
            {
                receipt.Success = true;
                receipt.State = options.CheckOnly ? "checked" : "up-to-date";
                return 0;
            }

            var userTables = UserTableCount(connection);
            if (before.Count == 0 && userTables != 0)
                throw new InvalidOperationException("Unjournaled nonempty database refused; review its existing schema first.");

            // A brand-new empty database has no existing application state to back up.
            receipt.Backup = userTables == 0 ? "empty-database-initialization" : Backup(connection, settings.InitialCatalog, options.BackupDirectory);
            receipt.State = "applying";
            WriteReceipt(options.ReceiptPath, receipt);
            var result = engine.PerformUpgrade();
            if (!result.Successful)
            {
                receipt.FailedScript = result.ErrorScript?.Name;
                throw new InvalidOperationException("Migration failed. Completed migrations remain journaled; stop deployment and review the failed script.");
            }
            var after = ReadJournal(connection);
            if (before.Any(row => !after.Contains(row)))
                throw new InvalidOperationException("Existing migration history changed; deployment stopped.");
            receipt.Applied = after.Where(row => !before.Contains(row)).Select(row => row.ScriptName).ToArray();
            if (engine.GetScriptsToExecute().Count != 0 || receipt.Applied.Length != receipt.PendingBefore.Length)
                throw new InvalidOperationException("Migration verification failed; deployment stopped.");
            receipt.Success = true;
            receipt.State = "applied";
            Console.WriteLine($"{product}: {receipt.Applied.Length} migration(s) applied and verified.");
            return 0;
        }
        catch (Exception error)
        {
            receipt.State = "failed";
            // Never print a connection string, SQL text, or provider exception containing data.
            receipt.Error = error is InvalidOperationException ? error.Message : $"Migration operation failed ({error.GetType().Name}).";
            Console.Error.WriteLine(receipt.Error);
            return 1;
        }
        finally
        {
            if (options != null) WriteReceipt(options.ReceiptPath, receipt);
        }
    }

    private static Options Parse(string[] args, string connectionVariable)
    {
        var result = new Options { ConnectionString = Environment.GetEnvironmentVariable(connectionVariable) ?? "" };
        for (var index = 0; index < args.Length; index++)
        {
            var name = args[index];
            if (name == "--check") { result.CheckOnly = true; continue; }
            if (!name.StartsWith("--", StringComparison.Ordinal) && index == 0) { result.ConnectionString = name; continue; }
            if (index + 1 >= args.Length) throw new InvalidOperationException("Missing migration option value.");
            var value = args[++index];
            switch (name)
            {
                case "--expected-server": result.ExpectedServer = value; break;
                case "--expected-database": result.ExpectedDatabase = value; break;
                case "--receipt": result.ReceiptPath = Path.GetFullPath(value); break;
                case "--backup-directory": result.BackupDirectory = value; break;
                default: throw new InvalidOperationException("Unknown migration option.");
            }
        }
        if (string.IsNullOrWhiteSpace(result.ConnectionString)) throw new InvalidOperationException($"Set {connectionVariable} before running migrations.");
        if ((result.ExpectedServer == null) != (result.ExpectedDatabase == null)) throw new InvalidOperationException("Expected server and database must be supplied together.");
        if (result.ReceiptPath != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(result.ReceiptPath)!);
            // Fail before touching SQL when the receipt cannot be written.
            using var stream = new FileStream(result.ReceiptPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        }
        return result;
    }

    private static void AssertTarget(string server, string database, Options options)
    {
        if (options.ExpectedServer != null &&
            (!string.Equals(server, options.ExpectedServer, StringComparison.OrdinalIgnoreCase) ||
             !string.Equals(database, options.ExpectedDatabase, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Connection string does not match the selected server/database. No migration was attempted.");
    }

    private static List<JournalRow> ReadJournal(SqlConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "IF OBJECT_ID(N'dbo.SchemaVersions',N'U') IS NOT NULL SELECT Id,ScriptName,Applied FROM dbo.SchemaVersions ORDER BY Id;";
        using var reader = command.ExecuteReader();
        var rows = new List<JournalRow>();
        while (reader.Read()) rows.Add(new JournalRow(reader.GetInt32(0), reader.GetString(1), reader.GetDateTime(2)));
        return rows;
    }

    private static int UserTableCount(SqlConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped=0";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static string Backup(SqlConnection connection, string database, string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            using var discover = connection.CreateCommand();
            discover.CommandText = "SELECT CONVERT(nvarchar(4000),SERVERPROPERTY('InstanceDefaultBackupPath'))";
            directory = discover.ExecuteScalar() as string;
        }
        if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("SQL Server backup directory is unavailable. Supply --backup-directory with a server-local path.");
        var safeName = string.Concat(database.Select(character => char.IsAsciiLetterOrDigit(character) ? character : '_'));
        var filename = $"{safeName}_before_publish_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}.bak";
        // The directory belongs to SQL Server, which may be on a different OS from this publisher.
        var separator = directory.Contains('\\') ? "\\" : "/";
        var path = directory.TrimEnd('\\', '/') + separator + filename;
        using var command = connection.CreateCommand();
        command.CommandTimeout = 300;
        command.CommandText = $"BACKUP DATABASE [{database.Replace("]", "]]", StringComparison.Ordinal)}] TO DISK=@path WITH COPY_ONLY,CHECKSUM; RESTORE VERIFYONLY FROM DISK=@path WITH CHECKSUM;";
        command.Parameters.AddWithValue("@path", path);
        command.ExecuteNonQuery();
        return path;
    }

    private static void WriteReceipt(string? path, MigrationReceipt receipt)
    {
        if (path == null) return;
        receipt.UpdatedAtUtc = DateTime.UtcNow;
        File.WriteAllText(path, JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class Options
    {
        public string ConnectionString { get; set; } = "";
        public bool CheckOnly { get; set; }
        public string? ExpectedServer { get; set; }
        public string? ExpectedDatabase { get; set; }
        public string? ReceiptPath { get; set; }
        public string? BackupDirectory { get; set; }
    }

    private sealed record JournalRow(int Id, string ScriptName, DateTime Applied);

    private sealed class MigrationReceipt
    {
        public string Product { get; set; } = "";
        public string? Server { get; set; }
        public string? Database { get; set; }
        public bool CheckOnly { get; set; }
        public bool Success { get; set; }
        public string State { get; set; } = "preflight";
        public string[] PendingBefore { get; set; } = [];
        public string[] Applied { get; set; } = [];
        public string? Backup { get; set; }
        public string? FailedScript { get; set; }
        public string? Error { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
