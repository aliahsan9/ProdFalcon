# ProdFalcon project audit

Audit date: 2026-11-08. This describes the code that is in the repository after the changes in this pass. It does not describe work that was only planned.

## What the product already was

ProdFalcon is an ASP.NET Core 8 API (`ProdFalcon.API`, Application, Domain, Infrastructure, Shared) plus an Angular app (`prodfalcon-ui`) and an xUnit project (`ProdFalcon.Tests`).

The scan lifecycle that was already in place:

1. A signed-in tenant uploads a ZIP or submits a `github.com` URL.
2. The API stores a `ScanProject` and a queued `ScanResult`, then hands the job to an in-memory bounded channel (`ScanQueue`).
3. `ScanWorkerHostedService` runs the job with a tenant scope and a linked cancellation token.
4. ZIP bytes are checked by `SafeZipExtractor` (entry count, declared size, compression ratio, path escape) and extracted under the tenant storage directory. Uploaded code is never executed.
5. GitHub acquisition allows only `https` URLs on `github.com`, downloads the archive through an allow-list of GitHub hosts, and refuses redirects to any other host.
6. `ScanRuleExecutor` runs the registered `IScanRule` implementations. A failure in one rule was already caught so the rest of the scan could continue.
7. Findings are fingerprinted, deduplicated, scored, and stored with the project-intelligence JSON.
8. The API already exposed scan status, history, finding filters, comparison, cancellation, and JSON/CSV/PDF export. The Angular app already polls scan status and renders finding detail.

Rules that were already real detectors included secret patterns, SQL construction, missing authorization on controllers, CORS wildcards, configuration checks, dependency queries against OSV (capped), and structural quality/performance notes.

## Defects found in that pipeline

- `HardcodedConnectionStringRule`, `HardcodedJwtSecretRule`, and `PlainTextPasswordRule` returned an empty list. Password, connection-string, and JWT findings were only produced inside `ApiKeyExposureRule`, and JWT assignments were labeled as generic secrets.
- `CodeQualityRule` reported `BinaryFormatter` / `TypeNameHandling.All` with line number 1 and no snippet. XSS, command injection, path traversal, weak cryptography, and password hashing were not implemented as detectors. Those categories are not claimed for any build from before this change.
- Every rule walked the tree and read file bytes itself. `DebugModeRule`, `HttpUsageRule`, `SensitiveLoggingRule`, `CorsWildcardRule`, `SwaggerInProductionRule`, and `MissingLoggingRule` called `File.ReadAllTextAsync` directly, so a shared cache would not have helped them.
- `DebugModeRule` flagged any file containing the substring `DEBUG` or `IsDevelopment()`. `SensitiveLoggingRule` flagged a file as Critical when the words "password" and "Log" both appeared anywhere, including `Login`. `HttpUsageRule` flagged every `http://` URL, including XML namespaces. `SwaggerInProductionRule` flagged Swagger even when the file checked `IsDevelopment()`.
- The directory exclusion list contained the literal `wwwroot/lib`, but the walker compared single directory names, so that entry never matched.
- `ScanService` loaded every previous scan, including every finding, only to read the latest completed score, commit, and branch.
- Quality and performance analyzers ran inside `ExtendedStaticAnalysisRule` and again inside `ProjectAnalysisPipeline`.
- A timeout and a user cancellation both ended as status `Cancelled`.
- The OpenAI client stored the bearer token on `DefaultRequestHeaders`, and the suggestions prompt included the server filesystem path. The system prompt did not tell the model to stay inside the stored findings.
- GitHub requests had no retry when the API returned a rate-limit response.
- ZIP extraction did not observe the cancellation token while copying, and `SaveZipAsync` did not enforce the upload cap while copying the stream.
- `appsettings.json` contains the JWT signing key `THIS_IS_SUPER_SECRET_KEY_123456789`. Production startup did not reject it.

## What this pass changed

- One `SourceSnapshot` per scan enumerates the tree once, skips configured directories and extensions, skips files over `MaxFileBytes`, skips binary content (NUL in the first 512 bytes), and reads the remaining text with bounded parallelism. Rules and project intelligence read that snapshot.
- Independent rules run with bounded parallelism (`Scanning:MaxDegreeOfParallelism`, default 4, clamped to 8). Progress writes to the database stay serialized because one `DbContext` is not thread-safe.
- `OwaspPatternRule` adds line-level findings for XSS, command injection, path traversal, insecure deserialization, weak cryptography, and fast password hashes. Each finding has a title, severity, path, line, snippet, description, impact, remediation, confidence, rule id, CWE, and an OWASP 2021 reference. OWASP is stored in the existing `References` field. No database migration was required.
- The three empty secret rules now return the matching subset of `SecretsAnalyzer`. The analyzer result is cached per file for the scan. Secret values stay redacted.
- The noisy debug, HTTP, Swagger, and sensitive-logging rules were narrowed to the line or file condition that actually indicates the issue.
- A scan in which every scanner throws is stored as `Failed`. A scan in which some scanners throw stays `Completed`, with the failures named in `ErrorMessage` and the stage message. One file that cannot be read is skipped.
- User cancellation stays `Cancelled`. A timeout is rewritten to `Failed` / `TimedOut`.
- Phase timings (acquisition, enumeration, read, rules, project intelligence) are stored on the analysis document.
- The previous completed scan is loaded as a header (id, score, commit, branch) instead of the full issue graph.
- OpenAI calls send the key on the request, retry once on HTTP 429 or 5xx, omit the server path, and include the stored evidence and remediation. Failures still fall back to the deterministic text.
- GitHub rate-limit responses retry up to two times, waiting on `Retry-After` when it is under 20 seconds.
- ZIP entry names containing NUL or `:` are rejected. Extraction observes cancellation. The upload copy stops at `MaxUploadBytes` and deletes the partial file.
- Production startup throws if `Jwt:Key` is shorter than 32 characters or still the development placeholder. Development logs a warning and still starts.

## Features that already existed and were left in place

Authentication and tenant filters, rate limits, health checks, scan queue, history, finding filters, comparison, status updates, JSON/CSV/PDF export, dashboard, Stripe billing hooks, and the Angular scan flow.

## Features that are still absent

- There is no "rescan this project" endpoint. A new upload or a new GitHub submission creates a new project.
- Identical GitHub archives are not cached between scans.
- Detection is pattern and entropy based. It does not build a compiler data-flow graph, and it does not prove that a finding is reachable.
- OSV dependency queries are still capped by `MaxDependencyQueries` and depend on the network. A failed query does not fail the whole scan.
- This pass did not call GitHub or OpenAI, and it did not execute a scan against the live SQL Server database. Those paths were reviewed in code and covered only where the tests do not need the external service.
