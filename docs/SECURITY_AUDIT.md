# ProdFalcon security audit

Audit date: 2026-11-08. ProdFalcon reads untrusted source trees. It must not execute that source, and it must not let an archive or a repository URL escape the scan sandbox.

## Controls that were already present

- JWT bearer authentication with issuer, audience, lifetime, and signing-key validation.
- Tenant query filters on scan projects, results, and findings. `TenantIsolationTests` still pass.
- Viewer role cannot upload or change finding status.
- Scan and auth endpoints are rate limited.
- GitHub URLs must be `https` on `github.com`, must not embed userinfo, and must have a valid owner and repository. Download redirects are limited to `api.github.com`, `codeload.github.com`, `github.com`, and `objects.githubusercontent.com`.
- A GitHub access token is kept on the in-memory queue item and is not written to `ScanProject`.
- `SafeZipExtractor` rejects `..`, rooted entry names, too many entries, an excessive declared compression ratio, and an extracted size above the limit. Extraction writes only under the destination root.
- The scanner does not start a process with uploaded code.
- Secret findings redact the matched value.
- Unhandled exceptions return a generic message outside Development. The trace id is logged.

## Issues addressed in this pass

| Issue | Why it mattered | Change |
| --- | --- | --- |
| Development JWT key in `appsettings.json` | Anyone who can reach a Production host using that file can forge tokens. | Production refuses to start when the key is the known placeholder or shorter than 32 characters. Development still starts and logs a warning. The key was not rotated in this file, because local tokens would stop validating. |
| OpenAI key on the shared client headers | A reused `HttpClient` can keep `Authorization` across calls, and a logging handler would see it. | The bearer token is set on each request. It is not written to the log statements added here. |
| Suggestions prompt included `ProjectPath` | The model received the server's storage path. | The prompt now carries severity, rule id, relative path, line, redacted evidence, and the stored remediation. The system prompt tells the model not to invent findings or repeat secrets. |
| ZIP entry names with NUL or `:` | On Windows a colon can denote an alternate data stream or a drive, and a NUL can truncate a path. | `SafeZipExtractor` rejects those names before creating a file. |
| Upload stream not capped while copying | `Content-Length` can disagree with the bytes that follow. | `SaveZipAsync` stops at `MaxUploadBytes` and deletes the partial file. |
| Extraction ignored cancellation | A cancelled or timed-out scan could keep inflating a ZIP. | The extract loop checks the cancellation token. |
| GitHub 403/429 | A rate limit failed the scan immediately. | Up to two retries, using `Retry-After` when it is shorter than 20 seconds. The token is not logged. |
| Timeout stored as `Cancelled` | Operators could not tell a user cancel from resource exhaustion. | Timeout becomes `Failed` with stage `TimedOut`. User cancel stays `Cancelled`. |

## Issues that remain

- `appsettings.json` still contains the development JWT placeholder, an empty OpenAI key, and an empty Stripe secret. Production must supply those from the environment or a secret store. This pass did not move the development key out of the file.
- ZIP compression-ratio checks use the sizes declared in the archive headers, then count the bytes actually written. A hostile archive can still force the scanner to read up to `MaxExtractedBytes`. That cap is the bound.
- Entry names containing `:` are rejected even when the colon would have been a legal character on Linux. The supported host for extraction in this codebase is Windows.
- Pattern rules can miss a vulnerability that is split across lines, and they can still flag a safe concatenation. Confidence on the new rules is `Medium` or `Low` where the pattern cannot see the data source. No finding is a proof of exploitability.
- `SensitiveLoggingRule` looks for a log call and a sensitive word on the same line. It can still flag a log message that merely mentions the word "secret".
- Dependency advisory lookups send package names to `api.osv.dev`. They do not send source code. Failures are logged and skipped.
- The Angular client now shows `references` on an open finding. It was not exercised in a browser against a rebuilt API, because the already-running API process was left in place and still has the previous binaries.
- This pass did not run a dependency vulnerability scanner (`dotnet list package --vulnerable` or npm audit) against ProdFalcon's own packages.

## Verification

`DetectionAndSafetyTests` rejects ZIP entries whose names contain NUL or a drive-style colon, checks that secret values are not copied into evidence, and checks the new vulnerability fixtures. `SecurityEngineTests` still rejects non-GitHub and credentialed repository URLs and still rejects a `..` ZIP entry. `TenantIsolationTests` passed in the same run. Live GitHub downloads and live OpenAI calls were not executed.
