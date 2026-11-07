# Security policy

ProdFalcon scans untrusted project archives. A vulnerability in authentication, tenant isolation, ZIP extraction, or secret storage can affect every tenant on a host.

The detailed code audit is [docs/SECURITY.md](docs/SECURITY.md). That document lists known gaps, including a development JWT signing key in configuration, unverified super-admin email matching, and extracted trees committed under `ProjectStorage/`. Those are not secrets to rediscover. A new report should describe something that document does not already cover, or a practical way the known issue is worse than stated.

## Reporting

Send a report privately to the maintainer. Use a GitHub security advisory on [github.com/aliahsan9/ProdFalcon](https://github.com/aliahsan9/ProdFalcon) if that feature is available on the repository. Otherwise contact Ali Ahsan through GitHub: [https://github.com/aliahsan9](https://github.com/aliahsan9).

Do not open a public issue for an unfixed vulnerability. Do not include live access tokens, password hashes, or customer archives in the report. Describe the route, the trust boundary, and the impact.

Include:

- The commit SHA you tested.
- The endpoint or component.
- The conditions required, such as an authenticated tenant member or an anonymous caller.
- What an attacker can read or change.
- Whether tenant data, credentials, or the host filesystem is affected.

## What happens next

The maintainer will confirm receipt when they see the report, reproduce the issue when a safe reproduction exists, and agree on a fix or an explicit documentation update if the behavior is already accepted. There is no public bug-bounty program and no paid response target in this repository.

Supported fix stream: the `master` branch of this repository. There are no versioned releases yet. See [CHANGELOG.md](CHANGELOG.md).

## Hardening expectations for operators

Until the items in the security audit are closed, do not expose a host that still uses the committed JWT key, the default super-admin email list, or a public upload endpoint without a review. Rotate any credential that has been in git.
