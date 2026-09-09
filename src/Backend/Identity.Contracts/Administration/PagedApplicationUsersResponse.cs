namespace Identity.Contracts.Administration;

public sealed record PagedApplicationUsersResponse(
    int Skip,
    int Take,
    int TotalCount,
    IReadOnlyList<ApplicationUserSummaryResponse> Items);
