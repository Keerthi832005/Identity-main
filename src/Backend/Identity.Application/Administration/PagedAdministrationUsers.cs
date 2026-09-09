namespace Identity.Application.Administration;

public sealed record PagedAdministrationUsers(
    int Skip,
    int Take,
    int TotalCount,
    IReadOnlyList<AdministrationUserSummary> Items);
