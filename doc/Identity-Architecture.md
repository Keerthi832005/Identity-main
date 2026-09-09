# Identity and Access Management Architecture

## Deployment boundary

Identity is an independently publishable application with its own SQL Server database. It shares no production project references or database tables with PTS.

```text
Client -> Identity.Api -> Identity database
Client -> PTS.Api -> PTS database
PTS.Api -> IAM discovery/JWKS for JWT validation
```

Identity owns accounts, credentials, applications, clients, modules, capabilities, application access, roles, permission overrides, devices, MFA, refresh tokens, and authentication audit.

## Solution boundaries

- `Identity.Domain` contains protected state and invariants and references no other project.
- `Identity.Application` contains typed use cases and infrastructure ports and references Domain only.
- `Identity.Contracts` contains transport records with no business behavior.
- `Identity.Infrastructure` implements persistence and cryptographic ports.
- `Identity.Api` is the HTTP composition root.
- `Identity.Database` is the forward-only DbUp migration runner.
- `Identity.AdminCli` provides secure bootstrap and administrative operations without a management UI.

## Administration boundary

Identity uses an application-owned typed request dispatcher; MediatR is not required. Administration commands operate through an authorization port, an EF Core store, and an explicit transaction runner. Commands create or revoke applications, clients, modules, capabilities, users, application access, roles, permissions, user overrides, and devices while writing append-only audit events in the same database transaction.

Every role or user-permission change increments the affected `AuthorizationVersion`. Effective capability evaluation unions active role grants and active user `Allow` overrides, then removes every active user `Deny` override. This makes Deny precedence explicit and provides a version that token consumers can use for revocation checks.

Administration discovery is exposed through bounded, no-tracking queries. The dashboard returns resource totals, active refresh-session count, failed-authentication count for the preceding 24 hours, and limited recent application, user, and audit summaries. `/api/v1/admin/applications` and `/api/v1/admin/users` provide search with `skip`/`take` paging capped at 50 rows. These read models intentionally exclude credential material, client-secret hashes, tokens, device fingerprints, login hashes, user-agent hashes, protected MFA values, and audit event JSON.

The application catalog detail endpoint returns clients, ordered hierarchical modules, and capabilities for one application. Client-secret hashes are never projected or serialized. Public clients carry no secret; confidential and service client secrets are accepted once by the create command and cleared from the administration UI immediately after the request.

User access discovery returns application assignments, authorization versions, active role assignments, and non-sensitive permission override metadata. Effective capabilities are evaluated at query time: active role grants and unexpired user `Allow` overrides form the allowed set, then active `Deny` overrides remove capabilities. The application access catalog exposes roles and capability grants so administrators can trace every effective result without reading IAM tables directly.

## Authentication and token boundary

Password credentials use PBKDF2-SHA256 with a random 32-byte salt and a bounded work factor. Client secrets and opaque 256-bit refresh tokens are stored only as SHA-256 hashes and compared in fixed time. Refresh tokens rotate once, belong to a token family, and revoke the entire family when reuse is detected. Password changes increment `SecurityVersion`; role, access, and permission changes increment `AuthorizationVersion`; refresh requests reject either stale version.

Access tokens are short-lived RS256 JWTs containing the subject, employee code, application, client, security version, authorization version, and effective capability claims. The RSA private key is deployment-provided through the composition root and never generated into source control or stored in SQL Server. The public modulus and exponent are exposed through typed signing metadata for the discovery/JWKS endpoints implemented by the HTTP API task.

## MFA, device, and audit boundary

TOTP enrollment creates a random 20-byte secret and persists only a versioned AES-256-GCM envelope with its deployment-provided key identifier. Verified primary TOTP methods gate login with a five-minute challenge and at most five attempts. Challenge identifiers are protected with HMAC-SHA256 and fixed-time comparison; successful challenges cannot be replayed.

An active device must be explicitly trusted before it can bypass TOTP. Revocation immediately removes trust and causes login requests naming that device to fail. Login identifiers are normalized and HMAC-SHA256 hashed before audit persistence. Security event JSON is produced only from approved sealed audit records, and neither raw credentials nor OTP, token, secret, or header values are accepted.

The administration security catalog exposes only credential lifecycle metadata, device labels and trust state, MFA method state, and the user's security version. Hashes, salts, encrypted MFA material, identifiers derived from fingerprints, and audit payload JSON never cross the API boundary. Browser credential, PIN, fingerprint, TOTP secret, and verification-code values remain transient and are cleared after success, cancellation, failure, navigation, or the two-minute enrollment window.

Security operations expose a bounded allow-list of audit columns and refresh-token family lifecycle metadata. Raw tokens, token hashes, login identifier hashes, client-address/user-agent hashes, and event payload JSON remain server-side. Administrative family revocation is transactional, capability-authorized, confirmation-driven, and itself appended to the audit stream.

## HTTP and operations boundary

The API exposes typed versioned authentication and administration routes. Administration requests require a correctly signed token for the dedicated administration audience, the `iam.admin` capability, and a numeric subject matching the audited actor. Public authentication routes use per-client-address fixed-window limits; all request bodies are bounded, validation errors are stable, and server exceptions are converted to non-sensitive problem responses with correlation identifiers.

Startup validates the SQL connection, RSA key, issuer, audiences, encryption/HMAC keys, and rate-limit bounds after all configuration providers are assembled. The readiness route checks SQL connectivity. The one-time CLI bootstrap reads credentials and cryptographic material only from environment variables and emits only created resource identifiers.

## Integration

PTS validates asymmetric IAM JWTs using configured issuer metadata and JWKS, audience, lifetime, signature, required version claims, and capability claims. IAM does not call PTS during authentication and PTS does not query the IAM database. With offline validation, a later security or authorization version cannot invalidate an already issued token before its expiry; short access-token lifetime is the V1 freshness boundary.

## Security boundaries

- Passwords use a per-credential salt and a configurable password KDF work factor.
- Client secrets and refresh tokens are random values stored only as hashes.
- JWT signing, MFA encryption, challenge HMAC, and identifier-hashing keys remain outside source control and SQL Server.
- User-level `Deny` overrides take precedence over `Allow` overrides and role capabilities.
- Audit rows are append-only and accept only typed, non-sensitive event data.
