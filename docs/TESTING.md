# Testing

ProdFalcon uses xUnit against the real API host for a small set of HTTP checks, and direct calls for scanner behavior. The test project is `ProdFalcon.Tests`. It references `ProdFalcon.API` and brings Application and Infrastructure in through that reference.

## Framework and layout

| Package | Version | Use |
| --- | --- | --- |
| xUnit | 2.5.3 | Test framework |
| xunit.runner.visualstudio | 2.5.3 | Visual Studio and `dotnet test` adapter |
| Microsoft.NET.Test.Sdk | 17.8.0 | Test host |
| Microsoft.AspNetCore.Mvc.Testing | 8.0.26 | `WebApplicationFactory` |
| Microsoft.EntityFrameworkCore.Sqlite | 8.0.26 | In-memory database for API tests |
| coverlet.collector | 6.0.0 | Coverage when collection is requested |

There is one test project and three test classes. There is no separate unit-test project and integration-test project.

| Class | Style | What it locks in |
| --- | --- | --- |
| `SecurityEngineTests` | Direct calls, no host | GitHub URL accept and reject lists, secret redaction, SQL concatenation versus parameters, ZIP escape, open-finding score, stable fingerprint |
| `IntelligenceAndQualityTests` | Direct calls, plus one archive read | Profile versus inferred purpose, architecture edges that must exist in source, quality versus vulnerable samples, reconciliation, workflow YAML, webhook signature, copilot limits, Hello World archive analysis |
| `TenantIsolationTests` | Hosted API | Cross-tenant scan read, dashboard isolation, AI isolation, tenant folder on upload, super-admin denial, anonymous project denial |

`ProdFalconWebApplicationFactory` sets the environment to `Testing`, replaces the SQL Server context with a shared in-memory SQLite connection, and removes every `IHostedService`. Removal matters because the scan worker and the cleanup job would touch that database and the disk during tests. `Program.cs` skips `MigrateAsync` when the environment is `Testing`. Tests call `EnsureCreated`.

## Unit and integration tests

A unit test here calls a type in process and does not boot the web host. `SecurityEngineTests` and most of `IntelligenceAndQualityTests` are unit tests in that sense. They are still integration tests of several classes when they run `ProjectAnalysisPipeline` or `SafeZipExtractor` against the filesystem. They do not cross a process or network boundary, except the Hello World test if it reads a local zip. They do not call GitHub.

An integration test in this repository boots `TestServer` and sends HTTP. `TenantIsolationTests` is that kind of test. It runs the middleware and EF Core against SQLite. It does not run SQL Server migrations, the scan worker, or the real GitHub, OpenAI, or Stripe clients unless a test calls a route that does. The isolation tests do not dequeue scans.

The gap between those styles is the hosted worker. Nothing in the suite waits for `ScanWorkerHostedService` to finish a queued upload. Queue behavior is untested.

## What is covered

- Parsing of public GitHub repository URLs and rejection of unsafe ones.
- Secret detection that does not keep the dummy value, and suppression of password placeholders.
- A difference between concatenated SQL and parameterized SQL in the analyzer.
- Rejection of a ZIP entry that escapes the destination.
- Score calculation from open findings only.
- Fingerprint stability.
- Project profile and architecture behavior on fixtures defined in the test file.
- Webhook signature rejection when the body is tampered.
- Copilot answers that must not invent a dependency upgrade, and a list of production limits.
- Tenant A versus tenant B for scan get, dashboard projects, and AI suggestions.
- Upload storage under the tenant folder.
- A normal user calling `GET /api/admin/tenants`.
- An anonymous call to `GET /api/projects`.

## What is not covered

