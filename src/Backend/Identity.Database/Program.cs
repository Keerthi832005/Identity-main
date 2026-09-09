using System.Reflection;
using Identity.Database;

return DatabaseMigrationRunner.Run(args, Assembly.GetExecutingAssembly(), "IAM", "IDENTITY_DATABASE_CONNECTION");
