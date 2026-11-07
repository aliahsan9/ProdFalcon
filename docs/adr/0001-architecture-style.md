# ADR 0001: Layered monolith for the scan API

- **Status:** Documented architectural decision. The original discussion was not recorded. This ADR describes the structure that is in the repository on 2026-11-07.
- **Date:** 2026-11-07

## Context

ProdFalcon accepts archives and GitHub repositories, scans them, and stores tenant-scoped results. The solution is split into API, Application, Domain, Infrastructure, Shared, and Tests projects. The API process also runs the scan workers.

A strict Clean Architecture dependency rule would keep the web project from referencing Infrastructure and would keep Domain free of Shared. The project references do not do that.

## Decision

Keep one deployable process. Put HTTP composition in the API project, scan use cases and rules in Application, organization entities in Domain, and EF Core plus external HTTP in Infrastructure.

Accept three boundary exceptions that are already in the code:

- The API project references Infrastructure and uses it for migrations, the database health handler, report writing, and the Stripe service interface.
- Domain references Shared for `SubscriptionTier`.
- Scan tables are CLR types in Application, mapped by EF Core. They are not Domain entities.

Do not describe the solution as strict Clean Architecture.

## Alternatives Considered

- One project. Fewer references, and no place where the dependency direction is visible.
- Moving scan entities into Domain and hiding Infrastructure behind application interfaces only. That would match the textbook rule. It is a refactor, not the current code.
- Separate worker process. That would isolate archive parsing from the API and would require a durable queue the project does not have.

## Consequences

Readers can find rules without reading controllers. Tests can call `SafeZipExtractor` and the analyzers without SQL Server. New code can also call EF from a controller because the reference allows it. Reviews have to enforce the boundary the compiler does not.

The in-process worker can set tenant context with the same `ITenantProvider` the request uses. A second API instance does not share the queue.

## Trade-offs

One process is simpler to run and easier to trace. It concentrates disk, memory, and parser risk in the API host. The layering communicates intent and does not guarantee it.

See [../ARCHITECTURE.md](../ARCHITECTURE.md) and [../ENGINEERING_DECISIONS.md](../ENGINEERING_DECISIONS.md).
