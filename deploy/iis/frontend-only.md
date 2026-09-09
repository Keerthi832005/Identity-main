# Frontend-only IIS routing

`frontend-only.web.config` supports direct Angular navigation, such as
`/login?returnUrl=%2F`, when the frontend is copied to an IIS site root before
the environment's API is activated. It requires the IIS URL Rewrite module.

Install it as the root `web.config` only for a frontend-only site without an
existing routing configuration. Never overwrite a configured API/proxy site
with this file. Back up and review existing IIS configuration before changes.

GET/HEAD navigation routes fall back to `index.html`. Missing files still
return 404, and `identity`, `api`, `health`, `hubs` and `.well-known` routes
explicitly remain unavailable instead of returning the Angular login page.
The API must be configured separately using the environment-specific hosting
plan in [Identity operations](../../doc/Identity-Operations.md).

A successful `/login` response proves frontend routing only. Before declaring
the site ready, verify API readiness, issuer and key configuration, client
provisioning and authenticated sign-in. Do not report this temporary template
as a completed API deployment.
