# Consumer Integration

## Trust boundary

Consumers validate IAM access tokens locally through the HTTPS issuer metadata and public JSON Web Key Set. They must not reference an IAM production project, connect to the IAM database, or receive the RSA private key.

Configure each consumer with values registered for its IAM application client:

| Setting | Required value |
|---|---|
| Authority | Exact IAM `Identity:Jwt:Issuer`, without an application-specific path |
| Audience | Application client's registered audience, for example `pts-api` |
| Permission claim | `capability` |
| Required capability | Registered capability code, for example `pts.api` |
| HTTPS metadata | Required outside loopback development |

The hostname in `Identity:Jwt:Issuer` must also appear in the IAM API `AllowedHosts` setting. A mismatch causes discovery and key requests to fail with HTTP 400.

For the agreed production target, the issuer and consumer authority are **`https://iam.fujitecindia.com`**. PTS app/API is client-facing at **`https://pts.fujitecindia.com`**, with Portal `/`, Terminal `/terminal/` and isolated `/ds4` and `/qs4` API prefixes. Do not set authority to the PTS host, an internal port, or `/identity`: that prefix is a browser proxy, not the issuer. PTS browser login remains same-origin through `https://pts.fujitecindia.com/identity/...`; launching IAM administration opens `https://iam.fujitecindia.com` without transferring tokens. See the [planned production topology](../../DEPLOYMENT.md#production-hostnames-and-topology); local `.local` configuration and historical test evidence do not provision production.

## Discovery and signing keys

- Standards-compatible metadata: `/.well-known/openid-configuration`
- IAM extended discovery: `/.well-known/identity-configuration`
- Public signing keys: `/.well-known/jwks.json`

The standard metadata endpoint exposes the issuer, JWKS URI, and supported signing algorithm. JWT bearer middleware obtains the active public key from JWKS and can refresh metadata when an unknown key identifier is encountered.

## ASP.NET Core consumer configuration

Use the free/open-source ASP.NET Core JWT bearer package:

```csharp
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = configuration["Identity:Authority"];
        options.Audience = configuration["Identity:Audience"];
        options.RequireHttpsMetadata = true;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "sub",
            RoleClaimType = "role",
        };
    });

services.AddAuthorization(options => options.AddPolicy(
    "business-access",
    policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("capability", "pts.api")
        .RequireClaim("security_version")
        .RequireClaim("authorization_version")));
```

The current PTS host is configuration-compatible when `Identity:PermissionClaimType` is set to `capability`, its authority matches the IAM issuer, and its audience and named capability values match the registered IAM application.

## Required access-token checks

Consumers must validate:

- RS256 signature and the current public-key identifier;
- exact issuer and application audience;
- token lifetime with a small clock-skew allowance;
- required `capability` claims;
- presence of `sub`, `application_id`, `client_id`, `security_version`, and `authorization_version`.

An expired token is stale and must be rejected. Offline JWT validation cannot know that a user's security or authorization version increased after a token was issued. IAM therefore limits access tokens to at most 60 minutes; deployments should use a short lifetime and require reauthentication or refresh after sensitive permission changes. Immediate pre-expiry revocation would require a separately approved introspection or distributed revocation feature and is not part of this standards-only boundary.

## Key rotation

Deploy a new RSA private key and unique key identifier to IAM, retain the previous public key in JWKS until every token it signed has expired, then remove the old public key. The current implementation exposes one active key, so overlap rotation requires a brief coordinated deployment window or a future multi-key enhancement. Never copy a private signing key to a consumer.
