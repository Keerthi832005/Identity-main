namespace Identity.Application.Authentication;

public sealed record IssuedAccessToken(string Token, DateTime ExpiresAt);
