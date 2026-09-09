# T008: Shared-terminal PIN verification

- Status: COMPLETED
- Priority: P0 integration requirement
- Objective: Let independently deployed PTS verify one employee on a trusted shared terminal without copying IAM credentials or identity data into PTS.
- Scope: Four-digit PIN lifecycle, trusted terminal and service-client verification, lockout, rate limiting, audit events, HTTP contracts, and SQL migration.
- Dependencies: T005-T007 and PTS T018.

## Acceptance Criteria

An administrator can set a hashed operator PIN; a valid service client can verify an active employee with PTS application access on an active trusted terminal; invalid PIN attempts lock the account after five failures for fifteen minutes; untrusted/revoked terminals and invalid clients are rejected; and raw PINs and client secrets are never stored in plaintext or logged.

## Validation Results

- `FIN_IAM` migrations 0005 and corrective forward migration 0006 applied and replayed with Windows Authentication.
- IAM solution build passed with zero warnings and zero errors.
- 43 IAM tests passed, including SQL-backed terminal success, device rejection, repeated PIN failure, and lockout.
- Terminal verification remains behind the existing IAM authentication rate-limit policy.

## Definition of Done

The HTTP/admin contracts, domain and PBKDF2 implementation, persistence query, audit constraint, integration tests, documentation, and focused PTS T018 commit are complete.
