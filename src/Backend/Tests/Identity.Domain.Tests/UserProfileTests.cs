using Identity.Domain.Entities;

namespace Identity.Domain.Tests;

public sealed class UserProfileTests
{
    [Fact]
    public void Create_StoresOptionalEmailAndManager()
    {
        var user = UserAccount.Create("EMP-1", "Employee", DateTime.UtcNow, " employee@example.com ", 7);
        Assert.Equal("employee@example.com", user.Email);
        Assert.Equal(7, user.ManagerUserId);
        Assert.Null(UserAccount.Create("EMP-2", "Employee", DateTime.UtcNow).Email);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("person name@example.com")]
    [InlineData("Person <person@example.com>")]
    [InlineData("person@example.com\r\nBcc: other@example.com")]
    public void Create_RejectsMalformedEmail(string email) =>
        Assert.Throws<ArgumentException>(() => UserAccount.Create("EMP-1", "Employee", DateTime.UtcNow, email));

    [Fact]
    public void Create_RejectsOversizedEmailAndInvalidManager()
    {
        Assert.Throws<ArgumentException>(() => UserAccount.Create("EMP-1", "Employee", DateTime.UtcNow,
            new string('a', 250) + "@example.com"));
        Assert.Throws<ArgumentOutOfRangeException>(() => UserAccount.Create("EMP-1", "Employee", DateTime.UtcNow, null, 0));
    }

    [Fact]
    public void UpdateProfile_IsRepeatableAndDoesNotGrantAccessOrChangeSecurityVersion()
    {
        var user = UserAccount.Create("EMP-1", "Employee", DateTime.UtcNow, "employee@example.com", 7);
        var changedAt = DateTime.UtcNow.AddMinutes(1);
        Assert.True(user.UpdateProfile("Employee updated", null, null, changedAt));
        Assert.False(user.UpdateProfile("Employee updated", " ", null, changedAt.AddMinutes(1)));
        Assert.Null(user.Email);
        Assert.Null(user.ManagerUserId);
        Assert.Equal(changedAt, user.UpdatedAt);
        Assert.Equal(1, user.SecurityVersion);
        Assert.Equal("EMP-1", user.EmployeeCode);
    }
}
