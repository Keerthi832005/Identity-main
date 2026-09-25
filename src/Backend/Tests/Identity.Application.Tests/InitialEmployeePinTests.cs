using System.Reflection;
using Identity.Application.Administration;
using Identity.Application.Authentication;
using Identity.Application.Persistence;
using Identity.Domain.Entities;

namespace Identity.Application.Tests;

public sealed class InitialEmployeePinTests
{
    [Theory]
    [InlineData("INDE03275", "3275")]
    [InlineData("INDC00001", "0001")]
    [InlineData("  EMP1234  ", "1234")]
    [InlineData("1234", "1234")]
    [InlineData("S001", null)]
    [InlineData("USER", null)]
    [InlineData("EMP1234X", null)]
    [InlineData("EMP１２３４", null)]
    public void UsesOnlyFourTrailingAsciiDigits(string code, string? expected) =>
        Assert.Equal(expected, InitialEmployeePin.FromEmployeeCode(code));

    [Theory]
    [InlineData("INDE03275", "3275")]
    [InlineData("INDC00001", "0001")]
    [InlineData("BOOTSTRAP", null)]
    public async Task Creation_StoresOnlyHashedTemporaryPin_InUserTransaction(string code, string? expectedPin)
    {
        var added = new List<object>();
        var transactions = new Transactions();
        var store = Stub<IAdministrationStore>((method, args) =>
        {
            Assert.True(transactions.Active);
            if (method.Name != "Add") throw new InvalidOperationException(method.Name);
            added.Add(args[0]!);
            if (args[0] is UserAccount user) user.GetType().GetProperty("UserId")!.SetValue(user, 42L);
            return null;
        });
        var pins = Stub<IPinHasher>((method, args) =>
        {
            Assert.Equal("Hash", method.Name);
            Assert.Equal(expectedPin, args[0]);
            return new PasswordHash("Test-only", 100000, new byte[32], Enumerable.Repeat((byte)7, 32).ToArray());
        });
        var handler = new AdministrationCommandHandler(store,
            Stub<IAdministrationAuthorizer>((_, _) => ValueTask.CompletedTask),
            Stub<IUnitOfWork>((_, _) => Task.FromResult(1)), transactions, TimeProvider.System,
            BranchStore(), pins);
        var result = await handler.Handle(new CreateUserCommand(code, "Test employee", new AdministrationContext(null, Guid.NewGuid()), OrganizationMapping: new(null, null, 5)), TestContext.Current.CancellationToken);
        Assert.Equal(42, result.ResourceId);
        Assert.Equal(1, transactions.Calls);
        var credentials = added.OfType<UserCredential>().ToArray();
        if (expectedPin is null) Assert.Empty(credentials);
        else
        {
            var pin = Assert.Single(credentials);
            Assert.Equal(42, pin.UserId);
            Assert.True(pin.RequiresChange);
            Assert.Equal(32, pin.SecretHash.Length);
            Assert.Null(pin.RevokedAt);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    [InlineData(-1L)]
    public async Task Creation_RejectsMissingOrInvalidBranchBeforeWriting(long? branchId)
    {
        var handler = new AdministrationCommandHandler(
            Stub<IAdministrationStore>((_, _) => throw new Exception("Must not write")),
            Stub<IAdministrationAuthorizer>((_, _) => ValueTask.CompletedTask),
            Stub<IUnitOfWork>((_, _) => throw new Exception("Must not save")),
            new Transactions(), TimeProvider.System, BranchStore(),
            Stub<IPinHasher>((_, _) => throw new Exception("Must not hash")));
        var request = new CreateUserCommand("EMP1234", "Employee", new(null, Guid.NewGuid()),
            OrganizationMapping: branchId.HasValue ? new(null, null, branchId) : null);
        var error = await Assert.ThrowsAsync<AdministrationException>(async () =>
            await handler.Handle(request, TestContext.Current.CancellationToken));
        Assert.Contains("Select a branch", error.Message);
    }

    private static IOrganizationStore BranchStore()
    {
        var units = new List<OrganizationUnit>();
        var unit = OrganizationUnit.CreateRoot(1, "ORG", "Organization", DateTime.UtcNow);
        unit.GetType().GetProperty("OrganizationUnitId")!.SetValue(unit, 1L);
        units.Add(unit);
        foreach (var type in new[] { Identity.Domain.Enums.OrganizationUnitType.Country,
            Identity.Domain.Enums.OrganizationUnitType.Region, Identity.Domain.Enums.OrganizationUnitType.State,
            Identity.Domain.Enums.OrganizationUnitType.Branch })
        {
            unit = OrganizationUnit.CreateChild(1, unit, type, type.ToString(), type.ToString(), DateTime.UtcNow);
            unit.GetType().GetProperty("OrganizationUnitId")!.SetValue(unit, (long)units.Count + 1);
            units.Add(unit);
        }
        return Stub<IOrganizationStore>((method, args) => method.Name switch
        {
            "FindUnit" => ValueTask.FromResult<OrganizationUnit?>(units.Single(x => x.OrganizationUnitId == (long)args[0]!)),
            "LockOrganization" => Task.CompletedTask,
            _ => throw new InvalidOperationException(method.Name)
        });
    }

    private sealed class Transactions : ITransactionRunner
    {
        public bool Active;
        public int Calls;
        public async Task<T> Execute<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
        {
            Calls++;
            Active = true;
            try { return await operation(cancellationToken); }
            finally { Active = false; }
        }
    }
    private static T Stub<T>(Func<MethodInfo, object?[], object?> call) where T : class
    {
        var proxy = DispatchProxy.Create<T, Proxy>();
        ((Proxy)(object)proxy).Call = call;
        return proxy;
    }
    public class Proxy : DispatchProxy
    {
        public Func<MethodInfo, object?[], object?> Call = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Call(targetMethod!, args ?? []);
    }
}
