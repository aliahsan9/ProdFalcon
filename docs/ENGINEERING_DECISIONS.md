# Engineering decisions

These sections explain why the current design fits the problem the code actually solves. They are architectural rationale written against the source on 2026-11-07. They are not minutes of a design review, and they do not claim that alternatives were measured.

The format for each item is context, problem, decision, alternatives, advantages, trade-offs, consequences, and current status.

## 1. ASP.NET Core

**Context.** ProdFalcon is a backend used by a browser client and by CI. It must accept multipart uploads, authenticate callers, and run background work.

**Problem.** The HTTP surface, the scan workers, and the database need one hosting and dependency-injection model.

**Decision.** Host the system as an ASP.NET Core minimal hosting application on `net8.0`, with controllers rather than minimal-API route groups. `Program.cs` is the composition root. A public partial `Program` class exists so `WebApplicationFactory<Program>` can boot the API in tests.

**Alternatives considered.** A generic host without ASP.NET would still need an HTTP stack. A function-per-endpoint host would split the scan worker from the API that enqueues it. Neither is required at the current size.

**Advantages.** Authentication middleware, rate limiting, configuration, and hosted services are in the same framework as the controllers. The test project can boot the real pipeline.

**Trade-offs.** The scan shares a process with the API. Long work needs its own timeout, which the worker implements, but a bad archive can still pressure the host.

**Consequences.** Deployment is one web process plus SQL Server plus a disk. Background services must not use the request scope.

**Current implementation status.** Implemented. Swagger, health, CORS, JWT, and rate limiting are wired in `Program.cs`.

## 2. Layered architecture, not strict Clean Architecture

**Context.** The solution has API, Application, Domain, Infrastructure, Shared, and Tests projects.

**Problem.** Scanning, tenancy, and storage change for different reasons. Putting all of that in the API project would make rules untestable without HTTP.

**Decision.** Keep use-case code and scan rules in Application, EF Core and HTTP clients in Infrastructure, and organization entities in Domain. Accept that the API references Infrastructure and that Domain references Shared.

**Alternatives considered.** A single project would remove the project references that currently document the dependency direction. A stricter model would move scan entities into Domain, move `SubscriptionTier` out of Shared, and hide Infrastructure behind interfaces the API does not reference. That refactor is not done.

**Advantages.** Rules and ZIP checks have unit tests that do not start SQL Server. The solution structure tells a reader where to look.

**Trade-offs.** The dependency rule is already broken in specific places, so new code can follow either the diagram or the existing controller references. The broken spots are easy to copy.

**Consequences.** Reviews should reject new EF queries inside controllers even though the project reference allows them. Report generation and the Stripe stub are existing exceptions.

**Current implementation status.** Layering is implemented with the exceptions in [ARCHITECTURE.md](ARCHITECTURE.md).

## 3. Separate Domain and Application projects

**Context.** Tenancy concepts are stable relative to scan rules. Scan rules have grown quickly and include EF-mapped types.

**Problem.** If every new scanner type is added to Domain, the organization model and the analysis model become one pile of entities.

**Decision.** Domain holds users, tenants, members, audits, and subscriptions. Application holds scan orchestration and the scan tables' CLR types.

**Alternatives considered.** One model project for every entity. That would be simpler and would also hide the fact that scanning is the product and tenancy is the boundary around it. Putting scan entities in Domain without moving behavior there would not add invariants.

**Advantages.** The tenancy model can be read in one small project. Scan code can change without editing Domain.

**Trade-offs.** "Domain" understates the real business rules. Scoring, fingerprints, and ZIP policy are the product rules, and they live in Application.

**Consequences.** A reader looking only at Domain will miss the product. Documentation and onboarding should start from `ScanService`.

**Current implementation status.** The split is implemented. Domain entities are anemic. Product rules are services and static analyzers.

## 4. Entity Framework Core

