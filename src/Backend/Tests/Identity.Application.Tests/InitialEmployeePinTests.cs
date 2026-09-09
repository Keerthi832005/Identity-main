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
            Stub<IOrganizationStore>((method, _) => throw new InvalidOperationException(method.Name)), pins);
        var result = await handler.Handle(new CreateUserCommand(code, "Test employee", new AdministrationContext(null, Guid.NewGuid())), TestContext.Current.CancellationToken);
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
