namespace Identity.Application.Administration;

public sealed record AdministrationContext(long? ActorUserId, Guid CorrelationId);
