# API documentation

Base URL in the local `http` profile: `http://localhost:5014`.

Controllers use attribute routes. Unless a section says otherwise, the success body is:

```json
{
  "success": true,
  "message": "Success",
  "data": {},
  "traceId": null
}
```

`message` varies. `data` is omitted from the table below when the interesting part is its shape. Failures returned by actions use the same wrapper with `success: false` and `data` null. Failures thrown into `ExceptionHandlingMiddleware` use:

```json
{
  "success": false,
  "message": "An unexpected error occurred.",
  "traceId": "00-...",
  "errors": null
}
```

In Development, an unexpected 500 may include `errors.detail` with the exception message. Do not rely on that field.

Auth actions return a different error shape: `{ "success": false, "message": "..." }` without `traceId`. Login and register success bodies are the `AuthResponseDto` itself, not wrapped in `ApiResponse`.

## Authentication header

Protected routes expect:

```http
Authorization: Bearer <jwt>
```

The JWT is the `token` property from register or login. Lifetime is one day. Issuer and audience come from `Jwt:Issuer` and `Jwt:Audience` (`ProdFalcon` and `ProdFalconUsers` in the checked-in defaults).

Missing or invalid tokens receive the framework's 401 response. The API does not add a custom bearer challenge body.

Swagger UI at `/swagger` is available in Development only. It does not have an Authorize control, because `AddSwaggerGen` registers no security definition. Send the header from a client that can set it.

## Status codes used by this API

| Code | When |
| --- | --- |
| 200 | Success, including a queued scan |
| 202 | GitHub webhook accepted a scan |
| 400 | Validation failure, invalid ZIP, invalid status, finished scan cannot be cancelled |
| 401 | Bad credentials, bad webhook signature, bad CI token, middleware `UnauthorizedAccessException` |
| 403 | Viewer blocked from a write, or a non-super-admin calling `/api/admin` |
| 404 | Scan, finding, or invite not found |
| 409 | Email already registered |
| 422 | CI policy failed. The body is still `success: true` with `data.passed: false` |
| 402 | `/api/ai/*` when the tier is Free, and the environment is not Development or Testing |
| 429 | `scans` rate limit exceeded on upload or GitHub scan submit |
| 500 | Unhandled exception |
| 501 | GitHub auto-fix |
| 503 | GitHub webhook secret is not configured, or the database health handler cannot connect |

## Pagination and filtering

Implemented on:

- `GET /api/scan/history` with `page` (default 1) and `pageSize` (default 20, clamped to 1–100). Optional `projectId`.
- `GET /api/scan/findings` with `page` (default 1) and `pageSize` (default 25, clamped to 1–100).

Findings filters:

| Query | Behavior |
| --- | --- |
| `severity` | Exact match |
| `status` | Exact match |
| `category` | Exact match |
| `scope` | `quality` keeps quality, reliability, and performance findings. `security` excludes them. Empty returns all |
| `projectId` | Findings whose scan belongs to that project |
| `q` | Contains match on title, file path, rule name, or category |

Paged `data`:

```json
{
  "items": [],
  "page": 1,
  "pageSize": 20,
  "total": 0
}
```

Other list endpoints return a full array for the current tenant.

## Auth

### POST /api/auth/register

Anonymous. Creates a user, a tenant, an owner membership, and a free subscription.

Request:

```json
{
  "fullName": "Ada Lovelace",
  "email": "ada@example.com",
  "password": "a-password-the-server-does-not-score",
  "organizationName": "Analytical Engines"
}
```

`organizationName` is optional. When it is missing, the tenant name is `"{FullName}'s Workspace"`.

There is no minimum password length and no email verification.

Success: `200` and `AuthResponseDto`.

```json
{
  "token": "<jwt>",
  "email": "ada@example.com",
  "fullName": "Ada Lovelace",
  "tenantId": "00000000-0000-0000-0000-000000000000",
  "organization": "Analytical Engines",
  "role": "Owner",
  "plan": "Free",
  "isSuperAdmin": false
}
```

Errors: `409` if the email already exists.

### POST /api/auth/login

Anonymous.

```json
{
  "email": "ada@example.com",
  "password": "a-password-the-server-does-not-score"
}
```

Success: the same `AuthResponseDto` shape. The membership chosen is the active membership on an active, non-deleted tenant, ordered by role descending and then join time.

