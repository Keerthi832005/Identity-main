using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Tests;

public sealed class AgentInventoryProjectionTests
{
    [Fact]
    public void Current_user_is_projected_in_sql_without_loading_every_inventory_document()
    {
        using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer("Server=unused;Database=translation-only;Integrated Security=true").Options);
        var sql = db.Set<AgentInstallation>().Select(value => new
        {
            CurrentUser = IdentityDbContext.JsonValue(value.InventoryJson, "$.windowsUsers.currentUserName"),
            Reported = IdentityDbContext.JsonValue(value.InventoryJson, "$.windowsUsers.sessionsReported") == "true",
        }).ToQueryString();
        Assert.Contains("JSON_VALUE", sql);
        Assert.Contains("$.windowsUsers.currentUserName", sql);
    }
}
