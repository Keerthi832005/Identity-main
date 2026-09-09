using Identity.Application.Administration;
using Identity.Application.Messaging;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.AdminCli;

public sealed class ApplicationProvisioningRunner(
    IRequestDispatcher dispatcher,
    IAdministrationStore store,
    IAdministrationAuthorizer authorizer)
{
    public async Task<ApplicationProvisioningResult> Run(
        ApplicationProvisioningManifest manifest,
        long actorUserId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        manifest.Validate();
        if (actorUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(actorUserId));
        }

        var context = new AdministrationContext(actorUserId, Guid.NewGuid());
        await authorizer.Authorize(
            context,
            AdministrationAction.CreateApplication,
            cancellationToken);
        var application = await ProvisionApplication(manifest, context, cancellationToken);
        var clients = await ProvisionClients(
            application.ResourceId,
            manifest.PublicClients,
            context,
            cancellationToken);
        var modules = await ProvisionModules(
            application.ResourceId,
            manifest.Modules,
            context,
            cancellationToken);
        var capabilities = await ProvisionCapabilities(
            application.ResourceId,
            manifest.Modules,
            modules,
            context,
            cancellationToken);
        var roles = await ProvisionRoles(
            application.ResourceId,
            manifest.Roles,
            context,
            cancellationToken);
        var permissionCounts = await ProvisionRolePermissions(
            application.ResourceId,
            manifest.Roles,
            roles,
            capabilities,
            context,
            cancellationToken);
        return new ApplicationProvisioningResult(
            application,
            clients,
            modules,
            capabilities,
            roles,
            permissionCounts.Created,
            permissionCounts.Existing);
    }

    private async Task<ProvisionedResource> ProvisionApplication(
        ApplicationProvisioningManifest manifest,
        AdministrationContext context,
        CancellationToken cancellationToken)
    {
        var existing = await store.FindApplicationByCode(
            manifest.ApplicationCode,
            cancellationToken);
        if (existing is not null)
        {
            EnsureApplicationMatches(existing, manifest);
            return new ProvisionedResource(existing.ApplicationCode, existing.ApplicationId, false);
        }

        var created = await dispatcher.Send(new CreateApplicationCommand(
            manifest.ApplicationCode,
            manifest.ApplicationName,
            manifest.Description,
            manifest.Audience,
            manifest.AccessTokenLifetimeMinutes,
            manifest.RefreshTokenLifetimeDays,
            context), cancellationToken);
        return new ProvisionedResource(manifest.ApplicationCode, created.ResourceId, true);
    }

    private async Task<IReadOnlyList<ProvisionedResource>> ProvisionClients(
        long applicationId,
        IReadOnlyList<PublicClientManifest> clients,
        AdministrationContext context,
        CancellationToken cancellationToken)
    {
        List<ProvisionedResource> results = [];
        foreach (var value in clients)
        {
            var existing = await store.FindApplicationClientByClientId(
                value.ClientId,
                cancellationToken);
            if (existing is not null)
            {
                EnsureClientMatches(existing, applicationId, value);
                results.Add(new ProvisionedResource(existing.ClientId, existing.ApplicationClientId, false));
                continue;
            }

            var created = await dispatcher.Send(new CreateApplicationClientCommand(
                applicationId,
                value.ClientId,
                value.ClientName,
                ApplicationClientType.Public,
                null,
                null,
                context), cancellationToken);
            results.Add(new ProvisionedResource(value.ClientId, created.ResourceId, true));
        }

        return results;
    }

    private async Task<IReadOnlyList<ProvisionedResource>> ProvisionModules(
        long applicationId,
        IReadOnlyList<ApplicationModuleManifest> modules,
        AdministrationContext context,
        CancellationToken cancellationToken)
    {
        List<ProvisionedResource> results = [];
        foreach (var value in modules)
        {
            var existing = await store.FindModuleByCode(
                applicationId,
                value.ModuleCode,
                cancellationToken);
            if (existing is not null)
            {
                EnsureModuleMatches(existing, applicationId, value);
                results.Add(new ProvisionedResource(existing.ModuleCode, existing.ApplicationModuleId, false));
                continue;
            }

            var created = await dispatcher.Send(new CreateModuleCommand(
                applicationId,
                value.ModuleCode,
                value.ModuleName,
                value.Description,
                null,
                value.DisplayOrder,
                value.IsSystem,
                context), cancellationToken);
            results.Add(new ProvisionedResource(value.ModuleCode, created.ResourceId, true));
        }

        return results;
    }

    private async Task<IReadOnlyList<ProvisionedResource>> ProvisionCapabilities(
        long applicationId,
        IReadOnlyList<ApplicationModuleManifest> modules,
        IReadOnlyList<ProvisionedResource> provisionedModules,
        AdministrationContext context,
        CancellationToken cancellationToken)
    {
        var moduleIds = provisionedModules.ToDictionary(
            module => module.Code,
            module => module.ResourceId,
            StringComparer.OrdinalIgnoreCase);
        List<ProvisionedResource> results = [];
        foreach (var module in modules)
        {
            foreach (var value in module.Capabilities)
            {
                var existing = await store.FindCapabilityByCode(
                    value.CapabilityCode,
                    cancellationToken);
                if (existing is not null)
                {
                    EnsureCapabilityMatches(
                        existing,
                        applicationId,
                        moduleIds[module.ModuleCode],
                        value);
                    results.Add(new ProvisionedResource(
                        existing.CapabilityCode,
                        existing.ModuleCapabilityId,
                        false));
                    continue;
                }

                var created = await dispatcher.Send(new CreateCapabilityCommand(
                    applicationId,
                    moduleIds[module.ModuleCode],
                    value.CapabilityCode,
                    value.CapabilityName,
                    value.Description,
                    context), cancellationToken);
                results.Add(new ProvisionedResource(value.CapabilityCode, created.ResourceId, true));
            }
        }

        return results;
    }

    private async Task<IReadOnlyList<ProvisionedResource>> ProvisionRoles(
        long applicationId,
        IReadOnlyList<ApplicationRoleManifest> roles,
        AdministrationContext context,
        CancellationToken cancellationToken)
    {
        List<ProvisionedResource> results = [];
        foreach (var value in roles)
        {
            var existing = await store.FindRoleByCode(
                applicationId,
                value.RoleCode,
                cancellationToken);
            if (existing is not null)
            {
                EnsureRoleMatches(existing, applicationId, value);
                results.Add(new ProvisionedResource(existing.RoleCode, existing.RoleId, false));
                continue;
            }

            var created = await dispatcher.Send(new CreateRoleCommand(
                applicationId,
                value.RoleCode,
                value.RoleName,
                value.Description,
                value.IsSystem,
                context), cancellationToken);
            results.Add(new ProvisionedResource(value.RoleCode, created.ResourceId, true));
        }

        return results;
    }

    private async Task<(int Created, int Existing)> ProvisionRolePermissions(
        long applicationId,
        IReadOnlyList<ApplicationRoleManifest> roles,
        IReadOnlyList<ProvisionedResource> provisionedRoles,
        IReadOnlyList<ProvisionedResource> provisionedCapabilities,
        AdministrationContext context,
        CancellationToken cancellationToken)
    {
        var roleIds = provisionedRoles.ToDictionary(
            role => role.Code,
            role => role.ResourceId,
            StringComparer.OrdinalIgnoreCase);
        var capabilityIds = provisionedCapabilities.ToDictionary(
            capability => capability.Code,
            capability => capability.ResourceId,
            StringComparer.OrdinalIgnoreCase);
        var createdCount = 0;
        var existingCount = 0;
        foreach (var role in roles)
        {
            foreach (var capabilityCode in role.CapabilityCodes)
            {
                var roleId = roleIds[role.RoleCode];
                var capabilityId = capabilityIds[capabilityCode];
                if (await store.HasActiveRolePermission(
                    applicationId,
                    roleId,
                    capabilityId,
                    cancellationToken))
                {
                    existingCount++;
                    continue;
                }

                await dispatcher.Send(new GrantRolePermissionCommand(
                    applicationId,
                    roleId,
                    capabilityId,
                    context), cancellationToken);
                createdCount++;
            }
        }

        return (createdCount, existingCount);
    }

    private static void EnsureApplicationMatches(
        RegisteredApplication existing,
        ApplicationProvisioningManifest expected) => Ensure(
        existing.IsActive
        && existing.ApplicationCode == expected.ApplicationCode
        && existing.ApplicationName == expected.ApplicationName
        && existing.Description == expected.Description
        && existing.TokenAudience == expected.Audience
        && existing.AccessTokenLifetimeMinutes == expected.AccessTokenLifetimeMinutes
        && existing.RefreshTokenLifetimeDays == expected.RefreshTokenLifetimeDays,
        "application",
        expected.ApplicationCode);

    private static void EnsureClientMatches(
        ApplicationClient existing,
        long applicationId,
        PublicClientManifest expected) => Ensure(
        existing.IsActive
        && existing.ApplicationId == applicationId
        && existing.ClientId == expected.ClientId
        && existing.ClientName == expected.ClientName
        && existing.ClientType == ApplicationClientType.Public
        && existing.ClientSecretHash is null
        && existing.ExpiresAt is null,
        "public client",
        expected.ClientId);

    private static void EnsureModuleMatches(
        ApplicationModule existing,
        long applicationId,
        ApplicationModuleManifest expected) => Ensure(
        existing.IsActive
        && existing.ApplicationId == applicationId
        && existing.ModuleCode == expected.ModuleCode
        && existing.ModuleName == expected.ModuleName
        && existing.Description == expected.Description
        && existing.ParentApplicationModuleId is null
        && existing.DisplayOrder == expected.DisplayOrder
        && existing.IsSystem == expected.IsSystem,
        "module",
        expected.ModuleCode);

    private static void EnsureCapabilityMatches(
        ModuleCapability existing,
        long applicationId,
        long moduleId,
        ModuleCapabilityManifest expected) => Ensure(
        existing.IsActive
        && existing.ApplicationId == applicationId
        && existing.ApplicationModuleId == moduleId
        && existing.CapabilityCode == expected.CapabilityCode
        && existing.CapabilityName == expected.CapabilityName
        && existing.Description == expected.Description,
        "capability",
        expected.CapabilityCode);

    private static void EnsureRoleMatches(
        Role existing,
        long applicationId,
        ApplicationRoleManifest expected) => Ensure(
        existing.IsActive
        && existing.ApplicationId == applicationId
        && existing.RoleCode == expected.RoleCode
        && existing.RoleName == expected.RoleName
        && existing.Description == expected.Description
        && existing.IsSystem == expected.IsSystem,
        "role",
        expected.RoleCode);

    private static void Ensure(bool matches, string resourceType, string code)
    {
        if (!matches)
        {
            throw new ApplicationProvisioningException(
                $"Existing {resourceType} '{code}' does not match the manifest contract.");
        }
    }
}
