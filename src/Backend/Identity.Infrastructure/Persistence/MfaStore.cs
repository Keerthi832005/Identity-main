using Identity.Application.Mfa;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

internal sealed class MfaStore(IdentityDbContext dbContext) : IMfaStore
{
    public Task<UserMfaMethod?> FindPrimaryTotp(
        long userId,
        CancellationToken cancellationToken) => dbContext.UserMfaMethods.SingleOrDefaultAsync(
            method => method.UserId == userId
                && method.MethodType == MfaMethodType.Totp
                && method.IsPrimary
                && method.IsEnabled
                && method.IsVerified
                && method.RevokedAt == null,
            cancellationToken);

    public ValueTask<UserMfaMethod?> FindMethod(
        long userMfaMethodId,
        CancellationToken cancellationToken) => dbContext.UserMfaMethods.FindAsync(
            [userMfaMethodId],
            cancellationToken);

    public ValueTask<MfaChallenge?> FindChallenge(
        Guid mfaChallengeId,
        CancellationToken cancellationToken) => dbContext.MfaChallenges.FindAsync(
            [mfaChallengeId],
            cancellationToken);

    public ValueTask<ApplicationClient?> FindClient(
        long applicationClientId,
        CancellationToken cancellationToken) => dbContext.ApplicationClients.FindAsync(
            [applicationClientId],
            cancellationToken);

    public void Add<TEntity>(TEntity entity) where TEntity : class => dbContext.Add(entity);
}
