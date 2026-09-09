using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record ChangeAdministrationResourceStateCommand(
    AdministrationResourceKind ResourceKind,
    long ResourceId,
    long? UserId,
    long? ApplicationId,
    bool IsActive,
    AdministrationContext Context) : IRequest<AdministrationResult>;
