using System.Security.Cryptography;
using Identity.Application.Authentication;
using Identity.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure.Tests;

public sealed class TerminalAccessTokenTests
{
    [Fact]
    public void IssuesApplicationRoleClaimsWithoutGrantingCapabilities()
    {
        using var rsa = RSA.Create(2048);
        using var services = new ServiceCollection().AddIdentitySecurity(
            new JwtSigningOptions("https://identity.test", "fixture", rsa.ExportPkcs8PrivateKeyPem())).BuildServiceProvider();
        var issuer = services.GetRequiredService<IAccessTokenIssuer>();
        var now = DateTime.UtcNow;
        var request = new AccessTokenRequest(42, "EMP42", "Employee", 8, "ams-api", "ams-web", 1, 1,
            ["asset.view"], now, now.AddMinutes(15))
        {
            RoleCodes = ["employee", "approver", "employee"],
        };
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(issuer.Issue(request).Token);
        Assert.Equal(["employee", "approver"], token.Claims.Where(claim => claim.Type == "application_role").Select(claim => claim.Value));
        Assert.Equal(["asset.view"], token.Claims.Where(claim => claim.Type == "capability").Select(claim => claim.Value));
        var noRoles = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(
            issuer.Issue(request with { RoleCodes = [] }).Token);
        Assert.DoesNotContain(noRoles.Claims, claim => claim.Type == "application_role");
    }

    [Fact]
    public void ValidatesSignatureIssuerAudienceLifetimeAndRequiredClaims()
    {
        using var rsa = RSA.Create(2048);
        using var services = new ServiceCollection().AddIdentitySecurity(
            new JwtSigningOptions("https://identity.test", "fixture", rsa.ExportPkcs8PrivateKeyPem())).BuildServiceProvider();
        var issuer = services.GetRequiredService<IAccessTokenIssuer>();
        var validator = services.GetRequiredService<ITerminalAccessTokenValidator>();
        var now = DateTime.UtcNow;
        var request = new AccessTokenRequest(42, "EMP42", "Employee Forty Two", 7, "pts-api", "pts-web", 3, 5,
            ["pts.shopfloor.operate", "pts.production.read"], now, now.AddMinutes(15));
        var token = issuer.Issue(request).Token;
        Assert.Equal(new TerminalTokenIdentity(42, 7, "pts-web", 3, 5), validator.Validate(token, "pts-api", now));
        Assert.Null(validator.Validate(token, "identity-admin", now));
        Assert.Null(validator.Validate(token, "pts-api", now.AddMinutes(16)));
        Assert.Null(validator.Validate(token, "pts-api", now.AddMinutes(-1)));
        Assert.Null(validator.Validate("invalid", "pts-api", now));
        Assert.Null(validator.Validate(issuer.Issue(request with { UserId = 0 }).Token, "pts-api", now));
        Assert.Null(validator.Validate(issuer.Issue(request with { SecurityVersion = 0 }).Token, "pts-api", now));
        using var otherRsa = RSA.Create(2048);
        using var other = new ServiceCollection().AddIdentitySecurity(
            new JwtSigningOptions("https://identity.test", "fixture", otherRsa.ExportPkcs8PrivateKeyPem())).BuildServiceProvider();
        Assert.Null(validator.Validate(other.GetRequiredService<IAccessTokenIssuer>().Issue(request).Token, "pts-api", now));
        using var wrongIssuer = new ServiceCollection().AddIdentitySecurity(
            new JwtSigningOptions("https://other.test", "fixture", rsa.ExportPkcs8PrivateKeyPem())).BuildServiceProvider();
        Assert.Null(validator.Validate(wrongIssuer.GetRequiredService<IAccessTokenIssuer>().Issue(request).Token, "pts-api", now));
    }
}
