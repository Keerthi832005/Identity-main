using Identity.Domain.Entities;

namespace Identity.Application.Mfa;

public interface IMfaStore
{
    Task<UserMfaMethod?> FindPrimaryTotp(long userId, CancellationToken cancellationToken);
    ValueTask<UserMfaMethod?> FindMethod(long userMfaMethodId, CancellationToken cancellationToken);
    ValueTask<MfaChallenge?> FindChallenge(Guid mfaChallengeId, CancellationToken cancellationToken);
    ValueTask<ApplicationClient?> FindClient(long applicationClientId, CancellationToken cancellationToken);
    void Add<TEntity>(TEntity entity) where TEntity : class;
}
