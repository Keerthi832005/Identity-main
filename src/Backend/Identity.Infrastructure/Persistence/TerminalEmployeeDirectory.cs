using Identity.Application.Authentication;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

internal sealed class TerminalEmployeeDirectory(IdentityDbContext db, TimeProvider timeProvider)
    : ITerminalEmployeeDirectory
{
    public async Task<IReadOnlyList<TerminalEmployeeSuggestion>> Search(
        string clientId, string search, CancellationToken cancellationToken)
    {
        search = search.Trim();
        clientId = clientId.Trim();
        if (search.Length is < 3 or > 50 || clientId.Length is < 1 or > 150) return [];

        return await Candidates(db.UserAccounts.AsNoTracking(), db.UserApplications.AsNoTracking(),
            db.Applications.AsNoTracking(), db.ApplicationClients.AsNoTracking(),
            clientId, search, timeProvider.GetUtcNow().UtcDateTime).ToListAsync(cancellationToken);
    }

    internal static IQueryable<TerminalEmployeeSuggestion> Candidates(
        IQueryable<UserAccount> users, IQueryable<UserApplicationAccess> grants,
        IQueryable<RegisteredApplication> applications, IQueryable<ApplicationClient> clients,
        string clientId, string search, DateTime now)
    {
        var term = search.ToUpperInvariant();
        var normalizedClient = clientId.ToUpperInvariant();
        return (from user in users
                join grant in grants on user.UserId equals grant.UserId
                join application in applications on grant.ApplicationId equals application.ApplicationId
                where user.IsActive && grant.IsActive && grant.RevokedAt == null && application.IsActive
                    && application.ApplicationCode == "production-tracking"
                    && clients.Any(client => client.ApplicationId == application.ApplicationId
                        && client.ClientId.ToUpper() == normalizedClient
                        && client.ClientType == ApplicationClientType.Public
                        && client.IsActive && client.RevokedAt == null
                        && (client.ExpiresAt == null || client.ExpiresAt > now))
                    && (user.EmployeeCode.ToUpper().Contains(term) || user.DisplayName.ToUpper().Contains(term))
                orderby user.EmployeeCode.ToUpper() == term descending,
                    user.EmployeeCode.ToUpper().StartsWith(term) descending, user.EmployeeCode
                select new TerminalEmployeeSuggestion(user.EmployeeCode, user.DisplayName))
            .Take(10);
    }
}
