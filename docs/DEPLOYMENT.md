# Deployment

This document separates what the repository can do today from what a production deployment still needs. The supported path is a single machine running the API against SQL Server. Containers, Linux service files, and cloud-specific manifests are not in the repository.

## What works today

- `dotnet run --project ProdFalcon.API` or the `http` and `https` launch profiles.
- Windows scripts `scripts/Run-ProdFalconApi.ps1` and `scripts/Stop-ProdFalconApi.ps1`.
- EF Core migrations applied by the process on startup when the environment is not `Testing`.
- Configuration from `appsettings.json`, `appsettings.{Environment}.json`, environment variables, and the launch profile.
- A `/health` route that can report database connectivity, with the duplicate-mapping caveat in [API_DOCUMENTATION.md](API_DOCUMENTATION.md).
- Serilog console logging from the `Serilog` section.

Publishing:

```bash
dotnet publish ProdFalcon.API/ProdFalcon.API.csproj --configuration Release --output ./publish
```

The publish output is a normal ASP.NET Core application. Run it with the ASP.NET Core runtime 8, or publish self-contained if you choose that extra flag. The repository's CI build does not publish.

## Environment configuration

Set these before the process starts. Environment variables use `__` as the section separator.

| Setting | Development default | Production expectation |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Development` in launch settings | `Production` |
| `ASPNETCORE_URLS` | `http://localhost:5014` on the http profile | The internal bind address behind a proxy |
| `ConnectionStrings__DefaultConnection` | Integrated security to `.\SQLEXPRESS` and database `ProdFalconDb` | A SQL Server connection that does not live in git |
| `Jwt__Key` | A placeholder in `appsettings.json` | A long random secret from a store. Rotate the placeholder |
| `Jwt__Issuer` | `ProdFalcon` | Your issuer string |
| `Jwt__Audience` | `ProdFalconUsers` | Your audience string |
| `Storage__RootPath` | Empty, which resolves to `ProdFalcon_Storage` beside the solution when the path heuristic matches | An absolute directory on a data disk |
| `Scanning__*` | Limits in `appsettings.json` | Revisit upload and extract caps for the disk you have |
| `OpenAI__ApiKey` | Empty | Set only if external suggestions are wanted |
| `GitHub__WebhookSecret` | Empty, webhook returns 503 | Required if GitHub webhooks are enabled |
| `Stripe__SecretKey` and `Stripe__WebhookSecret` | Empty | Unused until the stub is replaced |
| `SuperAdmin__Emails` | A configured list | Empty unless you fully control those mailboxes and accept the lack of verification |
| `AllowedHosts` | `*` | The public host name |

`appsettings.Development.json` only sets log levels. It does not override the JWT key.

The connection string in source uses `Trusted_Connection=True` and `TrustServerCertificate=True`. That is appropriate for a local developer using Windows authentication. It trusts the server certificate, which should not be copied into a hostile network. Production SQL authentication should use a secret store. This document intentionally does not include a password-bearing connection string.

## Secret management

The API uses the standard configuration stack. It does not integrate a vault client.

Local development:

```bash
dotnet user-secrets init --project ProdFalcon.API
dotnet user-secrets set "Jwt:Key" "<long-random-value>" --project ProdFalcon.API
```

User secrets override `appsettings.json` when the environment is Development and the user-secrets id is present. Check `ProdFalcon.API.csproj` for a `UserSecretsId`. If it is absent, `dotnet user-secrets init` adds one. Do not commit that file's secrets. User secrets stay outside the repository.

For any other environment, set environment variables or inject configuration through the host. Do not bake secrets into a published `appsettings.json`.

Startup does not refuse to run in Production when the JWT key is still the development value. That check is a recommended addition, not current behavior.

## Database migrations

On startup, outside `Testing`:

```csharp
await app.Services.MigrateDatabaseAsync();
```

That calls `Database.MigrateAsync` with the application's connection string. The SQL login therefore needs permission to apply DDL, or the migration will fail and the process will exit.

Recommended production sequence, once you operate more than one instance:

1. Take a database backup.
2. Apply migrations from a release step:

