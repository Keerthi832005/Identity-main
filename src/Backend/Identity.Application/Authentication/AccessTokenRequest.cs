namespace Identity.Application.Authentication;

public sealed record AccessTokenRequest(
    long UserId,
    string EmployeeCode,
    string DisplayName,
    long ApplicationId,
    string Audience,
    string ClientId,
    int SecurityVersion,
    int AuthorizationVersion,
    IReadOnlyList<string> CapabilityCodes,
    DateTime IssuedAt,
    DateTime ExpiresAt);
