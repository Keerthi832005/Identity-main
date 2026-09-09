# Identity and Access Management

This directory is a self-contained .NET solution for an independently deployed identity application. It can be moved to its own repository without taking a source or binary dependency on a consuming application.

For local IIS publishing, deployment, database prerequisites, agent installation and verification commands, see the repository [Deployment guide](../DEPLOYMENT.md).

Production is planned at `https://iam.fujitecindia.com` for both app and API, with separate internal IAM-Web/IAM-Api sites and pools. PTS uses `https://pts.fujitecindia.com`. The [production topology](../DEPLOYMENT.md#production-hostnames-and-topology) defines routing and acceptance; the local deployment scripts do not provision these production hosts.

Personnel setup: [default employee roster seed](tasks/A008-default-employee-roster.md) and [user department/team mapping](doc/User-Department-And-Team.md). These operations do not provision credentials or grant access.

## Projects

- `Identity.Api`: authentication, discovery, signing keys, health, and administration HTTP host.
- `Identity.Database`: forward-only SQL Server migration runner.
- `Identity.AdminCli`: trusted one-time administration bootstrap and repeatable application-manifest provisioning.
- `src/Frontend`: independently deployable Angular/DevExtreme administration UI with secure browser-session authentication and light/dark/system themes.
- `Identity.Application`, `Identity.Domain`, `Identity.Infrastructure`, and `Identity.Contracts`: internal clean-architecture layers.

## Local quality gate

See [Authentication rate limits](doc/Authentication-Rate-Limits.md) for independent login, refresh and logout budgets, configuration and browser retry countdowns.

```powershell
dotnet restore Identity.slnx
dotnet format Identity.slnx --no-restore --verify-no-changes
dotnet build Identity.slnx --no-restore
dotnet test Identity.slnx --no-build
```

Tests that require SQL Server are skipped when `IDENTITY_TEST_SQL_CONNECTION` is absent. Set it to a disposable migrated test database to run the full persistence suite.

## Run the API locally

`Identity.Api` has one launch profile, **`IAM API`**, bound to `http://localhost:5000`
under `ASPNETCORE_ENVIRONMENT=Development`. Set it as the solution's single
startup project.

```powershell
dotnet run --project src/Backend/Identity.Api --launch-profile "IAM API"
```

The frontend proxies expect exactly this port. Both PTS Portal and PTS Terminal
also proxy `/identity` here, so this API must be running before signing in to
either PTS frontend.

## Frontend

The **Organizations** navigation entry manages the eight approved organization types, hierarchy browsing, bounded searchable lists, shared details, creation, edits and active state. See [Organization management](doc/Organization-Management.md) for API/UI behavior and [T020–T024 verification](doc/T020-T024-Verification.md) for test evidence and deployment boundaries. Business-database rollout and real-account provisioning require separate approval.

```powershell
cd src/Frontend
npm ci
npm run build
npm test -- --watch=false
```

To review frontend changes against the existing local IIS IAM API:

```powershell
cd src/Frontend
npm run start:local
```

Open `http://localhost:4301/` and sign in with your IAM account. This serves the
working frontend with live reload and proxies `/identity` to the existing
`https://iam.local.fujitecindia.com` installation. It uses Node's system CA trust
and keeps certificate verification enabled. The local IIS certificate must
already be trusted by Windows. This does not deploy or start another API; any
administrative changes made through this local UI affect the existing IAM data.

`npm start` serves the same frontend on the fixed port `4300` and proxies
`/identity` to the separate development API on port `5000`. Both ports are
pinned in `package.json`; neither falls back to the Angular default. See the
[local development ports](../README.md#local-development-ports) for the map
shared with PTS.

### Bulk data

Every management entity supports smart paste and Excel template export and import through one
pipeline; an upload and a paste stage identically and nothing is written until the administrator
commits. The audit trail and sessions are export only, refused at the descriptor rather than by
convention. See [IAM Bulk Data](doc/IAM-Bulk-Data.md) for the template contract, error codes and how
to add an entity, and [IAM Design System](doc/IAM-Design-System.md) for the tokens and primitives
every screen renders against.

Excel handling uses the **DevExpress Office File API**, which is commercial and licensed. With
DevExtreme it is one of two approved exceptions to the free/open-source dependency rule; both are
recorded in [THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES.md). **No DevExpress package source is
registered, so a clean checkout or CI agent cannot restore** until a credentialed feed is added or the
package is vendored.

The frontend reads deployment values from `public/config.json`. Keep `identityBaseUrl` as a same-origin path (normally `/identity`) so the API's strict HttpOnly refresh cookie remains first-party. Provision the configured `identityClientId` as a public client of the IAM administration application. DevExtreme remains the approved commercial UI exception; provide its browser license key through deployment-specific runtime configuration rather than committing it to source.

## Publish

Run the repository-owned publishing entry point from this directory:

```powershell
./scripts/Publish-Identity.ps1
```

It creates separate `api`, `database`, `admin-cli`, and static `frontend` outputs under `artifacts/publish`. Replace `frontend/config.json` at deployment time with the environment's same-origin API path, public browser client ID, application name, and approved DevExtreme license key. Never bake secrets into the static output. Build output and secrets are excluded from source control. See [Identity Operations](doc/Identity-Operations.md) for database, secret, bootstrap, reverse-proxy, cookie, and runtime configuration, and [Consumer Integration](doc/Consumer-Integration.md) for JWT validation.
