using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record SearchAdministrationApplicationUsersQuery(
    long ApplicationId,
    string? Search,
    int Skip = 0,
    int Take = 20) : IRequest<PagedAdministrationApplicationUsers>;
