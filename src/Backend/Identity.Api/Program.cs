using System.Security.Cryptography;
using Identity.Api.Administration;
using Identity.Api.Endpoints;
using Identity.Api.Errors;
using Identity.Api.Health;
using Identity.Api.Hosting;
using Identity.Api.RateLimiting;
using Identity.Api.Diagnostics;
using Identity.Application;
using Identity.Application.Administration;
using Identity.Infrastructure;
using Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 64 * 1024);
builder.Services.AddSingleton(provider => IdentityHostSettings.Load(
    provider.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<RSA>(provider =>
{
    var settings = provider.GetRequiredService<IdentityHostSettings>();
    var rsa = RSA.Create();
    rsa.ImportFromPem(settings.PrivateKeyPem);
    return rsa;
});
builder.Services.AddHostedService<HostSettingsValidationService>();
builder.Services.AddHostedService<BulkStagingExpiryService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddIdentityApplication();
builder.Services.AddIdentityPersistence(provider =>
    provider.GetRequiredService<IdentityHostSettings>().ConnectionString);
builder.Services.AddIdentitySecurity(
    provider =>
    {
        var settings = provider.GetRequiredService<IdentityHostSettings>();
        return new JwtSigningOptions(settings.Issuer, settings.KeyId, settings.PrivateKeyPem);
    },
    provider =>
    {
        var settings = provider.GetRequiredService<IdentityHostSettings>();
        return new SecurityProtectionOptions(
            settings.SecurityKeyId,
            settings.EncryptionKey,
            settings.ChallengeKey,
            settings.IdentifierHashKey);
    });
builder.Services.AddScoped<IAdministrationAuthorizer, ClaimsAdministrationAuthorizer>();
builder.Services.AddScoped<IReadinessProbe, DatabaseReadinessProbe>();
builder.Services.AddSingleton<IMachinePingTransport, SystemMachinePingTransport>();
builder.Services.AddSingleton<IMachinePingService, MachinePingService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IdentityHostSettings, RSA>((options, settings, validationRsa) =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = settings.Issuer,
            ValidateAudience = true,
            ValidAudience = settings.AdministrationAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new RsaSecurityKey(validationRsa) { KeyId = settings.KeyId },
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        options.Events = BearerEvents.Create();
    });
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AdministrationPolicy.Name, policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim(AdministrationPolicy.CapabilityClaim, AdministrationPolicy.RequiredCapability));
builder.Services.AddConfiguredForwardedHeaders(builder.Configuration);
builder.Services.AddIdentityRateLimiting();

var app = builder.Build();
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<RequestSizeLimitMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapIdentityHealthEndpoints();
app.MapIdentityDiscoveryEndpoints();
app.MapIdentityAuthenticationEndpoints();
app.MapIdentityAdministrationEndpoints();
app.MapAgentEndpoints();
app.MapAgentDistributionEndpoints();
app.MapOrganizationEndpoints()
    .MapBulkDataEndpoints();
app.MapOpenApi().AllowAnonymous();

app.Run();

public partial class Program;
