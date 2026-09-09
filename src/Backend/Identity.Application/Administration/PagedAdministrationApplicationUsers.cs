namespace Identity.Application.Administration;

public sealed record PagedAdministrationApplicationUsers(
    int Skip,
    int Take,
    int TotalCount,
    IReadOnlyList<AdministrationApplicationUserSummary> Items);