**Context.** The system stores users, tenants, scans, findings, and registrations, and it must not return one tenant's rows to another.

**Problem.** Hand-written SQL would duplicate tenant predicates and migration history.

**Decision.** Use EF Core 8 with a SQL Server provider, fluent configuration in `OnModelCreating`, global query filters on `ITenantEntity.TenantId`, and `SaveChanges` to stamp `TenantId` when a new tenant entity has an empty id.

**Alternatives considered.** Dapper or raw ADO.NET would make the filters explicit at each call site. That is easier to audit one query at a time and easier to get wrong on the next query. The code uses `IgnoreQueryFilters` in a few intentional places: login membership lookup, super-admin tenant operations, and a user-subscription lookup when no tenant is set. Those call sites are the ones to review when the filter changes.

**Advantages.** Migrations are in source. The tenant filter applies to normal queries. Tests replace the provider with SQLite.

**Trade-offs.** Query filters surprise anyone who forgets they exist, and `IgnoreQueryFilters` surprises anyone who uses it too widely. SQLite in tests does not prove SQL Server-specific behavior such as `nvarchar` lengths.

**Consequences.** Schema changes belong in migrations. See [DATABASE_DESIGN.md](DATABASE_DESIGN.md).

**Current implementation status.** Implemented. Three migrations are in the repository. The API applies them on startup outside the test environment.

## 5. SQL Server

**Context.** The development connection string targets `.\SQLEXPRESS` with integrated security. The provider package is SQL Server.

**Problem.** Scan history, findings, and memberships are relational. They need transactions around registration and durable storage of results.

**Decision.** SQL Server is the system of record for the API process. Integrated security is the local default so the repository does not contain a SQL password.

**Alternatives considered.** PostgreSQL would also fit. SQLite is already the test database and would be a poor shared store because the API and any second instance would need a server, and the in-memory test connection is not the production design. No provider other than SQL Server is registered for the API.

**Advantages.** Transactions, unique indexes, and EF's SQL Server provider match the existing migrations. Local setup does not require a password in git.

**Trade-offs.** Contributors on Linux need a SQL Server they can reach, or they can run tests only. The startup path does not fall back to SQLite.

**Consequences.** Production must supply `ConnectionStrings:DefaultConnection` through the environment or a secret store. The launch profile value is a workstation default.

**Current implementation status.** Implemented for the API. Not exercised by the automated tests, which swap in SQLite.

## 6. Dependency injection

**Context.** Rules, repositories, and HTTP clients are replaceable in tests and must be constructed with configuration.

**Problem.** A static service locator would hide lifetimes. The scan worker and the HTTP request need different scopes.

**Decision.** Use the built-in `IServiceCollection`. Rules are scoped. The queue is a singleton. Workers create a scope per job. Named HTTP clients carry their own timeouts and handlers.

**Alternatives considered.** Constructing services in controllers would bypass lifetimes and make the worker a second composition root. A third-party container is unnecessary at this size.

**Advantages.** The test host can remove hosted services and replace `DbContextOptions` without editing production constructors.

**Trade-offs.** A scoped service captured by the singleton queue would be a lifetime bug. The queue stores work items, not scoped services, which avoids that particular bug.

**Consequences.** New rules should be registered next to the existing `IScanRule` lines. A rule that is not registered does not run.

**Current implementation status.** Implemented in the two `*ServiceRegistration` classes.

## 7. DTOs separated from persisted entities

**Context.** Controllers return scan data, auth payloads, member lists, and dashboard aggregates.

**Problem.** Returning `ScanResult` directly would expose navigation properties, analysis JSON, and storage paths in the same shape the database uses. Some endpoints still return anonymous objects, so the separation is partial.

**Decision.** Auth, tenant admin, and scan queries use explicit DTO types such as `AuthResponseDto`, `ScanResultDto`, and `DashboardSummaryDto`. `ScanMappings` copies entities into those DTOs. Several GitHub and billing actions return anonymous objects inside `ApiResponse<object>`.

