namespace Identity.Contracts.Administration;

public sealed record OrganizationCreatedResponse(long OrganizationId, long OrganizationUnitId, string UnitType, string HierarchyPath, byte[] RowVersion);
