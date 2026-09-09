using Identity.Application.Authentication;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Tests;

public sealed class InitialPinPersistenceTests
{
    [Fact]
    public void InitialPin_IsSaltedHashed_AndRequiresChangeIsPersisted()
    {
        var hasher = new Pbkdf2PinHasher();
        var hash = hasher.Hash(InitialEmployeePin.FromEmployeeCode("INDE03275")!);
        var credential = UserCredential.CreatePin(42, hash.Algorithm, hash.IterationCount,
            hash.Salt, hash.Hash, DateTime.UtcNow, null, requiresChange: true);
        Assert.True(hasher.Verify("3275", credential));
        Assert.False(hasher.Verify("1234", credential));
        Assert.False(hash.Salt.SequenceEqual(hasher.Hash("3275").Salt));
        using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=ModelOnly;Integrated Security=true;TrustServerCertificate=true").Options);
        var model = db.Model.FindEntityType(typeof(UserCredential))!;
        Assert.False((bool)model.FindProperty(nameof(UserCredential.RequiresChange))!.GetDefaultValue()!);
        Assert.True(model.FindProperty(nameof(UserCredential.RevokedAt))!.IsConcurrencyToken);
        Assert.Contains("[RequiresChange]", db.Database.GenerateCreateScript());
        Assert.False(UserCredential.CreatePin(42, hash.Algorithm, hash.IterationCount,
            hash.Salt, hash.Hash, DateTime.UtcNow, null).RequiresChange);
    }
}