```bash
dotnet ef database update --project ProdFalcon.Infrastructure --startup-project ProdFalcon.API
```

3. Start the API.

Today the API will try to migrate again at startup. Two instances starting together can race on migration history. Run one instance, or apply migrations before scale-out, until that startup call is gated.

Test the pending migration against a copy of the database. The automated suite does not apply SQL Server migrations.

## HTTPS

The `https` launch profile binds `https://localhost:7036` and `http://localhost:5014`. Kestrel development certificates are a local concern. `UseHttpsRedirection` is always registered.

In production, terminate TLS at a reverse proxy or configure Kestrel certificates through configuration. The repository does not ship a certificate. Redirect rules behind a proxy usually need forwarded headers. `UseForwardedHeaders` is not registered, so a proxy that sends `X-Forwarded-Proto` will not affect scheme generation until that middleware is added. GitHub registration links are built from `Request.Scheme` and `Request.Host`. Behind a TLS-terminating proxy those values can be wrong without forwarded headers. That matters for the workflow file's callback URL.

## Logging

Serilog writes to the console at Information, with Microsoft and EF Core at Warning. There is no file sink, no centralized sink, and no log shipping in the repository. A service supervisor should collect stdout.

See [OBSERVABILITY.md](OBSERVABILITY.md).

## Production configuration gaps

These are recommendations, not implemented switches.

- Override CORS. The only allowed origins are localhost ports 4200.
- Set a real JWT key and fail startup when it is missing.
- Do not leave `SuperAdmin:Emails` pointed at an address an attacker can register.
- Put `Storage:RootPath` on a volume with monitoring and a quota.
- Decide whether startup should migrate. For a locked-down database, run migrations separately and change the host before you rely on that.
- Add `UseForwardedHeaders` if a proxy terminates TLS.
- Restrict `AllowedHosts`.
- Rate-limit authentication.
- Plan for one process only until the scan queue is durable. A second instance runs its own workers and its own in-memory queue. It will not see jobs enqueued on the first instance. Both will run cleanup against the same disk if they share `StorageRoot`.
- Stripe keys can stay empty. Checkout will not become a real Stripe integration by setting them. The service does not read `Stripe:SecretKey` when building the URL.

## Health

`AddHealthChecks()` is called with no checks registered. `MapHealthChecks("/health")` and a custom database handler are both mapped. Use an external probe only after you confirm the response body on the build you deployed. A database outage should surface as a non-200 from the custom handler when that handler is the one that runs.

There is no readiness versus liveness split.

## Hosting considerations

The process needs:

- Outbound HTTPS to `api.github.com`, `codeload.github.com`, `github.com`, `objects.githubusercontent.com`, and `api.osv.dev` if those features are used.
- Outbound HTTPS to the OpenAI base URL only when a key is set.
- Inbound HTTP or HTTPS from the Angular origin and from GitHub webhooks or CI runners.
- A stable disk for `StorageRoot`.
- Enough memory for two concurrent archives buffered up to the upload cap during GitHub downloads.

The scan timeout defaults to 10 minutes. The CI route holds the HTTP request for the whole scan. Proxy timeouts need to be at least that long if CI uses this API synchronously.

## Backup and recovery

Back up the SQL Server database. It holds users, tenants, scans, findings, and registration token hashes.

Back up `StorageRoot` only if you must re-open extracted trees. The product can lose those directories after two days and still serve stored findings. A scan left in `Queued` or `Scanning` at crash time is not restarted.

Restore order: restore SQL, restore or recreate the storage directory, then start one API instance. Registration tokens cannot be recovered from the hash. Callers still holding the original token can authenticate. Everyone else must register again. JWT signing key loss invalidates existing user tokens, which is the desired outcome of a rotation. Keep the new key. There is no refresh-token table to migrate.

Test a restore. The repository does not include a backup script.

## Related documents

- [SECURITY.md](SECURITY.md)
- [DATABASE_DESIGN.md](DATABASE_DESIGN.md)
- [OBSERVABILITY.md](OBSERVABILITY.md)
