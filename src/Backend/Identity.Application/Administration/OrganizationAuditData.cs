using Identity.Domain.Enums;

namespace Identity.Application.Administration;

public sealed record OrganizationAuditData(long OrganizationId, long OrganizationUnitId, OrganizationUnitType UnitType);