**Alternatives considered.** One shared model for storage and HTTP would remove the mapping and would couple clients to column changes. That is already the risk for the anonymous responses.

**Advantages.** `ScanResultDto` omits the raw analysis document from the main scan payload. Intelligence, architecture, and threats have their own endpoints. Secret evidence is a redacted string by the time it is mapped, assuming the rule used `SecretRedactor`.

**Trade-offs.** Mapping can drift. Anonymous responses have no single contract type for Swagger to name.

**Consequences.** New public payloads should be DTO types. The existing anonymous responses are documented as such in [API_DOCUMENTATION.md](API_DOCUMENTATION.md).

**Current implementation status.** Implemented for scans, auth, members, and dashboard. Partial for GitHub, billing, and admin command responses.

## 8. Request validation

**Context.** The API accepts archives, repository URLs, tokens, and status strings from outside the process.

**Problem.** An unbound request can queue work, write disk, or change a finding.

**Decision.** Validate at the edge that is about to do something expensive or persistent: file extension and size, ZIP structure, GitHub URL and branch shape, finding status allow-list, and pagination clamps. Do not rely on an unused validation package.

**Alternatives considered.** FluentValidation is referenced and would centralize DTO rules. It is not registered. Data annotations are not applied to `RegisterDto` or `LoginDto`.

**Advantages.** The checks that exist sit next to the dangerous operation, especially ZIP validation before a scan is queued.

**Trade-offs.** Auth and invite DTOs are weakly checked. Empty or short passwords are not rejected. There is no single error shape for validation.

**Consequences.** Password and email rules are a product gap. Archive rules are a security control and should stay in `SafeZipExtractor` even if DTO validation is added later.

**Current implementation status.** Archive, URL, status, and paging checks are implemented. Account validation is not.

## 9. JWT authentication

**Context.** The Angular client and the CI caller need to call the API without a server session.

**Problem.** The API must know the user, the tenant, and the role on each request, and background work must not trust a missing token.

**Decision.** Issue an HMAC-SHA256 JWT from `JwtService` with a one-day lifetime. Claims include subject, name identifier, email, name, `tenantId`, `organization`, `role`, `plan`, and `is_super_admin`. `HttpTenantProvider` reads those claims. Repository CI uses a separate random token stored as a SHA-256 hash, not a user JWT.

**Alternatives considered.** Cookie sessions would fit a same-site browser and would complicate the CI caller. ASP.NET Core Identity is referenced and unused. A reference token stored in SQL would allow revocation and would add a lookup on every request. That lookup does not exist today.

**Advantages.** The API can authorize a request without a session row. Tenant context is present even when the database filter runs.

**Trade-offs.** A stolen token works until it expires. There is no refresh token and no denylist. The super-admin flag is a claim, so it is whatever the token said at login. Role checks are hand-written. There is no `[Authorize(Roles = ...)]` policy. Changing a user's role does not affect tokens that were already issued.

**Consequences.** Signing keys must come from a secret store. The value currently checked in under `Jwt:Key` is a development placeholder and has to be rotated anywhere it might have been used. See [SECURITY.md](SECURITY.md).

**Current implementation status.** Implemented for user sessions. Not a complete authorization model.

## 10. Centralized exception handling

**Context.** Services throw `KeyNotFoundException`, `ConflictException`, `InvalidOperationException`, and `UnauthorizedAccessException`.

**Problem.** Without a middleware boundary, some actions return `ApiResponse` and others would return an HTML or empty 500.

**Decision.** `ExceptionHandlingMiddleware` logs the exception with `HttpContext.TraceIdentifier` and writes `ApiErrorResponse`. Unexpected messages are hidden outside Development.

**Alternatives considered.** An exception filter would only see MVC exceptions. Middleware also covers failures outside an action. Returning `ApiResponse` from every action avoids exceptions for control flow. The code uses both styles.

