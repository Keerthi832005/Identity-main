namespace Identity.Application.Administration;

public sealed record PagedAdministrationApplications(
    int Skip,
    int Take,
    int TotalCount,
    IReadOnlyList<AdministrationApplicationSummary> Items);
