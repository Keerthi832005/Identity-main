using Identity.Application.Administration;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

public sealed class AdministrationDiscoveryStore(IdentityDbContext dbContext)
    : IAdministrationDiscoveryStore
{
    public async Task<AdministrationDashboard> GetDashboard(
        int recentLimit,
        DateTime generatedAt,
        CancellationToken cancellationToken)
    {
        var since = generatedAt.AddHours(-24);
        var applicationCount = await dbContext.Applications
            .AsNoTracking()
            .CountAsync(cancellationToken);
        var activeApplicationCount = await dbContext.Applications
            .AsNoTracking()
            .CountAsync(application => application.IsActive, cancellationToken);
        var userCount = await dbContext.UserAccounts
            .AsNoTracking()
            .CountAsync(cancellationToken);
        var activeUserCount = await dbContext.UserAccounts
            .AsNoTracking()
            .CountAsync(user => user.IsActive, cancellationToken);
        var activeSessionCount = await dbContext.RefreshTokens
            .AsNoTracking()
            .CountAsync(token => token.ConsumedAt == null
                && token.RevokedAt == null
                && token.ExpiresAt > generatedAt,
                cancellationToken);
        var failedAuthenticationCount = await dbContext.AuthenticationAudits
            .AsNoTracking()
            .CountAsync(audit => !audit.Succeeded && audit.OccurredAt >= since, cancellationToken);
        var authenticationTrend = await GetAuthenticationTrend(generatedAt, cancellationToken);
        var lockedOutUserCount = await dbContext.UserAccounts
            .AsNoTracking()
            .CountAsync(user => user.IsActive && user.LockoutEndAt != null && user.LockoutEndAt > generatedAt,
                cancellationToken);
        var usersWithoutMfaCount = await dbContext.UserAccounts
            .AsNoTracking()
            .Where(user => user.IsActive)
            .CountAsync(user => !dbContext.UserMfaMethods.Any(method =>
                method.UserId == user.UserId
                && method.IsEnabled
                && method.IsVerified
                && method.RevokedAt == null),
                cancellationToken);
        var expiringClientSecretCount = await dbContext.ApplicationClients
            .AsNoTracking()
            .CountAsync(client => client.IsActive
                && client.ExpiresAt != null
                && client.ExpiresAt > generatedAt
                && client.ExpiresAt <= generatedAt.AddDays(30),
                cancellationToken);
        var recentApplications = await dbContext.Applications
            .AsNoTracking()
            .OrderByDescending(application => application.CreatedAt)
            .ThenByDescending(application => application.ApplicationId)
            .Take(recentLimit)
            .Select(application => new AdministrationApplicationSummary(
                application.ApplicationId,
                application.ApplicationCode,
                application.ApplicationName,
                application.TokenAudience,
                application.IsActive,
                application.CreatedAt,
                application.UpdatedAt,
                application.Description,
                dbContext.ApplicationClients.Count(client => client.ApplicationId == application.ApplicationId),
                dbContext.ApplicationModules.Count(module => module.ApplicationId == application.ApplicationId),
                dbContext.Roles.Count(role => role.ApplicationId == application.ApplicationId),
                dbContext.UserApplications.Count(access =>
                    access.ApplicationId == application.ApplicationId && access.RevokedAt == null)))
            .ToListAsync(cancellationToken);
        var recentUsers = await dbContext.UserAccounts
            .AsNoTracking()
            .OrderByDescending(user => user.CreatedAt)
            .ThenByDescending(user => user.UserId)
            .Take(recentLimit)
            .Select(user => new AdministrationUserSummary(
                user.UserId,
                user.EmployeeCode,
                user.DisplayName,
                user.IsActive,
                user.SecurityVersion,
                user.LastLoginAt,
                user.LockoutEndAt,
                user.CreatedAt,
                user.UpdatedAt,
                user.Email,
                user.ManagerUserId,
                user.Manager == null ? null : user.Manager.DisplayName,
                user.DepartmentId,
                user.Department == null ? null : user.Department.Unit.UnitName,
                user.TeamId,
                user.Team == null ? null : user.Team.Unit.UnitName,
                user.BranchId,
                user.Branch == null ? null : user.Branch.Unit.UnitName,
                user.Branch == null ? null : user.Branch.StateId,
                user.Branch == null ? null : user.Branch.State.Unit.UnitName,
                user.Branch == null ? null : user.Branch.State.RegionId,
                user.Branch == null ? null : user.Branch.State.Region.Unit.UnitName,
                user.Branch == null ? null : user.Branch.State.Region.CountryId,
                user.Branch == null ? null : user.Branch.State.Region.Country.Unit.UnitName))
            .ToListAsync(cancellationToken);
        var recentAuditEvents = await dbContext.AuthenticationAudits
            .AsNoTracking()
            .OrderByDescending(audit => audit.OccurredAt)
            .ThenByDescending(audit => audit.AuthenticationAuditId)
            .Take(recentLimit)
            .Select(audit => new AdministrationAuditSummary(
                audit.AuthenticationAuditId,
                audit.UserId,
                audit.ApplicationId,
                audit.EventType,
                audit.Succeeded,
                audit.FailureCode,
                audit.CorrelationId,
                audit.OccurredAt,
                dbContext.UserAccounts.Where(user => user.UserId == audit.UserId)
                    .Select(user => user.DisplayName).FirstOrDefault(),
                dbContext.UserAccounts.Where(user => user.UserId == audit.UserId)
                    .Select(user => user.EmployeeCode).FirstOrDefault(),
                dbContext.Applications.Where(application => application.ApplicationId == audit.ApplicationId)
                    .Select(application => application.ApplicationName).FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return new AdministrationDashboard(
            applicationCount,
            activeApplicationCount,
            userCount,
            activeUserCount,
            activeSessionCount,
            failedAuthenticationCount,
            recentApplications,
            recentUsers,
            recentAuditEvents,
            generatedAt,
            authenticationTrend,
            lockedOutUserCount,
            usersWithoutMfaCount,
            expiringClientSecretCount);
    }

    private async Task<IReadOnlyList<AdministrationAuthenticationTrendHour>> GetAuthenticationTrend(
        DateTime generatedAt,
        CancellationToken cancellationToken)
    {
        // Bucket boundaries are hour-aligned so DATEDIFF(HOUR, ...) offsets map onto clock hours.
        var currentHour = new DateTime(
            generatedAt.Year, generatedAt.Month, generatedAt.Day, generatedAt.Hour, 0, 0, DateTimeKind.Utc);
        var windowStart = currentHour.AddHours(-23);
        var buckets = await dbContext.AuthenticationAudits
            .AsNoTracking()
            .Where(audit => audit.OccurredAt >= windowStart && audit.OccurredAt < generatedAt)
            .GroupBy(audit => EF.Functions.DateDiffHour(windowStart, audit.OccurredAt))
            .Select(group => new
            {
                HourOffset = group.Key,
                Succeeded = group.Count(audit => audit.Succeeded),
                Failed = group.Count(audit => !audit.Succeeded),
            })
            .ToDictionaryAsync(value => value.HourOffset, cancellationToken);
        return Enumerable.Range(0, 24)
            .Select(offset =>
            {
                var bucket = buckets.GetValueOrDefault(offset);
                return new AdministrationAuthenticationTrendHour(
                    windowStart.AddHours(offset),
                    bucket?.Succeeded ?? 0,
                    bucket?.Failed ?? 0);
            })
            .ToArray();
    }

    public async Task<PagedAdministrationApplications> SearchApplications(
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Applications.AsNoTracking();
        if (search is not null)
        {
            query = query.Where(application =>
                application.ApplicationCode.Contains(search)
                || application.ApplicationName.Contains(search)
                || application.TokenAudience.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(application => application.ApplicationName)
            .ThenBy(application => application.ApplicationId)
            .Skip(skip)
            .Take(take)
            .Select(application => new AdministrationApplicationSummary(
                application.ApplicationId,
                application.ApplicationCode,
                application.ApplicationName,
                application.TokenAudience,
                application.IsActive,
                application.CreatedAt,
                application.UpdatedAt,
                application.Description,
                dbContext.ApplicationClients.Count(client => client.ApplicationId == application.ApplicationId),
                dbContext.ApplicationModules.Count(module => module.ApplicationId == application.ApplicationId),
                dbContext.Roles.Count(role => role.ApplicationId == application.ApplicationId),
                dbContext.UserApplications.Count(access =>
                    access.ApplicationId == application.ApplicationId && access.RevokedAt == null)))
            .ToListAsync(cancellationToken);
        return new PagedAdministrationApplications(skip, take, totalCount, items);
    }

    public async Task<PagedAdministrationUsers> SearchUsers(
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query = dbContext.UserAccounts.AsNoTracking();
        if (search is not null)
        {
            query = query.Where(user =>
                user.EmployeeCode.Contains(search)
                || user.DisplayName.Contains(search)
                || (user.Email != null && user.Email.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(user => user.DisplayName)
            .ThenBy(user => user.UserId)
            .Skip(skip)
            .Take(take)
            .Select(user => new AdministrationUserSummary(
                user.UserId,
                user.EmployeeCode,
                user.DisplayName,
                user.IsActive,
                user.SecurityVersion,
                user.LastLoginAt,
                user.LockoutEndAt,
                user.CreatedAt,
                user.UpdatedAt,
                user.Email,
                user.ManagerUserId,
                user.Manager == null ? null : user.Manager.DisplayName,
                user.DepartmentId,
                user.Department == null ? null : user.Department.Unit.UnitName,
                user.TeamId,
                user.Team == null ? null : user.Team.Unit.UnitName,
                user.BranchId,
                user.Branch == null ? null : user.Branch.Unit.UnitName,
                user.Branch == null ? null : user.Branch.StateId,
                user.Branch == null ? null : user.Branch.State.Unit.UnitName,
                user.Branch == null ? null : user.Branch.State.RegionId,
                user.Branch == null ? null : user.Branch.State.Region.Unit.UnitName,
                user.Branch == null ? null : user.Branch.State.Region.CountryId,
                user.Branch == null ? null : user.Branch.State.Region.Country.Unit.UnitName))
            .ToListAsync(cancellationToken);
        return new PagedAdministrationUsers(skip, take, totalCount, items);
    }

    public async Task<AdministrationApplicationCatalog?> GetApplicationCatalog(
        long applicationId,
        CancellationToken cancellationToken)
    {
        var application = await dbContext.Applications
            .AsNoTracking()
            .Where(value => value.ApplicationId == applicationId)
            .Select(value => new AdministrationApplicationSummary(
                value.ApplicationId,
                value.ApplicationCode,
                value.ApplicationName,
                value.TokenAudience,
                value.IsActive,
                value.CreatedAt,
                value.UpdatedAt,
                value.Description,
                dbContext.ApplicationClients.Count(client => client.ApplicationId == value.ApplicationId),
                dbContext.ApplicationModules.Count(module => module.ApplicationId == value.ApplicationId),
                dbContext.Roles.Count(role => role.ApplicationId == value.ApplicationId),
                dbContext.UserApplications.Count(access =>
                    access.ApplicationId == value.ApplicationId && access.RevokedAt == null)))
            .SingleOrDefaultAsync(cancellationToken);
        if (application is null) return null;

        var clients = await dbContext.ApplicationClients
            .AsNoTracking()
            .Where(client => client.ApplicationId == applicationId)
            .OrderBy(client => client.ClientName)
            .ThenBy(client => client.ApplicationClientId)
            .Select(client => new AdministrationClientSummary(
                client.ApplicationClientId,
                client.ApplicationId,
                client.ClientId,
                client.ClientName,
                client.ClientType == Identity.Domain.Enums.ApplicationClientType.Public
                    ? "Public"
                    : client.ClientType == Identity.Domain.Enums.ApplicationClientType.Confidential
                        ? "Confidential"
                        : "Service",
                client.SecretVersion,
                client.IsActive,
                client.CreatedAt,
                client.ExpiresAt,
                client.RevokedAt))
            .ToListAsync(cancellationToken);
        var modules = await dbContext.ApplicationModules
            .AsNoTracking()
            .Where(module => module.ApplicationId == applicationId)
            .OrderBy(module => module.DisplayOrder)
            .ThenBy(module => module.ModuleName)
            .Select(module => new AdministrationModuleSummary(
                module.ApplicationModuleId,
                module.ApplicationId,
                module.ModuleCode,
                module.ModuleName,
                module.Description,
                module.ParentApplicationModuleId,
                module.DisplayOrder,
                module.IsSystem,
                module.IsActive,
                module.CreatedAt,
                module.UpdatedAt))
            .ToListAsync(cancellationToken);
        var capabilities = await dbContext.ModuleCapabilities
            .AsNoTracking()
            .Where(capability => capability.ApplicationId == applicationId)
            .OrderBy(capability => capability.CapabilityCode)
            .Select(capability => new AdministrationCapabilitySummary(
                capability.ModuleCapabilityId,
                capability.ApplicationId,
                capability.ApplicationModuleId,
                capability.CapabilityCode,
                capability.CapabilityName,
                capability.Description,
                capability.IsActive,
                capability.CreatedAt,
                capability.UpdatedAt))
            .ToListAsync(cancellationToken);
        return new AdministrationApplicationCatalog(application, clients, modules, capabilities);
    }

    public async Task<PagedAdministrationApplicationUsers?> SearchApplicationUsers(
        long applicationId,
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Applications.AsNoTracking().AnyAsync(
            application => application.ApplicationId == applicationId,
            cancellationToken)) return null;

        var query =
            from access in dbContext.UserApplications.AsNoTracking()
            join user in dbContext.UserAccounts.AsNoTracking() on access.UserId equals user.UserId
            where access.ApplicationId == applicationId && access.RevokedAt == null
            select new { access, user };
        if (search is not null)
        {
            query = query.Where(row =>
                row.user.EmployeeCode.Contains(search)
                || row.user.DisplayName.Contains(search)
                || (row.user.Email != null && row.user.Email.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = await query
            .OrderBy(row => row.user.DisplayName)
            .ThenBy(row => row.user.UserId)
            .Skip(skip)
            .Take(take)
            .Select(row => new
            {
                row.user.UserId,
                row.user.EmployeeCode,
                row.user.DisplayName,
                row.user.Email,
                row.user.IsActive,
                row.access.AssignedAt,
                row.access.RevokedAt,
            })
            .ToListAsync(cancellationToken);

        // Batch-load role names for the page instead of a per-row correlated collection query.
        var userIds = page.Select(row => row.UserId).ToArray();
        var roles = await (
            from assignment in dbContext.UserRoles.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking() on assignment.RoleId equals role.RoleId
            where userIds.Contains(assignment.UserId)
                && assignment.ApplicationId == applicationId
                && assignment.RevokedAt == null
            select new { assignment.UserId, role.RoleName })
            .ToListAsync(cancellationToken);
        var rolesByUser = roles
            .GroupBy(value => value.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(value => value.RoleName).ToArray());

        var items = page
            .Select(row => new AdministrationApplicationUserSummary(
                row.UserId,
                row.EmployeeCode,
                row.DisplayName,
                row.Email,
                row.IsActive,
                row.AssignedAt,
                row.RevokedAt,
                rolesByUser.GetValueOrDefault(row.UserId, Array.Empty<string>())))
            .ToArray();
        return new PagedAdministrationApplicationUsers(skip, take, totalCount, items);
    }

    public async Task<AdministrationUserAccessCatalog?> GetUserAccessCatalog(
        long userId,
        DateTime evaluatedAt,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.UserAccounts
            .AsNoTracking()
            .Where(value => value.UserId == userId)
            .Select(value => new AdministrationUserSummary(
                value.UserId,
                value.EmployeeCode,
                value.DisplayName,
                value.IsActive,
                value.SecurityVersion,
                value.LastLoginAt,
                value.LockoutEndAt,
                value.CreatedAt,
                value.UpdatedAt,
                value.Email,
                value.ManagerUserId,
                value.Manager == null ? null : value.Manager.DisplayName,
                value.DepartmentId,
                value.Department == null ? null : value.Department.Unit.UnitName,
                value.TeamId,
                value.Team == null ? null : value.Team.Unit.UnitName,
                value.BranchId,
                value.Branch == null ? null : value.Branch.Unit.UnitName,
                value.Branch == null ? null : value.Branch.StateId,
                value.Branch == null ? null : value.Branch.State.Unit.UnitName,
                value.Branch == null ? null : value.Branch.State.RegionId,
                value.Branch == null ? null : value.Branch.State.Region.Unit.UnitName,
                value.Branch == null ? null : value.Branch.State.Region.CountryId,
                value.Branch == null ? null : value.Branch.State.Region.Country.Unit.UnitName))
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null) return null;

        var accesses = await (
            from access in dbContext.UserApplications.AsNoTracking()
            join application in dbContext.Applications.AsNoTracking()
                on access.ApplicationId equals application.ApplicationId
            where access.UserId == userId
            orderby application.ApplicationName
            select new AdministrationUserApplicationSummary(
                access.UserId,
                access.ApplicationId,
                application.ApplicationCode,
                application.ApplicationName,
                access.IsActive,
                access.AuthorizationVersion,
                access.AssignedAt,
                access.RevokedAt,
                Array.Empty<string>()))
            .ToListAsync(cancellationToken);
        var assignments = await (
            from assignment in dbContext.UserRoles.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking() on assignment.RoleId equals role.RoleId
            where assignment.UserId == userId
            orderby role.RoleName
            select new AdministrationUserRoleSummary(
                assignment.UserRoleId,
                assignment.UserId,
                assignment.ApplicationId,
                assignment.RoleId,
                role.RoleCode,
                role.RoleName,
                assignment.AssignedAt,
                assignment.RevokedAt))
            .ToListAsync(cancellationToken);
        var overrides = await (
            from permissionOverride in dbContext.UserPermissionOverrides.AsNoTracking()
            join capability in dbContext.ModuleCapabilities.AsNoTracking()
                on permissionOverride.ModuleCapabilityId equals capability.ModuleCapabilityId
            where permissionOverride.UserId == userId
            orderby capability.CapabilityCode
            select new AdministrationUserOverrideSummary(
                permissionOverride.UserPermissionOverrideId,
                permissionOverride.UserId,
                permissionOverride.ApplicationId,
                permissionOverride.ModuleCapabilityId,
                capability.CapabilityCode,
                permissionOverride.Effect == Identity.Domain.Enums.PermissionEffect.Allow
                    ? "Allow"
                    : "Deny",
                permissionOverride.Reason,
                permissionOverride.AssignedAt,
                permissionOverride.ExpiresAt,
                permissionOverride.RevokedAt))
            .ToListAsync(cancellationToken);
        var applicationIds = accesses.Select(access => access.ApplicationId).ToArray();
        var rolePermissions = await dbContext.RolePermissions
            .AsNoTracking()
            .Where(permission => applicationIds.Contains(permission.ApplicationId))
            .ToListAsync(cancellationToken);
        var capabilities = await dbContext.ModuleCapabilities
            .AsNoTracking()
            .Where(capability => applicationIds.Contains(capability.ApplicationId))
            .ToListAsync(cancellationToken);
        var evaluatedAccesses = accesses.Select(access => access with
        {
            EffectiveCapabilities = EvaluateCapabilities(
                access,
                assignments,
                rolePermissions,
                overrides,
                capabilities,
                evaluatedAt),
        }).ToArray();
        return new AdministrationUserAccessCatalog(
            user,
            evaluatedAccesses,
            assignments,
            overrides,
            evaluatedAt);
    }

    public async Task<AdministrationApplicationAccessCatalog?> GetApplicationAccessCatalog(
        long applicationId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Applications.AsNoTracking().AnyAsync(
            application => application.ApplicationId == applicationId,
            cancellationToken)) return null;

        var roles = await dbContext.Roles
            .AsNoTracking()
            .Where(role => role.ApplicationId == applicationId)
            .OrderBy(role => role.RoleName)
            .Select(role => new AdministrationRoleSummary(
                role.RoleId,
                role.ApplicationId,
                role.RoleCode,
                role.RoleName,
                role.Description,
                role.IsSystem,
                role.IsActive,
                dbContext.UserRoles.Count(assignment =>
                    assignment.RoleId == role.RoleId && assignment.RevokedAt == null)))
            .ToListAsync(cancellationToken);
        var permissions = await (
            from permission in dbContext.RolePermissions.AsNoTracking()
            join capability in dbContext.ModuleCapabilities.AsNoTracking()
                on permission.ModuleCapabilityId equals capability.ModuleCapabilityId
            where permission.ApplicationId == applicationId
            orderby capability.CapabilityCode
            select new AdministrationRolePermissionSummary(
                permission.RolePermissionId,
                permission.ApplicationId,
                permission.RoleId,
                permission.ModuleCapabilityId,
                capability.CapabilityCode,
                permission.GrantedAt,
                permission.RevokedAt))
            .ToListAsync(cancellationToken);
        var capabilities = await dbContext.ModuleCapabilities
            .AsNoTracking()
            .Where(capability => capability.ApplicationId == applicationId)
            .OrderBy(capability => capability.CapabilityCode)
            .Select(capability => new AdministrationCapabilitySummary(
                capability.ModuleCapabilityId,
                capability.ApplicationId,
                capability.ApplicationModuleId,
                capability.CapabilityCode,
                capability.CapabilityName,
                capability.Description,
                capability.IsActive,
                capability.CreatedAt,
                capability.UpdatedAt))
            .ToListAsync(cancellationToken);
        return new AdministrationApplicationAccessCatalog(applicationId, roles, permissions, capabilities);
    }

    public async Task<AdministrationUserSecurityCatalog?> GetUserSecurityCatalog(
        long userId,
        DateTime evaluatedAt,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.UserAccounts
            .AsNoTracking()
            .Where(value => value.UserId == userId)
            .Select(value => new AdministrationUserSummary(
                value.UserId,
                value.EmployeeCode,
                value.DisplayName,
                value.IsActive,
                value.SecurityVersion,
                value.LastLoginAt,
                value.LockoutEndAt,
                value.CreatedAt,
                value.UpdatedAt,
                value.Email,
                value.ManagerUserId,
                value.Manager == null ? null : value.Manager.DisplayName,
                value.DepartmentId,
                value.Department == null ? null : value.Department.Unit.UnitName,
                value.TeamId,
                value.Team == null ? null : value.Team.Unit.UnitName,
                value.BranchId,
                value.Branch == null ? null : value.Branch.Unit.UnitName,
                value.Branch == null ? null : value.Branch.StateId,
                value.Branch == null ? null : value.Branch.State.Unit.UnitName,
                value.Branch == null ? null : value.Branch.State.RegionId,
                value.Branch == null ? null : value.Branch.State.Region.Unit.UnitName,
                value.Branch == null ? null : value.Branch.State.Region.CountryId,
                value.Branch == null ? null : value.Branch.State.Region.Country.Unit.UnitName))
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null) return null;

        var credentials = await dbContext.UserCredentials
            .AsNoTracking()
            .Where(value => value.UserId == userId)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new AdministrationCredentialSummary(
                value.UserCredentialId,
                value.CredentialType.ToString(),
                value.CreatedAt,
                value.ExpiresAt,
                value.RevokedAt))
            .ToListAsync(cancellationToken);
        var devices = await dbContext.Devices
            .AsNoTracking()
            .Where(value => value.UserId == userId)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new AdministrationDeviceSummary(
                value.DeviceId,
                value.DeviceName,
                value.DeviceType,
                value.IsTrusted && value.TrustedUntil > evaluatedAt,
                value.TrustedUntil,
                value.IsActive,
                value.FirstSeenAt,
                value.LastSeenAt,
                value.RevokedAt))
            .ToListAsync(cancellationToken);
        var methods = await dbContext.UserMfaMethods
            .AsNoTracking()
            .Where(value => value.UserId == userId)
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new AdministrationMfaMethodSummary(
                value.UserMfaMethodId,
                value.MethodType.ToString(),
                value.MethodName,
                value.IsPrimary,
                value.IsEnabled,
                value.IsVerified,
                value.CreatedAt,
                value.VerifiedAt,
                value.LastUsedAt,
                value.RevokedAt))
            .ToListAsync(cancellationToken);
        return new AdministrationUserSecurityCatalog(user, credentials, devices, methods);
    }

    public async Task<AdministrationSecurityOperations> GetSecurityOperations(
        GetAdministrationSecurityOperationsQuery query,
        DateTime generatedAt,
        CancellationToken cancellationToken)
    {
        var auditQuery = dbContext.AuthenticationAudits.AsNoTracking().AsQueryable();
        if (query.UserId.HasValue) auditQuery = auditQuery.Where(value => value.UserId == query.UserId);
        if (query.ApplicationId.HasValue) auditQuery = auditQuery.Where(value => value.ApplicationId == query.ApplicationId);
        if (query.EventType is not null) auditQuery = auditQuery.Where(value => value.EventType == query.EventType);
        if (query.Succeeded.HasValue) auditQuery = auditQuery.Where(value => value.Succeeded == query.Succeeded);
        if (query.CorrelationId.HasValue) auditQuery = auditQuery.Where(value => value.CorrelationId == query.CorrelationId);
        if (query.From.HasValue) auditQuery = auditQuery.Where(value => value.OccurredAt >= query.From);
        if (query.To.HasValue) auditQuery = auditQuery.Where(value => value.OccurredAt <= query.To);
        var total = await auditQuery.CountAsync(cancellationToken);
        var audits = await auditQuery
            .OrderByDescending(value => value.OccurredAt)
            .ThenByDescending(value => value.AuthenticationAuditId)
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(value => new AdministrationAuditSummary(
                value.AuthenticationAuditId,
                value.UserId,
                value.ApplicationId,
                value.EventType,
                value.Succeeded,
                value.FailureCode,
                value.CorrelationId,
                value.OccurredAt,
                dbContext.UserAccounts.Where(user => user.UserId == value.UserId)
                    .Select(user => user.DisplayName).FirstOrDefault(),
                dbContext.UserAccounts.Where(user => user.UserId == value.UserId)
                    .Select(user => user.EmployeeCode).FirstOrDefault(),
                dbContext.Applications.Where(application => application.ApplicationId == value.ApplicationId)
                    .Select(application => application.ApplicationName).FirstOrDefault()))
            .ToListAsync(cancellationToken);
        var tokens = await dbContext.RefreshTokens
            .AsNoTracking()
            .OrderByDescending(value => value.IssuedAt)
            .Take(500)
            .ToListAsync(cancellationToken);
        var sessions = tokens
            .GroupBy(value => value.TokenFamilyId)
            .Select(group =>
            {
                var latest = group.OrderByDescending(value => value.IssuedAt).First();
                var active = group.Any(value => value.RevokedAt is null && value.ConsumedAt is null && value.ExpiresAt > generatedAt);
                return new AdministrationSessionSummary(
                    group.Key,
                    latest.UserId,
                    latest.ApplicationId,
                    latest.ApplicationClientId,
                    latest.DeviceId,
                    group.Min(value => value.IssuedAt),
                    group.Max(value => value.ExpiresAt),
                    group.Max(value => value.ConsumedAt),
                    group.Max(value => value.RevokedAt),
                    active);
            })
            .Where(value => !query.UserId.HasValue || value.UserId == query.UserId)
            .Where(value => !query.ApplicationId.HasValue || value.ApplicationId == query.ApplicationId)
            .OrderByDescending(value => value.IssuedAt)
            .Take(100)
            .ToArray();
        var userIds = sessions.Select(value => value.UserId).Distinct().ToArray();
        var applicationIds = sessions.Select(value => value.ApplicationId).Distinct().ToArray();
        var clientIds = sessions.Select(value => value.ApplicationClientId).Distinct().ToArray();
        var users = await dbContext.UserAccounts.AsNoTracking().Where(value => userIds.Contains(value.UserId))
            .Select(value => new { value.UserId, value.DisplayName, value.EmployeeCode })
            .ToDictionaryAsync(value => value.UserId, cancellationToken);
        var applications = await dbContext.Applications.AsNoTracking().Where(value => applicationIds.Contains(value.ApplicationId))
            .Select(value => new { value.ApplicationId, value.ApplicationName })
            .ToDictionaryAsync(value => value.ApplicationId, cancellationToken);
        var clients = await dbContext.ApplicationClients.AsNoTracking().Where(value => clientIds.Contains(value.ApplicationClientId))
            .Select(value => new { value.ApplicationClientId, value.ClientName })
            .ToDictionaryAsync(value => value.ApplicationClientId, cancellationToken);
        sessions = sessions.Select(value => value with
        {
            UserDisplayName = users.GetValueOrDefault(value.UserId)?.DisplayName,
            EmployeeCode = users.GetValueOrDefault(value.UserId)?.EmployeeCode,
            ApplicationName = applications.GetValueOrDefault(value.ApplicationId)?.ApplicationName,
            ClientName = clients.GetValueOrDefault(value.ApplicationClientId)?.ClientName,
        }).ToArray();
        return new AdministrationSecurityOperations(query.Skip, query.Take, total, audits, sessions, generatedAt);
    }

    private static IReadOnlyList<string> EvaluateCapabilities(
        AdministrationUserApplicationSummary access,
        IReadOnlyList<AdministrationUserRoleSummary> assignments,
        IReadOnlyList<Identity.Domain.Entities.RolePermission> permissions,
        IReadOnlyList<AdministrationUserOverrideSummary> overrides,
        IReadOnlyList<Identity.Domain.Entities.ModuleCapability> capabilities,
        DateTime evaluatedAt)
    {
        if (!access.IsActive) return [];
        var activeRoleIds = assignments
            .Where(assignment => assignment.ApplicationId == access.ApplicationId
                && assignment.RevokedAt is null)
            .Select(assignment => assignment.RoleId)
            .ToHashSet();
        var allowedIds = permissions
            .Where(permission => permission.ApplicationId == access.ApplicationId
                && permission.RevokedAt is null
                && activeRoleIds.Contains(permission.RoleId))
            .Select(permission => permission.ModuleCapabilityId)
            .ToHashSet();
        var activeOverrides = overrides.Where(value =>
            value.ApplicationId == access.ApplicationId
            && value.RevokedAt is null
            && (value.ExpiresAt is null || value.ExpiresAt > evaluatedAt));
        allowedIds.UnionWith(activeOverrides
            .Where(value => value.Effect == "Allow")
            .Select(value => value.ModuleCapabilityId));
        var deniedIds = activeOverrides
            .Where(value => value.Effect == "Deny")
            .Select(value => value.ModuleCapabilityId)
            .ToHashSet();
        return capabilities
            .Where(capability => capability.ApplicationId == access.ApplicationId
                && capability.IsActive
                && allowedIds.Contains(capability.ModuleCapabilityId)
                && !deniedIds.Contains(capability.ModuleCapabilityId))
            .Select(capability => capability.CapabilityCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
