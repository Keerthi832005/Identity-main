using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record GetAdministrationSecurityOperationsQuery(
    long? UserId,
    long? ApplicationId,
    string? EventType,
    bool? Succeeded,
    Guid? CorrelationId,
    DateTime? From,
    DateTime? To,
    int Skip,
    int Take) : IRequest<AdministrationSecurityOperations>;