- Login, registration, password edge cases, and super-admin promotion.
- Stripe checkout and the webhook stub.
- Billing quota enforcement. There is no enforcement to test. The endpoint's hardcoded usage is untested.
- `POST /api/github/ci`, webhook acceptance, and auto-fix 501.
- Report JSON, CSV, and PDF responses.
- Cancel, compare, finding status, and pagination.
- OSV dependency lookups. Those need the network.
- OpenAI HTTP calls.
- SQL Server migrations applied to a real instance.
- The two-worker queue, timeout, and restart behavior.
- Rate limiting and CORS.
- `SubscriptionValidationMiddleware` outside Development and Testing. The test host is `Testing`, so the middleware skips the 402 check.
- Dashboard summary fields, including the `-1` placeholders.

## Security testing that exists

The security tests are deterministic unit checks, not a dynamic scan of a running ProdFalcon instance.

- ZIP slip: one test builds an archive with an unsafe entry and expects rejection.
- Secret storage: one test checks a dummy key is detected and not retained.
- Webhook: one test checks a tampered body fails verification.
- Tenant isolation: HTTP tests with two registered users.

There is no fuzzing of `SafeZipExtractor`, no authentication bypass suite, and no test that the committed JWT key is rejected.

## Commands

From the repository root:

```bash
dotnet test ProdFalcon.Tests/ProdFalcon.Tests.csproj
```

Release configuration, TRX logger, and Coverlet:

```bash
dotnet test ProdFalcon.Tests/ProdFalcon.Tests.csproj --configuration Release --logger "trx;LogFileName=test-results.trx" --collect:"XPlat Code Coverage" --results-directory TestResults
```

Coverage files land under `TestResults` as Cobertura XML named `coverage.cobertura.xml`. Coverlet's collector reports a line-rate for the assemblies it instruments. That number is not a statement that every endpoint was executed. Assemblies without hits, generated migrations, and code that only runs inside the removed hosted services pull the interpretation in different directions. Read the XML for the commit you built.

## Measured coverage

On 2026-11-07 the Release suite was run on the authoring machine with SDK `10.0.402`:

```bash
dotnet test ProdFalcon.Tests/ProdFalcon.Tests.csproj --configuration Release --no-build --logger "trx;LogFileName=test-results.trx" --collect:"XPlat Code Coverage" --results-directory TestResults
```

The run reported 30 passed, 0 failed, and 0 skipped. Test execution time in the runner summary was 25 seconds, after the host had already been built.

Coverlet collector 6.0.0 wrote Cobertura XML with:

| Metric | Value |
| --- | --- |
| Line coverage | 22.88% (2,168 of 9,475 lines) |
| Branch coverage | 27.35% |

Per assembly line rate in that file:

| Assembly | Line rate |
| --- | --- |
| ProdFalcon.API | 29.33% |
| ProdFalcon.Application | 44.12% |
| ProdFalcon.Domain | 72.91% |
| ProdFalcon.Infrastructure | 9.67% |
| ProdFalcon.Shared | 54.54% |

Infrastructure includes the EF Core migration and model snapshot source. Those generated lines are in the 9,475 and are not executed by this suite, so the Infrastructure rate is not a measure of the services alone. The percentages describe this run's instrumented assemblies. They do not mean 23% of the product's behavior was specified. Re-run the command for any other commit.

`TestResults/` is gitignored. CI uploads the same artifact from the workflow.

## Limitations

SQLite `EnsureCreated` builds tables from the current model. A migration can be wrong and the tests can still pass. Background work is disabled, so a green suite does not mean a queued scan completes. The factory uses one SQLite connection for the host. Tests that need a clean database must not assume process-wide isolation beyond what each fact arranges.

`dotnet test` does not start the SQL Server database and does not require `Jwt:Key` from your user secrets. The test host uses the application's configuration files plus the `Testing` environment. Prefer not to point tests at a production connection string. The factory replaces the context options, so the SQL connection string is not used for test queries even if it is present.

## Related documents

- [adr/0005-testing-strategy.md](adr/0005-testing-strategy.md)
- [../.github/workflows/ci.yml](../.github/workflows/ci.yml)
