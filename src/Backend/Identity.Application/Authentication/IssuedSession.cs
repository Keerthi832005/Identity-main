using Identity.Domain.Entities;

namespace Identity.Application.Authentication;

public sealed record IssuedSession(
    AuthenticationResult Result,
    RefreshToken RefreshToken);
