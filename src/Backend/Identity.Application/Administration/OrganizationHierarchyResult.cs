using Identity.Domain.Enums;

namespace Identity.Application.Administration;

public sealed record OrganizationHierarchyResult(
    long OrganizationId, long OrganizationUnitId, OrganizationUnitType UnitType, string HierarchyPath, byte[] RowVersion);
