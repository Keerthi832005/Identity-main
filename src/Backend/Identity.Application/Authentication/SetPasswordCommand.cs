using Identity.Application.Administration;
using Identity.Application.Messaging;

namespace Identity.Application.Authentication;

public sealed record SetPasswordCommand(
    long UserId,
    string Password,
    DateTime? ExpiresAt,
    AdministrationContext Context) : IRequest<OperationResult>;
