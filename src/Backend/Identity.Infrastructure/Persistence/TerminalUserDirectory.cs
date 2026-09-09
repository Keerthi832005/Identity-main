using Identity.Application.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence;

internal sealed class TerminalUserDirectory(IdentityDbContext db) : ITerminalUserDirectory
{
    public async Task<TerminalUserDirectoryPage> List(
        long applicationId,
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var query =
            from user in db.UserAccounts.AsNoTracking()
            join access in db.UserApplications.AsNoTracking() on user.UserId equals access.UserId
            where access.ApplicationId == applicationId
                && user.IsActive
                && access.IsActive
                && access.RevokedAt == null
            select user;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpper();
            query = query.Where(user =>
                user.EmployeeCode.ToUpper().Contains(term)
                || user.DisplayName.ToUpper().Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var users = await query
            .OrderBy(user => user.EmployeeCode)
            .Skip(skip)
            .Take(take)
            .Select(user => new { user.UserId, user.EmployeeCode, user.DisplayName })
            .ToListAsync(cancellationToken);
        var userIds = users.Select(user => user.UserId).ToArray();
        var assignments = await (
            from assignment in db.UserRoles.AsNoTracking()
            join role in db.Roles.AsNoTracking() on assignment.RoleId equals role.RoleId
            where assignment.ApplicationId == applicationId
                && role.ApplicationId == applicationId
                && userIds.Contains(assignment.UserId)
                && assignment.RevokedAt == null
                && role.IsActive
            orderby role.RoleName
            select new { assignment.UserId, role.RoleCode, role.RoleName })
            .ToListAsync(cancellationToken);
        var rolesByUser = assignments
            .GroupBy(assignment => assignment.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TerminalUserRole>)group
                    .Select(role => new TerminalUserRole(role.RoleCode, role.RoleName))
                    .ToArray());
        var items = users.Select(user => new TerminalDirectoryUser(
            user.EmployeeCode,
            user.DisplayName,
            rolesByUser.GetValueOrDefault(user.UserId, Array.Empty<TerminalUserRole>())))
            .ToArray();
        return new TerminalUserDirectoryPage(skip, take, total, items);
    }
}
