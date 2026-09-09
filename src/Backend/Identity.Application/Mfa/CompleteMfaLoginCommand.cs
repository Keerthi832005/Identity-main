using Identity.Application.Authentication;
using Identity.Application.Messaging;

namespace Identity.Application.Mfa;

public sealed record CompleteMfaLoginCommand(
    Guid MfaChallengeId,
    string Code,
    Guid CorrelationId) : IRequest<AuthenticationResult>;
