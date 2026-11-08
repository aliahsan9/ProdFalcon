# Testing strategy

## How to run

From `ProdFalcon`:

```
dotnet test ProdFalcon.Tests/ProdFalcon.Tests.csproj -c Release
```

If `ProdFalcon.API` is already running, its output directory is locked. Point the test build somewhere else:

```
dotnet test ProdFalcon.Tests/ProdFalcon.Tests.csproj -c Release -o D:\Prod_Falcon\artifacts\test-out
```

The rule-engine benchmark is separate and does not need the API:

```
dotnet run --project tools/ScanBench/ScanBench.csproj -c Release -- 400 200
```

## Result of this pass

Release run on 2026-11-08, output directory `D:\Prod_Falcon\artifacts\test-out`:

**40 passed, 0 failed, 0 skipped.**

That run includes the existing security, intelligence, and tenant-isolation tests plus the tests added below. The benchmark numbers are in `SCANNING_PERFORMANCE.md`, not in the xUnit assertions. `ScanPerformanceTests` only checks that the executor finishes and that at least one rule succeeds.

## Tests added

`DetectionAndSafetyTests` covers:

- XSS (`Html.Raw`), command injection (`Process.Start` concatenation), path traversal (`File.ReadAllText` concatenation), `BinaryFormatter`, `MD5.Create`, and a password hashed with MD5. Each positive check looks at rule id or CWE, and the XSS check looks for the OWASP reference.
- A clean C# sample (`SHA256`, `File.ReadAllText(path)`, `Process.Start(fileName)`, `JsonSerializer.Deserialize`) produces no OWASP-pattern findings.
- A commented `BinaryFormatter` line produces no finding.
- Password, JWT, and AWS-key samples receive `SEC-PWD-001`, `SEC-JWT-001`, and `SEC-SECRET-001`. The password value is not present in `Evidence`.
- A log line that does not mention a password is ignored. A log line that interpolates `password` is reported at that line.
- An XML schema `http://` URL is ignored. A non-local `http://` URL on the next line is reported.
- A snapshot of a tree with `src` and `node_modules` contains only `App.cs`, and a second read returns the same string instance.
- ZIP entries whose names contain a NUL or `C:` are rejected before extraction.

`SecurityEngineTests` still covers GitHub URL acceptance and rejection, ZIP `..` entries, parameterized SQL versus concatenated SQL, placeholder passwords, scoring, and fingerprints.

## What the tests do not cover

- A real GitHub download, including rate-limit retries.
- An OpenAI HTTP call. The fallback path is what runs when `OpenAI:ApiKey` is empty. That was not given a new test in this pass.
- SQL Server. Tenant tests use the existing SQLite web-application factory.
- A multi-gigabyte ZIP. The size and ratio checks exist in `SafeZipExtractor` and were not re-benchmarked.
- Browser interaction. The scan-detail template now renders `references` when a finding has one. The running UI was not clicked, because the API process that was already listening was not rebuilt.
