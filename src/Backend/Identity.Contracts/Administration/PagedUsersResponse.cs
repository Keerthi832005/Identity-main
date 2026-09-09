namespace Identity.Contracts.Administration;

public sealed record PagedUsersResponse(
    int Skip,
    int Take,
    int TotalCount,
    IReadOnlyList<UserSummaryResponse> Items);
