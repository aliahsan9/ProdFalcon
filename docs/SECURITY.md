# Security

This is an audit of the ProdFalcon API as implemented in this repository. It is not a penetration test and it is not a statement that the product is safe to expose to the internet. Authentication exists. Several controls around it are incomplete, and the product's job is to accept untrusted archives.

Findings are ordered so archive handling and credential handling come first. Risk levels describe the current code, not a future design.

No secret values are copied into this document. Configuration keys are named. If a value is already committed, treat it as public and rotate it.

## Archive processing

### ZIP path traversal

- **Risk level:** Mitigated in the current extractor, with residual risk if a new extract path bypasses it.
- **Affected component:** `SafeZipExtractor`, called by `ProjectStorageService.ValidateZipAsync` and `ExtractZipAsync`.
- **Explanation:** Entry names are normalized. Rooted paths and any entry containing `..` are rejected. The combined full path must start with the destination directory prefix, compared ordinal-ignore-case. Extraction writes only to the resolved path.
- **Potential impact:** A bypass would let an archive write outside the tenant directory, including over source or configuration if the process identity can write there.
- **Recommended mitigation:** Keep all extraction on `SafeZipExtractor`. Add tests for absolute paths, mixed separators, and prefix tricks such as a sibling directory whose name begins with the destination name. Prefer a trailing-separator check that cannot match a sibling prefix.
- **Current remediation status:** Implemented and covered by `Rejects_zip_entries_that_escape_the_destination`. The prefix check uses `StartsWith` on a path that is forced to end with a separator, which blocks the sibling-prefix case for that helper. No second extractor was found.

### ZIP bombs and decompression limits

- **Risk level:** Medium.
- **Affected component:** `SafeZipExtractor`, `ScanLimitsOptions`, upload `[RequestSizeLimit]`.
- **Explanation:** Defaults are 100 MB upload, 500 MB declared or written uncompressed bytes, 8,000 entries, and a compression ratio of 100. The ratio check applies to entries larger than 1 MB and to the archive total when uncompressed size exceeds 1 MB. Extraction counts bytes written and stops. Overlapping entries that declare a small size and then expand are bounded by the written-byte counter. Many small entries are bounded by `MaxEntries`.
- **Potential impact:** A craft that stays inside the limits can still consume 500 MB of disk per scan, times concurrent workers, plus the ZIP itself. Two workers can do this at once. The GitHub client also buffers the download in memory up to the upload cap.
- **Recommended mitigation:** Cap concurrent extracts with disk quotas per tenant, stream GitHub downloads to disk, and alert on `StorageRoot` growth. Consider a lower default on public hosts.
- **Current remediation status:** Partial. Limits exist and are configurable. Disk quotas and streaming downloads are not implemented. The ratio rule is a heuristic.

### File type and malicious content

- **Risk level:** Medium.
- **Affected component:** `ScanController.UploadZip`, `GitHubController.ContinuousIntegration`, `SourceTree`.
- **Explanation:** The upload action checks the file extension, then the ZIP parser. A non-ZIP renamed to `.zip` fails validation. There is no antivirus or malware sandbox. Analysis skips common binary extensions and does not execute the project. It does read source as text. Extracted trees remain on disk until the two-day cleanup, or longer if the directory is still being written.
- **Potential impact:** The host stores and parses attacker-controlled content. A bug in the ZIP implementation or in a scanner that shells out would run as the API identity. No scanner currently starts a shell for the archive. Parsers can still be vulnerable.
- **Recommended mitigation:** Run the API as a low-privilege account, keep `StorageRoot` off the system drive and off the source tree, and scan uploads with a separate malware tool before extract if the API is exposed.
- **Current remediation status:** Extension and ZIP parse checks are implemented. Malware scanning is not.

### Committed extracted projects

