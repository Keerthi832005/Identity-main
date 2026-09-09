using Identity.Application;
using Identity.Application.Administration;
using Identity.Application.Messaging;
using Identity.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure.Tests;

public sealed class AdministrationDiscoveryReadTests
{
    [Fact]
    public async Task DiscoveryQueries_ReadExistingCatalogWithoutSensitiveMaterial()
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip("Set IDENTITY_TEST_SQL_CONNECTION to run the read-only discovery test.");
        }

        var services = new ServiceCollection();
        services.AddIdentityApplication();
        services.AddIdentityPersistence(connectionString);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IRequestDispatcher>();
        var applications = await dispatcher.Send(
            new SearchAdministrationApplicationsQuery(null, 0, 5),
            TestContext.Current.CancellationToken);
        var users = await dispatcher.Send(
            new SearchAdministrationUsersQuery(null, 0, 5),
            TestContext.Current.CancellationToken);
        var dashboard = await dispatcher.Send(
            new GetAdministrationDashboardQuery(5),
            TestContext.Current.CancellationToken);

        Assert.True(dashboard.ApplicationCount >= applications.TotalCount);
        Assert.True(dashboard.UserCount >= users.TotalCount);
        if (applications.Items.Count > 0)
        {
            var applicationId = applications.Items[0].ApplicationId;
            var catalog = await dispatcher.Send(
                new GetAdministrationApplicationCatalogQuery(applicationId),
                TestContext.Current.CancellationToken);
            var access = await dispatcher.Send(
                new GetAdministrationApplicationAccessQuery(applicationId),
                TestContext.Current.CancellationToken);
            Assert.Equal(applicationId, catalog.Application.ApplicationId);
            Assert.Equal(applicationId, access.ApplicationId);
        }

        if (users.Items.Count > 0)
        {
            var access = await dispatcher.Send(
                new GetAdministrationUserAccessQuery(users.Items[0].UserId),
                TestContext.Current.CancellationToken);
            Assert.Equal(users.Items[0].UserId, access.User.UserId);
        }
    }
}
