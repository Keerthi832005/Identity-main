# IAM Multi-Application Integration Guide

## Overview

This guide documents how to integrate independently deployed applications (like an AMS system) with the Fujitec IAM (Identity and Access Management) system. IAM provides authentication, authorization, and JWT-based token validation through a trust boundary—consumers validate tokens locally without direct IAM database access.

---

## Current IAM System Architecture

The IAM system consists of four independently deployable components:

### Identity.Api
- Authentication & sign-in (browser & programmatic)
- JWT issuance & key rotation
- Discovery endpoints (OpenID, JWKS)
- Rate limiting
- Binds to port `5000` (local)

### Identity.AdminCli
- One-time bootstrap
- Application provisioning
- Manifest validation
- Employee roster seeding
- Trusted database access only

### Identity.Database
- Forward-only migrations (SQL Server)
- User/app/capability schema
- Never edited after applied
- Can be audited before apply

### Angular Frontend
- Administration UI (orgs, users, apps)
- Browser session auth
- HttpOnly refresh cookies
- DevExtreme-based UI

---

## CLI Features (Identity.AdminCli)

**Status:** ✅ EXISTS

The AdminCli is a trusted bootstrap and provisioning tool. Run commands with environment variables for secrets—never pass them as arguments.

### 1. Bootstrap (One-Time Setup)

**Status:** Fully Implemented

Creates the initial IAM installation: application, confidential client, initial user, capability, and role.

```bash
dotnet run --project src/Backend/Identity.AdminCli -- bootstrap \
  --employee-code ADMIN-001 \
  --display-name "Initial Administrator" \
  --client-id identity-administration-client \
  [--email admin@fujitec.com]
```

