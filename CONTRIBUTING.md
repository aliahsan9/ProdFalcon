# Contributing

Thank you for considering a change to ProdFalcon. The useful contributions are accurate code, tests for the behavior you touch, and documentation that matches the source.

Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) and [docs/SECURITY.md](docs/SECURITY.md) before changing scan, auth, or storage code.

## Branch naming

Use a short branch off `master`:

- `fix/<area>-<summary>` for defects
- `feat/<area>-<summary>` for behavior
- `docs/<summary>` for documentation only

Example: `fix/zip-ratio-small-entries`.

Do not commit directly to `master` if you are opening a pull request.

## Commits

Write a sentence that says why the change exists. The subject can be imperative and under about 72 characters.

```text
Reject ZIP entries whose declared size understates the bytes written.

The extractor already capped total output. This records the reason in the
error path the upload action returns to the client.
```

Keep unrelated edits out of the commit. Do not commit `bin/`, `obj/`, `ProdFalcon_Storage/`, user secrets, or a real JWT key, API key, or connection password.

## Pull requests

Use the pull request template. A reviewer should be able to tell:

- What changed.
- Why it changed.
- How you tested it.
- Whether docs or the security notes need an update.

A documentation-only change does not need a new test. A behavior change does, unless you explain why the existing suite cannot see it. Run:

```bash
dotnet test ProdFalcon.Tests/ProdFalcon.Tests.csproj
```

If you change a controller, a DTO, or a status code, update [docs/API_DOCUMENTATION.md](docs/API_DOCUMENTATION.md). If you change a table or a filter, update [docs/DATABASE_DESIGN.md](docs/DATABASE_DESIGN.md). If you discover a security gap, update [docs/SECURITY.md](docs/SECURITY.md) or describe it in the pull request without pasting secrets.

## Coding standards

- Target `net8.0`. Nullable reference types are enabled.
- Follow `.editorconfig`.
- Register new scan rules in `ApplicationServiceRegistration` or, when the rule needs an HTTP client, next to `DependencyVulnerabilityRule`.
- Depend on application interfaces from use-case code. Do not add a new EF query in a controller.
- Tenant-owned entities need `TenantId` and must remain covered by the global query filter, or the pull request must explain why they are not.
- Archive writes go through `SafeZipExtractor` and `ProjectStorageService`.
- Do not log passwords, JWT signing keys, GitHub access tokens, or CI tokens.
- Do not invent a finding, a score, or a pull request URL. The product prefers an explicit gap over a fabricated result. `POST /api/github/auto-fix/{id}` returns 501 for that reason.

`FluentValidation`, `AutoMapper`, Scalar, and ASP.NET Core Identity are referenced and unused. Using one of them means registering it and documenting it. Leaving a new unused package is not an improvement.

## Tests

- Unit-style facts belong beside the existing engine tests when they do not need HTTP.
- Tenant and auth regressions belong in the web factory. The factory removes hosted services and uses SQLite. If your fix is inside the worker, say so and add a test that does not flake on the shared in-memory database.
- SQL Server migration changes still need a migration file. `dotnet test` will not apply it.

## Documentation

Write from the code. Mark unimplemented behavior as planned. Link to an existing document instead of copying a long explanation into a second file.

## License

By contributing, you agree that your contribution is licensed under the MIT License in [LICENSE.txt](LICENSE.txt).
