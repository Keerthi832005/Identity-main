using Identity.Application.Messaging;
using Identity.Application.Authentication;

namespace Identity.Application.Administration;

public sealed record RevokeSessionFamilyCommand(
    Guid TokenFamilyId,
    AdministrationContext Context) : IRequest<OperationResult>;
