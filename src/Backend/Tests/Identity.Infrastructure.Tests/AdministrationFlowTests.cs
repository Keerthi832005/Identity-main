using Identity.Application;
using Identity.Application.Administration;
using Identity.Application.Messaging;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure.Tests;

public sealed class AdministrationFlowTests
{
    [Fact]
    public async Task AdministrationFlow_CreatesReadsRevokesAndAuditsApprovedRelationships()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Set IDENTITY_TEST_SQL_CONNECTION to run the SQL Server administration flow.");
        }

        var services = new ServiceCollection();
        var authorizer = new TestAdministrationAuthorizer();
        services.AddSingleton<IAdministrationAuthorizer>(authorizer);
        services.AddIdentityApplication();
        services.AddIdentityPersistence(connectionString);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        var context = new AdministrationContext(null, Guid.NewGuid());
        var applicationCode = $"app-{Guid.NewGuid():N}";
        var employeeCode = $"E-{Guid.NewGuid():N}";

        var application = await dispatcher.Send(new CreateApplicationCommand(
            applicationCode,
            "Administration Test",
            null,
            $"urn:identity:test:{Guid.NewGuid():N}",
            15,
            7,
            context), TestContext.Current.CancellationToken);
        var user = await dispatcher.Send(new CreateUserCommand(
            employeeCode,
            "Administration User",
            context), TestContext.Current.CancellationToken);
        var actorContext = context with { ActorUserId = user.ResourceId };
        var client = await dispatcher.Send(new CreateApplicationClientCommand(
            application.ApplicationId!.Value,
            $"client-{Guid.NewGuid():N}",
            "Administration Client",
            ApplicationClientType.Confidential,
            new byte[32],
            null,
            actorContext), TestContext.Current.CancellationToken);
        var module = await dispatcher.Send(new CreateModuleCommand(
            application.ApplicationId.Value,
            $"module-{Guid.NewGuid():N}",
            "Administration Module",
            null,
            null,
            1,
            false,
            actorContext), TestContext.Current.CancellationToken);
        var capability = await dispatcher.Send(new CreateCapabilityCommand(
            application.ApplicationId.Value,
            module.ResourceId,
            $"administration.capability-{Guid.NewGuid():N}",
            "Administration Capability",
            null,
            actorContext), TestContext.Current.CancellationToken);
        var access = await dispatcher.Send(new GrantUserApplicationCommand(
            user.ResourceId,
            application.ApplicationId.Value,
            actorContext), TestContext.Current.CancellationToken);
        var role = await dispatcher.Send(new CreateRoleCommand(
            application.ApplicationId.Value,
            $"role-{Guid.NewGuid():N}",
            "Administration Role",
            null,
            false,
            actorContext), TestContext.Current.CancellationToken);
        var rolelessOverride = await Assert.ThrowsAsync<AdministrationException>(() =>
            dispatcher.Send(new SetUserPermissionOverrideCommand(
                user.ResourceId,
                application.ApplicationId.Value,
                capability.ResourceId,
                PermissionEffect.Allow,
                "A role is required",
                null,
                actorContext), TestContext.Current.CancellationToken).AsTask());
        Assert.Contains("Assign at least one active application role", rolelessOverride.Message);
        var assignment = await dispatcher.Send(new AssignRoleCommand(
            user.ResourceId,
            application.ApplicationId.Value,
            role.ResourceId,
            actorContext), TestContext.Current.CancellationToken);
        var rolePermission = await dispatcher.Send(new GrantRolePermissionCommand(
            application.ApplicationId.Value,
            role.ResourceId,
            capability.ResourceId,
            actorContext), TestContext.Current.CancellationToken);
        var permissionOverride = await dispatcher.Send(new SetUserPermissionOverrideCommand(
            user.ResourceId,
            application.ApplicationId.Value,
            capability.ResourceId,
            PermissionEffect.Deny,
            "Explicit test deny",
            null,
            actorContext), TestContext.Current.CancellationToken);
        var device = await dispatcher.Send(new RegisterDeviceCommand(
            user.ResourceId,
            "Test workstation",
            "Desktop",
            new byte[32],
            null,
            null,
            actorContext), TestContext.Current.CancellationToken);

        var readOverride = await dispatcher.Send(new GetAdministrationResourceQuery(
            AdministrationResourceKind.UserPermissionOverride,
            permissionOverride.ResourceId), TestContext.Current.CancellationToken);
        var readAccess = await dispatcher.Send(new GetAdministrationResourceQuery(
            AdministrationResourceKind.UserApplication,
            application.ApplicationId.Value,
            user.ResourceId,
            application.ApplicationId.Value), TestContext.Current.CancellationToken);
        var deniedAuthorization = await dispatcher.Send(new GetEffectiveCapabilitiesQuery(
            user.ResourceId,
            application.ApplicationId.Value), TestContext.Current.CancellationToken);
        var dashboard = await dispatcher.Send(new GetAdministrationDashboardQuery(5),
            TestContext.Current.CancellationToken);
        var applications = await dispatcher.Send(new SearchAdministrationApplicationsQuery(
            applicationCode, 0, 10), TestContext.Current.CancellationToken);
        var users = await dispatcher.Send(new SearchAdministrationUsersQuery(
            employeeCode, 0, 10), TestContext.Current.CancellationToken);

        Assert.Equal(PermissionEffect.Deny, readOverride.PermissionEffect);
        Assert.Equal(4, readAccess.AuthorizationVersion);
        Assert.Empty(deniedAuthorization.CapabilityCodes);
        Assert.True(dashboard.ApplicationCount >= 1);
        Assert.Contains(applications.Items, item => item.ApplicationId == application.ApplicationId);
        Assert.Contains(users.Items, item => item.UserId == user.ResourceId);

        var finalRoleRemoval = await Assert.ThrowsAsync<AdministrationException>(() =>
            Revoke(
                dispatcher,
                AdministrationResourceKind.UserRole,
                assignment.ResourceId,
                actorContext).AsTask());
        Assert.Contains("At least one application role must remain assigned", finalRoleRemoval.Message);

        var revokedOverride = await Revoke(
            dispatcher,
            AdministrationResourceKind.UserPermissionOverride,
            permissionOverride.ResourceId,
            actorContext);
        var allowedAuthorization = await dispatcher.Send(new GetEffectiveCapabilitiesQuery(
            user.ResourceId,
            application.ApplicationId.Value), TestContext.Current.CancellationToken);
        await Revoke(
            dispatcher,
            AdministrationResourceKind.RolePermission,
            rolePermission.ResourceId,
            actorContext);
        var finalRoleRemovalWithoutOverrides = await Assert.ThrowsAsync<AdministrationException>(() =>
            Revoke(
                dispatcher,
                AdministrationResourceKind.UserRole,
                assignment.ResourceId,
                actorContext).AsTask());
        Assert.Contains(
            "Assign another role before removing this role",
            finalRoleRemovalWithoutOverrides.Message);
        await Revoke(dispatcher, AdministrationResourceKind.Device, device.ResourceId, actorContext);
        await Revoke(
            dispatcher,
            AdministrationResourceKind.ApplicationClient,
            client.ResourceId,
            actorContext);
        await Revoke(
            dispatcher,
            AdministrationResourceKind.UserApplication,
            application.ApplicationId.Value,
            actorContext,
            user.ResourceId,
            application.ApplicationId.Value);
        await Revoke(dispatcher, AdministrationResourceKind.Role, role.ResourceId, actorContext);
        await Revoke(dispatcher, AdministrationResourceKind.Capability, capability.ResourceId, actorContext);
        await Revoke(dispatcher, AdministrationResourceKind.Module, module.ResourceId, actorContext);
        await Revoke(dispatcher, AdministrationResourceKind.Application, application.ResourceId, actorContext);
        await Revoke(dispatcher, AdministrationResourceKind.User, user.ResourceId, actorContext);

        Assert.Equal(5, revokedOverride.AuthorizationVersion);
        Assert.Single(allowedAuthorization.CapabilityCodes);
        var disabledUser = await dispatcher.Send(new GetAdministrationResourceQuery(
            AdministrationResourceKind.User,
            user.ResourceId), TestContext.Current.CancellationToken);
        Assert.False(disabledUser.IsActive);
        authorizer.Deny = true;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => await dispatcher.Send(
            new CreateUserCommand(
                $"E-{Guid.NewGuid():N}",
                "Unauthorized User",
                actorContext),
            TestContext.Current.CancellationToken));
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.True(await dbContext.AuthenticationAudits.CountAsync(
            audit => audit.CorrelationId == context.CorrelationId,
            TestContext.Current.CancellationToken) >= 21);
    }

    [Fact]
    public async Task ReorderModule_MovesASiblingAndRefusesASystemModule()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Set IDENTITY_TEST_SQL_CONNECTION to run the SQL Server administration flow.");
        }

        var services = new ServiceCollection();
        services.AddSingleton<IAdministrationAuthorizer>(new TestAdministrationAuthorizer());
        services.AddIdentityApplication();
        services.AddIdentityPersistence(connectionString);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        var context = new AdministrationContext(null, Guid.NewGuid());

        var application = await dispatcher.Send(new CreateApplicationCommand(
            $"app-{Guid.NewGuid():N}",
            "Reorder Test",
            null,
            $"urn:identity:test:{Guid.NewGuid():N}",
            15,
            7,
            context), TestContext.Current.CancellationToken);
        var applicationId = application.ApplicationId!.Value;

        /* Every sibling is created with the same display order, which is what a bulk import leaves
           behind; a swap of two equal numbers would move nothing. */
        var first = await Module(dispatcher, applicationId, "first", 0, false, context);
        var second = await Module(dispatcher, applicationId, "second", 0, false, context);
        var third = await Module(dispatcher, applicationId, "third", 0, true, context);

        await dispatcher.Send(
            new ReorderModuleCommand(applicationId, second, MoveUp: true, context),
            TestContext.Current.CancellationToken);

        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var ordered = await dbContext.ApplicationModules
            .Where(module => module.ApplicationId == applicationId)
            .OrderBy(module => module.DisplayOrder)
            .ThenBy(module => module.ApplicationModuleId)
            .Select(module => module.ApplicationModuleId)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal([second, first, third], ordered);

        /* Moving the first entry up is a no-op rather than an error: the button is a nudge, not a
           command that has to succeed. */
        var unchanged = await dispatcher.Send(
            new ReorderModuleCommand(applicationId, second, MoveUp: true, context),
            TestContext.Current.CancellationToken);
        Assert.Equal(second, unchanged.ResourceId);

        /* Moving the last non-system module down would displace the system module behind it. */
        await dispatcher.Send(
            new ReorderModuleCommand(applicationId, first, MoveUp: false, context),
            TestContext.Current.CancellationToken);
        Assert.Equal(
            [second, first, third],
            await dbContext.ApplicationModules
                .Where(module => module.ApplicationId == applicationId)
                .OrderBy(module => module.DisplayOrder)
                .ThenBy(module => module.ApplicationModuleId)
                .Select(module => module.ApplicationModuleId)
                .ToListAsync(TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<AdministrationException>(async () => await dispatcher.Send(
            new ReorderModuleCommand(applicationId, third, MoveUp: true, context),
            TestContext.Current.CancellationToken));
    }

    private static async Task<long> Module(
        IRequestDispatcher dispatcher,
        long applicationId,
        string name,
        int displayOrder,
        bool isSystem,
        AdministrationContext context) =>
        (await dispatcher.Send(new CreateModuleCommand(
            applicationId,
            $"module-{name}-{Guid.NewGuid():N}",
            name,
            null,
            null,
            displayOrder,
            isSystem,
            context), TestContext.Current.CancellationToken)).ResourceId;

    private static ValueTask<AdministrationResult> Revoke(
        IRequestDispatcher dispatcher,
        AdministrationResourceKind resourceKind,
        long resourceId,
        AdministrationContext context,
        long? userId = null,
        long? applicationId = null) => dispatcher.Send(new ChangeAdministrationResourceStateCommand(
            resourceKind,
            resourceId,
            userId,
            applicationId,
            false,
            context), TestContext.Current.CancellationToken);

    private sealed class TestAdministrationAuthorizer : IAdministrationAuthorizer
    {
        public bool Deny { get; set; }

        public ValueTask Authorize(
            AdministrationContext context,
            AdministrationAction action,
            CancellationToken cancellationToken) => Deny
                ? ValueTask.FromException(new UnauthorizedAccessException("Denied by the test authorizer."))
                : ValueTask.CompletedTask;
    }
}
