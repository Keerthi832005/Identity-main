using Identity.Application.Persistence;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : DbContext(options), IUnitOfWork
{
    [DbFunction("JSON_VALUE", IsBuiltIn = true)]
    public static string? JsonValue(string? document, string path) => throw new NotSupportedException("SQL projection only.");

    public DbSet<RegisteredApplication> Applications => Set<RegisteredApplication>();
    public DbSet<ApplicationClient> ApplicationClients => Set<ApplicationClient>();
    public DbSet<ApplicationModule> ApplicationModules => Set<ApplicationModule>();
    public DbSet<ModuleCapability> ModuleCapabilities => Set<ModuleCapability>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<OrganizationUnit> OrganizationUnits => Set<OrganizationUnit>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<State> States => Set<State>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<UserCredential> UserCredentials => Set<UserCredential>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<UserApplicationAccess> UserApplications => Set<UserApplicationAccess>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRoleAssignment> UserRoles => Set<UserRoleAssignment>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserPermissionOverride> UserPermissionOverrides => Set<UserPermissionOverride>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuthenticationAudit> AuthenticationAudits => Set<AuthenticationAudit>();
    public DbSet<UserMfaMethod> UserMfaMethods => Set<UserMfaMethod>();
    public DbSet<MfaChallenge> MfaChallenges => Set<MfaChallenge>();
    public DbSet<BulkImportBatch> BulkImportBatches => Set<BulkImportBatch>();
    public DbSet<BulkImportRow> BulkImportRows => Set<BulkImportRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        IdentityModelConfiguration.Apply(modelBuilder);
}
