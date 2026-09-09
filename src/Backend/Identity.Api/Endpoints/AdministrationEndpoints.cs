using Identity.Api.Administration;
using Identity.Api.Hosting;
using Identity.Api.Validation;
using Identity.Application.Administration;
using Identity.Application.Authentication;
using Identity.Application.Messaging;
using Identity.Application.Mfa;
using Identity.Application.Security;
using Identity.Contracts.Administration;
using Identity.Contracts.Authentication;
using Identity.Domain.Enums;

namespace Identity.Api.Endpoints;

internal static class AdministrationEndpoints
{
    public static IEndpointRouteBuilder MapIdentityAdministrationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/admin")
            .RequireAuthorization(AdministrationPolicy.Name);
        group.MapGet("/dashboard", GetDashboard);
        group.MapGet("/applications", SearchApplications);
        group.MapGet("/applications/{applicationId:long}/catalog", GetApplicationCatalog);
        group.MapGet("/applications/{applicationId:long}/access", GetApplicationAccess);
        group.MapGet("/applications/{applicationId:long}/users", GetApplicationUsers);
        group.MapGet("/users", SearchUsers);
        group.MapGet("/users/{userId:long}/access", GetUserAccess);
        group.MapGet("/users/{userId:long}/security", GetUserSecurity);
        group.MapGet("/security/operations", GetSecurityOperations);
        group.MapDelete("/security/sessions/{tokenFamilyId:guid}", RevokeSessionFamily);
        group.MapPost("/applications", CreateApplication)
            .AddEndpointFilter<RequestValidationFilter<CreateApplicationRequest>>();
        group.MapPost("/applications/{applicationId:long}/clients", CreateApplicationClient)
            .AddEndpointFilter<RequestValidationFilter<CreateApplicationClientRequest>>();
        group.MapPost("/applications/{applicationId:long}/modules", CreateModule)
            .AddEndpointFilter<RequestValidationFilter<CreateModuleRequest>>();
        group.MapPost(
            "/applications/{applicationId:long}/modules/{moduleId:long}/move/{direction}",
            ReorderModule);
        group.MapPost(
            "/applications/{applicationId:long}/modules/{moduleId:long}/capabilities",
            CreateCapability)
            .AddEndpointFilter<RequestValidationFilter<CreateCapabilityRequest>>();
        group.MapPost("/applications/{applicationId:long}/roles", CreateRole)
            .AddEndpointFilter<RequestValidationFilter<CreateRoleRequest>>();
        group.MapPost("/users", CreateUser)
            .AddEndpointFilter<RequestValidationFilter<CreateUserRequest>>();
        group.MapPut("/users/{userId:long}/profile", UpdateUserProfile)
            .AddEndpointFilter<RequestValidationFilter<UpdateUserProfileRequest>>();
        group.MapPost("/users/{userId:long}/applications/{applicationId:long}", GrantApplication);
        group.MapPost(
            "/users/{userId:long}/applications/{applicationId:long}/roles/{roleId:long}",
            AssignRole);
        group.MapPost(
            "/applications/{applicationId:long}/roles/{roleId:long}/capabilities/{capabilityId:long}",
            GrantRolePermission);
        group.MapPost(
            "/users/{userId:long}/applications/{applicationId:long}/capabilities/{capabilityId:long}/override",
            SetPermissionOverride)
            .AddEndpointFilter<RequestValidationFilter<PermissionOverrideRequest>>();
        group.MapPost("/users/{userId:long}/devices", RegisterDevice)
            .AddEndpointFilter<RequestValidationFilter<RegisterDeviceRequest>>();
        group.MapPost("/users/{userId:long}/password", SetPassword)
            .AddEndpointFilter<RequestValidationFilter<SetPasswordRequest>>();
        group.MapPost("/users/{userId:long}/pin", SetPin)
            .AddEndpointFilter<RequestValidationFilter<SetPinRequest>>();
        group.MapPost("/users/{userId:long}/mfa/totp", EnrollTotp)
            .AddEndpointFilter<RequestValidationFilter<EnrollTotpRequest>>();
        group.MapPost("/mfa/{methodId:long}/verify", VerifyTotp)
            .AddEndpointFilter<RequestValidationFilter<VerifyTotpRequest>>();
        group.MapDelete("/mfa/{methodId:long}", RevokeMfa);
        group.MapPost("/devices/{deviceId:long}/trust", TrustDevice);
        group.MapDelete("/resources/{resourceKind}/{resourceId:long}", RevokeResource);
        group.MapGet("/resources/{resourceKind}/{resourceId:long}", GetResource);
        group.MapGet(
            "/users/{userId:long}/applications/{applicationId:long}/capabilities",
            GetEffectiveCapabilities);
        return endpoints;
    }

    private static async Task<IResult> CreateApplication(
        CreateApplicationRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken) => Created(await dispatcher.Send(
            new CreateApplicationCommand(
                request.ApplicationCode,
                request.ApplicationName,
                request.Description,
                request.TokenAudience,
                request.AccessTokenLifetimeMinutes,
                request.RefreshTokenLifetimeDays,
                RequestContextFactory.Administration(context)),
            cancellationToken));

    private static async Task<IResult> GetDashboard(
        int? recent,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(
            new GetAdministrationDashboardQuery(recent ?? 5),
            cancellationToken);
        return Results.Ok(new AdministrationDashboardResponse(
            result.ApplicationCount,
            result.ActiveApplicationCount,
            result.UserCount,
            result.ActiveUserCount,
            result.ActiveSessionCount,
            result.FailedAuthenticationCountLast24Hours,
            result.RecentApplications.Select(Map).ToArray(),
            result.RecentUsers.Select(Map).ToArray(),
            result.RecentAuditEvents.Select(Map).ToArray(),
            result.GeneratedAt,
            result.AuthenticationTrend?.Select(Map).ToArray(),
            result.LockedOutUserCount,
            result.UsersWithoutMfaCount,
            result.ExpiringClientSecretCount));
    }

    private static async Task<IResult> SearchApplications(
        string? search,
        int? skip,
        int? take,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(
            new SearchAdministrationApplicationsQuery(search, skip ?? 0, take ?? 20),
            cancellationToken);
        return Results.Ok(new PagedApplicationsResponse(
            result.Skip,
            result.Take,
            result.TotalCount,
            result.Items.Select(Map).ToArray()));
    }

    private static async Task<IResult> GetApplicationCatalog(
        long applicationId,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(
            new GetAdministrationApplicationCatalogQuery(applicationId),
            cancellationToken);
        return Results.Ok(new ApplicationCatalogResponse(
            Map(result.Application),
            result.Clients.Select(Map).ToArray(),
            result.Modules.Select(Map).ToArray(),
            result.Capabilities.Select(Map).ToArray()));
    }

    private static async Task<IResult> GetApplicationUsers(
        long applicationId,
        string? search,
        int? skip,
        int? take,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        RequirePositive(applicationId, nameof(applicationId));
        var result = await dispatcher.Send(
            new SearchAdministrationApplicationUsersQuery(applicationId, search, skip ?? 0, take ?? 20),
            cancellationToken);
        return Results.Ok(new PagedApplicationUsersResponse(
            result.Skip,
            result.Take,
            result.TotalCount,
            result.Items.Select(Map).ToArray()));
    }

    private static async Task<IResult> SearchUsers(
        string? search,
        int? skip,
        int? take,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(
            new SearchAdministrationUsersQuery(search, skip ?? 0, take ?? 20),
            cancellationToken);
        return Results.Ok(new PagedUsersResponse(
            result.Skip,
            result.Take,
            result.TotalCount,
            result.Items.Select(Map).ToArray()));
    }

    private static async Task<IResult> GetUserAccess(
        long userId,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(
            new GetAdministrationUserAccessQuery(userId),
            cancellationToken);
        return Results.Ok(new UserAccessCatalogResponse(
            Map(result.User),
            result.Applications.Select(Map).ToArray(),
            result.RoleAssignments.Select(Map).ToArray(),
            result.Overrides.Select(Map).ToArray(),
            result.EvaluatedAt));
    }

    private static async Task<IResult> GetApplicationAccess(
        long applicationId,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(
            new GetAdministrationApplicationAccessQuery(applicationId),
            cancellationToken);
        return Results.Ok(new ApplicationAccessCatalogResponse(
            result.ApplicationId,
            result.Roles.Select(Map).ToArray(),
            result.RolePermissions.Select(Map).ToArray(),
            result.Capabilities.Select(Map).ToArray()));
    }

    private static async Task<IResult> GetUserSecurity(
        long userId,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(
            new GetAdministrationUserSecurityQuery(userId),
            cancellationToken);
        return Results.Ok(new UserSecurityCatalogResponse(
            Map(result.User),
            result.Credentials.Select(value => new CredentialSummaryResponse(
                value.UserCredentialId,
                value.CredentialType,
                value.CreatedAt,
                value.ExpiresAt,
                value.RevokedAt)).ToArray(),
            result.Devices.Select(value => new DeviceSummaryResponse(
                value.DeviceId,
                value.DeviceName,
                value.DeviceType,
                value.IsTrusted,
                value.TrustedUntil,
                value.IsActive,
                value.FirstSeenAt,
                value.LastSeenAt,
                value.RevokedAt)).ToArray(),
            result.MfaMethods.Select(value => new MfaMethodSummaryResponse(
                value.UserMfaMethodId,
                value.MethodType,
                value.MethodName,
                value.IsPrimary,
                value.IsEnabled,
                value.IsVerified,
                value.CreatedAt,
                value.VerifiedAt,
                value.LastUsedAt,
                value.RevokedAt)).ToArray()));
    }

    private static async Task<IResult> GetSecurityOperations(
        long? userId,
        long? applicationId,
        string? eventType,
        bool? succeeded,
        Guid? correlationId,
        DateTime? from,
        DateTime? to,
        int? skip,
        int? take,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new GetAdministrationSecurityOperationsQuery(
            userId, applicationId, eventType, succeeded, correlationId, from, to,
            skip ?? 0, take ?? 50), cancellationToken);
        return Results.Ok(new SecurityOperationsResponse(
            result.Skip,
            result.Take,
            result.TotalAuditCount,
            result.Audits.Select(Map).ToArray(),
            result.Sessions.Select(value => new SessionSummaryResponse(
                value.TokenFamilyId,
                value.UserId,
                value.ApplicationId,
                value.ApplicationClientId,
                value.DeviceId,
                value.IssuedAt,
                value.ExpiresAt,
                value.LastConsumedAt,
                value.RevokedAt,
                value.IsActive,
                value.UserDisplayName,
                value.EmployeeCode,
                value.ApplicationName,
                value.ClientName)).ToArray(),
            result.GeneratedAt));
    }

    private static async Task<IResult> RevokeSessionFamily(
        Guid tokenFamilyId,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new RevokeSessionFamilyCommand(
            tokenFamilyId,
            RequestContextFactory.Administration(context)), cancellationToken);
        return Results.Ok(new OperationResponse(result.Succeeded, result.FailureCode?.ToString()));
    }

    private static async Task<IResult> CreateApplicationClient(
        long applicationId,
        CreateApplicationClientRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        ISecretHasher secretHasher,
        CancellationToken cancellationToken)
    {
        RequirePositive(applicationId, nameof(applicationId));
        var clientType = Enum.Parse<ApplicationClientType>(request.ClientType, ignoreCase: false);
        if (clientType == ApplicationClientType.Public && request.ClientSecret is not null)
        {
            throw new ArgumentException("Public clients cannot provide a client secret.", nameof(request));
        }

        if (clientType != ApplicationClientType.Public && string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            throw new ArgumentException("This client type requires a client secret.", nameof(request));
        }

        var hash = request.ClientSecret is null ? null : secretHasher.Hash(request.ClientSecret);
        return Created(await dispatcher.Send(new CreateApplicationClientCommand(
            applicationId,
            request.ClientId,
            request.ClientName,
            clientType,
            hash,
            request.ExpiresAt,
            RequestContextFactory.Administration(context)), cancellationToken));
    }

    private static async Task<IResult> CreateModule(
        long applicationId,
        CreateModuleRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        RequirePositive(applicationId, nameof(applicationId));
        return Created(await dispatcher.Send(new CreateModuleCommand(
            applicationId,
            request.ModuleCode,
            request.ModuleName,
            request.Description,
            request.ParentApplicationModuleId,
            request.DisplayOrder,
            request.IsSystem,
            RequestContextFactory.Administration(context)), cancellationToken));
    }

    private static async Task<IResult> ReorderModule(
        long applicationId,
        long moduleId,
        string direction,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        RequirePositive(applicationId, nameof(applicationId));
        RequirePositive(moduleId, nameof(moduleId));
        var moveUp = string.Equals(direction, "up", StringComparison.OrdinalIgnoreCase);
        if (!moveUp && !string.Equals(direction, "down", StringComparison.OrdinalIgnoreCase))
        {
            throw new AdministrationException("A module moves either up or down.");
        }

        return Results.Ok(Map(await dispatcher.Send(new ReorderModuleCommand(
            applicationId,
            moduleId,
            moveUp,
            RequestContextFactory.Administration(context)), cancellationToken)));
    }

    private static async Task<IResult> CreateCapability(
        long applicationId,
        long moduleId,
        CreateCapabilityRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        RequirePositive(applicationId, nameof(applicationId));
        RequirePositive(moduleId, nameof(moduleId));
        return Created(await dispatcher.Send(new CreateCapabilityCommand(
            applicationId,
            moduleId,
            request.CapabilityCode,
            request.CapabilityName,
            request.Description,
            RequestContextFactory.Administration(context)), cancellationToken));
    }

    private static async Task<IResult> CreateRole(
        long applicationId,
        CreateRoleRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        RequirePositive(applicationId, nameof(applicationId));
        return Created(await dispatcher.Send(new CreateRoleCommand(
            applicationId,
            request.RoleCode,
            request.RoleName,
            request.Description,
            request.IsSystem,
            RequestContextFactory.Administration(context)), cancellationToken));
    }

    private static async Task<IResult> CreateUser(
        CreateUserRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken) => Created(await dispatcher.Send(new CreateUserCommand(
            request.EmployeeCode,
            request.DisplayName,
            RequestContextFactory.Administration(context),
            request.Email,
            request.ManagerUserId,
            Map(request.OrganizationMapping)), cancellationToken));

    private static async Task<IResult> UpdateUserProfile(
        long userId,
        UpdateUserProfileRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        RequirePositive(userId, nameof(userId));
        return Results.Ok(Map(await dispatcher.Send(new UpdateUserProfileCommand(
            userId,
            request.DisplayName,
            request.Email,
            request.ManagerUserId,
            RequestContextFactory.Administration(context),
            Map(request.OrganizationMapping)), cancellationToken)));
    }

    private static async Task<IResult> GrantApplication(
        long userId,
        long applicationId,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        RequirePositive(userId, nameof(userId));
        RequirePositive(applicationId, nameof(applicationId));
        return Created(await dispatcher.Send(new GrantUserApplicationCommand(
            userId,
            applicationId,
            RequestContextFactory.Administration(context)), cancellationToken));
    }

    private static async Task<IResult> AssignRole(
        long userId,
        long applicationId,
        long roleId,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        RequirePositive(userId, nameof(userId));
        RequirePositive(applicationId, nameof(applicationId));
        RequirePositive(roleId, nameof(roleId));
        return Created(await dispatcher.Send(new AssignRoleCommand(
            userId,
            applicationId,
            roleId,
            RequestContextFactory.Administration(context)), cancellationToken));
    }

    private static async Task<IResult> GrantRolePermission(
        long applicationId,
        long roleId,
        long capabilityId,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        RequirePositive(applicationId, nameof(applicationId));
        RequirePositive(roleId, nameof(roleId));
        RequirePositive(capabilityId, nameof(capabilityId));
        return Created(await dispatcher.Send(new GrantRolePermissionCommand(
            applicationId,
            roleId,
            capabilityId,
            RequestContextFactory.Administration(context)), cancellationToken));
    }

    private static async Task<IResult> SetPermissionOverride(
        long userId,
        long applicationId,
        long capabilityId,
        PermissionOverrideRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        RequirePositive(userId, nameof(userId));
        RequirePositive(applicationId, nameof(applicationId));
        RequirePositive(capabilityId, nameof(capabilityId));
        var effect = Enum.Parse<PermissionEffect>(request.Effect, ignoreCase: false);
        return Created(await dispatcher.Send(new SetUserPermissionOverrideCommand(
            userId,
            applicationId,
            capabilityId,
            effect,
            request.Reason,
            request.ExpiresAt,
            RequestContextFactory.Administration(context)), cancellationToken));
    }

    private static async Task<IResult> RegisterDevice(
        long userId,
        RegisterDeviceRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        IIdentifierHasher identifierHasher,
        CancellationToken cancellationToken)
    {
        RequirePositive(userId, nameof(userId));
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var clientAddress = context.Connection.RemoteIpAddress?.ToString();
        return Created(await dispatcher.Send(new RegisterDeviceCommand(
            userId,
            request.DeviceName,
            request.DeviceType,
            identifierHasher.Hash(request.DeviceFingerprint),
            HashOptional(identifierHasher, userAgent),
            HashOptional(identifierHasher, clientAddress),
            RequestContextFactory.Administration(context)), cancellationToken));
    }

    private static async Task<IResult> SetPassword(
        long userId,
        SetPasswordRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        RequirePositive(userId, nameof(userId));
        var result = await dispatcher.Send(new SetPasswordCommand(
            userId,
            request.Password,
            request.ExpiresAt,
            RequestContextFactory.Administration(context)), cancellationToken);
        return Results.Ok(new OperationResponse(result.Succeeded, result.FailureCode?.ToString()));
    }

    private static async Task<IResult> SetPin(
        long userId,
        SetPinRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        RequirePositive(userId, nameof(userId));
        var result = await dispatcher.Send(new SetPinCommand(
            userId,
            request.Pin,
            RequestContextFactory.Administration(context)), cancellationToken);
        return Results.Ok(new OperationResponse(result.Succeeded, result.FailureCode?.ToString()));
    }

    private static async Task<IResult> EnrollTotp(
        long userId,
        EnrollTotpRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        RequirePositive(userId, nameof(userId));
        var result = await dispatcher.Send(new EnrollTotpCommand(
            userId,
            request.MethodName,
            request.IsPrimary,
            RequestContextFactory.Administration(context)), cancellationToken);
        return Results.Ok(new TotpEnrollmentResponse(
            result.UserMfaMethodId,
            result.Secret,
            result.Algorithm,
            result.Digits,
            result.PeriodSeconds));
    }

    private static async Task<IResult> VerifyTotp(
        long methodId,
        VerifyTotpRequest request,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        RequirePositive(methodId, nameof(methodId));
        var result = await dispatcher.Send(new VerifyTotpEnrollmentCommand(
            methodId,
            request.Code,
            RequestContextFactory.Administration(context)), cancellationToken);
        return Results.Ok(new OperationResponse(result.Succeeded, result.FailureCode?.ToString()));
    }

    private static async Task<IResult> RevokeMfa(
        long methodId,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        RequirePositive(methodId, nameof(methodId));
        var result = await dispatcher.Send(new RevokeMfaMethodCommand(
            methodId,
            RequestContextFactory.Administration(context)), cancellationToken);
        return Results.Ok(new OperationResponse(result.Succeeded, result.FailureCode?.ToString()));
    }

    private static async Task<IResult> TrustDevice(
        long deviceId,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        RequirePositive(deviceId, nameof(deviceId));
        var result = await dispatcher.Send(new TrustDeviceCommand(
            deviceId,
            RequestContextFactory.Administration(context)), cancellationToken);
        return Results.Ok(new OperationResponse(result.Succeeded, result.FailureCode?.ToString()));
    }

    private static async Task<IResult> RevokeResource(
        AdministrationResourceKind resourceKind,
        long resourceId,
        long? userId,
        long? applicationId,
        HttpContext context,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        RequirePositive(resourceId, nameof(resourceId));
        return Results.Ok(Map(await dispatcher.Send(new ChangeAdministrationResourceStateCommand(
            resourceKind,
            resourceId,
            userId,
            applicationId,
            false,
            RequestContextFactory.Administration(context)), cancellationToken)));
    }

    private static async Task<IResult> GetResource(
        AdministrationResourceKind resourceKind,
        long resourceId,
        long? userId,
        long? applicationId,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        RequirePositive(resourceId, nameof(resourceId));
        var result = await dispatcher.Send(new GetAdministrationResourceQuery(
            resourceKind,
            resourceId,
            userId,
            applicationId), cancellationToken);
        return Results.Ok(new AdministrationResourceResponse(
            result.ResourceKind.ToString(),
            result.ResourceId,
            result.UserId,
            result.ApplicationId,
            result.Code,
            result.Name,
            result.IsActive,
            result.AuthorizationVersion,
            result.RevokedAt,
            result.PermissionEffect?.ToString()));
    }

    private static async Task<IResult> GetEffectiveCapabilities(
        long userId,
        long applicationId,
        IRequestDispatcher dispatcher,
        CancellationToken cancellationToken)
    {
        RequirePositive(userId, nameof(userId));
        RequirePositive(applicationId, nameof(applicationId));
        var result = await dispatcher.Send(new GetEffectiveCapabilitiesQuery(
            userId,
            applicationId), cancellationToken);
        return Results.Ok(new EffectiveCapabilitiesResponse(
            result.AuthorizationVersion,
            result.CapabilityCodes));
    }

    private static IResult Created(AdministrationResult result) => Results.Json(
        Map(result),
        statusCode: StatusCodes.Status201Created);

    private static AdministrationResponse Map(AdministrationResult result) => new(
        result.ResourceType,
        result.ResourceId,
        result.UserId,
        result.ApplicationId,
        result.AuthorizationVersion);

    private static ApplicationSummaryResponse Map(AdministrationApplicationSummary summary) => new(
        summary.ApplicationId,
        summary.ApplicationCode,
        summary.ApplicationName,
        summary.TokenAudience,
        summary.IsActive,
        summary.CreatedAt,
        summary.UpdatedAt,
        summary.Description,
        summary.ClientCount,
        summary.ModuleCount,
        summary.RoleCount,
        summary.UserCount);

    private static UserOrganizationMapping? Map(UserOrganizationMappingRequest? mapping) =>
        mapping is null ? null : new(mapping.DepartmentId, mapping.TeamId, mapping.BranchId);

    private static UserSummaryResponse Map(AdministrationUserSummary summary) => new(
        summary.UserId,
        summary.EmployeeCode,
        summary.DisplayName,
        summary.IsActive,
        summary.SecurityVersion,
        summary.LastLoginAt,
        summary.LockoutEndAt,
        summary.CreatedAt,
        summary.UpdatedAt,
        summary.Email,
        summary.ManagerUserId,
        summary.ManagerDisplayName,
        summary.DepartmentId,
        summary.DepartmentName,
        summary.TeamId,
        summary.TeamName,
        summary.BranchId,
        summary.BranchName,
        summary.StateId,
        summary.StateName,
        summary.RegionId,
        summary.RegionName,
        summary.CountryId,
        summary.CountryName);

    private static AuditSummaryResponse Map(AdministrationAuditSummary summary) => new(
        summary.AuthenticationAuditId,
        summary.UserId,
        summary.ApplicationId,
        summary.EventType,
        summary.Succeeded,
        summary.FailureCode,
        summary.CorrelationId,
        summary.OccurredAt,
        summary.UserDisplayName,
        summary.EmployeeCode,
        summary.ApplicationName);

    private static AuthenticationTrendHourResponse Map(AdministrationAuthenticationTrendHour trend) => new(
        trend.Hour,
        trend.Succeeded,
        trend.Failed);

    private static ApplicationClientSummaryResponse Map(AdministrationClientSummary summary) => new(
        summary.ApplicationClientId,
        summary.ApplicationId,
        summary.ClientId,
        summary.ClientName,
        summary.ClientType,
        summary.SecretVersion,
        summary.IsActive,
        summary.CreatedAt,
        summary.ExpiresAt,
        summary.RevokedAt);

    private static ApplicationModuleSummaryResponse Map(AdministrationModuleSummary summary) => new(
        summary.ApplicationModuleId,
        summary.ApplicationId,
        summary.ModuleCode,
        summary.ModuleName,
        summary.Description,
        summary.ParentApplicationModuleId,
        summary.DisplayOrder,
        summary.IsSystem,
        summary.IsActive,
        summary.CreatedAt,
        summary.UpdatedAt);

    private static ModuleCapabilitySummaryResponse Map(AdministrationCapabilitySummary summary) => new(
        summary.ModuleCapabilityId,
        summary.ApplicationId,
        summary.ApplicationModuleId,
        summary.CapabilityCode,
        summary.CapabilityName,
        summary.Description,
        summary.IsActive,
        summary.CreatedAt,
        summary.UpdatedAt);

    private static RoleSummaryResponse Map(AdministrationRoleSummary summary) => new(
        summary.RoleId,
        summary.ApplicationId,
        summary.RoleCode,
        summary.RoleName,
        summary.Description,
        summary.IsSystem,
        summary.IsActive,
        summary.MemberCount);

    private static ApplicationUserSummaryResponse Map(AdministrationApplicationUserSummary summary) => new(
        summary.UserId,
        summary.EmployeeCode,
        summary.DisplayName,
        summary.Email,
        summary.IsActive,
        summary.AssignedAt,
        summary.RevokedAt,
        summary.Roles);

    private static RolePermissionSummaryResponse Map(AdministrationRolePermissionSummary summary) => new(
        summary.RolePermissionId,
        summary.ApplicationId,
        summary.RoleId,
        summary.ModuleCapabilityId,
        summary.CapabilityCode,
        summary.GrantedAt,
        summary.RevokedAt);

    private static UserApplicationSummaryResponse Map(AdministrationUserApplicationSummary summary) => new(
        summary.UserId,
        summary.ApplicationId,
        summary.ApplicationCode,
        summary.ApplicationName,
        summary.IsActive,
        summary.AuthorizationVersion,
        summary.AssignedAt,
        summary.RevokedAt,
        summary.EffectiveCapabilities);

    private static UserRoleSummaryResponse Map(AdministrationUserRoleSummary summary) => new(
        summary.UserRoleId,
        summary.UserId,
        summary.ApplicationId,
        summary.RoleId,
        summary.RoleCode,
        summary.RoleName,
        summary.AssignedAt,
        summary.RevokedAt);

    private static UserOverrideSummaryResponse Map(AdministrationUserOverrideSummary summary) => new(
        summary.UserPermissionOverrideId,
        summary.UserId,
        summary.ApplicationId,
        summary.ModuleCapabilityId,
        summary.CapabilityCode,
        summary.Effect,
        summary.Reason,
        summary.AssignedAt,
        summary.ExpiresAt,
        summary.RevokedAt);

    private static byte[]? HashOptional(IIdentifierHasher hasher, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : hasher.Hash(value);

    private static void RequirePositive(long value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, "Resource identifiers must be positive.");
        }
    }
}
