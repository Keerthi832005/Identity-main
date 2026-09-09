using Identity.Application.Administration;
using Identity.Application.Authentication;
using Identity.Application.BulkData;
using Identity.Application.Messaging;
using Identity.Application.Mfa;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IRequestDispatcher, RequestDispatcher>();
        services.AddScoped<IRequestHandler<CurrentTerminalVerificationCommand, TerminalVerificationResult>, CurrentTerminalVerificationHandler>();
        services.AddScoped<IRequestHandler<GetTerminalUserDirectoryQuery, TerminalUserDirectoryResult>, TerminalUserDirectoryHandler>();
        services.AddScoped<AdministrationCommandHandler>();
        services.AddScoped<OrganizationCommandHandler>();
        services.AddScoped<OrganizationManagementHandler>();
        services.AddScoped<IRequestHandler<SearchOrganizationUnitsQuery, PagedOrganizationUnits>>(provider => provider.GetRequiredService<OrganizationManagementHandler>());
        services.AddScoped<IRequestHandler<GetOrganizationUnitQuery, OrganizationUnitDetails>>(provider => provider.GetRequiredService<OrganizationManagementHandler>());
        services.AddScoped<IRequestHandler<UpdateOrganizationUnitCommand, OrganizationUnitDetails>>(provider => provider.GetRequiredService<OrganizationManagementHandler>());
        services.AddScoped<IRequestHandler<SetOrganizationUnitActiveCommand, OrganizationUnitDetails>>(provider => provider.GetRequiredService<OrganizationManagementHandler>());
        services.AddScoped<IRequestHandler<CreateOrganizationCommand, OrganizationHierarchyResult>>(
            provider => provider.GetRequiredService<OrganizationCommandHandler>());
        services.AddScoped<IRequestHandler<CreateOrganizationUnitCommand, OrganizationHierarchyResult>>(
            provider => provider.GetRequiredService<OrganizationCommandHandler>());
        services.TryAddSingleton(BulkStagingOptions.Default);
        services.TryAddSingleton<BulkValidationPipeline>();
        services.TryAddScoped<IBulkDescriptorCatalog, BulkDescriptorCatalog>();
        services.AddScoped<BulkDataHandler>();
        services.AddScoped<IRequestHandler<StageBulkBatchCommand, BulkBatchSummary>>(
            provider => provider.GetRequiredService<BulkDataHandler>());
        services.AddScoped<IRequestHandler<GetBulkBatchQuery, BulkBatchPage>>(
            provider => provider.GetRequiredService<BulkDataHandler>());
        services.AddScoped<IRequestHandler<CorrectBulkRowCommand, BulkStagedRow>>(
            provider => provider.GetRequiredService<BulkDataHandler>());
        services.AddScoped<IRequestHandler<CommitBulkBatchCommand, BulkCommitResult>>(
            provider => provider.GetRequiredService<BulkDataHandler>());
        services.AddScoped<IRequestHandler<DiscardBulkBatchCommand, BulkDiscardResult>>(
            provider => provider.GetRequiredService<BulkDataHandler>());
        services.AddSingleton(UsersBulkDescriptor.Descriptor);
        services.AddScoped<IBulkEntityValidator, UsersBulkValidator>();
        services.AddScoped<IBulkEntityCommitter, UsersBulkCommitter>();
        services.AddSingleton(CatalogBulkDescriptors.Modules);
        services.AddSingleton(CatalogBulkDescriptors.Capabilities);
        services.AddSingleton(CatalogBulkDescriptors.Roles);
        services.AddScoped<IBulkEntityValidator, ModulesBulkValidator>();
        services.AddScoped<IBulkEntityValidator, CapabilitiesBulkValidator>();
        services.AddScoped<IBulkEntityValidator, RolesBulkValidator>();
        services.AddScoped<IBulkEntityCommitter, ModulesBulkCommitter>();
        services.AddScoped<IBulkEntityCommitter, CapabilitiesBulkCommitter>();
        services.AddScoped<IBulkEntityCommitter, RolesBulkCommitter>();
        services.AddSingleton(OrganizationBulkDescriptor.Descriptor);
        services.AddScoped<IBulkEntityValidator, OrganizationBulkValidator>();
        services.AddScoped<IBulkEntityCommitter, OrganizationBulkCommitter>();
        services.AddSingleton(AccessGrantBulkDescriptors.UserApplications);
        services.AddSingleton(AccessGrantBulkDescriptors.UserRoles);
        services.AddScoped<IBulkEntityValidator, UserApplicationsBulkValidator>();
        services.AddScoped<IBulkEntityValidator, UserRolesBulkValidator>();
        services.AddScoped<IBulkEntityCommitter, UserApplicationsBulkCommitter>();
        services.AddScoped<IBulkEntityCommitter, UserRolesBulkCommitter>();
        services.AddSingleton(AssuranceBulkDescriptors.AuditEvents);
        services.AddSingleton(AssuranceBulkDescriptors.Sessions);
        services.AddScoped<AdministrationDiscoveryHandler>();
        services.AddScoped<IRequestHandler<CreateApplicationCommand, AdministrationResult>>(
            ResolveHandler);
        services.AddScoped<IRequestHandler<CreateUserCommand, AdministrationResult>>(ResolveHandler);
        services.AddScoped<IRequestHandler<UpdateUserProfileCommand, AdministrationResult>>(ResolveHandler);
        services.AddScoped<IRequestHandler<CreateApplicationClientCommand, AdministrationResult>>(
            ResolveHandler);
        services.AddScoped<IRequestHandler<CreateModuleCommand, AdministrationResult>>(ResolveHandler);
        services.AddScoped<IRequestHandler<ReorderModuleCommand, AdministrationResult>>(ResolveHandler);
        services.AddScoped<IRequestHandler<CreateCapabilityCommand, AdministrationResult>>(ResolveHandler);
        services.AddScoped<IRequestHandler<GrantUserApplicationCommand, AdministrationResult>>(
            ResolveHandler);
        services.AddScoped<IRequestHandler<CreateRoleCommand, AdministrationResult>>(ResolveHandler);
        services.AddScoped<IRequestHandler<AssignRoleCommand, AdministrationResult>>(ResolveHandler);
        services.AddScoped<IRequestHandler<GrantRolePermissionCommand, AdministrationResult>>(
            ResolveHandler);
        services.AddScoped<IRequestHandler<SetUserPermissionOverrideCommand, AdministrationResult>>(
            ResolveHandler);
        services.AddScoped<IRequestHandler<RegisterDeviceCommand, AdministrationResult>>(ResolveHandler);
        services.AddScoped<IRequestHandler<ChangeAdministrationResourceStateCommand, AdministrationResult>>(
            ResolveHandler);
        services.AddScoped<IRequestHandler<GetAdministrationResourceQuery, AdministrationResourceResult>>(
            ResolveHandler);
        services.AddScoped<IRequestHandler<GetEffectiveCapabilitiesQuery, EffectiveAuthorization>>(
            ResolveHandler);
        services.AddScoped<IRequestHandler<GetAdministrationDashboardQuery, AdministrationDashboard>>(
            ResolveDiscoveryHandler);
        services.AddScoped<IRequestHandler<SearchAdministrationApplicationsQuery, PagedAdministrationApplications>>(
            ResolveDiscoveryHandler);
        services.AddScoped<IRequestHandler<SearchAdministrationUsersQuery, PagedAdministrationUsers>>(
            ResolveDiscoveryHandler);
        services.AddScoped<IRequestHandler<GetAdministrationApplicationCatalogQuery, AdministrationApplicationCatalog>>(
            ResolveDiscoveryHandler);
        services.AddScoped<IRequestHandler<GetAdministrationUserAccessQuery, AdministrationUserAccessCatalog>>(
            ResolveDiscoveryHandler);
        services.AddScoped<IRequestHandler<GetAdministrationApplicationAccessQuery, AdministrationApplicationAccessCatalog>>(
            ResolveDiscoveryHandler);
        services.AddScoped<IRequestHandler<GetAdministrationUserSecurityQuery, AdministrationUserSecurityCatalog>>(
            ResolveDiscoveryHandler);
        services.AddScoped<IRequestHandler<GetAdministrationSecurityOperationsQuery, AdministrationSecurityOperations>>(
            ResolveDiscoveryHandler);
        services.AddScoped<IRequestHandler<RevokeSessionFamilyCommand, OperationResult>>(ResolveHandler);
        services.AddScoped<AuthenticationCommandHandler>();
        services.AddScoped<ISessionIssuer, SessionIssuer>();
        services.AddScoped<IRequestHandler<SetPasswordCommand, OperationResult>>(
            ResolveAuthenticationHandler);
        services.AddScoped<IRequestHandler<ChangeOwnPasswordCommand, OperationResult>>(
            ResolveAuthenticationHandler);
        services.AddScoped<IRequestHandler<SetPinCommand, OperationResult>>(
            ResolveAuthenticationHandler);
        services.AddScoped<IRequestHandler<TerminalVerificationCommand, TerminalVerificationResult>>(
            ResolveAuthenticationHandler);
        services.AddScoped<IRequestHandler<LoginCommand, AuthenticationResult>>(
            ResolveAuthenticationHandler);
        services.AddScoped<IRequestHandler<RefreshSessionCommand, AuthenticationResult>>(
            ResolveAuthenticationHandler);
        services.AddScoped<IRequestHandler<LogoutCommand, OperationResult>>(
            ResolveAuthenticationHandler);
        services.AddScoped<IRequestHandler<GetSigningMetadataQuery, SigningMetadata>>(
            ResolveAuthenticationHandler);
        services.AddScoped<MfaCommandHandler>();
        services.AddScoped<IRequestHandler<EnrollTotpCommand, TotpEnrollmentResult>>(
            ResolveMfaHandler);
        services.AddScoped<IRequestHandler<VerifyTotpEnrollmentCommand, OperationResult>>(
            ResolveMfaHandler);
        services.AddScoped<IRequestHandler<RevokeMfaMethodCommand, OperationResult>>(
            ResolveMfaHandler);
        services.AddScoped<IRequestHandler<CompleteMfaLoginCommand, AuthenticationResult>>(
            ResolveMfaHandler);
        services.AddScoped<IRequestHandler<TrustDeviceCommand, OperationResult>>(
            ResolveMfaHandler);
        services.TryAddSingleton(AuthenticationSecurityPolicy.Default);
        services.AddSingleton(TimeProvider.System);
        return services;
    }

    private static AdministrationCommandHandler ResolveHandler(IServiceProvider provider) =>
        provider.GetRequiredService<AdministrationCommandHandler>();

    private static AdministrationDiscoveryHandler ResolveDiscoveryHandler(IServiceProvider provider) =>
        provider.GetRequiredService<AdministrationDiscoveryHandler>();

    private static AuthenticationCommandHandler ResolveAuthenticationHandler(IServiceProvider provider) =>
        provider.GetRequiredService<AuthenticationCommandHandler>();

    private static MfaCommandHandler ResolveMfaHandler(IServiceProvider provider) =>
        provider.GetRequiredService<MfaCommandHandler>();
}