**Advantages.** One log line and one JSON shape for unhandled failures. Development still gets `exception.Message` for 500 responses.

**Trade-offs.** Using exceptions for expected 404 responses costs a throw. Auth actions bypass the middleware and use a slightly different anonymous shape. `InvalidOperationException` becomes 400, including failures that are really "the server cannot do this" rather than "the client sent a bad value".

**Consequences.** Clients should read `success`, `message`, and `traceId`. They should not depend on `errors.detail` outside local development.

**Current implementation status.** Implemented. Coverage of every controller path is not.

## 11. Automated testing

**Context.** The product evaluates other people's code. Regressions in tenant filters, ZIP paths, or secret storage would be serious.

**Problem.** Manual scanning cannot guard the tenant filter or the ZIP prefix check on every change.

**Decision.** Keep an xUnit project that references the API host. Pure tests call analyzers and `SafeZipExtractor` directly. Isolation tests boot the host with SQLite and without background workers.

**Alternatives considered.** No tests would leave the filter and the ZIP checks unverified. A full SQL Server integration suite would be closer to production and is not what the project runs today. Browser tests of the Angular client are out of scope for this repository.

**Advantages.** CI can run on a GitHub-hosted runner without SQL Server. The ZIP slip test and the cross-tenant test lock in two properties that are easy to break.

**Trade-offs.** SQLite does not execute the SQL Server migrations. `EnsureCreated` builds the model instead. A migration that does not match the model can pass tests and fail on SQL Server.

**Consequences.** Schema changes need a migration and a startup check against SQL Server, not only `dotnet test`. Untested areas are listed in [TESTING.md](TESTING.md).

**Current implementation status.** Implemented for the cases in the three test classes. Not a full endpoint suite.

## 12. File processing and storage

**Context.** Callers upload archives the API did not build. Those archives can contain hostile paths and highly compressed entries.

**Problem.** Extracting a ZIP with `ZipFile.ExtractToDirectory` would follow unsafe paths and would not cap decompressed size.

**Decision.** Save the ZIP under a tenant-scoped directory, validate with `SafeZipExtractor` before queueing, and extract with the same limits while counting bytes written. Skip dependency folders and large files during analysis. Delete directories older than two days. Keep database rows.

**Alternatives considered.** Extracting in the request would make the upload timeout the limiting factor and would hold an HTTP thread for the whole scan. The interactive API queues instead. The CI endpoint still scans inline because the caller wants a pass or fail status. Scanning in place on a user-supplied path is not supported. GitHub downloads are copied into the same store so both sources share one extractor.

**Advantages.** Path checks and size checks are unit tested. Disk layout is predictable. A second tenant does not receive the first tenant's path.

**Trade-offs.** The queue is not durable. Cleanup can remove files a scan row still references. There is no malware scanner. The ratio check is a heuristic, not a proof that decompression is cheap. Committed historical extracts under `ProjectStorage/` show that earlier output was not kept out of git.

**Consequences.** Operators need disk monitoring and a private `Storage:RootPath`. The repository should stop tracking extracted trees. Limits belong in configuration, and the request-size attribute on the upload action should stay aligned with `MaxUploadBytes`.

**Current implementation status.** Validation, extraction, tenant paths, and cleanup are implemented. Malware scanning, durable queueing, and removal of historical `ProjectStorage` content are not.

## Related records

- [adr/0001-architecture-style.md](adr/0001-architecture-style.md)
- [adr/0002-database-and-orm.md](adr/0002-database-and-orm.md)
- [adr/0003-authentication-and-authorization.md](adr/0003-authentication-and-authorization.md)
- [adr/0004-file-storage-and-processing.md](adr/0004-file-storage-and-processing.md)
- [adr/0005-testing-strategy.md](adr/0005-testing-strategy.md)
- [adr/0006-error-handling-and-validation.md](adr/0006-error-handling-and-validation.md)
