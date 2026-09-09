using Identity.Domain.Entities;

namespace Identity.Application.Administration;

public interface IAdministrationStore
{
    ValueTask<RegisteredApplication?> FindApplication(long applicationId, CancellationToken cancellationToken);
    Task<RegisteredApplication?> FindApplicationByCode(
        string applicationCode,
        CancellationToken cancellationToken);
    ValueTask<ApplicationClient?> FindApplicationClient(long applicationClientId, CancellationToken cancellationToken);
    Task<ApplicationClient?> FindApplicationClientByClientId(
        string clientId,
        CancellationToken cancellationToken);
    ValueTask<ApplicationModule?> FindModule(long applicationModuleId, CancellationToken cancellationToken);
    Task<ApplicationModule?> FindModuleByCode(
        long applicationId,
        string moduleCode,
        CancellationToken cancellationToken);
    /// <summary>Siblings under the same parent, in their current order. Reordering needs the set.</summary>
    Task<IReadOnlyList<ApplicationModule>> ListSiblingModules(
        long applicationId,
        long? parentApplicationModuleId,
        CancellationToken cancellationToken);

    ValueTask<ModuleCapability?> FindCapability(long moduleCapabilityId, CancellationToken cancellationToken);
    Task<ModuleCapability?> FindCapabilityByCode(
        string capabilityCode,
        CancellationToken cancellationToken);
    ValueTask<UserAccount?> FindUser(long userId, CancellationToken cancellationToken);

    Task LockUserHierarchy(CancellationToken cancellationToken);
    Task<UserAccount?> ReadUserForHierarchy(long userId, CancellationToken cancellationToken);
    ValueTask<UserApplicationAccess?> FindUserApplication(
        long userId,
        long applicationId,
        CancellationToken cancellationToken);
    ValueTask<Role?> FindRole(long roleId, CancellationToken cancellationToken);
    Task<Role?> FindRoleByCode(
        long applicationId,
        string roleCode,
        CancellationToken cancellationToken);
    ValueTask<UserRoleAssignment?> FindUserRole(long userRoleId, CancellationToken cancellationToken);
    ValueTask<RolePermission?> FindRolePermission(long rolePermissionId, CancellationToken cancellationToken);
    ValueTask<UserPermissionOverride?> FindUserPermissionOverride(
        long userPermissionOverrideId,
        CancellationToken cancellationToken);
    ValueTask<Device?> FindDevice(long deviceId, CancellationToken cancellationToken);
    ValueTask<bool> HasActiveUserRole(
        long userId,
        long applicationId,
        long roleId,
        CancellationToken cancellationToken);
    ValueTask<bool> HasAnyActiveUserRole(
        long userId,
        long applicationId,
        CancellationToken cancellationToken);
    ValueTask<bool> HasOtherActiveUserRole(
        long userId,
        long applicationId,
        long excludedUserRoleId,
        CancellationToken cancellationToken);
    ValueTask<bool> HasActiveRolePermission(
        long applicationId,
        long roleId,
        long moduleCapabilityId,
        CancellationToken cancellationToken);
    ValueTask<bool> HasActiveUserPermissionOverride(
        long userId,
        long applicationId,
        long moduleCapabilityId,
        CancellationToken cancellationToken);
    Task<EffectiveAuthorization?> GetEffectiveAuthorization(
        long userId,
        long applicationId,
        DateTime evaluatedAt,
        CancellationToken cancellationToken);
    Task InvalidateRoleUsers(
        long applicationId,
        long roleId,
        long? actorUserId,
        DateTime updatedAt,
        CancellationToken cancellationToken);
    Task<int> RevokeSessionFamily(Guid tokenFamilyId, DateTime revokedAt, CancellationToken cancellationToken);
    void Add<TEntity>(TEntity entity) where TEntity : class;
}
