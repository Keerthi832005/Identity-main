using Identity.Application.Messaging;

namespace Identity.Application.Administration;

public sealed record GetAdministrationDashboardQuery(int RecentLimit = 5)
    : IRequest<AdministrationDashboard>;