Errors: `401` with `Invalid credentials.` or `No active organization membership found.` A suspended tenant message exists in `AuthService` after a membership was already required to be active, so the suspended path is effectively unreachable for the query as written. Suspended users fail the active-tenant filter first.

## Scans

All routes below require a user JWT. Rate limit policy `scans` applies only to `POST /api/scan/upload` and `POST /api/scan/github`.

### POST /api/scan/upload

`multipart/form-data`.

| Field | Required | Meaning |
| --- | --- | --- |
| `file` | Yes | `.zip` archive, not larger than `Scanning:MaxUploadBytes` |
| `configuration` | No | JSON string of `ScanModuleSelection`. Omitted means `{ "fullAudit": true }` |

Viewers receive `403`. Invalid ZIP content receives `400` and the saved directory is deleted.

Success `data`:

```json
{
  "projectId": "11111111-1111-1111-1111-111111111111",
  "storageRoot": "",
  "uploadedZip": "<tenantId>/<projectId>/upload.zip",
  "extractedProject": "",
  "scan": {
    "projectId": "11111111-1111-1111-1111-111111111111",
    "scanResultId": 1,
    "status": "Queued",
    "stage": "Queued",
    "stageMessage": "Waiting for a scan worker.",
    "sourceType": "Zip",
    "issues": []
  }
}
```

`storageRoot` and `extractedProject` stay empty on this response. Extraction happens on the worker. `uploadedZip` is a relative display path, not a download URL.

### POST /api/scan/github

```json
{
  "repositoryUrl": "https://github.com/dotnet/runtime",
  "branch": "main",
  "accessToken": null,
  "configuration": {
    "fullAudit": true,
    "secrets": true,
    "dependencies": true,
    "staticAnalysis": true,
    "sqlInjection": true,
    "authentication": true,
    "configuration": true,
    "codeQuality": true
  }
}
```

`repositoryUrl` must parse as a GitHub repository URL. `branch` is optional and must pass `GitHubUrlParser.IsValidBranch` when supplied. `accessToken` is optional and is kept on the in-memory work item for the download. It is not stored as a scan column.

Viewers receive `403`. An unparsable URL receives `400`. A missing repository fails later, on the worker, as a `Failed` scan.

Success matches the upload response with `sourceType` `GitHub` and an empty `uploadedZip`.

### GET /api/scan/{scanResultId}

Returns `ScanResultDto` for a scan in the caller's tenant: scores, stage, technologies, repository fields, and issue summaries. `404` if the id is unknown to this tenant.

Issue summary fields include `id`, `title`, `severity`, `filePath`, `ruleName`, `ruleId`, `category`, `description`, `lineNumber`, `confidence`, `cwe`, `cve`, `evidence`, `impact`, `remediation`, `secureExample`, `status`, `detectionMethod`, `references`, `notes`, and `fixSuggestion`.

### GET /api/scan/history

See pagination. Items are `ScanHistoryItemDto` values ordered by `createdAt` descending.

### GET /api/scan/findings

See filtering. Items are issue summaries ordered by id descending.

### GET /api/scan/compare?baselineId=1&currentId=2

Both ids must exist in the tenant. `404` with the missing side named in `message`. `data` contains `baselineId`, `currentId`, `baselineScore`, `currentScore`, `scoreChange`, `introduced`, `resolved`, and `unchanged`.

### PATCH /api/scan/findings/{issueId}

Viewers receive `403`.

```json
{
  "status": "Acknowledged",
  "notes": "Reviewed in sprint planning."
}
```

Allowed `status` values: `Open`, `Detected`, `Acknowledged`, `InProgress`, `Resolved`, `AcceptedRisk`. `Verified` returns `400`. Unknown statuses return `400`. Unknown ids return `404`.

The update writes the issue, a tracked finding when a fingerprint exists, and a history row.

### POST /api/scan/{scanResultId}/cancel

`404` if missing. `400` if status is `Completed`, `Failed`, or `Cancelled`. Otherwise signals the queue and sets the row to `Cancelled`.

```json
{
  "scanResultId": 1,
  "status": "Cancelled"
}
```

### GET /api/scan/{scanResultId}/report?format=json

`format` is `json` (default), `csv`, or `pdf`. The response is a file download, not an `ApiResponse`. Names use the project name or `scan-{id}`.

- JSON: `application/json`, includes the scoring notice and findings.
- CSV: `text/csv`.
- PDF: `application/pdf`.

