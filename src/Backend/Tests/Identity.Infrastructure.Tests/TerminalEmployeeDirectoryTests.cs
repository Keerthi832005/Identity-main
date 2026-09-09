using Identity.Application.Authentication;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Tests;

public sealed class TerminalEmployeeDirectoryTests
{
    private static readonly DateTime Now = new(2026, 8, 31, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Suggestions_exclude_inactive_users_revoked_grants_and_other_applications()
    {
        var users = Enumerable.Range(1, 5).Select(i => WithId(UserAccount.Create($"EMP00{i}", $"Person {i}", Now), "UserId", i)).ToArray();
        users[1].SetActive(false, Now);
        var grants = Enumerable.Range(1, 4).Select(i => UserApplicationAccess.Create(i, i == 4 ? 2 : 1, null, Now)).ToArray();
        grants[2].Revoke(null, Now);
        var result = Query(users, grants, "MP0").ToArray();
        var employee = Assert.Single(result);
        Assert.Equal("EMP001", employee.EmployeeCode);
        Assert.Equal("Person 1", employee.DisplayName);
    }

    [Theory]
    [InlineData("wrong-client", false, false, false, false)]
    [InlineData("pts-web", true, false, false, false)]
    [InlineData("pts-web", false, true, false, false)]
    [InlineData("pts-web", false, false, true, false)]
    [InlineData("pts-web", false, false, false, true)]
    public void Invalid_client_or_application_returns_no_directory(
        string clientId, bool revoked, bool expired, bool service, bool inactiveApplication)
    {
        var user = WithId(UserAccount.Create("EMP001", "Person", Now), "UserId", 1);
        var app = Application(1, "production-tracking");
        if (inactiveApplication) app.SetActive(false, Now);
        var client = ApplicationClient.Create(1, "pts-web", "PTS", service ? ApplicationClientType.Service : ApplicationClientType.Public,
            service ? new byte[32] : null, Now.AddDays(-1), expired ? Now : null);
        if (revoked) client.Revoke(Now);
        Assert.Empty(TerminalEmployeeDirectory.Candidates(new[] { user }.AsQueryable(),
            new[] { UserApplicationAccess.Create(1, 1, null, Now) }.AsQueryable(), new[] { app }.AsQueryable(),
            new[] { client }.AsQueryable(), clientId, "EMP", Now));
    }

    [Fact]
    public void Contains_search_is_case_insensitive_bounded_and_treats_wildcards_literally()
    {
        var users = Enumerable.Range(1, 25).Select(i => WithId(UserAccount.Create($"EMP{i:000}", $"Person {i}", Now), "UserId", i)).ToArray();
        var grants = users.Select(user => UserApplicationAccess.Create(user.UserId, 1, null, Now)).ToArray();
        Assert.Equal(10, Query(users, grants, "emp").Count());
        Assert.Equal("EMP012", Assert.Single(Query(users, grants, "emp012")).EmployeeCode);
        Assert.NotEmpty(Query(users, grants, "person 2"));
        Assert.Empty(Query(users, grants, "EMP%"));
        Assert.Empty(Query(users, grants, "EMP_"));
        Assert.Empty(Query(users, grants, "[EMP]"));
    }

    [Theory]
    [InlineData("3275")]
    [InlineData("e032")]
    [InlineData("deswar")]
    public void Partial_code_and_middle_of_name_find_the_full_employee(string search)
    {
        var user = WithId(UserAccount.Create("INDE03275", "Siddeswaran S", Now), "UserId", 1);
        var result = Assert.Single(Query([user], [UserApplicationAccess.Create(1, 1, null, Now)], search));
        Assert.Equal("INDE03275", result.EmployeeCode);
        Assert.Equal("Siddeswaran S", result.DisplayName);
    }

    [Fact]
    public void Exact_then_prefix_codes_rank_before_substring_matches()
    {
        var users = new[] { "INDE03275", "32750", "3275", "A3275" }
            .Select((code, i) => WithId(UserAccount.Create(code, "Person", Now), "UserId", i + 1)).ToArray();
        var grants = users.Select(user => UserApplicationAccess.Create(user.UserId, 1, null, Now)).ToArray();
        Assert.Equal(["3275", "32750", "A3275", "INDE03275"],
            Query(users, grants, "3275").Select(user => user.EmployeeCode).ToArray());
    }

    [Fact]
    public void Query_translates_to_bounded_sql_and_returns_only_code_and_name()
    {
        using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer("Server=unused;Database=translation-only;Integrated Security=true").Options);
        var sql = TerminalEmployeeDirectory.Candidates(db.UserAccounts, db.UserApplications,
            db.Applications, db.ApplicationClients, "pts-web", "3275", Now).ToQueryString();
        Assert.Contains("TOP(", sql);
        Assert.Contains("EXISTS", sql);
        Assert.Contains("production-tracking", sql);
        Assert.Contains("RevokedAt", sql);
        Assert.Contains("%3275%", sql);
        Assert.DoesNotContain("[Email]", sql);
        Assert.DoesNotContain("[SecurityVersion]", sql);
    }

    [Fact]
    public void Contains_query_escapes_sql_wildcards_in_search_text()
    {
        using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer("Server=unused;Database=translation-only;Integrated Security=true").Options);
        var sql = TerminalEmployeeDirectory.Candidates(db.UserAccounts, db.UserApplications,
            db.Applications, db.ApplicationClients, "pts-web", "EMP%_[", Now).ToQueryString();
        Assert.Contains("%EMP\\%\\_\\[%", sql);
        Assert.Contains("ESCAPE N'\\'", sql);
    }

    private static IQueryable<TerminalEmployeeSuggestion> Query(UserAccount[] users, UserApplicationAccess[] grants, string search = "EMP") =>
        TerminalEmployeeDirectory.Candidates(users.AsQueryable(), grants.AsQueryable(),
            new[] { Application(1, "production-tracking"), Application(2, "iam") }.AsQueryable(),
            new[] { ApplicationClient.Create(1, "pts-web", "PTS", ApplicationClientType.Public, null, Now, null) }.AsQueryable(),
            "pts-web", search, Now);

    private static RegisteredApplication Application(long id, string code) => WithId(
        RegisteredApplication.Create(code, code, null, code, 10, 7, Now), "ApplicationId", id);

    private static T WithId<T>(T entity, string property, long id)
    {
        typeof(T).GetProperty(property)!.SetValue(entity, id);
        return entity;
    }
}
