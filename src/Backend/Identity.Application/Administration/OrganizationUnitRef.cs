using Identity.Domain.Enums;

namespace Identity.Application.Administration;

public sealed record OrganizationUnitRef(
    long OrganizationUnitId,
    long OrganizationId,
    OrganizationUnitType UnitType);
