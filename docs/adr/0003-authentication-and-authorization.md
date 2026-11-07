# ADR 0003: JWT sessions and claim-based tenant context

- **Status:** Documented architectural decision. The security gaps in this design are current behavior, not accepted risk for a public deployment.
- **Date:** 2026-11-07

## Context

The API serves a browser client and CI. Each request needs a user, a tenant, a role, and a plan. Background scans and the GitHub webhook need a tenant without a user JWT. Repository CI needs a credential that is not a user session.

ASP.NET Core Identity is referenced and unused. Accounts are `AppUser` rows with BCrypt password hashes.

## Decision

Issue a one-day HMAC-SHA256 JWT from `JwtService`. Put `tenantId`, `organization`, `role`, `plan`, and `is_super_admin` in the token. Validate issuer, audience, lifetime, and signing key. Read tenant context from claims in `HttpTenantProvider`.

Store CI credentials as SHA-256 hashes of a 32-byte random token. Verify GitHub webhooks with HMAC when `GitHub:WebhookSecret` is set, and refuse the webhook when it is not.

Keep role checks in the actions that need them. Do not add a global role policy that the code does not currently have.

## Alternatives Considered

- Server sessions. They fit a browser and add a session store the CI path would not use.
- ASP.NET Core Identity. The package is present. The schema and services are custom. Adopting Identity would replace `AuthService` rather than describe it.
- Reference tokens stored in SQL. They can be revoked on the next request. The current JWT cannot.

## Consequences

A valid token is trusted until expiry, including its role and super-admin flag. Registration marks super-admin when the submitted email matches configuration, without proving mailbox ownership. The signing key in `appsettings.json` is a development placeholder and must be replaced outside a private workstation. `Program.cs` substitutes an empty key when configuration is missing, while token creation throws. Those are implementation facts to fix, not properties of JWT as a standard.

Viewer restrictions and super-admin checks are easy to miss on a new route because they are not policies.

The CI token can be verified without storing the raw token. It cannot be shown again. There is no revoke route.

## Trade-offs

Stateless user authentication avoids a session lookup and blocks immediate revocation. Separate CI tokens avoid handing user JWTs to GitHub Actions. Claim-based tenant filters are fast and wrong if the token is forged or stale.

See [../SECURITY.md](../SECURITY.md).
