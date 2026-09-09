namespace Identity.AdminCli.Tests;

public sealed class ProvisionApplicationOptionsTests
{
    [Fact]
    public void Parse_RequiresManifestAndPositiveActor()
    {
        var options = ProvisionApplicationOptions.Parse([
            "--manifest", "application.json",
            "--actor-user-id", "42",
        ]);

        Assert.Equal("application.json", options.ManifestPath);
        Assert.Equal(42, options.ActorUserId);
        Assert.Equal("iam-administration", options.AdministrationApplicationCode);
        Assert.Throws<ArgumentException>(() => ProvisionApplicationOptions.Parse([
            "--manifest", "application.json",
            "--actor-user-id", "0",
        ]));
    }

    [Fact]
    public void Manifest_RejectsUnknownRoleCapabilityAndDuplicateCodes()
    {
        var unknown = CreateManifest(
            [new ApplicationRoleManifest(
                "operator",
                "Operator",
                null,
                true,
                ["application.unknown"])]);
        var duplicate = CreateManifest(
            [new ApplicationRoleManifest(
                "operator",
                "Operator",
                null,
                true,
                ["application.use", "application.use"])]);

        Assert.Throws<ApplicationProvisioningException>(unknown.Validate);
        Assert.Throws<ApplicationProvisioningException>(duplicate.Validate);
    }

    [Fact]
    public async Task Host_ValidatesManifestWithoutDatabaseConfiguration()
    {
        var manifestPath = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(
                manifestPath,
                """
                {
                  "applicationCode": "application-test",
                  "applicationName": "Application Test",
                  "description": null,
                  "audience": "application-test-api",
                  "accessTokenLifetimeMinutes": 15,
                  "refreshTokenLifetimeDays": 7,
                  "publicClients": [{ "clientId": "application-test-web", "clientName": "Web" }],
                  "modules": [{
                    "moduleCode": "application",
                    "moduleName": "Application",
                    "description": null,
                    "displayOrder": 0,
                    "isSystem": true,
                    "capabilities": [{
                      "capabilityCode": "application.use",
                      "capabilityName": "Use application",
                      "description": null
                    }]
                  }],
                  "roles": [{
                    "roleCode": "operator",
                    "roleName": "Operator",
                    "description": null,
                    "isSystem": true,
                    "capabilityCodes": ["application.use"]
                  }]
                }
                """,
                TestContext.Current.CancellationToken);
            using var output = new StringWriter();
            using var error = new StringWriter();

            var exitCode = await AdminCliHost.Run(
                ["validate-application-manifest", "--manifest", manifestPath],
                output,
                error,
                TestContext.Current.CancellationToken);

            Assert.Equal(0, exitCode);
            Assert.Empty(error.ToString());
            Assert.Contains("\"CapabilityCount\":1", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("\"RoleCapabilityGrantCount\":1", output.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(manifestPath);
        }
    }

    private static ApplicationProvisioningManifest CreateManifest(
        IReadOnlyList<ApplicationRoleManifest> roles) => new(
        "application-test",
        "Application Test",
        null,
        "application-test-api",
        15,
        7,
        [new PublicClientManifest("application-test-web", "Application Test Web")],
        [new ApplicationModuleManifest(
            "application",
            "Application",
            null,
            0,
            true,
            [new ModuleCapabilityManifest("application.use", "Use application", null)])],
        roles);
}
