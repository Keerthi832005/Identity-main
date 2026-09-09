using Identity.AdminCli;

namespace Identity.AdminCli.Tests;

public sealed class ProvisionAdministrationWebOptionsTests
{
    [Fact]
    public void Parse_RequiresOneEmployeeCode()
    {
        var options = ProvisionAdministrationWebOptions.Parse(
            ["--employee-code", " PTS-ADMIN "]);

        Assert.Equal("PTS-ADMIN", options.EmployeeCode);
        Assert.Throws<ArgumentException>(() =>
            ProvisionAdministrationWebOptions.Parse([]));
        Assert.Throws<ArgumentException>(() =>
            ProvisionAdministrationWebOptions.Parse(["--employee-code", " "]));
    }
}
