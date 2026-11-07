# Roadmap

Status is based on the source and tests in this repository, not on the previous README. "Completed" means the behavior exists and is either executed by a test or is present as a wired code path. "In progress" is not used for work that has no partial implementation. Partial behavior is called out under the item.

## Completed

- Layered solution: API, Application, Domain, Infrastructure, Shared, and Tests, targeting `net8.0`.
- JWT registration and login with BCrypt hashes, tenant creation, and membership.
- Tenant query filters and HTTP tests that reject cross-tenant scan, dashboard, and AI access.
- ZIP upload, GitHub URL submission, and a two-worker in-memory queue.
- `SafeZipExtractor` path and size checks, with a unit test for an escaping entry.
- Static rules for secrets, SQL concatenation, authorization, configuration, logging, quality, and an OSV-backed dependency rule.
- Scores, finding fingerprints, status updates, history, filters, pagination, and scan comparison.
- Project intelligence, architecture graph, and threat model persisted on the scan and returned by API routes.
- JSON, CSV, and PDF report writers.
- Repository registration, CI token hash, workflow YAML download, webhook signature check, and synchronous CI policy result.
- Local heuristic AI answers when no OpenAI key is configured, and an OpenAI call when a key is present.
- Serilog request logging, exception middleware, Development-only Swagger, local CORS, and a scan rate limit.
- EF Core migrations through repository-registration uniqueness per tenant.
- xUnit coverage of the security, intelligence, and tenant cases listed in [TESTING.md](TESTING.md).

## In progress

Nothing in the tree is marked as a half-merged feature branch. The items below are incomplete implementations that already have types and routes. They are tracked as planned work, not as active uncommitted product changes.

## Planned

Ordered by user impact, then by the damage if they stay unfinished, then by how much of the surrounding code already exists.

1. **Remove committed scan output and stop tracking it.** `ProjectStorage/ExtractedProjects/` is not the product. It is the highest-confidence repository risk because the files are already public if the remote is public. Impact is immediate. The code change is a gitignore and an index removal, followed by history cleanup only if secrets were present.
2. **Rotate and externalize the JWT signing key.** Reject startup when the key is missing or still the known placeholder. Users cannot trust a deployed API until this is done. The issuance path already reads configuration.
3. **Stop granting super-admin from an unverified email.** The flag and the admin routes exist. The missing piece is a bootstrap that does not trust `RegisterDto.Email`.
4. **Make queued scans durable.** Restart loses the channel and leaves rows at `Queued`. Users experience stuck scans. Implementation is larger: a claim column or an external queue, plus a recovery pass. Do this before running more than one API instance.
5. **Enforce the quotas the billing endpoint describes.** `Tenant.ScanLimit` and the plan matrix are stored. Upload and CI ignore them, and `scansUsed` is hardcoded. Users see limits that do not apply.
6. **Password rules and login rate limiting.** Registration stores any string. Login is unthrottled. Both are user-facing and small relative to a new scanner.
7. **Authorization policies for roles.** Viewer checks are scattered. Admin and developer are mostly equivalent on scan routes. The enum already exists.
8. **Finish or remove the Stripe stub.** A public webhook that does nothing is safe only while it stays a stub. Either verify signatures and update `UserSubscription`, or unmap the route until that work is real.
9. **Align health endpoints.** Two mappings share `/health`. Operators need one documented response.
10. **Register a Swagger bearer scheme.** The package is already referenced. Clients currently cannot authorize from the Swagger page.
11. **Expand tests to CI, auth, reports, and a queued scan with the worker enabled in a controlled fixture.** The current suite does not start workers.
12. **Invite delivery that does not return the raw token in JSON.** The token and expiry are already stored.

## Future considerations

These are not started. They are listed because the current design will hit them, not because a commitment exists to build them.

- Stream GitHub archives to disk instead of buffering the ZIP in memory.
- Per-tenant disk quotas and malware scanning before analysis.
- A separate liveness and readiness check, and a metrics endpoint for queue depth and scan duration.
- Authorization that reads membership from SQL for sensitive actions so demotion affects the next request.
- Running scanners out of process so a parser fault does not share the API's address space.
- SQL aggregation for dashboard summary instead of loading every issue.
- Using the referenced FluentValidation package for DTO rules, or removing the unused package reference.
- Removing unused package references: AutoMapper, Scalar, and ASP.NET Core Identity, unless a feature starts using them.
- Email verification, password reset, and token revocation.
- Automatic pull requests. The route exists and returns 501 on purpose. Building it means choosing a GitHub installation model and a diff the system can actually produce. The current copilot tests explicitly refuse invented upgrades. A pull request feature should keep that constraint.
- Multi-instance cleanup coordination so two hosts do not delete the same directory.
- A production migration job that does not require the web process to hold DDL rights.

## Related documents

- [SECURITY.md](SECURITY.md)
- [SYSTEM_DESIGN.md](SYSTEM_DESIGN.md)
- [ENGINEERING_DECISIONS.md](ENGINEERING_DECISIONS.md)