`404` if the scan does not exist.

### GET /api/scan/{scanResultId}/intelligence

`data` is the `ProjectIntelligenceProfile` stored on the scan. `404` if the scan is missing or has no analysis document.

### GET /api/scan/{scanResultId}/architecture

`data` is the `ArchitectureGraph` from that document.

### GET /api/scan/{scanResultId}/threats

`data` is the `ThreatModelDocument` from that document.

### GET /api/scan/{scanResultId}/conversation

`data` is an array of `{ role, content, createdAt }` for copilot messages on that scan. `404` if the scan does not exist. An empty array is a successful scan with no messages.

### GET /api/scan/scoring

Anonymous is not allowed, because the controller is `[Authorize]`. `data.methodology` is the fixed scoring explanation from `SecurityScoreMethodology.Summary`.

## Dashboard and projects

Require a user JWT. Results are limited to the caller's tenant by the query filter.

### GET /api/dashboard/summary

`DashboardSummaryDto`. Counts and averages come from loaded scans. `documentationScore`, `testCoverageScore`, and `bugRiskScore` are `-1`. `aiRecommendations` is a local list built from averages, not an OpenAI call.

### GET /api/dashboard/projects

Same payload as `GET /api/projects`.

### GET /api/dashboard/trends

Array of `{ date, averageScore, scanCount, issueCount }`.

### GET /api/projects

Array of `ProjectDashboardDto`: identity, file name, status, latest scores, scan count, issue count, trend string, source type, repository URL, and branch.

## AI

Require a user JWT. Outside Development and Testing, a Free plan receives `402` before the action runs. In Development the middleware does not apply that check.

### POST /api/ai/suggestions

```json
{ "scanResultId": 1 }
```

`400` if `scanResultId` is less than or equal to zero. `404` if the scan is not in the tenant.

`data`:

```json
{
  "scanResultId": 1,
  "fixSuggestions": [],
  "refactoringRecommendations": [],
  "securityImprovements": [],
  "productionReadinessAdvice": [],
  "summary": ""
}
```

When `OpenAI:ApiKey` is empty or the HTTP call fails, the lists are built from stored findings. The response does not include a flag that distinguishes those two sources on this endpoint.

### POST /api/ai/ask

```json
{
  "scanResultId": 1,
  "findingId": null,
  "question": "Which finding should be fixed first?"
}
```

`400` if the scan id is missing or the question is blank. `404` if the scan is missing.

```json
{
  "scanResultId": 1,
  "answer": "",
  "usedExternalModel": false,
  "grounding": "Stored scan findings"
}
```

`usedExternalModel` is false when the local copilot answers.

## Tenants

### GET /api/tenants/members

User JWT. Array of members for the current tenant: id, user id, email, full name, role, status, invited time, joined time.

### POST /api/tenants/members/invite

User JWT. Owners and admins can invite. Other roles receive `403` from the service's `UnauthorizedAccessException`, which this action catches.

```json
{
  "email": "grace@example.com",
  "fullName": "Grace Hopper",
  "role": "Developer"
}
```

`role` is the `TenantRole` enum: `Viewer` 0, `Developer` 1, `SecurityReviewer` 2, `Admin` 3, `Owner` 4. JSON may send the name or the number. `TenantMemberService` stores `Owner` as `Admin`, so an invite cannot create a second owner.

The caller must be an owner, an admin, or a super-admin. Other roles receive `403`. If the email is already a non-removed member, the service throws `ConflictException`, which the middleware returns as `409`. A new email creates an `AppUser` with a random BCrypt hash that the inviter does not see. Accept replaces that hash only when `password` is non-empty. If the new user omits `password`, the random hash remains and login with a chosen password will fail. The invite expires after seven days. An expired invite returns `401`.

Success `data` includes `email`, `inviteToken`, `expiresAt`, and `role`. The token is the credential for accept. No email is sent.

### POST /api/tenants/invites/accept

Anonymous.

```json
{
  "token": "<inviteToken>",
  "password": "required-when-the-email-has-no-user-yet",
  "fullName": "Grace Hopper"
}
```

`404` if the token is unknown. `401` if the invite cannot be accepted, including a bad password path thrown as `UnauthorizedAccessException`.

## Admin

Require a user JWT whose `is_super_admin` claim is true. Otherwise `403` with `SuperAdmin access required.`

