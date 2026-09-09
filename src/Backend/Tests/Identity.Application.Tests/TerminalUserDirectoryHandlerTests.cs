using System.Reflection;
using Identity.Application.Authentication;
using Identity.Application.Security;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Tests;

public sealed class TerminalUserDirectoryHandlerTests
{
    [Fact]
    public async Task TrustedPtsServiceAndTerminal_ReturnRequestedPage()
    {
        var fixture = new Fixture();
        var result = await fixture.Handler.Handle(new(
            77, "pts-service", "fixture-secret", "quality", 20, 50),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(7, fixture.Directory.ApplicationId);
        Assert.Equal("quality", fixture.Directory.Search);
        Assert.Equal(20, fixture.Directory.Skip);
        Assert.Equal(50, fixture.Directory.Take);
        Assert.Equal("EMP001", Assert.Single(result.Page!.Items).EmployeeCode);
    }

    [Fact]
    public async Task InvalidServiceSecret_FailsClosedWithoutReadingDirectory()
    {
        var fixture = new Fixture { SecretValid = false };
        var result = await fixture.Handler.Handle(new(
            77, "pts-service", "wrong-secret", null, 0, 100),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(TerminalVerificationFailureCode.InvalidClient, result.FailureCode);
        Assert.Equal(0, fixture.Directory.Calls);
    }

    [Fact]
    public async Task UntrustedTerminal_FailsClosedWithoutReadingDirectory()
    {
        var fixture = new Fixture();
        fixture.Device.Revoke(null, fixture.Now);
        var result = await fixture.Handler.Handle(new(
            77, "pts-service", "fixture-secret", null, 0, 100),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(TerminalVerificationFailureCode.TerminalNotTrusted, result.FailureCode);
        Assert.Equal(0, fixture.Directory.Calls);
    }

    private sealed class Fixture
    {
        public DateTime Now { get; } = DateTime.UtcNow;
        public Device Device { get; }
        public Directory Directory { get; } = new();
        public bool SecretValid = true;
        public TerminalUserDirectoryHandler Handler { get; }

        public Fixture()
        {
            var application = RegisteredApplication.Create(
                "production-tracking", "PTS", null, "pts-api", 15, 7, Now);
            SetId(application, "ApplicationId", 7);
            var service = ApplicationClient.Create(
                7, "pts-service", "PTS service", ApplicationClientType.Service,
                new byte[32], Now, null);
            Device = Device.Create(42, "Terminal", "Terminal", new byte[32], null, null, Now);
            Device.Trust(Now, TimeSpan.FromDays(1));
            var store = Stub<IAuthenticationStore>((method, args) => method.Name switch
            {
                "FindClientByClientId" => Task.FromResult<ApplicationClient?>(service),
                "FindApplication" => ValueTask.FromResult<RegisteredApplication?>(application),
                "FindDevice" => ValueTask.FromResult<Device?>(Device),
                _ => throw new InvalidOperationException(method.Name),
            });
            var secrets = Stub<ISecretHasher>((_, _) => SecretValid);
            Handler = new(store, secrets, Directory, TimeProvider.System);
        }

        private static void SetId(object entity, string name, long id) =>
            entity.GetType().GetProperty(name)!.SetValue(entity, id);
    }

    private sealed class Directory : ITerminalUserDirectory
    {
        public int Calls { get; private set; }
        public long ApplicationId { get; private set; }
        public string? Search { get; private set; }
        public int Skip { get; private set; }
        public int Take { get; private set; }

        public Task<TerminalUserDirectoryPage> List(
            long applicationId,
            string? search,
            int skip,
            int take,
            CancellationToken cancellationToken)
        {
            Calls++;
            ApplicationId = applicationId;
            Search = search;
            Skip = skip;
            Take = take;
            return Task.FromResult(new TerminalUserDirectoryPage(skip, take, 1,
                [new("EMP001", "Quality User", [new("quality-user", "Quality User")])]));
        }
    }

    private static T Stub<T>(Func<MethodInfo, object?[], object?> call) where T : class
    {
        var proxy = DispatchProxy.Create<T, StrictProxy>();
        ((StrictProxy)(object)proxy).Call = call;
        return proxy;
    }

    public class StrictProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> Call { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            Call(targetMethod!, args ?? []);
    }
}
