namespace Identity.Application.Authentication;

public sealed record AuthenticationResult(
    bool Succeeded,
    AuthenticationFailureCode? FailureCode,
    string? AccessToken,
    DateTime? AccessTokenExpiresAt,
    string? RefreshToken,
    DateTime? RefreshTokenExpiresAt,
    int? AuthorizationVersion,
    Guid? MfaChallengeId)
{
    public static AuthenticationResult Rejected(AuthenticationFailureCode failureCode) => new(
        false,
        failureCode,
        null,
        null,
        null,
        null,
        null,
        null);

    public static AuthenticationResult Issued(
        IssuedAccessToken accessToken,
        string refreshToken,
        DateTime refreshTokenExpiresAt,
        int authorizationVersion) => new(
            true,
            null,
            accessToken.Token,
            accessToken.ExpiresAt,
            refreshToken,
            refreshTokenExpiresAt,
            authorizationVersion,
            null);

    public static AuthenticationResult MfaRequired(Guid challengeId) => new(
        false,
        null,
        null,
        null,
        null,
        null,
        null,
        challengeId);
}
