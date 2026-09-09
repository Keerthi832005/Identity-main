# Identity database

The Identity database is independent from the PTS database. SQL files in `db/migrations/` are the production source of truth. EF Core maps to the deployed schema and never creates or applies migrations.

Apply migrations with a connection string argument:

```powershell
dotnet run --project src/Backend/Identity.Database/Identity.Database.csproj -- "Server=...;Database=Identity;Encrypt=True;..."
```

Alternatively set `IDENTITY_DATABASE_CONNECTION`. DbUp runs each script and its journal entry in one transaction, so a failed script does not leave a partially applied schema. It records script names and execution times in `dbo.SchemaVersions`; the current runner does not perform checksum verification. Never edit an applied migration; add a new ordered script and review its contents in source control.

Migration 0008 adds nullable user email/reporting manager; migration 0009 adds the normalized Organization module. See [Organization Module Database](../doc/Organization-Module-Database.md) for field descriptions, typed views, FK rules, and transactional provisioning requirements. Never clear or manually rewrite `dbo.SchemaVersions`; normal migration execution appends history only. These changes have not been applied to the business databases during development.
