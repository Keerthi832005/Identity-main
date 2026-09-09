namespace Identity.AdminCli.Tests;

public sealed class BootstrapOptionsTests
{
    [Fact]
    public void Parse_AcceptsIdentifiersAndAppliesSafeDefaults()
    {
        var options = BootstrapOptions.Parse([
            "--employee-code", "ADMIN-001",
            "--display-name", "Initial Administrator",
            "--client-id", "identity-bootstrap-client",
            "--email", "administrator@example.com",
        ]);

        Assert.Equal("ADMIN-001", options.EmployeeCode);
        Assert.Equal("administrator@example.com", options.Email);
        Assert.Equal("iam-administration", options.ApplicationCode);
        Assert.Equal("urn:identity:administration", options.Audience);
    }

    [Fact]
    public void Parse_RejectsSecretsAndUnknownSwitches()
    {
        var exception = Assert.Throws<ArgumentException>(() => BootstrapOptions.Parse([
            "--employee-code", "ADMIN-001",
            "--display-name", "Initial Administrator",
            "--client-id", "identity-bootstrap-client",
            "--password", "must-not-be-accepted",
        ]));

        Assert.Contains("--password", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("must-not-be-accepted", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Host_InvalidCommandReturnsUsageWithoutReadingSecrets()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await AdminCliHost.Run(
            ["unknown"],
            output,
            error,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage:", error.ToString(), StringComparison.Ordinal);
    }
}