- **Risk level:** High for repository hygiene.
- **Affected component:** `ProjectStorage/ExtractedProjects/` tracked in git.
- **Explanation:** The running storage service writes to `ProdFalcon_Storage/` unless `Storage:RootPath` is set, and that directory is gitignored. An older tree of extracted projects is still tracked, including copies of other codebases. Those files are not product source. Any secret that was inside them is in git history as well as the working tree.
- **Potential impact:** Disclosure of third-party source and any credentials that were in the samples. Clone size and accidental dependency confusion for readers.
- **Recommended mitigation:** Stop tracking `ProjectStorage/`, remove it from the index, and rotate anything sensitive that was in those trees. Deleting the files in a new commit does not erase history.
- **Current remediation status:** Not remediated. Documented only. This change set does not delete those files.

## Authentication and session tokens

### Committed JWT signing key

- **Risk level:** High if the repository's key is used anywhere except a disposable local database.
- **Affected component:** `Jwt:Key` in `appsettings.json`. `JwtService` signs with HMAC-SHA256. `Program.cs` validates issuer, audience, lifetime, and signing key.
- **Explanation:** A development key is stored in the repository. Anyone who can read the repository can forge a token if a host still uses that key. Token lifetime is one day. There is no revocation list.
- **Potential impact:** Forged tenant and super-admin claims against any environment that did not override the key.
- **Recommended mitigation:** Rotate the key. Load it from user secrets or a secret store. Fail startup when the key is missing or shorter than the HMAC-SHA256 requirement. `JwtService` already throws when the key is missing. `Program.cs` uses an empty string instead, so validation setup does not fail fast.
- **Current remediation status:** Not remediated. The placeholder remains so existing local setup keeps working. Do not deploy it.

### No email verification before super-admin grant

- **Risk level:** High on any host that keeps the default `SuperAdmin:Emails` list.
- **Affected component:** `AuthService.RegisterAsync` and `LoginAsync`.
- **Explanation:** Registration stores the email the client sends. If it matches the configured list, `IsSuperAdmin` is set. Login promotes an existing user the same way. Nothing proves the caller controls the mailbox.
- **Potential impact:** The first person to register a configured address becomes a super-admin and can list, suspend, soft-delete, and re-plan every tenant.
- **Recommended mitigation:** Remove the default address, require an out-of-band bootstrap, and do not grant the flag from a self-service email string.
- **Current remediation status:** Not remediated.

### Password handling

- **Risk level:** Medium.
- **Affected component:** `AuthService`, `RegisterDto`.
- **Explanation:** Passwords are hashed with BCrypt.Net. They are not logged by the auth service. There is no length, complexity, or breach check. Login uses a generic `Invalid credentials.` message for unknown users and bad passwords. There is no lockout and no rate limit on `/api/auth/login`.
- **Potential impact:** Weak passwords and online guessing against login. A short password is stored as a normal hash, which does not make the password strong.
- **Recommended mitigation:** Reject short passwords, add a login rate limit, and consider a breached-password check.
- **Current remediation status:** Hashing is implemented. Policy and login throttling are not.

### Authorization model

- **Risk level:** Medium.
- **Affected component:** Controllers and `HttpTenantProvider`.
- **Explanation:** `[Authorize]` requires a valid JWT. Viewer blocks exist on upload, GitHub scan submit, finding updates, and repository registration. Super-admin checks the `is_super_admin` claim. Invite checks owner or admin inside `TenantMemberService`. Developer, security reviewer, admin, and owner are otherwise treated the same by the scan API. Roles inside an already issued token are not refreshed from the database.
- **Potential impact:** A member who should be read-only but is not a viewer can upload archives and spend worker time. A demoted admin keeps the old role until the token expires.
- **Recommended mitigation:** Add authorization policies per action and, for sensitive operations, read the membership row.
- **Current remediation status:** Partial. Tenant data filters are stronger than role checks. Cross-tenant reads of scans are tested.

### CI and webhook credentials

