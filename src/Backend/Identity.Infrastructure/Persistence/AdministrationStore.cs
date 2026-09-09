using Identity.Application.Administration;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

internal sealed class AdministrationStore(IdentityDbContext dbContext)
    : IAdministrationStore, IUserCodeResolver, ICatalogCodeResolver, IAccessGrantLookup
{
    public ValueTask<RegisteredApplication?> FindApplication(
        long applicationId,
        CancellationToken cancellationToken) => dbContext.Applications.FindAsync([applicationId], cancellationToken);

    public Task<RegisteredApplication?> FindApplicationByCode(
        string applicationCode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationCode);
        var normalized = applicationCode.ToUpperInvariant();
        return dbContext.Applications.SingleOrDefaultAsync(
            application => EF.Property<string>(application, "NormalizedApplicationCode") == normalized,
            cancellationToken);
    }

    public ValueTask<ApplicationClient?> FindApplicationClient(
        long applicationClientId,
        CancellationToken cancellationToken) => dbContext.ApplicationClients.FindAsync(
            [applicationClientId],
            cancellationToken);

    public Task<ApplicationClient?> FindApplicationClientByClientId(
        string clientId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        var normalized = clientId.ToUpperInvariant();
        return dbContext.ApplicationClients.SingleOrDefaultAsync(
            client => EF.Property<string>(client, "NormalizedClientId") == normalized,
            cancellationToken);
    }

    public ValueTask<ApplicationModule?> FindModule(
        long applicationModuleId,
        CancellationToken cancellationToken) => dbContext.ApplicationModules.FindAsync(
            [applicationModuleId],
            cancellationToken);

    public Task<ApplicationModule?> FindModuleByCode(
        long applicationId,
        string moduleCode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleCode);
        var normalized = moduleCode.ToUpperInvariant();
        return dbContext.ApplicationModules.SingleOrDefaultAsync(
            module => module.ApplicationId == applicationId
                && EF.Property<string>(module, "NormalizedModuleCode") == normalized,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ApplicationModule>> ListSiblingModules(
        long applicationId,
        long? parentApplicationModuleId,
        CancellationToken cancellationToken) =>
        await dbContext.ApplicationModules
            .Where(module => module.ApplicationId == applicationId
                && module.ParentApplicationModuleId == parentApplicationModuleId)
            .OrderBy(module => module.DisplayOrder)
            .ThenBy(module => module.ApplicationModuleId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public ValueTask<ModuleCapability?> FindCapability(
        long moduleCapabilityId,
        CancellationToken cancellationToken) => dbContext.ModuleCapabilities.FindAsync(
            [moduleCapabilityId],
            cancellationToken);

    public Task<ModuleCapability?> FindCapabilityByCode(
        string capabilityCode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(capabilityCode);
        var normalized = capabilityCode.ToUpperInvariant();
        return dbContext.ModuleCapabilities.SingleOrDefaultAsync(
            capability => EF.Property<string>(capability, "NormalizedCapabilityCode") == normalized,
            cancellationToken);
    }

    public ValueTask<UserAccount?> FindUser(
        long userId,
        CancellationToken cancellationToken) => dbContext.UserAccounts.FindAsync([userId], cancellationToken);

    public async Task LockUserHierarchy(CancellationToken cancellationToken)
    {
        // A transaction-owned lock prevents concurrent A -> B / B -> A assignments.
        await dbContext.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = N'Identity.UserAccount.ManagementHierarchy',
                @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 10000;
            IF @result < 0 THROW 51008, 'User hierarchy is busy. Retry the change.', 1;
            """, cancellationToken);
    }

    public Task<UserAccount?> ReadUserForHierarchy(long userId, CancellationToken cancellationToken) =>
        dbContext.UserAccounts.AsNoTracking().SingleOrDefaultAsync(user => user.UserId == userId, cancellationToken);

    public async Task<IReadOnlyDictionary<string, long>> FindUserIdsByEmployeeCodes(
        IReadOnlyCollection<string> employeeCodes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(employeeCodes);
        var normalized = employeeCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalized.Length == 0)
        {
            return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        }

        var matches = await dbContext.UserAccounts
            .Where(user => normalized.Contains(EF.Property<string>(user, "NormalizedEmployeeCode")))
            .Select(user => new { user.EmployeeCode, user.UserId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return matches.ToDictionary(
            match => match.EmployeeCode,
            match => match.UserId,
            StringComparer.OrdinalIgnoreCase);
    }

    public Task<IReadOnlyDictionary<string, long>> FindApplicationIdsByCodes(
        IReadOnlyCollection<string> applicationCodes,
        CancellationToken cancellationToken) =>
        Resolve(
            applicationCodes,
            normalized => dbContext.Applications
                .Where(application =>
                    normalized.Contains(EF.Property<string>(application, "NormalizedApplicationCode")))
                .Select(application => new CodeMatch(application.ApplicationCode, application.ApplicationId)),
            cancellationToken);

    public Task<IReadOnlyDictionary<string, long>> FindModuleIdsByCodes(
        long applicationId,
        IReadOnlyCollection<string> moduleCodes,
        CancellationToken cancellationToken) =>
        Resolve(
            moduleCodes,
            normalized => dbContext.ApplicationModules
                .Where(module => module.ApplicationId == applicationId
                    && normalized.Contains(EF.Property<string>(module, "NormalizedModuleCode")))
                .Select(module => new CodeMatch(module.ModuleCode, module.ApplicationModuleId)),
            cancellationToken);

    public Task<IReadOnlyDictionary<string, long>> FindRoleIdsByCodes(
        long applicationId,
        IReadOnlyCollection<string> roleCodes,
        CancellationToken cancellationToken) =>
        Resolve(
            roleCodes,
            normalized => dbContext.Roles
                .Where(role => role.ApplicationId == applicationId
                    && normalized.Contains(EF.Property<string>(role, "NormalizedRoleCode")))
                .Select(role => new CodeMatch(role.RoleCode, role.RoleId)),
            cancellationToken);

    public Task<IReadOnlyDictionary<string, long>> FindCapabilityIdsByCodes(
        long applicationId,
        IReadOnlyCollection<string> capabilityCodes,
        CancellationToken cancellationToken) =>
        Resolve(
            capabilityCodes,
            normalized => dbContext.ModuleCapabilities
                .Where(capability => capability.ApplicationId == applicationId
                    && normalized.Contains(EF.Property<string>(capability, "NormalizedCapabilityCode")))
                .Select(capability => new CodeMatch(capability.CapabilityCode, capability.ModuleCapabilityId)),
            cancellationToken);

    private sealed record CodeMatch(string Code, long Id);

    private static async Task<IReadOnlyDictionary<string, long>> Resolve(
        IReadOnlyCollection<string> codes,
        Func<string[], IQueryable<CodeMatch>> query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(codes);
        var normalized = codes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalized.Length == 0)
        {
            return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        }

        var matches = await query(normalized).ToListAsync(cancellationToken).ConfigureAwait(false);
        return matches.ToDictionary(
            match => match.Code,
            match => match.Id,
            StringComparer.OrdinalIgnoreCase);
    }

    public ValueTask<UserApplicationAccess?> FindUserApplication(
        long userId,
        long applicationId,
        CancellationToken cancellationToken) => dbContext.UserApplications.FindAsync(
            [userId, applicationId],
            cancellationToken);

    public ValueTask<Role?> FindRole(long roleId, CancellationToken cancellationToken) =>
        dbContext.Roles.FindAsync([roleId], cancellationToken);

    public Task<Role?> FindRoleByCode(
        long applicationId,
        string roleCode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleCode);
        var normalized = roleCode.ToUpperInvariant();
        return dbContext.Roles.SingleOrDefaultAsync(
            role => role.ApplicationId == applicationId
                && EF.Property<string>(role, "NormalizedRoleCode") == normalized,
            cancellationToken);
    }

    public ValueTask<UserRoleAssignment?> FindUserRole(
        long userRoleId,
        CancellationToken cancellationToken) => dbContext.UserRoles.FindAsync([userRoleId], cancellationToken);

    public ValueTask<RolePermission?> FindRolePermission(
        long rolePermissionId,
        CancellationToken cancellationToken) => dbContext.RolePermissions.FindAsync(
            [rolePermissionId],
            cancellationToken);

    public ValueTask<UserPermissionOverride?> FindUserPermissionOverride(
        long userPermissionOverrideId,
        CancellationToken cancellationToken) => dbContext.UserPermissionOverrides.FindAsync(
            [userPermissionOverrideId],
            cancellationToken);

    public ValueTask<Device?> FindDevice(long deviceId, CancellationToken cancellationToken) =>
        dbContext.Devices.FindAsync([deviceId], cancellationToken);

    public ValueTask<bool> HasActiveUserRole(
        long userId,
        long applicationId,
        long roleId,
        CancellationToken cancellationToken) => new(dbContext.UserRoles.AnyAsync(
            assignment => assignment.UserId == userId
                && assignment.ApplicationId == applicationId
                && assignment.RoleId == roleId
                && assignment.RevokedAt == null,
            cancellationToken));

    public ValueTask<bool> HasAnyActiveUserRole(
        long userId,
        long applicationId,
        CancellationToken cancellationToken) => new(dbContext.UserRoles.AnyAsync(
            assignment => assignment.UserId == userId
                && assignment.ApplicationId == applicationId
                && assignment.RevokedAt == null,
            cancellationToken));

    public ValueTask<bool> HasOtherActiveUserRole(
        long userId,
        long applicationId,
        long excludedUserRoleId,
        CancellationToken cancellationToken) => new(dbContext.UserRoles.AnyAsync(
            assignment => assignment.UserId == userId
                && assignment.ApplicationId == applicationId
                && assignment.UserRoleId != excludedUserRoleId
                && assignment.RevokedAt == null,
            cancellationToken));

    public ValueTask<bool> HasActiveRolePermission(
        long applicationId,
        long roleId,
        long moduleCapabilityId,
        CancellationToken cancellationToken) => new(dbContext.RolePermissions.AnyAsync(
            permission => permission.ApplicationId == applicationId
                && permission.RoleId == roleId
                && permission.ModuleCapabilityId == moduleCapabilityId
                && permission.RevokedAt == null,
            cancellationToken));

    public ValueTask<bool> HasActiveUserPermissionOverride(
        long userId,
        long applicationId,
        long moduleCapabilityId,
        CancellationToken cancellationToken) => new(dbContext.UserPermissionOverrides.AnyAsync(
            permission => permission.UserId == userId
                && permission.ApplicationId == applicationId
                && permission.ModuleCapabilityId == moduleCapabilityId
                && permission.RevokedAt == null,
            cancellationToken));

    public async Task<EffectiveAuthorization?> GetEffectiveAuthorization(
        long userId,
        long applicationId,
        DateTime evaluatedAt,
        CancellationToken cancellationToken)
    {
        var access = await (
            from value in dbContext.UserApplications.AsNoTracking()
            join user in dbContext.UserAccounts on value.UserId equals user.UserId
            join application in dbContext.Applications on value.ApplicationId equals application.ApplicationId
            where value.UserId == userId
                && value.ApplicationId == applicationId
                && value.IsActive
                && user.IsActive
                && application.IsActive
            select value)
            .SingleOrDefaultAsync(cancellationToken);
        if (access is null)
        {
            return null;
        }

        var hasActiveRole = await dbContext.UserRoles.AsNoTracking().AnyAsync(
            assignment => assignment.UserId == userId
                && assignment.ApplicationId == applicationId
                && assignment.RevokedAt == null,
            cancellationToken);
        if (!hasActiveRole)
        {
            // Overrides customize an assigned role; they are never an independent
            // source of application permissions. This also neutralizes legacy
            // roleless overrides created before that invariant was enforced.
            return new EffectiveAuthorization(access.AuthorizationVersion, []);
        }

        var roleCapabilities = await (
            from assignment in dbContext.UserRoles
            join role in dbContext.Roles on assignment.RoleId equals role.RoleId
            join permission in dbContext.RolePermissions on role.RoleId equals permission.RoleId
            join capability in dbContext.ModuleCapabilities
                on permission.ModuleCapabilityId equals capability.ModuleCapabilityId
            join module in dbContext.ApplicationModules
                on capability.ApplicationModuleId equals module.ApplicationModuleId
            where assignment.UserId == userId
                && assignment.ApplicationId == applicationId
                && assignment.RevokedAt == null
                && role.ApplicationId == applicationId
                && role.IsActive
                && permission.ApplicationId == applicationId
                && permission.RevokedAt == null
                && capability.ApplicationId == applicationId
                && capability.IsActive
                && module.IsActive
            select capability.CapabilityCode)
            .Distinct()
            .ToListAsync(cancellationToken);

        var overrides = await (
            from permissionOverride in dbContext.UserPermissionOverrides
            join capability in dbContext.ModuleCapabilities
                on permissionOverride.ModuleCapabilityId equals capability.ModuleCapabilityId
            join module in dbContext.ApplicationModules
                on capability.ApplicationModuleId equals module.ApplicationModuleId
            where permissionOverride.UserId == userId
                && permissionOverride.ApplicationId == applicationId
                && permissionOverride.RevokedAt == null
                && (permissionOverride.ExpiresAt == null || permissionOverride.ExpiresAt > evaluatedAt)
                && capability.ApplicationId == applicationId
                && capability.IsActive
                && module.IsActive
            select new PermissionEffectProjection(
                capability.CapabilityCode,
                permissionOverride.Effect))
            .ToListAsync(cancellationToken);

        var denied = overrides
            .Where(value => value.Effect == Identity.Domain.Enums.PermissionEffect.Deny)
            .Select(value => value.CapabilityCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allowedOverrides = overrides
            .Where(value => value.Effect == Identity.Domain.Enums.PermissionEffect.Allow)
            .Select(value => value.CapabilityCode);
        var effective = roleCapabilities
            .Concat(allowedOverrides)
            .Where(value => !denied.Contains(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new EffectiveAuthorization(access.AuthorizationVersion, effective);
    }

    public async Task InvalidateRoleUsers(
        long applicationId,
        long roleId,
        long? actorUserId,
        DateTime updatedAt,
        CancellationToken cancellationToken)
    {
        var accesses = await (
            from access in dbContext.UserApplications
            join assignment in dbContext.UserRoles
                on new { access.UserId, access.ApplicationId }
                equals new { assignment.UserId, assignment.ApplicationId }
            where assignment.ApplicationId == applicationId
                && assignment.RoleId == roleId
                && assignment.RevokedAt == null
            select access)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var access in accesses)
        {
            access.InvalidateAuthorization(actorUserId, updatedAt);
        }
    }

    public async Task<int> RevokeSessionFamily(
        Guid tokenFamilyId,
        DateTime revokedAt,
        CancellationToken cancellationToken)
    {
        var tokens = await dbContext.RefreshTokens
            .Where(value => value.TokenFamilyId == tokenFamilyId && value.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in tokens) token.Revoke(revokedAt);
        return tokens.Count;
    }

    public void Add<TEntity>(TEntity entity) where TEntity : class => dbContext.Add(entity);

    public async ValueTask<bool> HasApplicationGrant(
        long userId,
        long applicationId,
        CancellationToken cancellationToken)
    {
        var grant = await FindUserApplication(userId, applicationId, cancellationToken)
            .ConfigureAwait(false);
        // A revoked grant is re-granted rather than treated as already present.
        return grant is { RevokedAt: null };
    }

    public ValueTask<bool> HasRoleAssignment(
        long userId,
        long applicationId,
        long roleId,
        CancellationToken cancellationToken) =>
        HasActiveUserRole(userId, applicationId, roleId, cancellationToken);
}
