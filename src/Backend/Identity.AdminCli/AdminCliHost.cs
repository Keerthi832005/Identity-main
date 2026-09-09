using System.Reflection;
using System.Text.Json;
using Identity.Application;
using Identity.Application.Administration;
using Identity.Application.Authentication;
using Identity.Application.Security;
using Identity.Infrastructure;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.AdminCli;

public static class AdminCliHost
{
    public static async Task<int> Run(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);
        if (args is ["--version"])
        {
            await output.WriteLineAsync(
                Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown");
            return 0;
        }

        if (args.Count == 0)
        {
            await WriteUsage(error);
            return 2;
        }

        return args[0] switch
        {
            "seed-default-administrator" => await RunSeedDefaultAdministrator(
                args.Skip(1).ToArray(), output, error, cancellationToken),
            "seed-default-employees" => await RunSeedDefaultEmployees(
                args.Skip(1).ToArray(), output, error, cancellationToken),
            "bootstrap" => await RunBootstrap(
                args.Skip(1).ToArray(),
                output,
                error,
                cancellationToken),
            "provision-application" => await RunProvisionApplication(
                args.Skip(1).ToArray(),
                output,
                error,
                cancellationToken),
            "provision-administration-web" => await RunProvisionAdministrationWeb(
                args.Skip(1).ToArray(),
                output,
                error,
                cancellationToken),
            "validate-application-manifest" => await RunValidateApplicationManifest(
                args.Skip(1).ToArray(),
                output,
                error,
                cancellationToken),
            _ => await UnknownCommand(error),
        };
    }

