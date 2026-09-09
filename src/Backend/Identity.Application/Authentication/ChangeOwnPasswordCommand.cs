using Identity.Application.Messaging;

namespace Identity.Application.Authentication;

public sealed record ChangeOwnPasswordCommand(
    string RefreshToken,
    string ClientId,
    string CurrentPassword,
    string NewPassword,
    Guid CorrelationId) : IRequest<OperationResult>;
