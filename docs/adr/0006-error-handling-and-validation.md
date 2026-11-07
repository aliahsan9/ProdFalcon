# ADR 0006: Mixed validation and a single exception middleware

- **Status:** Documented architectural decision.
- **Date:** 2026-11-07

## Context

Some failures are client mistakes (bad ZIP, unknown finding status, duplicate email). Some are unexpected. The codebase throws for several expected cases and returns `ApiResponse<T>.Fail` for others. FluentValidation and AutoMapper are package references. Neither is registered.

Clients need a JSON body they can log. Operators need the exception, including a trace identifier, without sending a stack trace to the caller.

## Decision

Keep validation next to the operation that needs it:

- Upload size, extension, and `SafeZipExtractor` before a scan is queued.
- `GitHubUrlParser` for repository URLs and branch names.
- An allow-list in `ScanQueryService` for finding status.
- Page and page-size clamps in the query service.

Do not pretend a validation framework runs. Add one later if DTO rules grow, or remove the unused package.

Handle unhandled exceptions in `ExceptionHandlingMiddleware`. Log the exception with `HttpContext.TraceIdentifier`. Map known exception types to 404, 409, 400, 401, and 501. Map everything else to 500 with a fixed message. Include `exception.Message` on that 500 only when the environment is Development.

Let auth actions catch their own conflicts and credential failures so the client receives `Invalid credentials.` or `User already exists.` Those messages are intentional and do not include a password.

Persist scan failures on the scan row inside `ScanService`, and let the worker catch the exception so one bad job does not stop the host.

## Alternatives Considered

- FluentValidation for every DTO. The package is already there. Wiring it would centralize password and required-field rules that do not exist today. It would not replace ZIP parsing.
- An MVC exception filter. It would miss failures outside actions. Middleware covers the pipeline that is registered around controllers.
- Result types instead of exceptions for not-found and conflict. Clearer control flow. The services already throw, and the middleware already understands those types.

## Consequences

Error JSON is mostly `success`, `message`, and `traceId`, with two shapes: `ApiResponse` / `ApiErrorResponse`, and the anonymous auth error. `InvalidOperationException` becomes HTTP 400 even when the failure is a missing GitHub repository during a synchronous CI call. Queued GitHub failures do not use that mapping. They become a `Failed` scan.

Development servers reveal exception messages for unexpected errors. Production configuration must not use the Development environment on a shared host.

Password policy is absent because no validator implements it. That is a product gap tracked in [../ROADMAP.md](../ROADMAP.md).

## Trade-offs

Local checks are obvious when you are reading the action and easy to forget on the next action. A central validator is the opposite. The middleware gives one log point for surprises and does not make the thrown exceptions consistent with the returned `ApiResponse` failures.

See [../ARCHITECTURE.md](../ARCHITECTURE.md) and [../API_DOCUMENTATION.md](../API_DOCUMENTATION.md).