- **Risk level:** Low for the token store, medium if the webhook secret is weak or empty.
- **Affected component:** `GitHubController`, `RepositoryRegistration`.
- **Explanation:** CI tokens are 32 random bytes, shown once, and stored as SHA-256 hex. The webhook requires `X-Hub-Signature-256` and returns 503 when `GitHub:WebhookSecret` is empty. Signature failure returns 401. The webhook then trusts the matching registration's tenant.
- **Potential impact:** A leaked CI token can push archives into that tenant and run a scan. A leaked webhook secret can queue scans for a registered repository. An empty secret does not accept unsigned calls.
- **Recommended mitigation:** Keep the webhook secret in a secret store. Allow registration tokens to be revoked. The hash store is the right shape.
- **Current remediation status:** Hashing and signature checks are implemented. Token revocation is not a separate endpoint. Deleting the row is not exposed as its own route.

### GitHub access tokens on scan requests

- **Risk level:** Medium.
- **Affected component:** `POST /api/scan/github`, `ScanWorkItem.GitHubAccessToken`.
- **Explanation:** The client may send a GitHub token so the worker can download a private repository. The token stays on the in-memory work item. It is not a `ScanProject` column. Current log lines record owner, name, and branch, not the token. The process still holds the secret until the job finishes. A crash dump or a future log of the work item would capture it.
- **Potential impact:** Disclosure of a GitHub token with whatever scope the caller granted.
- **Recommended mitigation:** Prefer the CI ZIP upload over long-lived tokens. If a token is required, accept it, use it, and avoid storing it in a type that gets serialized.
- **Current remediation status:** Not persisted to SQL. Still resident in memory for the job. No dedicated scrubbing beyond ordinary garbage collection.

## Platform controls

### SQL injection in this API

- **Risk level:** Low in the API's own queries.
- **Affected component:** EF Core LINQ in repositories and query services.
- **Explanation:** Queries seen in the audit are LINQ. The findings search uses `Contains` parameters, not concatenated SQL. The product also detects string-built SQL in uploaded projects. That detector does not protect ProdFalcon's database. It reports on the archive.
- **Potential impact:** A future raw SQL change could reintroduce injection. The current LINQ surface does not show that pattern.
- **Recommended mitigation:** Keep queries in LINQ or parameterized commands. Review any `FromSqlRaw` before adding it.
- **Current remediation status:** No raw SQL execution was found in the product projects.

### Sensitive data in responses and logs

- **Risk level:** Medium.
- **Affected component:** `SecretRedactor`, audit metadata, exception middleware, invite responses.
- **Explanation:** Secret findings are designed to store a redacted description and a length. Tests check that a dummy API key is not stored. Audit metadata for registration includes the email address. Invite responses return the raw invite token. Development 500 responses include `exception.Message`. Serilog's default template plus request logging records method, path, and status. Query strings are the main place a token would leak into request logs if a client put one there. The GitHub token is a JSON property, which the current request logging configuration does not show as a body.
- **Potential impact:** Invite token disclosure through logs or client history. Email addresses in `AuditLogs`. Exception text in Development only.
- **Recommended mitigation:** Send invites through a mailbox and return only a non-secret confirmation. Avoid building audit metadata by string interpolation of email if the audit store is broadly readable. Keep Development exception details off shared servers.
- **Current remediation status:** Secret redaction in scanners is implemented. Invite tokens are still returned. Development exception details are intentional and environment-gated.

### CORS, hosts, and rate limiting

- **Risk level:** Low locally, medium if the same file is used in production unchanged.
- **Affected component:** `Program.cs`, `appsettings.json` `AllowedHosts`.
- **Explanation:** CORS allows only `http://localhost:4200` and `https://localhost:4200`, with any header, any method, and credentials. `AllowedHosts` is `*`. The `scans` limiter allows 10 requests per minute for the whole policy, with no queue, on upload and GitHub submit only. Login, AI, and CI are not limited.
- **Potential impact:** A browser on another origin cannot call the API with credentials under this policy. A non-browser client ignores CORS. Unthrottled login and CI remain. `AllowedHosts: *` does not restrict binding.
- **Recommended mitigation:** Configure production origins explicitly. Rate-limit login and CI. Set `AllowedHosts` to the public host name.
- **Current remediation status:** Local CORS restriction is implemented. Production origin configuration and broader rate limits are not.

### Stripe webhook

