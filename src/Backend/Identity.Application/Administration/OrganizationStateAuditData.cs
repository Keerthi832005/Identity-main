using Identity.Domain.Enums;

namespace Identity.Application.Administration;

public sealed record OrganizationStateAuditData(long OrganizationId, long OrganizationUnitId, OrganizationUnitType UnitType, bool IsActive);
