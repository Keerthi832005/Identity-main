using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record SearchAdministrationApplicationsQuery(
    string? Search,
    int Skip = 0,
    int Take = 20) : IRequest<PagedAdministrationApplications>;
