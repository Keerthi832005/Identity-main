# T046: Terminal device attestation

- Status: IN_PROGRESS
- Objective: Let a trusted Terminal computer obtain an IAM session from its enrolled agent key, so employee code and PIN sign-in works without a person first typing an IAM username and password on the shop floor.
- Scope: `IAM.Agent` local attestation endpoint, `Identity.Contracts` signing payload, `Identity.Domain` device service account, `Identity.Application` attestation command, `Identity.Api` challenge and attestation endpoints, IAM administration screen field, and the PTS Terminal connect page. PTS backend is unchanged; it keeps consuming IAM-issued tokens.
- Requirements covered: An operator arriving at a terminal signs on with a code and PIN alone. Today `connection-page.ts` disables that method until `auth.authenticated()` is true, because `/api/terminal-authentications` carries `RequireAuthorization(ApiPolicies.ShopFloorOperation)` and there is no bearer token before an IAM password login.
- Files/components: `src/Backend/IAM.Agent/AgentWeb.cs`, `src/Backend/Identity.Contracts/Agents/AgentProtocol.cs`, `src/Backend/Identity.Domain/Entities/Device.cs`, `src/Backend/Identity.Application/Authentication/`, `src/Backend/Identity.Api/Endpoints/AuthenticationEndpoints.cs`, `db/migrations/0019_terminal_device_service_account.sql`, `../PTS/src/Frontend/Terminal/src/app/core/api/`, `../PTS/src/Frontend/Terminal/src/app/features/access/connection-page.ts`.
- Dependencies: existing agent enrollment (`AgentInstallation` holds the enrolled public key), device trust (`Device.IsTrustedAt`), and browser session issuance (`MapBrowser`).
- Risks/assumptions: This removes a password from a sign-in path, so the whole task is a security change. A hostname is an identifier, not a credential: `AgentWeb.cs` serves `/v1/identity` unsigned, so nothing may trust a hostname alone. Attestation must rest on the agent's ECDSA P-256 device key, whose private half stays under DPAPI `LocalMachine`. Anyone with physical access to a terminal can obtain a terminal token, which is the intended trust model for a shop-floor terminal and is why each operator still proves themselves with a PIN and why the token must be capped to shop-floor capabilities. Widening the agent's local HTTP surface to accept a POST weakens an existing guarantee asserted by `AgentHttpTests.Browser_reads_identity_but_untrusted_origins_hosts_and_writes_are_denied`; that test must be revised deliberately, not silently.

## Implementation Steps

Add `TerminalServiceUserId` to `Identity.Device` as a nullable foreign key to `UserAccount`, with a
filtered index. A device attests only while `IsTrustedAt(now)` and this column is set, so a terminal
can never inherit the rights of the administrator who enrolled it. `NULL` refuses attestation.

Add a shared attestation payload to `AgentProtocol` so the agent and IAM cannot disagree about what
was signed, plus a `VerifyAttestation` helper reusing the existing P-256 rules and
`IeeeP1363FixedFieldConcatenation` format already used for machine reports.

Add `POST /v1/attest` to the agent. It signs an IAM-issued nonce with the enrolled device key and
returns the installation id and signature. The key never leaves the machine and the nonce is never
stored or interpreted locally. The local CORS gate widens to this one route and verb only; every
other write stays denied, and `AgentHttpTests` is extended to assert both halves.

Add an IAM challenge endpoint issuing a single-use nonce bound to an installation id with a short
expiry, and an attestation endpoint that resolves the installation, loads the device, requires
`CanAttestAt(now)`, verifies the signature against the enrolled public key, consumes the nonce, then
issues a session for `TerminalServiceUserId` through the existing `ISessionIssuer` and `MapBrowser`.
The issued authorization is intersected with the shop-floor capabilities so the token cannot exceed
`pts.shopfloor.operate` and `pts.production.read` whatever the account holds.

Add the terminal service account to the IAM administration device screen so an administrator can set
and clear it, and audit both.

In the PTS Terminal, attempt attestation on connect-page load before showing the password form. On
success the employee code and PIN method is available immediately; the IAM password path remains as
the fallback when no agent, no trust or no service account is configured.

## Acceptance Criteria

A trusted terminal with a service account assigned reaches the workspace using only an employee code
and PIN, with no IAM password typed. The same terminal with trust revoked, with the device type not
`Terminal`, or with no service account, falls back to the password form and explains why. A replayed
nonce is rejected. A signature from a different key is rejected. An attested token carries only the
two shop-floor capabilities even when the service account holds more. The agent still refuses every
write other than `/v1/attest` and still refuses untrusted origins and hosts.

## Required Tests and Validation

`dotnet format --verify-no-changes`, `dotnet build`, `dotnet test` for `Identity.slnx` on a pristine
disposable database; `npm run build`, `npm test`, `npm run format:check` for the Terminal. Domain
tests for `CanAttestAt` and `AssignTerminalService`. Contract tests for payload stability and
signature rejection. Application tests for replay, untrusted device, missing service account and the
capability ceiling. `AgentHttpTests` extended for the widened yet still-closed local surface. Real
evidence of a terminal reaching the workspace with no password, and SQL showing the audit rows.

## Definition of Done

Attestation shipped end to end and proven on a real terminal, the capability ceiling enforced and
tested, the agent's local surface still closed to everything else, suites green, and
`PTS/doc/PTS-Operations.md` plus `PTS/docs/connected-terminal-deployment.md` updated to describe
enrollment with a terminal service account and the new failure modes.

## Progress

Step 1 complete. `Device.TerminalServiceUserId` with `AssignTerminalService` and `CanAttestAt`,
migration `0019`, its EF mapping, the shared `AttestationPayload` and `VerifyAttestation`, and the
agent's `POST /v1/attest` are in, with domain, contract and HTTP tests. The agent's local gate admits
POST on `/v1/attest` alone; `/v1/identity` keeps refusing writes and its original preflight answer,
so no existing guarantee was relaxed.

Traceability note: part of this step was swept into `dc2bd5a` ("feat: add fully automated IAM UAT
deployment suite") by a concurrent session, which does not mention T046. The correcting change is
`a993a77`. History was not rewritten.

Remaining: the IAM challenge and attestation endpoints with the capability ceiling and nonce replay
store, the administration field, and the Terminal connect-page wiring, then the two PTS documents.
