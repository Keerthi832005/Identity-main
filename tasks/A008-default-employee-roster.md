# A008 — Default employee roster

The supplied personnel roster is stored in `db/seeds/default-employees.json`: 164 unique employee codes (67 INDC and 97 INDE). Names are retained exactly, including repeated names with different codes. No department/team mappings were supplied.

Run the administrator CLI with `IDENTITY_DATABASE_CONNECTION` configured securely and an existing active IAM administrator as actor:

```powershell
dotnet run --project IAM/src/Backend/Identity.AdminCli -- seed-default-employees --actor-user-id 1
# Inspect the preview, then apply to the same verified database:
dotnet run --project IAM/src/Backend/Identity.AdminCli -- seed-default-employees --actor-user-id 1 --apply
```

The actor ID above is an example; the command verifies IAM administration authorization. Preview is the default. Apply inserts missing normalized employee codes in one transaction, uses normal user creation and audits, and leaves every existing account unchanged. It reports existing name differences without overwriting them. Replays add no users or audits. A transaction-owned lock serializes seed runs; a concurrent ordinary create collision rolls back the batch for safe retry.

The seed does not create passwords, PINs, roles, application access, manager relationships, or organization assignments. New employees require normal credential and access provisioning before sign-in. This explicit administrative command is the reusable default roster seed; startup does not recreate intentionally removed accounts.

Verified 2026-08-31:

- CLI build: zero errors/warnings; full CLI tests 18 passed, 3 SQL tests skipped without SQL configuration.
- Isolated SQL Server test run: 10 seed tests passed, including preview, apply, repeat, authorization, rollback, existing-account/security preservation, and input validation.
- Local live target verified as `HOCOM18502627\SQLEXPRESS2022`, database `FIN_IAM`, authorized actor 1 (`INDE03275`). Applied 163 additions; 1 existing account preserved. Correlation ID `945ff86a-5c16-4ca8-8036-ca00f3f76b02`.
- Post-apply preview: all 164 codes present, zero missing, zero name differences. No passwords/PINs/access grants changed. Other databases and services were untouched.
