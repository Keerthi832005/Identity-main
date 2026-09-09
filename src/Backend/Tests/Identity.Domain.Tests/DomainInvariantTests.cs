using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.Domain.Tests;

public sealed class DomainInvariantTests
{
    private static readonly DateTime Now = new(2026, 8, 29, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PublicApplicationClient_RejectsSecretHash()
    {
        var exception = Assert.Throws<ArgumentException>(() => ApplicationClient.Create(
            1,
            "browser",
            "Browser",
            ApplicationClientType.Public,
            new byte[32],
            Now,
            null));

        Assert.Contains("Public clients", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DisablingUser_IncrementsSecurityVersionOnce()
    {
        var user = UserAccount.Create("E100", "Example User", Now);

        user.SetActive(false, Now.AddMinutes(1));
        user.SetActive(false, Now.AddMinutes(2));

        Assert.False(user.IsActive);
        Assert.Equal(2, user.SecurityVersion);
        Assert.Equal(Now.AddMinutes(1), user.UpdatedAt);
    }

    [Fact]
    public void PermissionOverride_PreservesExplicitDeny()
    {
        var permissionOverride = UserPermissionOverride.Create(
            10,
            20,
            30,
            PermissionEffect.Deny,
            "Temporary segregation of duties",
            40,
            Now,
            Now.AddDays(1));

        Assert.Equal(PermissionEffect.Deny, permissionOverride.Effect);
        Assert.Equal("Temporary segregation of duties", permissionOverride.Reason);
    }

    [Fact]
    public void TrustedDevice_WithoutTerminalServiceAccount_CannotAttest()
    {
        var device = TerminalDevice();

        device.Trust(Now, TimeSpan.FromDays(30));

        Assert.True(device.IsTrustedAt(Now.AddDays(1)));
        Assert.False(device.CanAttestAt(Now.AddDays(1)));
    }

    [Fact]
    public void TrustedDevice_WithTerminalServiceAccount_AttestsUntilTrustExpires()
    {
        var device = TerminalDevice();

        device.Trust(Now, TimeSpan.FromDays(30));
        device.AssignTerminalService(500);

        Assert.Equal(500, device.TerminalServiceUserId);
        Assert.True(device.CanAttestAt(Now.AddDays(1)));
        Assert.False(device.CanAttestAt(Now.AddDays(31)));
    }

    [Fact]
    public void RevokingDevice_StopsAttestationAndRejectsReassignment()
    {
        var device = TerminalDevice();
        device.Trust(Now, TimeSpan.FromDays(30));
        device.AssignTerminalService(500);

        device.Revoke(9, Now.AddDays(2));

        Assert.False(device.CanAttestAt(Now.AddDays(3)));
        Assert.Throws<InvalidOperationException>(() => device.AssignTerminalService(500));
    }

    [Fact]
    public void ClearingTerminalServiceAccount_RefusesAttestationWhileTrustRemains()
    {
        var device = TerminalDevice();
        device.Trust(Now, TimeSpan.FromDays(30));
        device.AssignTerminalService(500);

        device.AssignTerminalService(null);

        Assert.Null(device.TerminalServiceUserId);
        Assert.True(device.IsTrustedAt(Now.AddDays(1)));
        Assert.False(device.CanAttestAt(Now.AddDays(1)));
    }

    [Fact]
    public void TerminalServiceAccount_RejectsNonPositiveUser()
    {
        var device = TerminalDevice();

        Assert.Throws<ArgumentOutOfRangeException>(() => device.AssignTerminalService(0));
    }

    private static Device TerminalDevice() => Device.Create(
        7,
        "HOCOM18502627",
        "Terminal",
        new byte[32],
        null,
        null,
        Now);
}
