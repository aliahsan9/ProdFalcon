# Scanning performance

Measured on this machine with `dotnet run --project tools/ScanBench/ScanBench.csproj -c Release --no-build`. The harness writes synthetic C# files plus a `node_modules` tree, then runs the real rule objects through `ScanRuleExecutor`. It does not upload a ZIP, call GitHub, or talk to SQL Server. Those stages were not timed.

The tree is 401 or 1,201 readable source files (the extra file is one SQL-injection sample) plus 200 or 800 JavaScript files under `node_modules`. Both runs found exactly one issue, the SQL sample. Clean files did not add findings.

## Before

Each rule walked the directory tree and read file text itself. Three secret rules returned immediately without reading. There was no OWASP pattern rule. Wall time and the sum of rule times match, which is what sequential execution looks like.

| Tree | Rules | Wall time | Sum of rule times | Findings |
| --- | ---: | ---: | ---: | ---: |
| 400 source files, 200 noise files | 15 | 6,768 ms | 6,707 ms | 1 |
| 1,200 source files, 800 noise files | 15 | 10,812 ms | 10,751 ms | 1 |

The slowest rule on the 400-file tree was `ApiKeyExposureRule` at 2,729 ms. On the 1,200-file tree it was 5,899 ms. Most of that was repeated disk reads, not the secret regex.

## After

The executor loads one snapshot (enumerate once, read once, bounded parallelism of 4) and then runs rules, including the new `OwaspPatternRule` and the three secret rules that now actually scan, against the cached text. Rule times overlap, so the wall clock is lower than the sum.

| Tree | Log rows | Wall time | Sum of rule times | Snapshot time | Findings |
| --- | ---: | ---: | ---: | ---: | ---: |
| 400 source files, 200 noise files | 17 | 1,943 ms | 2,969 ms | 1,236 ms | 1 |
| 1,200 source files, 800 noise files | 17 | 2,781 ms | 3,322 ms | 2,218 ms | 1 |

The 17 log rows are the snapshot plus 16 rules. The before measurement had 15 rules and did not do the work the new rule and the three secret rules now do. The wall clock still dropped from 6,768 ms to 1,943 ms (400-file tree) and from 10,812 ms to 2,781 ms (1,200-file tree).

On the larger tree the snapshot is most of the remaining time (2,218 ms of 2,781 ms). Further gains have to come from reading fewer or smaller files, not from running the same rules again.

## What changed in the engine

- `SourceSnapshotLoader` enumerates with the configured directory and extension exclusions, caps the list at `MaxFilesScanned`, and reads text in parallel.
- `node_modules`, `bin`, `obj`, `.git`, `dist`, `build`, `coverage`, `vendor`, and the other names in `ScanLimitsOptions.ExcludedDirectories` are not entered. `lib` is skipped only when its parent is `wwwroot`.
- Binary files and files larger than `MaxFileBytes` are not passed to the rules.
- `ScanRuleExecutor` runs rules with `MaxDegreeOfParallelism` (default 4). Database stage updates stay one at a time.
- Project intelligence, the architecture reader, and technology detection call `SourceTree.ReadText`, which returns the snapshot string when a scan is active.
- `ProjectAnalysisPipeline` no longer runs the quality and performance analyzers a second time when the rule engine already produced findings. The threat model still receives those findings.
- The previous completed scan is a single header query. It no longer loads every historical finding row during the scan.

## What was not measured

ZIP extraction time, GitHub download time, SQL persistence time, and OpenAI time. Persistence and AI happen after the rule engine and were not part of `ScanBench`. A scan that is dominated by a GitHub download or by OSV queries will not get the same ratio.

Repeat the measurement with:

```
dotnet run --project tools/ScanBench/ScanBench.csproj -c Release -- 400 200
dotnet run --project tools/ScanBench/ScanBench.csproj -c Release -- 1200 800
```
