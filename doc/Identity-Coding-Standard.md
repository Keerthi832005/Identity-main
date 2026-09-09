# Identity Coding Standard

- Target .NET 10 with nullable references, deterministic builds, warnings as errors, and central package management.
- Production dependencies must be free/open-source and self-hostable without production license keys.
  Two approved exceptions, both commercial and licensed: DevExtreme for the browser UI, and
  `DevExpress.Document.Processor` (Office File API) for all Excel generation and parsing. The Excel
  engine is referenced only from `Identity.Infrastructure` and sits behind `Identity.Application`
  ports, so Domain and Application stay engine-free and the dependency remains replaceable. Any
  further commercial dependency needs the same explicit approval. Recorded in full by T042.
- Domain references no other project; Application references Domain; Infrastructure implements Application ports; API and AdminCli are composition roots.
- Use the application-owned typed dispatcher; do not add MediatR, AutoMapper, or commercially restricted assertion packages.
- Use file-scoped namespaces, one public type per file, sealed classes by default, and immutable sealed records for commands, responses, claims, audit data, and transport payloads.
- Do not create anonymous request, response, metadata, claim, or audit payload objects.
- Pass `CancellationToken` across asynchronous boundaries and use `TimeProvider` for application time.
- Use fixed-time comparisons for secret-derived values and never log or persist raw passwords, PINs, OTPs, client secrets, refresh tokens, JWTs, cookies, or authorization headers.
- Keep signing/encryption/HMAC keys outside SQL Server, configuration files, and source control.
- Do not use EF migrations. DbUp scripts are ordered, forward-only, checksummed, and validated against SQL Server.
- Every state-changing administration request is authorized, validated, bounded, auditable, and idempotent where replay is possible.
- Every task passes format, build, relevant tests, dependency review, documentation update, and a focused commit.
