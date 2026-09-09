using Identity.Application.Messaging;

namespace Identity.Application.Authentication;

public sealed record LogoutCommand(
    string RefreshToken,
    Guid CorrelationId) : IRequest<OperationResult>;
