using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record GetAdministrationResourceQuery(
    AdministrationResourceKind ResourceKind,
    long ResourceId,
    long? UserId = null,
    long? ApplicationId = null) : IRequest<AdministrationResourceResult>;
