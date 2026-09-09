# Identity Operations

## Production hosting target

Use **`https://iam.fujitecindia.com` for both IAM administration and its API**. Keep IAM-Web and IAM-Api as separate IIS sites/pools; a separate `iam-api.fujitecindia.com` is not required. This is the agreed production design, not a completed rollout. Existing `.local` sites and scripts remain local-only. See the shared [production deployment contract](../../DEPLOYMENT.md#production-hostnames-and-topology) for DNS, TLS, routing, PTS and acceptance requirements.

IAM-Web serves `/` and proxies `/identity/*` to IAM-Api with that prefix removed. Also preserve root `/api/*`, `/health/*` and `/.well-known/*` API routes: discovery and machine/bootstrap clients use them. On a single server, IAM-Api may retain `127.0.0.1:18100`; only IAM-Web has the client-facing HTTPS binding. A separate API server requires a private, protected upstream instead of loopback.

Set `Identity__Jwt__Issuer` to `https://iam.fujitecindia.com`. PTS at `https://pts.fujitecindia.com` uses this same authority but proxies browser authentication through its own `/identity` route. Retain host-local cookies, not a cookie shared across `fujitecindia.com`. Changing the issuer requires coordinated consumer configuration and fresh sign-in; local-issued tokens must not be accepted as production credentials.

## Administration frontend

The Angular application under `src/Frontend` is independently deployable static content. Build it with `npm ci` followed by `npm run build`; the release artifact is `dist/IdentityAdministration.Web/browser`.

At deployment, replace `config.json` with environment-specific values. `identityBaseUrl` must remain a same-origin path, normally `/identity`, and the reverse proxy must strip that prefix before forwarding to `Identity.Api`. This aligns browser requests with the API's strict refresh-cookie path and avoids cross-origin refresh credentials. Production must use HTTPS and keep `Identity:BrowserSession:RequireSecureCookie` enabled.

Provision `identityClientId` as a public client belonging to the IAM administration application. Its tokens require the administration audience and the `iam.admin` capability. The frontend keeps access tokens in memory only; startup recovery, rotation, and logout use the HttpOnly refresh cookie. Do not place credentials, refresh tokens, access tokens, or client secrets in `config.json`.

The DevExtreme browser license key is deployment configuration and can be supplied through `devExtremeLicenseKey`. It is intentionally empty in the committed default configuration. Light, dark, and system theme selection is local browser preference only.

## Required API configuration

Identity fails during startup when any required setting is absent or invalid. Supply deployment secrets through the environment or an external secret provider; do not add them to `appsettings.json`.

| Configuration key                                | Purpose                                                                                  |
| ------------------------------------------------ | ---------------------------------------------------------------------------------------- |
| `ConnectionStrings__Identity`                    | Independent SQL Server database connection                                               |
| `Identity__Jwt__Issuer`                          | HTTPS issuer URI                                                                         |
| `Identity__Jwt__KeyId`                           | Active RSA signing-key identifier                                                        |
| `Identity__Jwt__PrivateKeyPem`                   | RSA private key in PKCS#8 PEM form, at least 2048 bits                                   |
| `Identity__Jwt__AdministrationAudience`          | Audience accepted by administration endpoints                                            |
| `Identity__Security__KeyId`                      | Active data-protection/HMAC key identifier                                               |
| `Identity__Security__EncryptionKey`              | Base64-encoded 32-byte MFA encryption key                                                |
| `Identity__Security__ChallengeKey`               | Base64-encoded 32-byte challenge HMAC key                                                |
| `Identity__Security__IdentifierHashKey`          | Base64-encoded 32-byte identifier HMAC key                                               |
| `Identity__RateLimit__AuthenticationPermitLimit` | Authentication requests per client address per minute; defaults to 10                    |
| `Identity__BrowserSession__CookieName`           | Refresh-cookie name; defaults to `__Secure-identity-refresh` in checked-in host settings |
| `Identity__BrowserSession__CookiePath`           | Browser-visible reverse-proxy path; use `/identity` for PTS                              |
| `Identity__BrowserSession__RequireSecureCookie`  | Keep `true` for HTTPS deployments                                                        |
| `ReverseProxy__ForwardLimit`                     | Maximum trusted proxy hops; defaults to `1`                                              |
| `ReverseProxy__KnownProxies__0`                  | Exact immediate proxy IP address; add one indexed value per trusted proxy                |
| `ReverseProxy__KnownNetworks__0`                 | Trusted proxy network in CIDR notation; use only for controlled proxy subnets            |
| `AllowedHosts`                                   | Semicolon-separated deployed hostnames; override the wildcard default at the edge        |

The checked-in `AllowedHosts` wildcard permits deployment under a real hostname and delegates host
validation to the edge proxy. Override it with the concrete public hostname whenever the API is
reachable without that edge. The hostname must match `Identity__Jwt__Issuer` so consumer metadata
and JWKS requests are accepted.

Forwarded client/protocol headers are ignored by default because the checked-in trusted proxy lists
are empty. Configure the immediate reverse proxy address or controlled CIDR network at deployment.
IAM processes only `X-Forwarded-For` and `X-Forwarded-Proto`, requires the two header chains to be
symmetrical, and applies them before authentication rate limiting. Never trust a broad client-facing
network: doing so allows callers to select their own login rate-limit partition and apparent scheme.

Apply the forward-only database scripts before starting the API:

```powershell
dotnet run --project src/Backend/Identity.Database/Identity.Database.csproj
```

Run from `IAM` with `IDENTITY_DATABASE_CONNECTION` supplied securely in the environment, not expanded into process arguments. This applies pending migrations; review the target journal and verify an approved backup first. See the [database upgrade procedure](../../DEPLOYMENT.md#6-database-upgrades).

## Initial administration bootstrap

Bootstrap is a one-time operation for an empty migrated database. Secrets are accepted only from environment variables so they do not appear in command history or process arguments:

- `IDENTITY_DATABASE_CONNECTION`
- `IDENTITY_JWT_ISSUER`
- `IDENTITY_JWT_KEY_ID`
- `IDENTITY_JWT_PRIVATE_KEY_PEM`
- `IDENTITY_SECURITY_KEY_ID`
- `IDENTITY_ENCRYPTION_KEY`
- `IDENTITY_CHALLENGE_KEY`
- `IDENTITY_IDENTIFIER_HASH_KEY`
- `IDENTITY_BOOTSTRAP_PASSWORD`
- `IDENTITY_BOOTSTRAP_CLIENT_SECRET`

Run:

```powershell
dotnet run --project src/Backend/Identity.AdminCli/Identity.AdminCli.csproj -- bootstrap `
  --employee-code ADMIN-001 `
  --display-name "Initial Administrator" `
  --client-id identity-administration-client
```

The command creates the administration application, confidential client, initial user, `iam.admin` capability, role assignment, role permission, and password. Its JSON output contains only resource identifiers. Remove the two bootstrap secret environment variables after use.

After migration 0008, the optional `--email` switch stores the initial administrator's contact email. Bootstrap leaves `ManagerUserId` null. Existing users' email and reporting manager can be managed through **Users & access → Edit profile** or the authorized profile API; see [User Email and Manager](User-Email-And-Manager.md). Email does not replace employee-code login or enable email recovery.

## Repeatable application provisioning

After bootstrap, use the published administration CLI to provision independently deployed consumer applications from a versioned JSON manifest. The command requires only `IDENTITY_DATABASE_CONNECTION` and an existing actor user ID whose effective capabilities include `iam.admin` in the administration application:

```powershell
./Identity.AdminCli provision-application `
  --manifest C:/deployment/application-identity.json `
  --actor-user-id 1
```

Application manifests define the application/audience, public clients, modules, capabilities, roles, and role capability grants. They cannot contain confidential/service client secrets. The command is safe to rerun: matching resources and active grants are reused, missing resources are created and audited, and a conflicting existing contract stops the run instead of silently changing production authorization.

Validate a manifest offline before deployment or in CI; this command does not read database or secret configuration:

```powershell
./Identity.AdminCli validate-application-manifest `
  --manifest C:/deployment/application-identity.json
```

Run provisioning only from a trusted administration environment with direct database access. The CLI verifies the actor's current `iam.admin` capability even when every manifest resource already exists.

For an existing account that must receive independently deployed IAM web administration access, run the idempotent break-glass provisioning command from the trusted database host:

```powershell
$env:IDENTITY_DATABASE_CONNECTION = '<Windows-authenticated FIN_IAM connection>'
./Identity.AdminCli provision-administration-web --employee-code PTS-ADMIN
```

The command creates or verifies the stable `iam-administration` application, public `identity-admin-web` client, `iam.admin` capability and administrator role, then grants the existing active account access. All resource and authorization changes use the audited application command handlers; the command never reads or changes the user's password.

## HTTP boundaries

- Public authentication: `/api/v1/auth/*`
- Standards-compatible discovery: `/.well-known/openid-configuration`
- Extended discovery and signing keys: `/.well-known/identity-configuration` and `/.well-known/jwks.json`
- Administration: `/api/v1/admin/*`, requiring a valid administration-audience JWT with the `iam.admin` capability
- Liveness/readiness: `/health/live` and `/health/ready`
- OpenAPI: `/openapi/v1.json`

Authentication routes are rate-limited. Request bodies are capped at 64 KiB. Error responses contain a stable code and correlation identifier but no exception, credential, token, or database detail.

Browser clients use `/api/v1/auth/browser/*` with `X-Identity-Session: browser`. IAM keeps the rotating refresh token only in an `HttpOnly`, `Secure`, `SameSite=Strict` cookie scoped to the configured reverse-proxy path; browser JSON responses never expose it. Login and MFA set the cookie, refresh rotates it, and logout revokes and deletes it. The custom header plus strict same-site cookie policy provides the CSRF boundary; do not enable permissive cross-origin access on these routes.

## Independent publishing

Publish the API, database runner, and bootstrap CLI into separate deployable directories:

```powershell
./scripts/Publish-Identity.ps1 -Configuration Release
```

The default output is `artifacts/publish`, which is ignored by Git. The API deployment requires the runtime configuration above. Run the database artifact before the API during each release; use the admin CLI artifact only from a trusted administration environment for bootstrap or manifest provisioning.
