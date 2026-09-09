using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Identity.Infrastructure.Persistence.Configurations;

internal static class IdentityModelConfiguration
{
    private static readonly IReadOnlyDictionary<string, int> StringLengths =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Algorithm"] = 30,
            ["ApplicationCode"] = 100,
            ["ApplicationName"] = 150,
            ["CapabilityCode"] = 150,
            ["CapabilityName"] = 150,
            ["ChallengeKeyId"] = 100,
            ["ClientId"] = 150,
            ["ClientName"] = 150,
            ["ClientType"] = 20,
            ["Description"] = 500,
            ["DeviceName"] = 200,
            ["DeviceType"] = 50,
            ["DisplayName"] = 200,
            ["Effect"] = 10,
            ["Email"] = 254,
            ["EmployeeCode"] = 50,
            ["EncryptionKeyId"] = 100,
            ["EventType"] = 50,
            ["FailureCode"] = 100,
            ["MethodName"] = 100,
            ["MethodType"] = 20,
            ["ModuleCode"] = 100,
            ["ModuleName"] = 150,
            ["Reason"] = 500,
            ["RoleCode"] = 50,
            ["RoleName"] = 100,
            ["TokenAudience"] = 200,
        };

    public static void Apply(ModelBuilder modelBuilder)
    {
        MapTables(modelBuilder);
        ConfigureKeys(modelBuilder);
        ConfigureComputedColumns(modelBuilder);
        ConfigureValues(modelBuilder);
        ConfigureRelationships(modelBuilder);
        ConfigureIndexes(modelBuilder);
        OrganizationModelConfiguration.Apply(modelBuilder);
        BulkDataModelConfiguration.Apply(modelBuilder);
        ConfigureCommonColumns(modelBuilder);
        AgentModelConfiguration.Apply(modelBuilder);
    }

    private static void MapTables(ModelBuilder modelBuilder)
    {
        Map<RegisteredApplication>(modelBuilder, "Application");
        Map<ApplicationClient>(modelBuilder, "ApplicationClient");
        Map<ApplicationModule>(modelBuilder, "ApplicationModule");
        Map<ModuleCapability>(modelBuilder, "ModuleCapability");
        Map<UserAccount>(modelBuilder, "UserAccount");
        Map<UserCredential>(modelBuilder, "UserCredential");
        Map<Device>(modelBuilder, "Device");
        Map<UserApplicationAccess>(modelBuilder, "UserApplication");
        Map<Role>(modelBuilder, "Role");
        Map<UserRoleAssignment>(modelBuilder, "UserRole");
        Map<RolePermission>(modelBuilder, "RolePermission");
        Map<UserPermissionOverride>(modelBuilder, "UserPermissionOverride");
        Map<RefreshToken>(modelBuilder, "RefreshToken");
        Map<AuthenticationAudit>(modelBuilder, "AuthenticationAudit");
        Map<UserMfaMethod>(modelBuilder, "UserMfaMethod");
        Map<MfaChallenge>(modelBuilder, "MfaChallenge");
    }

    private static void ConfigureKeys(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RegisteredApplication>().HasKey(x => x.ApplicationId);
        modelBuilder.Entity<ApplicationClient>().HasKey(x => x.ApplicationClientId);
        modelBuilder.Entity<ApplicationModule>().HasKey(x => x.ApplicationModuleId);
        modelBuilder.Entity<ModuleCapability>().HasKey(x => x.ModuleCapabilityId);
        modelBuilder.Entity<UserAccount>().HasKey(x => x.UserId);
        modelBuilder.Entity<UserCredential>().HasKey(x => x.UserCredentialId);
        modelBuilder.Entity<Device>().HasKey(x => x.DeviceId);
        modelBuilder.Entity<UserApplicationAccess>().HasKey(x => new { x.UserId, x.ApplicationId });
        modelBuilder.Entity<Role>().HasKey(x => x.RoleId);
        modelBuilder.Entity<UserRoleAssignment>().HasKey(x => x.UserRoleId);
        modelBuilder.Entity<RolePermission>().HasKey(x => x.RolePermissionId);
        modelBuilder.Entity<UserPermissionOverride>().HasKey(x => x.UserPermissionOverrideId);
        modelBuilder.Entity<RefreshToken>().HasKey(x => x.RefreshTokenId);
        modelBuilder.Entity<AuthenticationAudit>().HasKey(x => x.AuthenticationAuditId);
        modelBuilder.Entity<UserMfaMethod>().HasKey(x => x.UserMfaMethodId);
        modelBuilder.Entity<MfaChallenge>().HasKey(x => x.MfaChallengeId);

        modelBuilder.Entity<ApplicationClient>()
            .HasAlternateKey(x => new { x.ApplicationClientId, x.ApplicationId });
        modelBuilder.Entity<ApplicationModule>()
            .HasAlternateKey(x => new { x.ApplicationModuleId, x.ApplicationId });
        modelBuilder.Entity<ModuleCapability>()
            .HasAlternateKey(x => new { x.ModuleCapabilityId, x.ApplicationId });
        modelBuilder.Entity<Role>().HasAlternateKey(x => new { x.RoleId, x.ApplicationId });
        modelBuilder.Entity<Device>().HasAlternateKey(x => new { x.DeviceId, x.UserId });
        modelBuilder.Entity<UserMfaMethod>()
            .HasAlternateKey(x => new { x.UserMfaMethodId, x.UserId });
    }

    private static void ConfigureComputedColumns(ModelBuilder modelBuilder)
    {
        Computed(modelBuilder.Entity<RegisteredApplication>(), "NormalizedApplicationCode", "ApplicationCode");
        Computed(modelBuilder.Entity<RegisteredApplication>(), "NormalizedTokenAudience", "TokenAudience");
        Computed(modelBuilder.Entity<ApplicationClient>(), "NormalizedClientId", "ClientId");
        Computed(modelBuilder.Entity<ApplicationModule>(), "NormalizedModuleCode", "ModuleCode");
        Computed(modelBuilder.Entity<ModuleCapability>(), "NormalizedCapabilityCode", "CapabilityCode");
        Computed(modelBuilder.Entity<UserAccount>(), "NormalizedEmployeeCode", "EmployeeCode");
        Computed(modelBuilder.Entity<Role>(), "NormalizedRoleCode", "RoleCode");
    }

    private static void ConfigureValues(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationClient>().Property(x => x.ClientType).HasConversion<string>();
        modelBuilder.Entity<UserCredential>().Property(x => x.CredentialType).HasConversion<string>();
        modelBuilder.Entity<UserPermissionOverride>().Property(x => x.Effect).HasConversion<string>();
        modelBuilder.Entity<UserMfaMethod>().Property(x => x.MethodType).HasConversion<string>();
        modelBuilder.Entity<UserMfaMethod>().Property(x => x.LastAcceptedTimeStep)
            .IsConcurrencyToken();
        modelBuilder.Entity<MfaChallenge>().Property(x => x.MfaChallengeId)
            .HasDefaultValueSql("NEWSEQUENTIALID()");
    }

    private static void ConfigureRelationships(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>().HasOne(x => x.Manager).WithMany()
            .HasForeignKey(x => x.ManagerUserId).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<ApplicationClient>().HasOne<RegisteredApplication>().WithMany()
            .HasForeignKey(x => x.ApplicationId);
        modelBuilder.Entity<ApplicationModule>().HasOne<RegisteredApplication>().WithMany()
            .HasForeignKey(x => x.ApplicationId);
        modelBuilder.Entity<ApplicationModule>().HasOne<ApplicationModule>().WithMany()
            .HasForeignKey(x => new { x.ParentApplicationModuleId, x.ApplicationId })
            .HasPrincipalKey(x => new { x.ApplicationModuleId, x.ApplicationId });
        modelBuilder.Entity<ModuleCapability>().HasOne<ApplicationModule>().WithMany()
            .HasForeignKey(x => new { x.ApplicationModuleId, x.ApplicationId })
            .HasPrincipalKey(x => new { x.ApplicationModuleId, x.ApplicationId });
        modelBuilder.Entity<UserCredential>().HasOne<UserAccount>().WithMany()
            .HasForeignKey(x => x.UserId);
        modelBuilder.Entity<Device>().HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId);
        modelBuilder.Entity<Device>().HasOne<UserAccount>().WithMany()
            .HasForeignKey(x => x.TerminalServiceUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<UserApplicationAccess>().HasOne<UserAccount>().WithMany()
            .HasForeignKey(x => x.UserId);
        modelBuilder.Entity<UserApplicationAccess>().HasOne<RegisteredApplication>().WithMany()
            .HasForeignKey(x => x.ApplicationId);
        modelBuilder.Entity<Role>().HasOne<RegisteredApplication>().WithMany()
            .HasForeignKey(x => x.ApplicationId);
        modelBuilder.Entity<UserRoleAssignment>().HasOne<UserApplicationAccess>().WithMany()
            .HasForeignKey(x => new { x.UserId, x.ApplicationId });
        modelBuilder.Entity<UserRoleAssignment>().HasOne<Role>().WithMany()
            .HasForeignKey(x => new { x.RoleId, x.ApplicationId })
            .HasPrincipalKey(x => new { x.RoleId, x.ApplicationId });
        modelBuilder.Entity<RolePermission>().HasOne<Role>().WithMany()
            .HasForeignKey(x => new { x.RoleId, x.ApplicationId })
            .HasPrincipalKey(x => new { x.RoleId, x.ApplicationId });
        modelBuilder.Entity<RolePermission>().HasOne<ModuleCapability>().WithMany()
            .HasForeignKey(x => new { x.ModuleCapabilityId, x.ApplicationId })
            .HasPrincipalKey(x => new { x.ModuleCapabilityId, x.ApplicationId });
        modelBuilder.Entity<UserPermissionOverride>().HasOne<UserApplicationAccess>().WithMany()
            .HasForeignKey(x => new { x.UserId, x.ApplicationId });
        modelBuilder.Entity<UserPermissionOverride>().HasOne<ModuleCapability>().WithMany()
            .HasForeignKey(x => new { x.ModuleCapabilityId, x.ApplicationId })
            .HasPrincipalKey(x => new { x.ModuleCapabilityId, x.ApplicationId });
        modelBuilder.Entity<RefreshToken>().HasOne<UserApplicationAccess>().WithMany()
            .HasForeignKey(x => new { x.UserId, x.ApplicationId });
        modelBuilder.Entity<RefreshToken>().HasOne<ApplicationClient>().WithMany()
            .HasForeignKey(x => new { x.ApplicationClientId, x.ApplicationId })
            .HasPrincipalKey(x => new { x.ApplicationClientId, x.ApplicationId });
        modelBuilder.Entity<RefreshToken>().HasOne<Device>().WithMany()
            .HasForeignKey(x => new { x.DeviceId, x.UserId })
            .HasPrincipalKey(x => new { x.DeviceId, x.UserId });
        modelBuilder.Entity<RefreshToken>().HasOne<RefreshToken>().WithMany()
            .HasForeignKey(x => x.ReplacedByRefreshTokenId);
        modelBuilder.Entity<UserMfaMethod>().HasOne<UserAccount>().WithMany()
            .HasForeignKey(x => x.UserId);
        modelBuilder.Entity<MfaChallenge>().HasOne<UserApplicationAccess>().WithMany()
            .HasForeignKey(x => new { x.UserId, x.ApplicationId });
        modelBuilder.Entity<MfaChallenge>().HasOne<ApplicationClient>().WithMany()
            .HasForeignKey(x => new { x.ApplicationClientId, x.ApplicationId })
            .HasPrincipalKey(x => new { x.ApplicationClientId, x.ApplicationId });
        modelBuilder.Entity<MfaChallenge>().HasOne<UserMfaMethod>().WithMany()
            .HasForeignKey(x => new { x.UserMfaMethodId, x.UserId })
            .HasPrincipalKey(x => new { x.UserMfaMethodId, x.UserId });
        modelBuilder.Entity<MfaChallenge>().HasOne<Device>().WithMany()
            .HasForeignKey(x => new { x.DeviceId, x.UserId })
            .HasPrincipalKey(x => new { x.DeviceId, x.UserId });
    }

    private static void ConfigureIndexes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RegisteredApplication>().HasIndex("NormalizedApplicationCode").IsUnique();
        modelBuilder.Entity<RegisteredApplication>().HasIndex("NormalizedTokenAudience").IsUnique();
        modelBuilder.Entity<ApplicationClient>().HasIndex("NormalizedClientId").IsUnique();
        modelBuilder.Entity<ApplicationModule>().HasIndex("ApplicationId", "NormalizedModuleCode").IsUnique();
        modelBuilder.Entity<ModuleCapability>().HasIndex("NormalizedCapabilityCode").IsUnique();
        modelBuilder.Entity<UserAccount>().HasIndex("NormalizedEmployeeCode").IsUnique();
        modelBuilder.Entity<UserAccount>().HasIndex(x => x.ManagerUserId);
        modelBuilder.Entity<UserAccount>().HasOne(x => x.Department).WithMany()
            .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<Team>().HasAlternateKey(x => new { x.TeamId, x.DepartmentId })
            .HasName("UqTeamDepartmentMapping");
        modelBuilder.Entity<UserAccount>().HasOne(x => x.Team).WithMany()
            .HasForeignKey(x => new { x.TeamId, x.DepartmentId })
            .HasPrincipalKey(x => new { x.TeamId, x.DepartmentId }).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<UserAccount>().HasIndex(x => new { x.DepartmentId, x.TeamId })
            .HasDatabaseName("IxUserDepartmentTeam");
        modelBuilder.Entity<UserAccount>().HasOne(x => x.Branch).WithMany()
            .HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<UserAccount>().HasIndex(x => x.BranchId)
            .HasDatabaseName("IxUserAccountBranch");
        modelBuilder.Entity<Role>().HasIndex("ApplicationId", "NormalizedRoleCode").IsUnique();
        modelBuilder.Entity<UserCredential>().HasIndex(x => new { x.UserId, x.CredentialType })
            .IsUnique().HasFilter("[RevokedAt] IS NULL");
        modelBuilder.Entity<Device>().HasIndex(x => new { x.UserId, x.DeviceFingerprintHash })
            .IsUnique().HasFilter("[RevokedAt] IS NULL");
        modelBuilder.Entity<UserRoleAssignment>().HasIndex(x => new { x.UserId, x.ApplicationId, x.RoleId })
            .IsUnique().HasFilter("[RevokedAt] IS NULL");
        modelBuilder.Entity<RolePermission>().HasIndex(x => new { x.ApplicationId, x.RoleId, x.ModuleCapabilityId })
            .IsUnique().HasFilter("[RevokedAt] IS NULL");
        modelBuilder.Entity<UserPermissionOverride>()
            .HasIndex(x => new { x.UserId, x.ApplicationId, x.ModuleCapabilityId })
            .IsUnique().HasFilter("[RevokedAt] IS NULL");
        modelBuilder.Entity<RefreshToken>().HasIndex(x => x.TokenHash).IsUnique();
        modelBuilder.Entity<UserMfaMethod>().HasIndex(x => x.UserId)
            .IsUnique().HasFilter("[IsPrimary] = 1 AND [RevokedAt] IS NULL");
    }

    private static void ConfigureCommonColumns(ModelBuilder modelBuilder)
    {
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(x => x.GetProperties()))
        {
            if (property.ClrType == typeof(string) && StringLengths.TryGetValue(property.Name, out var length))
            {
                property.SetMaxLength(length);
            }

            if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
            {
                property.SetPrecision(3);
            }
        }

        modelBuilder.Entity<ApplicationClient>().Property(x => x.ClientSecretHash).HasMaxLength(32);
        modelBuilder.Entity<UserCredential>().Property(x => x.Salt).HasMaxLength(32);
        modelBuilder.Entity<UserCredential>().Property(x => x.SecretHash).HasMaxLength(64);
        modelBuilder.Entity<UserCredential>().Property(x => x.RequiresChange).HasDefaultValue(false);
        modelBuilder.Entity<UserCredential>().Property(x => x.RevokedAt).IsConcurrencyToken();
        modelBuilder.Entity<Device>().Property(x => x.DeviceFingerprintHash).HasMaxLength(32);
        modelBuilder.Entity<RefreshToken>().Property(x => x.TokenHash).HasMaxLength(32);
        modelBuilder.Entity<MfaChallenge>().Property(x => x.ChallengeHash).HasMaxLength(32);
    }

    private static void Map<TEntity>(ModelBuilder modelBuilder, string table)
        where TEntity : class => modelBuilder.Entity<TEntity>().ToTable(table, "Identity");

    private static void Computed<TEntity>(
        EntityTypeBuilder<TEntity> builder,
        string property,
        string sourceColumn)
        where TEntity : class => builder.Property<string>(property)
            .HasComputedColumnSql($"UPPER(LTRIM(RTRIM([{sourceColumn}])))", stored: true);
}
