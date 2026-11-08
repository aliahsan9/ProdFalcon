# Production readiness

Assessment date: 2026-11-08.

ProdFalcon can queue a scan, isolate it by tenant, reject hostile ZIP paths, and finish a pattern scan of about a thousand source files in a few seconds on the machine used for `SCANNING_PERFORMANCE.md`. It is not ready to treat a scan result as a complete security assessment, and it is not ready to run in Production with the settings currently checked in.

## Ready

- The API builds in Release, and the xUnit suite reported 40 passed / 0 failed.
- Scan jobs run on a background worker with a timeout (`Scanning:ScanTimeoutMinutes`) and a cancellation token. A timeout is stored as `Failed`. A user cancel is stored as `Cancelled`. A scan is `Completed` only after scoring and save. If every rule throws, the scan is `Failed`.
- One unreadable file does not stop the scan. One failing rule does not stop the other rules.
- Upload size, entry count, extracted size, compression ratio, and path checks bound ZIP work. The worker does not execute the uploaded project.
- GitHub fetches stay on an HTTPS host allow-list.
- Secrets in findings are redacted. The OpenAI request does not include the raw secret value or the server path.
- Health endpoints `/health/live` and `/health` already exist. `/health` checks that the database accepts a connection.
- Phase timings are stored on the analysis document for a completed scan, so a slow scan can be explained without a profiler.

## Not ready

- Replace `Jwt:Key`, the OpenAI key, the GitHub webhook secret, and the Stripe secrets before any shared deployment. Production startup now refuses the placeholder JWT key. It does not create a strong key for you.
- The development `appsettings.json` still contains the placeholder JWT key. Do not deploy that file as Production configuration.
- Pattern detection misses vulnerabilities that are split across lines or hidden behind wrappers, and it can still flag safe code. Ship the results as static findings with the stored confidence, not as confirmed exploits.
- GitHub acquisition and OpenAI suggestions were not called during this audit. A bad token, a rate limit beyond the two retries, or an OpenAI outage falls back or fails the scan. Those failure paths were read, not exercised against the live services.
- This pass did not apply a new EF migration. OWASP mapping is in `References`. Confirm the existing migrations against the target SQL Server before go-live. That database was not migrated as part of this audit.
- The in-memory `ScanQueue` does not survive a process restart. A queued scan that is in flight when the process stops is not resumed.
- `ProjectStorage` still holds extracted trees on disk until the cleanup service removes them. Treat that directory as sensitive.
- The Angular references line was not verified in the browser against the new API.

## Recommended next steps

1. Move every secret to the host environment and start the API once in Production mode to confirm the JWT check passes.
2. Run one authenticated ZIP scan and one public GitHub scan against a disposable repository, and confirm status, findings, cancellation, and the PDF/JSON download.
3. Add a rescan action for an existing `ScanProject` if users need to repeat a scan without uploading again.
4. Cache a GitHub archive by commit SHA only after the download has been verified, with a size cap and a tenant boundary.
5. Keep adding fixtures when a false positive or a missed vulnerability shows up. Do not lower a rule's severity to make a noisy project look clean.
