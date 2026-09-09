namespace Identity.Contracts.Administration;

public sealed record PagedApplicationsResponse(
    int Skip,
    int Take,
    int TotalCount,
    IReadOnlyList<ApplicationSummaryResponse> Items);
