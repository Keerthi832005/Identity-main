using Identity.Application.Messaging;

namespace Identity.Application.Authentication;

public sealed record RefreshSessionCommand(
    string RefreshToken,
    string ClientId,
    string? ClientSecret,
    Guid CorrelationId) : IRequest<AuthenticationResult>;
