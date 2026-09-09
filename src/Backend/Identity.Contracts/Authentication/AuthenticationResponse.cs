namespace Identity.Contracts.Authentication;

public sealed record AuthenticationResponse(
    bool Succeeded,
    string? FailureCode,
    string? AccessToken,
    DateTime? AccessTokenExpiresAt,
    string? RefreshToken,
    DateTime? RefreshTokenExpiresAt,
    int? AuthorizationVersion,
    Guid? MfaChallengeId);
