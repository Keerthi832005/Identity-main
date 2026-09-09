using Identity.Application.Messaging;
using Identity.Application.Persistence;
using Identity.Application.Authentication;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Administration;

public sealed class AdministrationCommandHandler(
    IAdministrationStore store,
    IAdministrationAuthorizer authorizer,
    IUnitOfWork unitOfWork,
    ITransactionRunner transactionRunner,
    TimeProvider timeProvider,
    IOrganizationStore organizations,
    IPinHasher pinHasher) :
    IRequestHandler<CreateApplicationCommand, AdministrationResult>,
    IRequestHandler<CreateUserCommand, AdministrationResult>,
    IRequestHandler<UpdateUserProfileCommand, AdministrationResult>,
    IRequestHandler<CreateApplicationClientCommand, AdministrationResult>,
    IRequestHandler<CreateModuleCommand, AdministrationResult>,
    IRequestHandler<ReorderModuleCommand, AdministrationResult>,
    IRequestHandler<CreateCapabilityCommand, AdministrationResult>,
    IRequestHandler<GrantUserApplicationCommand, AdministrationResult>,
    IRequestHandler<CreateRoleCommand, AdministrationResult>,
    IRequestHandler<AssignRoleCommand, AdministrationResult>,
    IRequestHandler<GrantRolePermissionCommand, AdministrationResult>,
    IRequestHandler<SetUserPermissionOverrideCommand, AdministrationResult>,
    IRequestHandler<RegisterDeviceCommand, AdministrationResult>,
    IRequestHandler<ChangeAdministrationResourceStateCommand, AdministrationResult>,
    IRequestHandler<GetAdministrationResourceQuery, AdministrationResourceResult>,
    IRequestHandler<GetEffectiveCapabilitiesQuery, EffectiveAuthorization>,
    IRequestHandler<RevokeSessionFamilyCommand, OperationResult>
{
    public ValueTask<OperationResult> Handle(
        RevokeSessionFamilyCommand request,
        CancellationToken cancellationToken) => new(transactionRunner.Execute(async token =>
        {
            await authorizer.Authorize(request.Context, AdministrationAction.ChangeResourceState, token);
            if (request.TokenFamilyId == Guid.Empty) throw new ArgumentException("Token family is required.", nameof(request));
            var now = UtcNow();
            var count = await store.RevokeSessionFamily(request.TokenFamilyId, now, token);
            if (count == 0) return OperationResult.Rejected(AuthenticationFailureCode.InvalidRefreshToken);
            AddAudit(AdministrationAuditEventType.SessionFamilyRevoked, request.Context, now);
            await unitOfWork.SaveChangesAsync(token);
            return OperationResult.Success;
        }, cancellationToken));
    public ValueTask<AdministrationResult> Handle(
        CreateApplicationCommand request,
        CancellationToken cancellationToken) => Run(request.Context, AdministrationAction.CreateApplication, async token =>
    {
        var now = UtcNow();
        var application = RegisteredApplication.Create(
            request.ApplicationCode,
            request.ApplicationName,
            request.Description,
            request.TokenAudience,
            request.AccessTokenLifetimeMinutes,
            request.RefreshTokenLifetimeDays,
            now);
        store.Add(application);
        await unitOfWork.SaveChangesAsync(token);
        AddAudit(
            AdministrationAuditEventType.ApplicationCreated,
            request.Context,
            now,
            applicationId: application.ApplicationId);
        await unitOfWork.SaveChangesAsync(token);
        return Result("Application", application.ApplicationId, applicationId: application.ApplicationId);
    }, cancellationToken);

    public ValueTask<AdministrationResult> Handle(
        CreateUserCommand request,
        CancellationToken cancellationToken) => Run(request.Context, AdministrationAction.CreateUser, async token =>
    {
        var now = UtcNow();
        var user = UserAccount.Create(request.EmployeeCode, request.DisplayName, now, request.Email, request.ManagerUserId);
        if (request.ManagerUserId.HasValue)
        {
            await store.LockUserHierarchy(token);
            await ValidateManager(null, request.ManagerUserId, token);
        }
        await ApplyOrganizationMapping(user, request.OrganizationMapping, now, token);
        store.Add(user);
        await unitOfWork.SaveChangesAsync(token);
        if (InitialEmployeePin.FromEmployeeCode(user.EmployeeCode) is { } initialPin)
        {
            var hash = pinHasher.Hash(initialPin);
            store.Add(UserCredential.CreatePin(user.UserId, hash.Algorithm, hash.IterationCount,
                hash.Salt, hash.Hash, now, request.Context.ActorUserId, requiresChange: true));
        }
        AddAudit(AdministrationAuditEventType.UserCreated, request.Context, now, userId: user.UserId);
        await unitOfWork.SaveChangesAsync(token);
        return Result("User", user.UserId, user.UserId);
    }, cancellationToken);

    public ValueTask<AdministrationResult> Handle(
        UpdateUserProfileCommand request,
        CancellationToken cancellationToken) => Run(request.Context, AdministrationAction.UpdateUserProfile, async token =>
    {
        if (request.UserId <= 0) throw new ArgumentOutOfRangeException(nameof(request));
        await store.LockUserHierarchy(token);
        var user = await store.FindUser(request.UserId, token)
            ?? throw new AdministrationException("User was not found.");
        await ValidateManager(user.UserId, request.ManagerUserId, token);
        var now = UtcNow();
        var mappingChanged = await ApplyOrganizationMapping(user, request.OrganizationMapping, now, token);
        if (user.UpdateProfile(request.DisplayName, request.Email, request.ManagerUserId, now) || mappingChanged)
        {
            AddAudit(AdministrationAuditEventType.UserProfileUpdated, request.Context, now, userId: user.UserId);
            await unitOfWork.SaveChangesAsync(token);
        }

        return Result("User", user.UserId, user.UserId);
    }, cancellationToken);

    private async Task<bool> ApplyOrganizationMapping(UserAccount user, UserOrganizationMapping? mapping,
        DateTime now, CancellationToken token)
    {
        // Older clients and bulk imports omit this object and must not erase existing mappings.
        if (mapping is null || (mapping.DepartmentId == user.DepartmentId && mapping.TeamId == user.TeamId
            && mapping.BranchId == user.BranchId)) return false;
        if (mapping.DepartmentId is <= 0 || mapping.TeamId is <= 0 || mapping.BranchId is <= 0)
            throw new AdministrationException("Department, team and branch IDs must be positive.");
        if (mapping.TeamId.HasValue && !mapping.DepartmentId.HasValue)
            throw new AdministrationException("Select a department before assigning a team.");
        long? organizationId = null;
        if (mapping.DepartmentId is { } departmentId)
        {
            var department = await organizations.FindUnit(departmentId, token)
                ?? throw new AdministrationException("Department was not found.");
            // Use the same transaction lock as hierarchy edits, then refresh before validating.
            await organizations.LockOrganization(department.OrganizationId, token);
            department = await organizations.FindUnit(departmentId, token)
                ?? throw new AdministrationException("Department was not found.");
            organizationId = department.OrganizationId;
            if (department.UnitType != OrganizationUnitType.Department)
                throw new AdministrationException("The selected unit is not a department.");
            // Allow keeping an existing inactive department while clearing its team.
            // New assignments require the department and all its ancestors to be active.
            if (user.DepartmentId != departmentId || mapping.TeamId.HasValue)
            {
                OrganizationUnit? current = department;
                var visited = new HashSet<long>();
                while (current is not null)
                {
                    if (!current.IsActive || !visited.Add(current.OrganizationUnitId))
                        throw new AdministrationException("Department and its organization must be active.");
                    current = current.ParentOrganizationUnitId is { } parentId
                        ? await organizations.FindUnit(parentId, token)
                            ?? throw new AdministrationException("Department hierarchy is incomplete.")
                        : null;
                }
            }
            if (mapping.TeamId is { } teamId)
            {
                var team = await organizations.FindUnit(teamId, token)
                    ?? throw new AdministrationException("Team was not found.");
                if (team.UnitType != OrganizationUnitType.Team || team.OrganizationId != department.OrganizationId
                    || team.ParentOrganizationUnitId != departmentId)
                    throw new AdministrationException("Team must belong to the selected department.");
                if (!team.IsActive) throw new AdministrationException("Team must be active.");
            }
        }
        if (mapping.BranchId is { } branchId)
        {
            var branch = await organizations.FindUnit(branchId, token)
                ?? throw new AdministrationException("Branch was not found.");
            await organizations.LockOrganization(branch.OrganizationId, token);
            branch = await organizations.FindUnit(branchId, token)
                ?? throw new AdministrationException("Branch was not found.");
            if (branch.UnitType != OrganizationUnitType.Branch)
                throw new AdministrationException("The selected unit is not a branch.");
            if (organizationId.HasValue && organizationId.Value != branch.OrganizationId)
                throw new AdministrationException("Branch, department and team must belong to the same organization.");
            if (user.BranchId != branchId)
            {
                OrganizationUnit? current = branch;
                var visited = new HashSet<long>();
                while (current is not null)
                {
                    if (!current.IsActive || !visited.Add(current.OrganizationUnitId))
                        throw new AdministrationException("Branch and its country hierarchy must be active.");
                    current = current.ParentOrganizationUnitId is { } parentId
                        ? await organizations.FindUnit(parentId, token)
                            ?? throw new AdministrationException("Branch hierarchy is incomplete.")
                        : null;
                }
            }
        }
        return user.SetOrganizationMapping(mapping.DepartmentId, mapping.TeamId, mapping.BranchId, now);
    }

    private async Task ValidateManager(long? userId, long? managerUserId, CancellationToken cancellationToken)
    {
        var visited = new HashSet<long>();
        var currentId = managerUserId;
        while (currentId.HasValue)
        {
            if (currentId <= 0 || currentId == userId || !visited.Add(currentId.Value))
            {
                throw new AdministrationException("Manager assignment must not contain a reporting cycle.");
            }

            if (visited.Count > 100)
            {
                throw new AdministrationException("The manager hierarchy cannot exceed 100 levels.");
            }

            var manager = await store.ReadUserForHierarchy(currentId.Value, cancellationToken)
                ?? throw new AdministrationException("Manager was not found.");
            if (currentId == managerUserId && !manager.IsActive)
            {
                throw new AdministrationException("Manager must be an active user.");
            }

            currentId = manager.ManagerUserId;
        }
    }

    public ValueTask<AdministrationResult> Handle(
        CreateApplicationClientCommand request,
        CancellationToken cancellationToken) => Run(request.Context, AdministrationAction.CreateApplicationClient, async token =>
    {
        var application = RequireActive(
            await store.FindApplication(request.ApplicationId, token),
            "Application");
        var now = UtcNow();
        var client = ApplicationClient.Create(
            application.ApplicationId,
            request.ClientId,
            request.ClientName,
            request.ClientType,
            request.ClientSecretHash,
            now,
            request.ExpiresAt);
        store.Add(client);
        await unitOfWork.SaveChangesAsync(token);
        AddAudit(
            AdministrationAuditEventType.ApplicationClientCreated,
            request.Context,
            now,
            applicationId: application.ApplicationId,
            applicationClientId: client.ApplicationClientId);
        await unitOfWork.SaveChangesAsync(token);
        return Result("ApplicationClient", client.ApplicationClientId, applicationId: application.ApplicationId);
    }, cancellationToken);

    public ValueTask<AdministrationResult> Handle(
        CreateModuleCommand request,
        CancellationToken cancellationToken) => Run(request.Context, AdministrationAction.CreateModule, async token =>
    {
        var application = RequireActive(
            await store.FindApplication(request.ApplicationId, token),
            "Application");
        if (request.ParentApplicationModuleId.HasValue)
        {
            var parent = RequireActive(
                await store.FindModule(request.ParentApplicationModuleId.Value, token),
                "Parent module");
            EnsureSameApplication(application.ApplicationId, parent.ApplicationId);
        }

        var now = UtcNow();
        var module = ApplicationModule.Create(
            application.ApplicationId,
            request.ModuleCode,
            request.ModuleName,
            request.Description,
            request.ParentApplicationModuleId,
            request.DisplayOrder,
            request.IsSystem,
            now);
        store.Add(module);
        await unitOfWork.SaveChangesAsync(token);
        AddAudit(
            AdministrationAuditEventType.ModuleCreated,
            request.Context,
            now,
            applicationId: application.ApplicationId);
        await unitOfWork.SaveChangesAsync(token);
        return Result("Module", module.ApplicationModuleId, applicationId: application.ApplicationId);
    }, cancellationToken);

    public ValueTask<AdministrationResult> Handle(
        ReorderModuleCommand request,
        CancellationToken cancellationToken) => Run(request.Context, AdministrationAction.CreateModule, async token =>
    {
        var application = RequireActive(
            await store.FindApplication(request.ApplicationId, token),
            "Application");
        var module = RequireActive(
            await store.FindModule(request.ApplicationModuleId, token),
            "Module");
        EnsureSameApplication(application.ApplicationId, module.ApplicationId);

        /* A system module's position is part of the contract a consuming application was provisioned
           against, not a display preference. */
        if (module.IsSystem)
        {
            throw new AdministrationException("A system module cannot be reordered.");
        }

        var siblings = (await store.ListSiblingModules(
            application.ApplicationId,
            module.ParentApplicationModuleId,
            token)).ToList();
        var index = siblings.FindIndex(
            candidate => candidate.ApplicationModuleId == module.ApplicationModuleId);
        var target = request.MoveUp ? index - 1 : index + 1;

        /* Nothing to swap with, or the neighbour is a system module: a system module holds its place
           whether it is the one being moved or the one that would be displaced. Neither is an error -
           it is the end of the range the button can travel. */
        if (index < 0
            || target < 0
            || target >= siblings.Count
            || siblings[target].IsSystem)
        {
            return Result("Module", module.ApplicationModuleId, applicationId: application.ApplicationId);
        }

        (siblings[index], siblings[target]) = (siblings[target], siblings[index]);

        /* Renumbered from the new sequence rather than swapping two values, because a bulk import
           can leave several siblings sharing one display order, and swapping equal numbers moves
           nothing. */
        var now = UtcNow();
        for (var position = 0; position < siblings.Count; position++)
        {
            siblings[position].SetDisplayOrder(position, now);
        }

        await unitOfWork.SaveChangesAsync(token);
        AddAudit(
            AdministrationAuditEventType.ModuleUpdated,
            request.Context,
            now,
            applicationId: application.ApplicationId);
        await unitOfWork.SaveChangesAsync(token);
        return Result("Module", module.ApplicationModuleId, applicationId: application.ApplicationId);
    }, cancellationToken);

    public ValueTask<AdministrationResult> Handle(
        CreateCapabilityCommand request,
        CancellationToken cancellationToken) => Run(request.Context, AdministrationAction.CreateCapability, async token =>
    {
        var application = RequireActive(
            await store.FindApplication(request.ApplicationId, token),
            "Application");
        var module = RequireActive(
            await store.FindModule(request.ApplicationModuleId, token),
            "Module");
        EnsureSameApplication(application.ApplicationId, module.ApplicationId);
        var now = UtcNow();
        var capability = ModuleCapability.Create(
            application.ApplicationId,
            module.ApplicationModuleId,
            request.CapabilityCode,
            request.CapabilityName,
            request.Description,
            now);
        store.Add(capability);
        await unitOfWork.SaveChangesAsync(token);
        AddAudit(
            AdministrationAuditEventType.CapabilityCreated,
            request.Context,
            now,
            applicationId: application.ApplicationId);
        await unitOfWork.SaveChangesAsync(token);
        return Result("Capability", capability.ModuleCapabilityId, applicationId: application.ApplicationId);
    }, cancellationToken);

    public ValueTask<AdministrationResult> Handle(
        GrantUserApplicationCommand request,
        CancellationToken cancellationToken) => Run(request.Context, AdministrationAction.GrantUserApplication, async token =>
    {
        var user = RequireActive(await store.FindUser(request.UserId, token), "User");
        var application = RequireActive(
            await store.FindApplication(request.ApplicationId, token),
            "Application");
        if (await store.FindUserApplication(user.UserId, application.ApplicationId, token) is not null)
        {
            throw new AdministrationException("User application access already exists.");
        }

        var now = UtcNow();
        var access = UserApplicationAccess.Create(
            user.UserId,
            application.ApplicationId,
            request.Context.ActorUserId,
            now);
        store.Add(access);
        await unitOfWork.SaveChangesAsync(token);
        AddAudit(
            AdministrationAuditEventType.UserApplicationAssigned,
            request.Context,
            now,
            user.UserId,
            application.ApplicationId);
        await unitOfWork.SaveChangesAsync(token);
        return Result(
            "UserApplication",
            application.ApplicationId,
            user.UserId,
            application.ApplicationId,
            access.AuthorizationVersion);
    }, cancellationToken);

    public ValueTask<AdministrationResult> Handle(
        CreateRoleCommand request,
        CancellationToken cancellationToken) => Run(request.Context, AdministrationAction.CreateRole, async token =>
    {
        var application = RequireActive(
            await store.FindApplication(request.ApplicationId, token),
            "Application");
        var now = UtcNow();
        var role = Role.Create(
            application.ApplicationId,
            request.RoleCode,
            request.RoleName,
            request.Description,
            request.IsSystem,
            now);
        store.Add(role);
        await unitOfWork.SaveChangesAsync(token);
        AddAudit(
            AdministrationAuditEventType.RoleCreated,
            request.Context,
            now,
            applicationId: application.ApplicationId);
        await unitOfWork.SaveChangesAsync(token);
        return Result("Role", role.RoleId, applicationId: application.ApplicationId);
    }, cancellationToken);

    public ValueTask<AdministrationResult> Handle(
        AssignRoleCommand request,
        CancellationToken cancellationToken) => Run(request.Context, AdministrationAction.AssignRole, async token =>
    {
        var access = RequireActive(
            await store.FindUserApplication(request.UserId, request.ApplicationId, token),
            "User application access");
        var role = RequireActive(await store.FindRole(request.RoleId, token), "Role");
        EnsureSameApplication(access.ApplicationId, role.ApplicationId);
        if (await store.HasActiveUserRole(
            access.UserId,
            access.ApplicationId,
            role.RoleId,
            token))
        {
            throw new AdministrationException("The role is already assigned to the user.");
        }

        var now = UtcNow();
        var assignment = UserRoleAssignment.Create(
            access.UserId,
            access.ApplicationId,
            role.RoleId,
            request.Context.ActorUserId,
            now);
        access.InvalidateAuthorization(request.Context.ActorUserId, now);
        store.Add(assignment);
        await unitOfWork.SaveChangesAsync(token);
        AddAudit(
            AdministrationAuditEventType.RoleAssigned,
            request.Context,
            now,
            access.UserId,
            access.ApplicationId);
        await unitOfWork.SaveChangesAsync(token);
        return Result(
            "UserRole",
            assignment.UserRoleId,
            access.UserId,
            access.ApplicationId,
            access.AuthorizationVersion);
    }, cancellationToken);

    public ValueTask<AdministrationResult> Handle(
        GrantRolePermissionCommand request,
        CancellationToken cancellationToken) => Run(request.Context, AdministrationAction.GrantRolePermission, async token =>
    {
        var role = RequireActive(await store.FindRole(request.RoleId, token), "Role");
        var capability = RequireActive(
            await store.FindCapability(request.ModuleCapabilityId, token),
            "Capability");
        EnsureSameApplication(request.ApplicationId, role.ApplicationId);
        EnsureSameApplication(request.ApplicationId, capability.ApplicationId);
        if (await store.HasActiveRolePermission(
            request.ApplicationId,
            role.RoleId,
            capability.ModuleCapabilityId,
            token))
        {
            throw new AdministrationException("The capability is already granted to the role.");
        }

        var now = UtcNow();
        var permission = RolePermission.Create(
            request.ApplicationId,
            role.RoleId,
            capability.ModuleCapabilityId,
            request.Context.ActorUserId,
            now);
        store.Add(permission);
        await store.InvalidateRoleUsers(
            request.ApplicationId,
            role.RoleId,
            request.Context.ActorUserId,
            now,
            token);
        await unitOfWork.SaveChangesAsync(token);
        AddAudit(
            AdministrationAuditEventType.PermissionAllowed,
            request.Context,
            now,
            applicationId: request.ApplicationId);
        await unitOfWork.SaveChangesAsync(token);
        return Result("RolePermission", permission.RolePermissionId, applicationId: request.ApplicationId);
    }, cancellationToken);

    public ValueTask<AdministrationResult> Handle(
        SetUserPermissionOverrideCommand request,
        CancellationToken cancellationToken) => Run(request.Context, AdministrationAction.SetUserPermissionOverride, async token =>
    {
        var actorUserId = request.Context.ActorUserId
            ?? throw new AdministrationException("A user permission override requires an actor user.");
        var access = RequireActive(
            await store.FindUserApplication(request.UserId, request.ApplicationId, token),
            "User application access");
        var capability = RequireActive(
            await store.FindCapability(request.ModuleCapabilityId, token),
            "Capability");
        EnsureSameApplication(access.ApplicationId, capability.ApplicationId);
        if (!await store.HasAnyActiveUserRole(access.UserId, access.ApplicationId, token))
        {
            throw new AdministrationException(
                "Assign at least one active application role before configuring user overrides.");
        }
        if (await store.HasActiveUserPermissionOverride(
            access.UserId,
            access.ApplicationId,
            capability.ModuleCapabilityId,
            token))
        {
            throw new AdministrationException("An active user permission override already exists.");
        }

        var now = UtcNow();
        var permissionOverride = UserPermissionOverride.Create(
            access.UserId,
            access.ApplicationId,
            capability.ModuleCapabilityId,
            request.Effect,
            request.Reason,
            actorUserId,
            now,
            request.ExpiresAt);
        access.InvalidateAuthorization(actorUserId, now);
        store.Add(permissionOverride);
        await unitOfWork.SaveChangesAsync(token);
        AddAudit(
            request.Effect == PermissionEffect.Deny
                ? AdministrationAuditEventType.PermissionDenied
                : AdministrationAuditEventType.PermissionAllowed,
            request.Context,
            now,
            access.UserId,
            access.ApplicationId);
        await unitOfWork.SaveChangesAsync(token);
        return Result(
            "UserPermissionOverride",
            permissionOverride.UserPermissionOverrideId,
            access.UserId,
            access.ApplicationId,
            access.AuthorizationVersion);
    }, cancellationToken);

    public ValueTask<AdministrationResult> Handle(
        RegisterDeviceCommand request,
        CancellationToken cancellationToken) => Run(request.Context, AdministrationAction.RegisterDevice, async token =>
    {
        var user = RequireActive(await store.FindUser(request.UserId, token), "User");
        var now = UtcNow();
        var device = Device.Create(
            user.UserId,
            request.DeviceName,
            request.DeviceType,
            request.DeviceFingerprintHash,
            request.UserAgentHash,
            request.ClientAddressHash,
            now);
        store.Add(device);
        await unitOfWork.SaveChangesAsync(token);
        AddAudit(
            AdministrationAuditEventType.DeviceRegistered,
            request.Context,
            now,
            user.UserId,
            deviceId: device.DeviceId);
        await unitOfWork.SaveChangesAsync(token);
        return Result("Device", device.DeviceId, user.UserId);
    }, cancellationToken);

    public ValueTask<AdministrationResult> Handle(
        ChangeAdministrationResourceStateCommand request,
        CancellationToken cancellationToken) => Run(
            request.Context,
            AdministrationAction.ChangeResourceState,
            token => ChangeState(request, token),
            cancellationToken);

    public async ValueTask<AdministrationResourceResult> Handle(
        GetAdministrationResourceQuery request,
        CancellationToken cancellationToken) => request.ResourceKind switch
        {
            AdministrationResourceKind.Application => Map(Require(
                await store.FindApplication(request.ResourceId, cancellationToken),
                "Application")),
            AdministrationResourceKind.ApplicationClient => Map(Require(
                await store.FindApplicationClient(request.ResourceId, cancellationToken),
                "Application client")),
            AdministrationResourceKind.Module => Map(Require(
                await store.FindModule(request.ResourceId, cancellationToken),
                "Module")),
            AdministrationResourceKind.Capability => Map(Require(
                await store.FindCapability(request.ResourceId, cancellationToken),
                "Capability")),
            AdministrationResourceKind.User => Map(Require(
                await store.FindUser(request.ResourceId, cancellationToken),
                "User")),
            AdministrationResourceKind.UserApplication => Map(Require(
                await store.FindUserApplication(
                    RequireId(request.UserId, "User id"),
                    RequireId(request.ApplicationId, "Application id"),
                    cancellationToken),
                "User application access")),
            AdministrationResourceKind.Role => Map(Require(
                await store.FindRole(request.ResourceId, cancellationToken),
                "Role")),
            AdministrationResourceKind.UserRole => Map(Require(
                await store.FindUserRole(request.ResourceId, cancellationToken),
                "User role")),
            AdministrationResourceKind.RolePermission => Map(Require(
                await store.FindRolePermission(request.ResourceId, cancellationToken),
                "Role permission")),
            AdministrationResourceKind.UserPermissionOverride => Map(Require(
                await store.FindUserPermissionOverride(request.ResourceId, cancellationToken),
                "User permission override")),
            AdministrationResourceKind.Device => Map(Require(
                await store.FindDevice(request.ResourceId, cancellationToken),
                "Device")),
            _ => throw new AdministrationException("Unsupported administration resource kind."),
        };

    public async ValueTask<EffectiveAuthorization> Handle(
        GetEffectiveCapabilitiesQuery request,
        CancellationToken cancellationToken) => await store.GetEffectiveAuthorization(
            request.UserId,
            request.ApplicationId,
            UtcNow(),
            cancellationToken)
        ?? throw new AdministrationException("Active user application access was not found.");

    private async Task<AdministrationResult> ChangeState(
        ChangeAdministrationResourceStateCommand request,
        CancellationToken cancellationToken)
    {
        var now = UtcNow();
        var result = request.ResourceKind switch
        {
            AdministrationResourceKind.Application => await ChangeApplication(request, now, cancellationToken),
            AdministrationResourceKind.ApplicationClient => await RevokeClient(request, now, cancellationToken),
            AdministrationResourceKind.Module => await ChangeModule(request, now, cancellationToken),
            AdministrationResourceKind.Capability => await ChangeCapability(request, now, cancellationToken),
            AdministrationResourceKind.User => await ChangeUser(request, now, cancellationToken),
            AdministrationResourceKind.UserApplication => await RevokeUserApplication(request, now, cancellationToken),
            AdministrationResourceKind.Role => await ChangeRole(request, now, cancellationToken),
            AdministrationResourceKind.UserRole => await RevokeUserRole(request, now, cancellationToken),
            AdministrationResourceKind.RolePermission => await RevokeRolePermission(request, now, cancellationToken),
            AdministrationResourceKind.UserPermissionOverride => await RevokeUserOverride(request, now, cancellationToken),
            AdministrationResourceKind.Device => await RevokeDevice(request, now, cancellationToken),
            _ => throw new AdministrationException("Unsupported administration resource kind."),
        };

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return result;
    }

    private async Task<AdministrationResult> ChangeApplication(
        ChangeAdministrationResourceStateCommand request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var application = Require(
            await store.FindApplication(request.ResourceId, cancellationToken),
            "Application");
        application.SetActive(request.IsActive, now);
        AddAudit(
            request.IsActive
                ? AdministrationAuditEventType.ApplicationUpdated
                : AdministrationAuditEventType.ApplicationDisabled,
            request.Context,
            now,
            applicationId: application.ApplicationId);
        return Result("Application", application.ApplicationId, applicationId: application.ApplicationId);
    }

    private async Task<AdministrationResult> RevokeClient(
        ChangeAdministrationResourceStateCommand request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        RequireRevocation(request);
        var client = Require(
            await store.FindApplicationClient(request.ResourceId, cancellationToken),
            "Application client");
        client.Revoke(now);
        AddAudit(
            AdministrationAuditEventType.ApplicationClientRevoked,
            request.Context,
            now,
            applicationId: client.ApplicationId,
            applicationClientId: client.ApplicationClientId);
        return Result("ApplicationClient", client.ApplicationClientId, applicationId: client.ApplicationId);
    }

    private async Task<AdministrationResult> ChangeModule(
        ChangeAdministrationResourceStateCommand request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var module = Require(await store.FindModule(request.ResourceId, cancellationToken), "Module");
        module.SetActive(request.IsActive, now);
        AddAudit(
            request.IsActive ? AdministrationAuditEventType.ModuleUpdated : AdministrationAuditEventType.ModuleDisabled,
            request.Context,
            now,
            applicationId: module.ApplicationId);
        return Result("Module", module.ApplicationModuleId, applicationId: module.ApplicationId);
    }

    private async Task<AdministrationResult> ChangeCapability(
        ChangeAdministrationResourceStateCommand request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var capability = Require(
            await store.FindCapability(request.ResourceId, cancellationToken),
            "Capability");
        capability.SetActive(request.IsActive, now);
        AddAudit(
            request.IsActive
                ? AdministrationAuditEventType.CapabilityUpdated
                : AdministrationAuditEventType.CapabilityDisabled,
            request.Context,
            now,
            applicationId: capability.ApplicationId);
        return Result("Capability", capability.ModuleCapabilityId, applicationId: capability.ApplicationId);
    }

    private async Task<AdministrationResult> ChangeUser(
        ChangeAdministrationResourceStateCommand request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var user = Require(await store.FindUser(request.ResourceId, cancellationToken), "User");
        user.SetActive(request.IsActive, now);
        AddAudit(
            request.IsActive ? AdministrationAuditEventType.AccountEnabled : AdministrationAuditEventType.AccountDisabled,
            request.Context,
            now,
            user.UserId);
        return Result("User", user.UserId, user.UserId);
    }

    private async Task<AdministrationResult> RevokeUserApplication(
        ChangeAdministrationResourceStateCommand request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        RequireRevocation(request);
        var userId = RequireId(request.UserId, "User id");
        var applicationId = RequireId(request.ApplicationId, "Application id");
        var access = Require(
            await store.FindUserApplication(userId, applicationId, cancellationToken),
            "User application access");
        access.Revoke(request.Context.ActorUserId, now);
        AddAudit(
            AdministrationAuditEventType.UserApplicationRevoked,
            request.Context,
            now,
            access.UserId,
            access.ApplicationId);
        return Result(
            "UserApplication",
            access.ApplicationId,
            access.UserId,
            access.ApplicationId,
            access.AuthorizationVersion);
    }

    private async Task<AdministrationResult> ChangeRole(
        ChangeAdministrationResourceStateCommand request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var role = Require(await store.FindRole(request.ResourceId, cancellationToken), "Role");
        role.SetActive(request.IsActive, now);
        await store.InvalidateRoleUsers(
            role.ApplicationId,
            role.RoleId,
            request.Context.ActorUserId,
            now,
            cancellationToken);
        AddAudit(
            request.IsActive ? AdministrationAuditEventType.RoleUpdated : AdministrationAuditEventType.RoleDisabled,
            request.Context,
            now,
            applicationId: role.ApplicationId);
        return Result("Role", role.RoleId, applicationId: role.ApplicationId);
    }

    private async Task<AdministrationResult> RevokeUserRole(
        ChangeAdministrationResourceStateCommand request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        RequireRevocation(request);
        var assignment = Require(
            await store.FindUserRole(request.ResourceId, cancellationToken),
            "User role");
        if (!await store.HasOtherActiveUserRole(
                assignment.UserId,
                assignment.ApplicationId,
                assignment.UserRoleId,
                cancellationToken))
        {
            throw new AdministrationException(
                "At least one application role must remain assigned. Assign another role before removing this role.");
        }
        assignment.Revoke(request.Context.ActorUserId, now);
        var access = Require(
            await store.FindUserApplication(assignment.UserId, assignment.ApplicationId, cancellationToken),
            "User application access");
        access.InvalidateAuthorization(request.Context.ActorUserId, now);
        AddAudit(
            AdministrationAuditEventType.RoleRevoked,
            request.Context,
            now,
            assignment.UserId,
            assignment.ApplicationId);
        return Result(
            "UserRole",
            assignment.UserRoleId,
            assignment.UserId,
            assignment.ApplicationId,
            access.AuthorizationVersion);
    }

    private async Task<AdministrationResult> RevokeRolePermission(
        ChangeAdministrationResourceStateCommand request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        RequireRevocation(request);
        var permission = Require(
            await store.FindRolePermission(request.ResourceId, cancellationToken),
            "Role permission");
        permission.Revoke(request.Context.ActorUserId, now);
        await store.InvalidateRoleUsers(
            permission.ApplicationId,
            permission.RoleId,
            request.Context.ActorUserId,
            now,
            cancellationToken);
        AddAudit(
            AdministrationAuditEventType.RolePermissionRevoked,
            request.Context,
            now,
            applicationId: permission.ApplicationId);
        return Result("RolePermission", permission.RolePermissionId, applicationId: permission.ApplicationId);
    }

    private async Task<AdministrationResult> RevokeUserOverride(
        ChangeAdministrationResourceStateCommand request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        RequireRevocation(request);
        var permissionOverride = Require(
            await store.FindUserPermissionOverride(request.ResourceId, cancellationToken),
            "User permission override");
        permissionOverride.Revoke(request.Context.ActorUserId, now);
        var access = Require(
            await store.FindUserApplication(
                permissionOverride.UserId,
                permissionOverride.ApplicationId,
                cancellationToken),
            "User application access");
        access.InvalidateAuthorization(request.Context.ActorUserId, now);
        AddAudit(
            AdministrationAuditEventType.PermissionOverrideRevoked,
            request.Context,
            now,
            permissionOverride.UserId,
            permissionOverride.ApplicationId);
        return Result(
            "UserPermissionOverride",
            permissionOverride.UserPermissionOverrideId,
            permissionOverride.UserId,
            permissionOverride.ApplicationId,
            access.AuthorizationVersion);
    }

    private async Task<AdministrationResult> RevokeDevice(
        ChangeAdministrationResourceStateCommand request,
        DateTime now,
        CancellationToken cancellationToken)
    {
        RequireRevocation(request);
        var device = Require(await store.FindDevice(request.ResourceId, cancellationToken), "Device");
        device.Revoke(request.Context.ActorUserId, now);
        AddAudit(
            AdministrationAuditEventType.DeviceRevoked,
            request.Context,
            now,
            device.UserId,
            deviceId: device.DeviceId);
        return Result("Device", device.DeviceId, device.UserId);
    }

    private ValueTask<AdministrationResult> Run(
        AdministrationContext context,
        AdministrationAction action,
        Func<CancellationToken, Task<AdministrationResult>> operation,
        CancellationToken cancellationToken) => new(transactionRunner.Execute(async token =>
    {
        await authorizer.Authorize(context, action, token);
        return await operation(token);
    }, cancellationToken));

    private void AddAudit(
        AdministrationAuditEventType eventType,
        AdministrationContext context,
        DateTime occurredAt,
        long? userId = null,
        long? applicationId = null,
        long? applicationClientId = null,
        long? deviceId = null) => store.Add(AuthenticationAudit.CreateAdministrationEvent(
            eventType,
            context.CorrelationId,
            occurredAt,
            userId,
            applicationId,
            applicationClientId,
            deviceId));

    private DateTime UtcNow() => timeProvider.GetUtcNow().UtcDateTime;

    private static T Require<T>(T? entity, string resourceName) where T : class => entity
        ?? throw new AdministrationException($"{resourceName} was not found.");

    private static T RequireActive<T>(T? entity, string resourceName) where T : class
    {
        var value = Require(entity, resourceName);
        var isActive = value switch
        {
            RegisteredApplication application => application.IsActive,
            ApplicationModule module => module.IsActive,
            ModuleCapability capability => capability.IsActive,
            UserAccount user => user.IsActive,
            UserApplicationAccess access => access.IsActive,
            Role role => role.IsActive,
            _ => throw new AdministrationException($"{resourceName} cannot be checked for active state."),
        };
        return isActive ? value : throw new AdministrationException($"{resourceName} is inactive.");
    }

    private static void EnsureSameApplication(long expectedApplicationId, long actualApplicationId)
    {
        if (expectedApplicationId != actualApplicationId)
        {
            throw new AdministrationException("The resources belong to different applications.");
        }
    }

    private static void RequireRevocation(ChangeAdministrationResourceStateCommand request)
    {
        if (request.IsActive)
        {
            throw new AdministrationException("Revoked resources cannot be reactivated by this command.");
        }
    }

    private static long RequireId(long? value, string name) => value is > 0
        ? value.Value
        : throw new AdministrationException($"{name} is required.");

    private static AdministrationResult Result(
        string resourceType,
        long resourceId,
        long? userId = null,
        long? applicationId = null,
        int? authorizationVersion = null) => new(
            resourceType,
            resourceId,
            userId,
            applicationId,
            authorizationVersion);

    private static AdministrationResourceResult Map(RegisteredApplication value) => new(
        AdministrationResourceKind.Application,
        value.ApplicationId,
        null,
        value.ApplicationId,
        value.ApplicationCode,
        value.ApplicationName,
        value.IsActive,
        null,
        null,
        null);

    private static AdministrationResourceResult Map(ApplicationClient value) => new(
        AdministrationResourceKind.ApplicationClient,
        value.ApplicationClientId,
        null,
        value.ApplicationId,
        value.ClientId,
        value.ClientName,
        value.IsActive,
        null,
        value.RevokedAt,
        null);

    private static AdministrationResourceResult Map(ApplicationModule value) => new(
        AdministrationResourceKind.Module,
        value.ApplicationModuleId,
        null,
        value.ApplicationId,
        value.ModuleCode,
        value.ModuleName,
        value.IsActive,
        null,
        null,
        null);

    private static AdministrationResourceResult Map(ModuleCapability value) => new(
        AdministrationResourceKind.Capability,
        value.ModuleCapabilityId,
        null,
        value.ApplicationId,
        value.CapabilityCode,
        value.CapabilityName,
        value.IsActive,
        null,
        null,
        null);

    private static AdministrationResourceResult Map(UserAccount value) => new(
        AdministrationResourceKind.User,
        value.UserId,
        value.UserId,
        null,
        value.EmployeeCode,
        value.DisplayName,
        value.IsActive,
        null,
        null,
        null);

    private static AdministrationResourceResult Map(UserApplicationAccess value) => new(
        AdministrationResourceKind.UserApplication,
        value.ApplicationId,
        value.UserId,
        value.ApplicationId,
        null,
        null,
        value.IsActive,
        value.AuthorizationVersion,
        value.RevokedAt,
        null);

    private static AdministrationResourceResult Map(Role value) => new(
        AdministrationResourceKind.Role,
        value.RoleId,
        null,
        value.ApplicationId,
        value.RoleCode,
        value.RoleName,
        value.IsActive,
        null,
        null,
        null);

    private static AdministrationResourceResult Map(UserRoleAssignment value) => new(
        AdministrationResourceKind.UserRole,
        value.UserRoleId,
        value.UserId,
        value.ApplicationId,
        null,
        null,
        value.RevokedAt is null,
        null,
        value.RevokedAt,
        null);

    private static AdministrationResourceResult Map(RolePermission value) => new(
        AdministrationResourceKind.RolePermission,
        value.RolePermissionId,
        null,
        value.ApplicationId,
        null,
        null,
        value.RevokedAt is null,
        null,
        value.RevokedAt,
        null);

    private static AdministrationResourceResult Map(UserPermissionOverride value) => new(
        AdministrationResourceKind.UserPermissionOverride,
        value.UserPermissionOverrideId,
        value.UserId,
        value.ApplicationId,
        null,
        null,
        value.RevokedAt is null,
        null,
        value.RevokedAt,
        value.Effect);

    private static AdministrationResourceResult Map(Device value) => new(
        AdministrationResourceKind.Device,
        value.DeviceId,
        value.UserId,
        null,
        null,
        value.DeviceName,
        value.IsActive,
        null,
        value.RevokedAt,
        null);
}
