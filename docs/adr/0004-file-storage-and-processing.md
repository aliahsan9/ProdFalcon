# ADR 0004: Tenant-scoped disk storage and a bounded ZIP extractor

- **Status:** Documented architectural decision.
- **Date:** 2026-11-07

## Context

Scans start from a ZIP the caller uploaded or from a GitHub zipball. Those files are untrusted. The API must not write outside a chosen directory, must not expand an archive without a size ceiling, and must keep one tenant's files out of another tenant's directory.

Historical extracts exist under `ProjectStorage/ExtractedProjects/` and are tracked by git. The current writer does not use that folder unless it is configured as `Storage:RootPath`.

## Decision

Store each project at `{StorageRoot}/{tenantId:N}/{projectId:N}/`. Write `upload.zip`, then extract into `extracted/` through `SafeZipExtractor`.

Reject empty archives, too many entries, declared or written uncompressed bytes above the configured maximum, unsafe paths, and compression ratios above the configured limit when the size thresholds in the extractor are met.

Queue interactive scans. Run CI scans inside the HTTP request so the caller can receive a policy result. Download GitHub archives with host allow-listing and a byte cap, then use the same extractor.

Delete project directories older than two days from a hosted service. Keep database rows.

Default `StorageRoot` to `ProdFalcon_Storage` under the solution path heuristic when `Storage:RootPath` is empty. That directory is gitignored.

## Alternatives Considered

- `ZipFile.ExtractToDirectory`. It does not apply this path or byte policy.
- Extracting during the upload request for every client. The upload would hold the connection for the whole scan. CI already makes that trade to return a status code. Interactive clients poll instead.
- Object storage. Not implemented. A single disk matches one API process. It will not match multiple hosts without a shared volume or a new store.

## Consequences

Limits are real and tested for path escape. They are not a malware boundary. Two workers can still write up to the configured extract size at the same time. GitHub downloads are buffered in memory up to the upload cap before they hit disk. Cleanup can remove files while scan rows still point at them. Re-extract works only when the ZIP is still there.

Committed `ProjectStorage` content is an operational mistake relative to this decision. The decision for new files is the gitignored root. Removing the old tree is follow-up work.

## Trade-offs

Local disk is simple and fast to inspect. It couples the API identity to every tenant's source and it disappears when the machine does. In-memory queueing avoids a new infrastructure component and loses work on restart.

See [../SYSTEM_DESIGN.md](../SYSTEM_DESIGN.md) and [../SECURITY.md](../SECURITY.md).
