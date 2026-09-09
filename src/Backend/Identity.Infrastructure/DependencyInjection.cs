using Identity.Application.Administration;
using Identity.Application.Authentication;
using Identity.Application.Mfa;
using Identity.Application.BulkData;
using Identity.Application.Persistence;
using Identity.Application.Security;
using Identity.Infrastructure.Documents;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Persistence.Interceptors;
using Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return services.AddIdentityPersistence(_ => connectionString);
    }

    public static IServiceCollection AddIdentityPersistence(
        this IServiceCollection services,
        Func<IServiceProvider, string> connectionStringFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(connectionStringFactory);
        services.AddSingleton<AppendOnlyAuditInterceptor>();
        services.AddDbContext<IdentityDbContext>((provider, options) => options
            .UseSqlServer(RequiredConnectionString(connectionStringFactory(provider)),
                sql => sql.EnableRetryOnFailure())
            .AddInterceptors(provider.GetRequiredService<AppendOnlyAuditInterceptor>()));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<IdentityDbContext>());
        services.AddScoped<IAdministrationStore, AdministrationStore>();
        services.AddScoped<Identity.Application.Agents.IAgentRegistry, AgentRegistry>();
        services.AddScoped<Identity.Application.Agents.IAgentControlRegistry, AgentControlRegistry>();
        services.AddScoped<IUserCodeResolver>(
            provider => (AdministrationStore)provider.GetRequiredService<IAdministrationStore>());
        services.AddScoped<ICatalogCodeResolver>(
            provider => (AdministrationStore)provider.GetRequiredService<IAdministrationStore>());
        services.AddScoped<IAccessGrantLookup>(
            provider => (AdministrationStore)provider.GetRequiredService<IAdministrationStore>());
        services.AddScoped<IOrganizationStore, OrganizationStore>();
        services.AddScoped<IOrganizationCodeResolver>(
            provider => (OrganizationStore)provider.GetRequiredService<IOrganizationStore>());
        services.AddScoped<IAdministrationDiscoveryStore, AdministrationDiscoveryStore>();
        services.AddScoped<IAuthenticationStore, AuthenticationStore>();
        services.AddScoped<ITerminalEmployeeDirectory, TerminalEmployeeDirectory>();
        services.AddScoped<ITerminalUserDirectory, TerminalUserDirectory>();
        services.AddScoped<IMfaStore, MfaStore>();
        services.AddScoped<ITransactionRunner, TransactionRunner>();
        services.AddScoped<IBulkStagingStore, BulkStagingStore>();
        // User creation needs only this standalone one-way hasher; no signing or
        // protection key is required by administrative/seed-only processes.
        services.TryAddSingleton<IPinHasher, Pbkdf2PinHasher>();
        services.AddSingleton<IBulkRowSerializer, BulkRowSerializer>();
        services.AddSingleton<IBulkWorkbookWriter, ExcelWorkbookWriter>();
        services.AddSingleton<IBulkWorkbookReader, ExcelWorkbookReader>();
        services.AddSingleton<IBulkWorkbookAnnotator, ExcelWorkbookAnnotator>();
        return services;
    }

    public static IServiceCollection AddIdentitySecurity(
        this IServiceCollection services,
        JwtSigningOptions signingOptions,
        SecurityProtectionOptions? protectionOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(signingOptions);
        services.AddSingleton(signingOptions);
        if (protectionOptions is not null)
        {
            services.AddSingleton(protectionOptions);
        }

        return services.AddIdentitySecurityServices(protectionOptions is not null);
    }

    public static IServiceCollection AddIdentitySecurity(
        this IServiceCollection services,
        Func<IServiceProvider, JwtSigningOptions> signingOptionsFactory,
        Func<IServiceProvider, SecurityProtectionOptions> protectionOptionsFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(signingOptionsFactory);
        ArgumentNullException.ThrowIfNull(protectionOptionsFactory);
        services.AddSingleton(signingOptionsFactory);
        services.AddSingleton(protectionOptionsFactory);
        return services.AddIdentitySecurityServices(includeProtection: true);
    }

    private static IServiceCollection AddIdentitySecurityServices(
        this IServiceCollection services,
        bool includeProtection)
    {
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.TryAddSingleton<IPinHasher, Pbkdf2PinHasher>();
        services.AddSingleton<ISecretHasher, Sha256SecretHasher>();
        services.AddSingleton<IRandomTokenGenerator, RandomTokenGenerator>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<ITerminalAccessTokenValidator>(provider =>
            (JwtAccessTokenIssuer)provider.GetRequiredService<IAccessTokenIssuer>());
        if (includeProtection)
        {
            services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
            services.AddSingleton<IChallengeHasher, HmacChallengeHasher>();
            services.AddSingleton<IIdentifierHasher, HmacIdentifierHasher>();
            services.AddSingleton<ITotpService, TotpService>();
            services.AddSingleton<IAuditPayloadSerializer, AuditPayloadSerializer>();
        }

        return services;
    }

    private static string RequiredConnectionString(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return connectionString;
    }
}
