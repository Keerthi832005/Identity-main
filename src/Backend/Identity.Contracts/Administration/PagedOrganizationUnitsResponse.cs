namespace Identity.Contracts.Administration;

public sealed record PagedOrganizationUnitsResponse(int Skip, int Take, int TotalCount, IReadOnlyList<OrganizationUnitResponse> Items);
