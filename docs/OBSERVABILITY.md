# Observability

ProdFalcon records operational events with Serilog and the ASP.NET Core logger. It does not ship a metrics pipeline, a tracing backend, or an error-tracking product. This document describes the current signals and a practical way to investigate a failed scan.

## Logging implementation

`Program.cs` calls `builder.Host.UseSerilog` and reads the `Serilog` section. The checked-in configuration:

- Uses `Serilog.Sinks.Console`.
- Enriches from `LogContext`. No application code was found that pushes a custom property into `LogContext`.
- Sets the default minimum level to Information.
- Overrides `Microsoft` and `Microsoft.EntityFrameworkCore` to Warning.
- Writes one console sink with Serilog's default output template for that sink configuration. The configuration block does not set an explicit `outputTemplate`.

`UseSerilogRequestLogging` adds one Information event per completed HTTP request. That event includes the method, path, status code, and elapsed time. It does not include the request body.

`ILogger<T>` is used in controllers and services. Examples that exist today:

- API startup logs the listen URLs and a Swagger hint.
- The scan worker logs completion, cancellation, and failure with `ScanResultId` and, on success, `TenantId`.
- The GitHub controller logs a queued webhook scan id and repository name.
- The GitHub client logs owner, name, and branch before download.
- OpenAI failures log a warning and the exception, then the service returns heuristics.
- Stripe logs user id, tenant id, and tier for the stub checkout, and whether a webhook signature header was present.
- Dashboard actions log the route and summary counts.
- Storage cleanup logs a warning on failure and an information event when a directory is deleted.
- The exception middleware logs unhandled exceptions at Error with `TraceId`.

EF Core SQL is not logged at the default levels because the EF override is Warning.

## Log levels in use

| Level | What it means here |
| --- | --- |
| Information | Request completed, scan completed, job queued, cleanup deleted a directory, startup |
| Warning | OpenAI call failed, ZIP validation threw an unexpected exception, cleanup job failed |
| Error | Unhandled HTTP exception, scan worker caught a non-cancellation failure |
| Fatal | Port 5014 already in use, from the startup `IOException` handler |

Debug and verbose are not configured. Raising `Microsoft.EntityFrameworkCore` to Information would log SQL and parameters. Do that only on a machine where parameters cannot leak secrets into a shared log sink. The findings search parameter is a user search string. A JWT is not a SQL parameter.

## Structured logging

Serilog message templates are structured. For example, the worker uses `Scan {ScanResultId} failed` with the id as a property. The console sink renders those properties into the line. Downstream collectors that parse Serilog's JSON would need a JSON formatter. The current sink is plain console text, so a log shipper sees a string unless you change the formatter. That change is not in the repository.

## Exception tracking

Unhandled exceptions pass through `ExceptionHandlingMiddleware`. The log contains the exception and `HttpContext.TraceIdentifier`. The client receives that identifier as `traceId` on `ApiErrorResponse`. There is no separate correlation-id middleware and no `X-Correlation-ID` reader. ASP.NET Core's trace identifier is the correlation handle that exists today.

Scan failures are not unhandled at the HTTP layer for queued work. The worker catches them. Look for `Scan {ScanResultId} failed` and then read `ErrorMessage`, `Status`, and `Stage` on the scan row. `MarkTerminalAsync` can itself fail and swallow that second exception, so a missing status update with an error log is possible.

`BackgroundServiceExceptionBehavior.Ignore` stops a hosted-service fault from tearing down the process. Combined with the worker's per-job catch, a single bad scan should not recycle the host. An exception outside the per-job catch, such as a failure inside the channel reader, is the case the host option is aimed at.

## Request diagnostics

For a failed interactive call:

1. Note the `traceId` in the JSON body if the middleware handled it.
2. Find that identifier in stdout.
3. The matching request-log line shows status and duration.
4. For 400 responses returned directly by a controller, there may be no error log. The response `message` is the validation result.

For a scan that stays at `Queued`:

1. Confirm the process that returned the id is still running. The queue is in memory.
2. Search logs for that `ScanResultId`. Absence of a completion or failure line means the worker never started it or the log stream is from a different process.
3. Read `Stage` and `StageMessage`. Values move from `Queued` through acquisition, detection, scanners, project understanding, and scoring.

For a `Failed` scan, `ErrorMessage` is the exception message truncated by the repository. GitHub status text and ZIP limit messages are expected there. Stack traces are in the worker error log, not in the API response.

## Health monitoring

`AddHealthChecks()` registers the health subsystem with no additional checks. `GET /health` is also mapped to a handler that calls `Database.CanConnectAsync`. A probe that expects the Health Checks JSON schema (`status: Healthy` from the health middleware) may instead receive `{ "status": "Healthy" }` from the custom handler, or an ambiguous-match failure. Verify the deployed response before you alert on a schema.

There are no metrics for queue depth, scan duration histograms, disk usage, or OSV error rate. Scan duration is stored on the row as `DurationMs` and can be queried. Queue depth is not exposed.

## Recommended monitoring, not implemented

- Ship stdout to a retained log store.
- Alert on `Scan * failed`, host exits, and disk free space on `StorageRoot`.
- Alert when rows remain `Queued` or `Scanning` longer than `ScanTimeoutMinutes` plus a margin. That condition is the visible symptom of a dead worker or a lost in-memory queue.
- Add a queue-depth gauge if the queue becomes durable.
- Keep EF command logging off in production.
- Add a distinct liveness check that does not touch SQL, and a readiness check that does, once the duplicate `/health` mapping is resolved.

## Investigating a production failure

1. Identify whether the caller received an HTTP error or a scan id.
2. If HTTP, use `traceId` and the request log.
3. If a scan id, read the row's `Status`, `Stage`, `StageMessage`, and `ErrorMessage`, then the worker log for that id.
4. If the failure mentions disk, path length, or an unsafe archive, inspect `StorageRoot/{tenant}/{project}` and the configured limits. Do not open untrusted archives on a workstation that you care about. Use a disposable environment.
5. If the failure mentions GitHub HTTP status, the repository was missing, private, or the branch did not resolve. The token is not in the log line.
6. If users see data from another tenant, treat that as a filter bug. Capture the route, both tenant ids, and the SQL if you can reproduce it in a non-production environment with EF logging. The isolation tests are the regression net for the known cases.

## Related documents

- [DEPLOYMENT.md](DEPLOYMENT.md)
- [SYSTEM_DESIGN.md](SYSTEM_DESIGN.md)
- [SECURITY.md](SECURITY.md)