- **Risk level:** Low today, high if the stub is completed without a signature check.
- **Affected component:** `StripeSubscriptionService.HandleWebhookAsync`.
- **Explanation:** The handler logs that a request arrived and whether the signature header was non-empty. It does not verify a signature and does not change `UserSubscription`.
- **Potential impact:** Today, an anonymous caller can write a log line. Later, an unverified handler that updates plans would let anyone change a tenant's tier.
- **Recommended mitigation:** Verify `Stripe-Signature` with the webhook secret before any state change, and reject missing secrets the way the GitHub webhook does.
- **Current remediation status:** Stub. No subscription mutation.

### Exception information disclosure

- **Risk level:** Low outside Development.
- **Affected component:** `ExceptionHandlingMiddleware`.
- **Explanation:** Unexpected errors return a fixed message and a trace id. `ArgumentException` and `InvalidOperationException` return their message, including ZIP and GitHub failures, which is useful and sometimes reveals a path or a GitHub status code. `UnauthorizedAccessException` is replaced with `Unauthorized.` when it hits the middleware. Auth actions return their own messages before the middleware.
- **Potential impact:** Clients learn validation details. They do not receive stack traces from this middleware.
- **Recommended mitigation:** Keep 500 bodies generic in every non-Development environment. Review `InvalidOperationException` messages for internal paths.
- **Current remediation status:** Implemented as described.

### Secrets management

- **Risk level:** High for the committed JWT key. Other secrets are empty in `appsettings.json`.
- **Affected component:** `appsettings.json`, launch settings, `scripts/Run-ProdFalconApi.ps1`.
- **Explanation:** The SQL connection string uses integrated security and contains no password. OpenAI, Stripe, and GitHub webhook values are empty strings. The JWT key is not empty. Launch settings repeat the SQL connection string as an environment variable and do not contain a password.
- **Potential impact:** JWT forgery where the committed key is reused. Empty integration secrets disable those features or, for Stripe, leave the stub in place.
- **Recommended mitigation:** User secrets locally, environment variables or a vault in deployment, and a startup check for production.
- **Current remediation status:** Not remediated for the JWT key.

### Storage permissions

- **Risk level:** Medium.
- **Affected component:** `ProjectStorageService`, `StorageCleanupHostedService`.
- **Explanation:** The process creates `StorageRoot` and writes archives as the account running the API. Paths are tenant-scoped. The cleanup job deletes old project directories as that same account. There is no per-tenant disk quota.
- **Potential impact:** The API account can read every tenant's source. A code execution bug is also a cross-tenant file read.
- **Recommended mitigation:** Dedicated low-privilege account, directory ACL limited to that account, disk quota, and separate hosts for untrusted parse work if the threat model requires it.
- **Current remediation status:** Logical tenant folders only. OS permissions are whatever the host account has.

### Logging of sensitive information

- **Risk level:** Low for the lines reviewed.
- **Affected component:** Serilog configuration, auth, GitHub client, Stripe stub, dashboard logs.
- **Explanation:** Minimum level is Information, with Microsoft and EF at Warning. Request logging is enabled. Auth does not log passwords. GitHub download logs do not include the bearer token. Stripe logs user id, tenant id, and tier. Dashboard logs counts. Audit rows store action names and a metadata string that can include an email.
- **Potential impact:** Operational logs plus audit metadata identify users. They should not contain passwords or GitHub tokens with the current statements.
- **Recommended mitigation:** Keep bodies out of logs. Treat audit metadata as sensitive. Review new log templates when adding token handling.
- **Current remediation status:** Current statements match the description above. This is not a guarantee about future log lines.

## What this audit did not do

- No dynamic exploit was run against a live server.
- Dependency CVEs for ProdFalcon's own NuGet graph were not scanned with `dotnet list package --vulnerable` as part of the documentation pass. OSV scanning applies to uploaded projects, not automatically to this solution at build time.
- The Angular client is outside this repository and was not reviewed.

## Related documents

- [SECURITY.md](../SECURITY.md) for how to report a vulnerability
- [adr/0003-authentication-and-authorization.md](adr/0003-authentication-and-authorization.md)
- [adr/0004-file-storage-and-processing.md](adr/0004-file-storage-and-processing.md)
