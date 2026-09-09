using Identity.Application.Administration;
using Identity.Application.Messaging;

namespace Identity.Application.Authentication;

public sealed record SetPinCommand(
    long UserId,
    string Pin,
    AdministrationContext Context) : IRequest<OperationResult>;