| Method | Route | Body | Result |
| --- | --- | --- | --- |
| GET | `/api/admin/tenants` | | Array of `TenantAdminDto` |
| POST | `/api/admin/tenants/{tenantId}/suspend` | | `{ tenantId }` |
| DELETE | `/api/admin/tenants/{tenantId}` | | Soft-deletes the tenant |
| PUT | `/api/admin/tenants/{tenantId}/plan` | `{ "plan": "Pro" }` | Updates plan, scan limit, and storage limit |

`TenantAdminDto` includes id, name, slug, status, plan, storage figures, scan limit, AI usage, member count, project count, scan count, created time, and owner user id.

## Billing and Stripe

### GET /api/billing/subscription

User JWT. Returns the current plan, whether it is anything other than Free, organization name, tenant id, and a static limit object.

Free: 5 scans, AI off, CI off. Pro: 100 scans, AI on, CI on. Enterprise: `scansPerMonth` `-1`, AI on, CI on.

`usage.scansUsed` is always `0`. The scan endpoints do not read this limit.

### POST /api/stripe/checkout

User JWT.

```json
{ "tier": "Pro" }
```

`data.checkoutUrl` is `Stripe:CheckoutSuccessUrl` plus `tenantId` and `tier` query values. No Stripe API call is made.

### POST /api/stripe/webhook

Anonymous. Reads the body and the `Stripe-Signature` header, logs whether a signature header was present, and returns an empty `200`. Subscriptions are not updated.

## GitHub integration

### GET /api/github/registrations

User JWT. Lists registrations for the tenant without the token hash. Fields: `id`, `owner`, `name`, `branch`, `tokenPrefix`, policy flags, `createdAt`.

### POST /api/github/registrations

User JWT. Viewers receive `403`.

```json
{
  "repositoryUrl": "https://github.com/aliahsan9/ProdFalcon",
  "branch": "master",
  "failOnNewCritical": true,
  "warnOnHigh": true,
  "allowAcceptedRisk": true,
  "preventScoreRegression": false
}
```

Success `data` includes `id`, `owner`, `name`, the raw `token` once, a `workflow` YAML string, and a message telling the caller to store the token as `PRODFALCON_TOKEN`.

### GET /api/github/workflow

User JWT. Downloads `prodfalcon.yml` as `text/yaml`. The document posts a ZIP to this API's `/api/github/ci`.

### POST /api/github/webhook

Anonymous. Requires `GitHub:WebhookSecret`. Sends `503` when that value is empty.

Header: `X-Hub-Signature-256`. The body is verified with HMAC. A bad signature is `401`. Actions other than `opened`, `synchronize`, and `reopened` return `200` with `message` `Event ignored.` An unknown repository returns `200` with a not-registered message. A matching registration queues a scan and returns `202` with `scanResultId` and `status` `Queued`.

The webhook does not use a user JWT. It sets the tenant from the registration.

### POST /api/github/ci

Anonymous at the JWT layer. Requires `Authorization: Bearer <registration token>`.

`multipart/form-data`:

| Field | Required | Meaning |
| --- | --- | --- |
| `file` | Yes | Repository ZIP |
| `branch` | No | Overrides the registered branch |
| `commitSha` | No | Stored on the project and scan |
| `changedFiles` | No | Comma-separated paths used to filter the returned findings |

Invalid token: `401`. Invalid ZIP: `400`. The scan runs in the request. Policy failure: `422`. Policy pass: `200`.

```json
{
  "passed": true,
  "scanResultId": 10,
  "overallScore": 80,
  "newCritical": 0,
  "newHigh": 0,
  "messages": [],
  "changedFindings": []
}
```

### POST /api/github/auto-fix/{scanResultId}

User JWT. Always `501`.

```json
{
  "success": false,
  "message": "Automatic pull requests are not implemented. ProdFalcon does not open a pull request or invent a URL.",
  "data": null,
  "traceId": null
}
```

## Health

`GET /health` is anonymous. Two endpoints are mapped to that path. The custom handler returns `200` `{ "status": "Healthy" }` when `CanConnectAsync` is true and `503` otherwise. Confirm the live response, because the duplicate `MapHealthChecks` registration is also present.

## Related documents

- [SECURITY.md](SECURITY.md)
- [SYSTEM_DESIGN.md](SYSTEM_DESIGN.md)
