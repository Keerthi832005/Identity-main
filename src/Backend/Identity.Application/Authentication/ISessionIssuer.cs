using Identity.Application.Administration;
using Identity.Domain.Entities;

namespace Identity.Application.Authentication;

public interface ISessionIssuer
{
    Task<IssuedSession> Issue(
        UserAccount user,
        RegisteredApplication application,
        ApplicationClient client,
        EffectiveAuthorization authorization,
        long? deviceId,
        Guid tokenFamilyId,
        DateTime issuedAt,
        CancellationToken cancellationToken);
}
