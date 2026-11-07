# ADR 0002: EF Core and SQL Server

- **Status:** Documented architectural decision. Not a record of a benchmark.
- **Date:** 2026-11-07

## Context

Users, tenants, memberships, scans, findings, and repository registrations are relational. Tenant isolation has to apply to ordinary reads. Schema changes need a history. The development connection uses SQL Server Express with Windows integrated security.

## Decision

Use Entity Framework Core 8.0.26 with the SQL Server provider. Configure lengths, indexes, and relationships in `ApplicationDbContext.OnModelCreating`. Use global query filters for tenant-owned entities. Stamp `TenantId` in `SaveChanges` when it is empty. Check migrations into `ProdFalcon.Infrastructure/Migrations` and apply them with `Database.MigrateAsync` on API startup outside the `Testing` environment.

Use SQLite only inside `ProdFalconWebApplicationFactory`, via `EnsureCreated`, so HTTP tests do not need SQL Server.

## Alternatives Considered

- Dapper or hand-written SQL. Each query would show its tenant predicate. Each new query could forget it. The code instead centralizes the predicate and uses `IgnoreQueryFilters` at a few call sites that must cross tenants.
- PostgreSQL. A viable relational store. No provider is registered.
- SQLite as the API database. It is the test double, not the configured API provider.

No alternative was load-tested in this repository.

## Consequences

Migrations are the schema source of truth. Tests can pass when a migration is wrong because they do not execute migration SQL. Startup migration requires DDL rights and can race if two instances start together. Integrated security keeps passwords out of the development connection string. Production must supply its own connection string through configuration, not through a committed secret.

`IgnoreQueryFilters` in login, super-admin, and a subscription lookup is part of the design. New uses of that API need a reason.

## Trade-offs

Filters reduce the chance of a missing `WHERE TenantId` and make cross-tenant queries look like ordinary queries until someone notices they returned nothing. SQL Server matches the author's environment and the existing migrations. Contributors without SQL Server can still run the automated tests.

See [../DATABASE_DESIGN.md](../DATABASE_DESIGN.md).