    private static async Task<int> RunSeedDefaultAdministrator(
        IReadOnlyList<string> args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        BootstrapSecrets? secrets = null;
        try
        {
            if (args.Count != 0 && args is not ["--apply"])
                throw new ArgumentException("Use seed-default-administrator [--apply]. Credentials must come from the secure environment, never arguments.");
            BootstrapSecrets LoadSecrets() => secrets ??= BootstrapSecrets.Load();
            var services = new ServiceCollection();
            services.AddSingleton<IAdministrationAuthorizer, BootstrapAdministrationAuthorizer>();
            services.AddIdentityApplication();
            services.AddIdentityPersistence(RequiredEnvironment("IDENTITY_DATABASE_CONNECTION"));
            // Lazy factories allow a read-only preview or verified replay without bootstrap secrets.
            services.AddIdentitySecurity(_ =>
            {
                var value = LoadSecrets();
                return new JwtSigningOptions(value.Issuer, value.SigningKeyId, value.PrivateKeyPem);
            }, _ =>
            {
                var value = LoadSecrets();
                return new SecurityProtectionOptions(value.SecurityKeyId, value.EncryptionKey, value.ChallengeKey, value.IdentifierHashKey);
            });
            services.AddScoped<BootstrapRunner>();
            services.AddScoped<ApplicationProvisioningRunner>();
            services.AddScoped<AdministrationWebProvisioningRunner>();
            services.AddScoped<DefaultAdministratorSeedRunner>();
            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<DefaultAdministratorSeedRunner>().Run(
                args.Count == 1, async token =>
                {
                    var value = LoadSecrets();
                    await ValidateBootstrapEnvironment(scope.ServiceProvider, token);
                    return await scope.ServiceProvider.GetRequiredService<BootstrapRunner>().Run(
                        DefaultAdministratorSeedRunner.Profile, value.Password, value.ClientSecret, token);
                }, cancellationToken);
            await output.WriteLineAsync(JsonSerializer.Serialize(result));
            return 0;
        }
        catch (ArgumentException exception)
        {
            await error.WriteLineAsync(exception.Message);
            return 2;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("Default administrator seed failed; no seed changes committed. Check the migrated target, existing account/access and secured bootstrap environment. Existing passwords and permissions were not reset.");
            return 1;
        }
        finally
        {
            if (secrets is not null)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(secrets.EncryptionKey);
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(secrets.ChallengeKey);
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(secrets.IdentifierHashKey);
            }
        }
    }

    private static async Task<int> RunSeedDefaultEmployees(
        IReadOnlyList<string> args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        try
        {
            var options = SeedDefaultEmployeesOptions.Parse(args);
            var roster = DefaultEmployeeRoster.Load();
            var services = new ServiceCollection();
            services.AddSingleton(new ProvisioningActor(options.ActorUserId, "iam-administration", "iam.admin"));
            services.AddScoped<IAdministrationAuthorizer, ProvisioningAdministrationAuthorizer>();
            services.AddIdentityApplication();
            services.AddIdentityPersistence(RequiredEnvironment("IDENTITY_DATABASE_CONNECTION"));
            services.AddScoped<DefaultEmployeeSeedRunner>();
            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<DefaultEmployeeSeedRunner>()
                .Run(roster, options, cancellationToken);
            await output.WriteLineAsync(JsonSerializer.Serialize(result));
            return 0;
        }
        catch (ArgumentException exception)
        {
            await error.WriteLineAsync(exception.Message);
            return 2;
        }
        catch (UnauthorizedAccessException)
        {
            await error.WriteLineAsync("Employee seed denied. The actor must have active iam.admin access.");
            return 1;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("Employee seed failed; no seed changes committed. Verify IAM migrations, connection and concurrent user creation before retrying.");
            return 1;
        }
    }

    private static async Task<int> RunValidateApplicationManifest(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = ValidateApplicationManifestOptions.Parse(args);
            var manifest = await ApplicationProvisioningManifest.Load(
                options.ManifestPath,
                cancellationToken);
            await output.WriteLineAsync(JsonSerializer.Serialize(ApplicationManifestSummary.From(manifest)));
            return 0;
        }
        catch (ArgumentException exception)
        {
            await error.WriteLineAsync(exception.Message);
            return 2;
        }
        catch (ApplicationProvisioningException exception)
        {
            await error.WriteLineAsync($"Manifest validation failed: {exception.Message}");
            return 1;
        }
        catch (JsonException)
        {
            await error.WriteLineAsync("Manifest validation failed. The application manifest is invalid.");
            return 1;
        }
        catch (IOException)
        {
            await error.WriteLineAsync("Manifest validation failed. The application manifest cannot be read.");
            return 1;
        }
        catch (UnauthorizedAccessException)
        {
            await error.WriteLineAsync("Manifest validation failed. The application manifest cannot be read.");
            return 1;
        }
    }

    private static async Task<int> RunBootstrap(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        BootstrapSecrets? secrets = null;
        try
        {
            var options = BootstrapOptions.Parse(args);
            secrets = BootstrapSecrets.Load();
            var services = new ServiceCollection();
            services.AddSingleton<IAdministrationAuthorizer, BootstrapAdministrationAuthorizer>();
            services.AddIdentityApplication();
            services.AddIdentityPersistence(secrets.ConnectionString);
            services.AddIdentitySecurity(
                new JwtSigningOptions(secrets.Issuer, secrets.SigningKeyId, secrets.PrivateKeyPem),
                new SecurityProtectionOptions(
                    secrets.SecurityKeyId,
                    secrets.EncryptionKey,
                    secrets.ChallengeKey,
                    secrets.IdentifierHashKey));
            services.AddScoped<BootstrapRunner>();
            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            await ValidateBootstrapEnvironment(scope.ServiceProvider, cancellationToken);
            var result = await scope.ServiceProvider.GetRequiredService<BootstrapRunner>().Run(
                options,
                secrets.Password,
                secrets.ClientSecret,
                cancellationToken);
            await output.WriteLineAsync(JsonSerializer.Serialize(result));
            return 0;
        }
        catch (ArgumentException exception)
        {
            await error.WriteLineAsync(exception.Message);
            return 2;
        }
        catch (InvalidOperationException)
        {
            await error.WriteLineAsync(
                "Bootstrap failed. Verify the environment, empty migrated database, and secured logs.");
            return 1;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync(
                "Bootstrap failed. Verify the environment, empty migrated database, and secured logs.");
            return 1;
        }
        finally
        {
            if (secrets is not null)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(secrets.EncryptionKey);
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(secrets.ChallengeKey);
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(secrets.IdentifierHashKey);
            }
        }
    }

    private static async Task<int> RunProvisionApplication(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = ProvisionApplicationOptions.Parse(args);
            var manifest = await ApplicationProvisioningManifest.Load(
                options.ManifestPath,
                cancellationToken);
            var services = new ServiceCollection();
            services.AddSingleton(new ProvisioningActor(
                options.ActorUserId,
                options.AdministrationApplicationCode,
                "iam.admin"));
            services.AddScoped<IAdministrationAuthorizer, ProvisioningAdministrationAuthorizer>();
            services.AddIdentityApplication();
            services.AddIdentityPersistence(RequiredEnvironment("IDENTITY_DATABASE_CONNECTION"));
            services.AddScoped<ApplicationProvisioningRunner>();
            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                throw new InvalidOperationException("The Identity database is unavailable.");
            }

            var result = await scope.ServiceProvider
                .GetRequiredService<ApplicationProvisioningRunner>()
                .Run(manifest, options.ActorUserId, cancellationToken);
            await output.WriteLineAsync(JsonSerializer.Serialize(result));
            return 0;
        }
        catch (ArgumentException exception)
        {
            await error.WriteLineAsync(exception.Message);
            return 2;
        }
        catch (ApplicationProvisioningException exception)
        {
            await error.WriteLineAsync($"Provisioning failed: {exception.Message}");
            return 1;
        }
        catch (UnauthorizedAccessException)
        {
            await error.WriteLineAsync(
                "Provisioning failed. The actor must have the iam.admin capability.");
            return 1;
        }
        catch (System.Text.Json.JsonException)
        {
            await error.WriteLineAsync("Provisioning failed. The application manifest is invalid.");
            return 1;
        }
        catch (IOException)
        {
            await error.WriteLineAsync("Provisioning failed. The application manifest cannot be read.");
            return 1;
        }
        catch (InvalidOperationException)
        {
            await error.WriteLineAsync(
                "Provisioning failed. Verify the migrated database and environment configuration.");
            return 1;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync(
                "Provisioning failed. Verify the migrated database and environment configuration.");
            return 1;
        }
    }

    private static async Task<int> RunProvisionAdministrationWeb(
        IReadOnlyList<string> args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        try
        {
            var options = ProvisionAdministrationWebOptions.Parse(args);
            var services = new ServiceCollection();
            services.AddSingleton<IAdministrationAuthorizer, BootstrapAdministrationAuthorizer>();
            services.AddIdentityApplication();
            services.AddIdentityPersistence(RequiredEnvironment("IDENTITY_DATABASE_CONNECTION"));
            services.AddScoped<ApplicationProvisioningRunner>();
            services.AddScoped<AdministrationWebProvisioningRunner>();
            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                throw new InvalidOperationException("The Identity database is unavailable.");
            }

            var result = await scope.ServiceProvider
                .GetRequiredService<AdministrationWebProvisioningRunner>()
                .Run(options, cancellationToken);
            await output.WriteLineAsync(JsonSerializer.Serialize(result));
            return 0;
        }
        catch (ArgumentException exception)
        {
            await error.WriteLineAsync(exception.Message);
            return 2;
        }
        catch (ApplicationProvisioningException exception)
        {
            await error.WriteLineAsync($"Administration web provisioning failed: {exception.Message}");
            return 1;
        }
        catch (InvalidOperationException exception)
        {
            await error.WriteLineAsync($"Administration web provisioning failed: {exception.Message}");
            return 1;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync(
                "Administration web provisioning failed. Verify the migrated database and user state.");
            return 1;
        }
    }

    private static string RequiredEnvironment(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Environment variable '{name}' is required.");

    private static async Task<int> UnknownCommand(TextWriter error)
    {
        await WriteUsage(error);
        return 2;
    }

    private static async Task WriteUsage(TextWriter error)
    {
        await error.WriteLineAsync(
            "   Identity.AdminCli seed-default-administrator [--apply] (INDE03275; preview without --apply)");
        await error.WriteLineAsync(
            "   Identity.AdminCli seed-default-employees --actor-user-id VALUE [--apply] (preview without --apply)");
        await error.WriteLineAsync(
            "Usage: Identity.AdminCli bootstrap --employee-code VALUE --display-name VALUE --client-id VALUE [--email VALUE]");
        await error.WriteLineAsync(
            "   or: Identity.AdminCli provision-application --manifest PATH --actor-user-id VALUE");
        await error.WriteLineAsync(
            "   or: Identity.AdminCli provision-administration-web --employee-code VALUE");
        await error.WriteLineAsync(
            "   or: Identity.AdminCli validate-application-manifest --manifest PATH");
    }

    private static async Task ValidateBootstrapEnvironment(
        IServiceProvider provider,
        CancellationToken cancellationToken)
    {
        _ = provider.GetRequiredService<IAccessTokenIssuer>().GetSigningMetadata();
        _ = provider.GetRequiredService<ISecretProtector>();
        _ = provider.GetRequiredService<IChallengeHasher>();
        _ = provider.GetRequiredService<IIdentifierHasher>();
        var dbContext = provider.GetRequiredService<IdentityDbContext>();
        if (!await dbContext.Database.CanConnectAsync(cancellationToken)
            || await dbContext.Applications.AnyAsync(cancellationToken)
            || await dbContext.UserAccounts.AnyAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                "Bootstrap requires an empty migrated Identity database.");
        }
    }
}
