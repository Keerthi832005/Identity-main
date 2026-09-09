using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Application.Administration;

public sealed record OrganizationUnitDetails(long OrganizationId, long OrganizationUnitId, long? ParentOrganizationUnitId,
 OrganizationUnitType UnitType, string UnitCode, string UnitName, string? Description, OrganizationUnitAddress Address,
 string HierarchyPath, bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt, byte[] RowVersion);
