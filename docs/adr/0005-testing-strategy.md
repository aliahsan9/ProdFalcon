# ADR 0005: xUnit with a SQLite web host for isolation checks

- **Status:** Documented architectural decision.
- **Date:** 2026-11-07

## Context

The expensive failures for this product are a ZIP that escapes its directory, a secret value copied into a finding, and one tenant reading another tenant's scan. The solution already has an API host that can be booted in memory.

SQL Server is the API database. Requiring it for every test would make CI heavier and would still not cover the worker unless tests opted in.

## Decision

Use one xUnit project, `ProdFalcon.Tests`.

- Call analyzers, scoring, fingerprints, URL parsing, and `SafeZipExtractor` directly.
- Boot the API with `WebApplicationFactory` only for tenant isolation and a small set of HTTP denials.
- Replace `DbContext` with SQLite in memory and create the schema with `EnsureCreated`.
- Remove hosted services in the test host so workers do not share that database.
- Skip startup migrations when the environment is `Testing`, which the factory sets.
- Collect coverage with the referenced Coverlet collector when the test command asks for it. Do not treat an unmeasured number as a gate.

## Alternatives Considered

- No automated tests. The ZIP and tenant checks would depend on manual runs.
- SQL Server in CI. Closer to production, and not the current factory. A migration defect can pass the suite.
- A separate unit project and integration project. Clearer names. The repository uses one project and three classes instead.

## Consequences

CI can run `dotnet test` without SQL Server. Green tests do not mean a queued scan finishes and do not mean migrations apply on SQL Server. Coverage XML describes the run that produced it. Endpoint areas listed in [../TESTING.md](../TESTING.md) remain untested until new facts exist.

The factory's removal of hosted services is deliberate. A test that wants the worker must add it back and accept the timing and database constraints.

## Trade-offs

The suite is fast and narrow. It protects the invariants that have tests and ignores billing, Stripe, CI upload, reports, and authentication. That is an honest scope, not broad safety coverage.

See [../TESTING.md](../TESTING.md).
