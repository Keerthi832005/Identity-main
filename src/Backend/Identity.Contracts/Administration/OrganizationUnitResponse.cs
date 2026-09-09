namespace Identity.Contracts.Administration;

public sealed record OrganizationUnitResponse(long OrganizationId, long OrganizationUnitId, long? ParentOrganizationUnitId,
 string UnitType, string UnitCode, string UnitName, string? Description, OrganizationAddressData Address,
 string HierarchyPath, bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt, byte[] RowVersion);