**Required environment variables:**
- `IDENTITY_DATABASE_CONNECTION` — SQL Server connection
- `IDENTITY_JWT_ISSUER` — e.g., `https://iam.fujitecindia.com`
- `IDENTITY_JWT_KEY_ID` — Unique key identifier
- `IDENTITY_JWT_PRIVATE_KEY_PEM` — RSA private key (PKCS#8, 2048+ bits)
- `IDENTITY_SECURITY_KEY_ID` — HMAC key ID
- `IDENTITY_ENCRYPTION_KEY` — Base64-encoded 32-byte key
- `IDENTITY_CHALLENGE_KEY` — Base64-encoded 32-byte key
- `IDENTITY_IDENTIFIER_HASH_KEY` — Base64-encoded 32-byte key
- `IDENTITY_BOOTSTRAP_PASSWORD` — Initial admin password
- `IDENTITY_BOOTSTRAP_CLIENT_SECRET` — Initial client secret

### 2. Provision Application (Repeatable)

**Status:** Fully Implemented

Safely applies an application manifest JSON (capabilities, roles, clients). Idempotent and audited.

```bash
./Identity.AdminCli provision-application \
  --manifest /path/to/application-identity.json \
  --actor-user-id 1
```

**Requirements:**
- `IDENTITY_DATABASE_CONNECTION` environment variable set
- Actor user must have `iam.admin` capability
- Can be run repeatedly (reuses matching resources)

### 3. Validate Manifest (Offline)

**Status:** Fully Implemented

Validate a JSON manifest before deployment. No database connection needed.

```bash
./Identity.AdminCli validate-application-manifest \
  --manifest /path/to/application-identity.json
```

### 4. Seed Default Employees

**Status:** Fully Implemented

Bulk seed default employee roster from embedded JSON.

```bash
./Identity.AdminCli seed-default-employees \
  --actor-user-id 1 [--apply]
```

Preview mode (no `--apply`) shows what would be created.

### 5. Provision Administration Web

**Status:** Fully Implemented

Grant existing user access to IAM web administration.

```bash
./Identity.AdminCli provision-administration-web \
  --employee-code PTS-ADMIN
```

---

## Integration Model: Trust Boundary

**Key principle:** Consumers (like AMS) validate tokens locally using public keys. They never connect to the IAM database or receive the private signing key. This keeps IAM as an isolated, independently deployable service.

### JWT Validation Flow

1. AMS application makes HTTP request with bearer token
2. AMS receives token, extracts JWT claims
3. AMS validates RS256 signature against public key from IAM's `/.well-known/jwks.json`
4. AMS checks token issuer matches configured IAM authority
5. AMS checks required capabilities/permissions in token
6. Request allowed or denied based on validation

### Configuration Required in AMS

| Setting | Value | Example |
|---------|-------|---------|
| **Authority** | Exact IAM issuer URL (no app path) | `https://iam.fujitecindia.com` |
| **Audience** | AMS's registered audience code | `ams-api` |
| **Permission Claim Type** | Always "capability" | `capability` |
| **Required Capability** | AMS's registered capability | `ams.access` |
| **HTTPS Metadata** | Required outside loopback dev | `true` |

---

## Discovery Endpoints (Public)

AMS applications use these standard endpoints to bootstrap trust:

| Endpoint | Purpose | Response |
|----------|---------|----------|
| `/.well-known/openid-configuration` | OpenID Connect metadata | Issuer, JWKS URI, algorithms |
| `/.well-known/identity-configuration` | IAM extended discovery | Application-specific metadata |
| `/.well-known/jwks.json` | Public signing keys | Current + recent keys (for rotation) |
| `/health/live` | Liveness probe | 200 OK |
| `/health/ready` | Readiness probe | 200 OK (database connected) |

---

## AMS Integration Roadmap

### Phase 1: Provisioning & Trust Setup

#### 1. Create AMS Application Manifest
Define `application-ams.json` with:
- Application code: `ams-application`
- Audience: `ams-api`
- Public client: `ams-browser-client`
- Confidential client: `ams-service-client`
- Capabilities: `ams.access`, `ams.admin`, etc.
- Roles: User, Admin, Manager, etc.
- Role-to-capability mappings

#### 2. Validate & Deploy Manifest

```bash
./Identity.AdminCli validate-application-manifest \
  --manifest application-ams.json
```

Then provision (after IAM bootstrap):

```bash
./Identity.AdminCli provision-application \
  --manifest application-ams.json \
  --actor-user-id 1
```

#### 3. Configure AMS to Validate Tokens
Update AMS appsettings.json:

```json
{
  "Identity": {
    "Authority": "https://iam.fujitecindia.com",
    "Audience": "ams-api",
    "RequireHttpsMetadata": true,
    "PermissionClaimType": "capability"
  }
}
```

Add JWT bearer authentication in Startup/Program.cs

### Phase 2: Browser & Service Authentication

#### 4. Implement Browser Login (OAuth/OIDC)
AMS browser app redirects to IAM:
- `https://iam.fujitecindia.com/api/v1/auth/browser/authorize`
- Client ID: `ams-browser-client` (public client)
- Redirect URI: `https://ams.fujitecindia.com/auth/callback`
- Receive JWT access token + HttpOnly refresh cookie

#### 5. Service-to-Service (Client Credentials)
AMS backend services acquire tokens for internal calls:
- Client ID: `ams-service-client` (confidential)
- Endpoint: `POST /api/v1/auth/token`
- Grant: `client_credentials`
- Scope: `ams-api`

### Phase 3: Administration & User Management

#### 6. AMS-Specific User Roles
Define in manifest or via administration APIs:
- AMS User: read-only access
- AMS Manager: full asset management
- AMS Admin: configuration & compliance
- Custom roles tied to Fujitec organization structure

#### 7. Sync User Data with Business Dept/Team
Use IAM's user profile APIs to pull:
- Employee code, display name
- Department & team assignments
- Manager/reporting structure
- Email (for notifications)

See `User-Email-And-Manager.md` in IAM docs.

### Phase 4: Advanced Features

#### 8. Terminal/Device Integration (Optional)
If AMS runs on terminal PCs, integrate with IAM.Agent:
- Local identity service (port 43127)
- Device enrollment and trust
- Hardware inventory tracking

See `IAM-Agent.md` documentation.

#### 9. Audit & Compliance Logging
AMS must log all identity-related events:
- Authentication attempts (success/failure)
- Authorization decisions (access granted/denied)
- Token refresh & expiry
- Sensitive operations (role changes, etc.)

---

## Application Manifest Structure (Example)

```json
{
  "application": {
    "code": "ams-application",
    "name": "Asset Management System",
    "description": "Company-wide asset tracking and lifecycle management"
  },
  "audience": "ams-api",
  "publicClients": [
    {
      "clientId": "ams-browser-client",
      "displayName": "AMS Browser Application"
    }
  ],
  "confidentialClients": [
    {
      "clientId": "ams-service-client",
      "displayName": "AMS Backend Service"
    }
  ],
  "capabilities": [
    {
      "code": "ams.access",
      "description": "Basic access to AMS"
    },
    {
      "code": "ams.admin",
      "description": "Administrative access to AMS"
    }
  ],
  "roles": [
    {
      "code": "ams-user",
      "name": "AMS User",
      "capabilities": ["ams.access"]
    },
    {
      "code": "ams-admin",
      "name": "AMS Administrator",
      "capabilities": ["ams.access", "ams.admin"]
    }
  ]
}
```

---

## Key API Endpoints (AMS Uses)

### Public (No Auth Required)
- `GET /.well-known/openid-configuration` — Discover metadata
- `GET /.well-known/jwks.json` — Get public signing keys
- `GET /health/live` — Check liveness

### Authentication (Public, Rate-Limited)
- `POST /api/v1/auth/browser/authorize` — Browser login
- `POST /api/v1/auth/token` — Token request (client credentials, refresh)
- `POST /api/v1/auth/logout` — Logout & revoke tokens

### Administration (Requires iam.admin Capability)
- `GET /api/v1/admin/applications` — List applications
- `GET /api/v1/admin/users` — List users
- `GET /api/v1/admin/users/{id}` — Get user details (includes dept/team)
- `PUT /api/v1/admin/users/{id}/profile` — Update user email/manager

---

## Hosting & Deployment Model

### IAM Hosting
- **Production:** `https://iam.fujitecindia.com`
- Separate IIS sites/pools (IAM-Web, IAM-Api)
- HTTPS only with strict TLS
- SQL Server backend (Windows auth or secrets)
- Independent from PTS & AMS

### AMS Hosting
- **Production:** `https://ams.fujitecindia.com`
- Independently deployed API & frontend
- Configures IAM authority at deploy time
- No direct IAM database access needed
- Validates tokens from `/.well-known/jwks.json`

---

## Checklist for AMS Integration

### Before Development
- [ ] Agree on AMS application code & audience identifiers
- [ ] Define AMS roles and required capabilities
- [ ] Decide on organization/team visibility rules
- [ ] Plan MFA requirements (if any)
- [ ] Identify sensitive operations needing audit logs

### Development Phase
- [ ] Create & validate application manifest JSON
- [ ] Test against local IAM instance
- [ ] Implement JWT validation in AMS backend
- [ ] Implement browser OIDC login flow
- [ ] Add comprehensive audit logging
- [ ] Test token refresh & expiry handling

### Deployment Phase
- [ ] Provision AMS application in production IAM
- [ ] Set production authority in AMS config
- [ ] Configure HTTPS metadata validation
- [ ] Test browser login with real IAM account
- [ ] Verify service-to-service authentication
- [ ] Validate audit trail captures all events

---

## Important Considerations

### Security & Operations
- **Never** share IAM's private signing key with AMS
- **Never** connect AMS directly to IAM database
- Validate tokens via HTTPS only (metadata required)
- Keep clock skew allowance small (15-30 sec)
- Monitor token expiry (default 60 min max)
- Refresh tokens before expiry for critical operations
- Log all authentication & authorization decisions
- Require re-authentication for sensitive changes

---

## References & Related Documentation

- **Consumer Integration:** `IAM/doc/Consumer-Integration.md` — JWT validation, discovery, configuration
- **Identity Operations:** `IAM/doc/Identity-Operations.md` — API config, bootstrap, provisioning, deployment
- **Organization Management:** `IAM/doc/Organization-Management.md` — Org hierarchy, sync with PTS
- **User Email & Manager:** `IAM/doc/User-Email-And-Manager.md` — Profile APIs, dept/team sync
- **Bulk Data:** `IAM/doc/IAM-Bulk-Data.md` — Excel import/export for user management
- **IAM Agent (Optional):** `IAM/doc/IAM-Agent.md` — Terminal PC enrollment & device tracking

---

## Next Steps

1. Review this guide and `Consumer-Integration.md` in the IAM project
2. Design AMS application manifest (capabilities, roles, clients)
3. Start development against local IAM instance (port 5000)
4. Implement JWT validation using ASP.NET Core JWT bearer middleware
5. Test browser login & token refresh flows
6. Add comprehensive audit logging
7. Prepare for production deployment & compliance review

---

**Document Version:** 1.0  
**Last Updated:** 2026-09-01  
**Scope:** IAM Integration for Multi-Application Support (AMS & others)
