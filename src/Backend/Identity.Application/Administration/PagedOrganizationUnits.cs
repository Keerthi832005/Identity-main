namespace Identity.Application.Administration;

public sealed record PagedOrganizationUnits(int Skip, int Take, int TotalCount, IReadOnlyList<OrganizationUnitDetails> Items);
