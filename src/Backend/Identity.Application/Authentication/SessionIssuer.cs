using Identity.Application.Administration;
using Identity.Application.Persistence;
using Identity.Domain.Entities;

namespace Identity.Application.Authentication;

internal sealed class SessionIssuer(
    IAuthenticationStore store,
    IRandomTokenGenerator randomTokenGenerator,
    ISecretHasher secretHasher,
    IAccessTokenIssuer accessTokenIssuer,
    IUnitOfWork unitOfWork) : ISessionIssuer
{
    public async Task<IssuedSession> Issue(
        UserAccount user,
        RegisteredApplication application,
        ApplicationClient client,
        EffectiveAuthorization authorization,
        long? deviceId,
        Guid tokenFamilyId,
        DateTime issuedAt,
        CancellationToken cancellationToken)
    {
        var refreshTokenValue = randomTokenGenerator.Generate();
        var refreshExpiresAt = issuedAt.AddDays(application.RefreshTokenLifetimeDays);
        var refreshToken = RefreshToken.Issue(
            user.UserId,
            application.ApplicationId,
            client.ApplicationClientId,
            secretHasher.Hash(refreshTokenValue),
            tokenFamilyId,
            user.SecurityVersion,
            authorization.AuthorizationVersion,
            deviceId,
            issuedAt,
            refreshExpiresAt,
            null,
            null);
        store.Add(refreshToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        var accessExpiresAt = issuedAt.AddMinutes(application.AccessTokenLifetimeMinutes);
        var accessToken = accessTokenIssuer.Issue(new AccessTokenRequest(
            user.UserId,
            user.EmployeeCode,
            user.DisplayName,
            application.ApplicationId,
            application.TokenAudience,
            client.ClientId,
            user.SecurityVersion,
            authorization.AuthorizationVersion,
            authorization.CapabilityCodes,
            issuedAt,
            accessExpiresAt));
        return new IssuedSession(
            AuthenticationResult.Issued(
                accessToken,
                refreshTokenValue,
                refreshExpiresAt,
                authorization.AuthorizationVersion),
            refreshToken);
    }
}
