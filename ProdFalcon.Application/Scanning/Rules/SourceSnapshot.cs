using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using ProdFalcon.Application.Scanning;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

public sealed class SourceFileRecord
{
    public required string FullPath { get; init; }

    public required string Name { get; init; }

    public required string Extension { get; init; }

    public string? Content { get; init; }

    public long Length { get; init; }
}

public sealed class SourceSnapshot
{
    private readonly ConcurrentDictionary<string, List<ScanIssue>> _secrets = new(StringComparer.OrdinalIgnoreCase);

    public required string Root { get; init; }

    public required IReadOnlyList<SourceFileRecord> Files { get; init; }

    public required IReadOnlyDictionary<string, SourceFileRecord> ByPath { get; init; }

    public int EnumerationMs { get; init; }

    public int ReadMs { get; init; }

    public int SkippedDirectories { get; init; }

    public int SkippedLarge { get; init; }

    public int SkippedBinary { get; init; }

    public bool Truncated { get; init; }

    public int MaxDegreeOfParallelism { get; init; } = 4;

    public List<ScanIssue> SecretsFor(string path, string content) =>
        _secrets.GetOrAdd(path, _ => SecretsAnalyzer.AnalyzeFile(path, content));
}

public static class SourceScanScope
{
    private static readonly AsyncLocal<SourceSnapshot?> CurrentSnapshot = new();

    public static SourceSnapshot? Current => CurrentSnapshot.Value;

    public static IDisposable Push(SourceSnapshot snapshot)
    {
        var prior = CurrentSnapshot.Value;
        CurrentSnapshot.Value = snapshot;
        return new Popper(prior);
    }

    private sealed class Popper(SourceSnapshot? prior) : IDisposable
    {
        public void Dispose() => CurrentSnapshot.Value = prior;
    }
}

public static class SourceSnapshotLoader
{
    public static async Task<SourceSnapshot> LoadAsync(
        string projectPath,
        ScanLimitsOptions limits,
        CancellationToken cancellationToken)
    {
        var enumeration = Stopwatch.StartNew();
        var discovered = SourceTree.DiscoverFiles(projectPath, limits, cancellationToken);
        enumeration.Stop();

        var records = new SourceFileRecord[discovered.Files.Count];
        var skippedLarge = 0;
        var skippedBinary = 0;
        var degree = Math.Clamp(limits.MaxDegreeOfParallelism, 1, 8);
        var reading = Stopwatch.StartNew();

        await Parallel.ForEachAsync(
            Enumerable.Range(0, discovered.Files.Count),
            new ParallelOptions { MaxDegreeOfParallelism = degree, CancellationToken = cancellationToken },
            async (index, token) =>
            {
                var record = await ReadAsync(discovered.Files[index], limits.MaxFileBytes, token);
                if (record.Content == null && record.Length > limits.MaxFileBytes)
                    Interlocked.Increment(ref skippedLarge);
                else if (record.Content == null && record.Length > 0)
                    Interlocked.Increment(ref skippedBinary);

                records[index] = record;
            });

        reading.Stop();

        var readable = new List<SourceFileRecord>(records.Length);
        var byPath = new Dictionary<string, SourceFileRecord>(records.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var record in records)
        {
            if (record.Content == null)
                continue;

            readable.Add(record);
            byPath[record.FullPath] = record;
        }

        return new SourceSnapshot
        {
            Root = Path.GetFullPath(projectPath),
            Files = readable,
            ByPath = byPath,
            EnumerationMs = (int)enumeration.ElapsedMilliseconds,
            ReadMs = (int)reading.ElapsedMilliseconds,
            SkippedDirectories = discovered.SkippedDirectories,
            SkippedLarge = skippedLarge,
            SkippedBinary = skippedBinary,
            Truncated = discovered.Truncated,
            MaxDegreeOfParallelism = degree
        };
    }

    private static async Task<SourceFileRecord> ReadAsync(string path, int maxBytes, CancellationToken cancellationToken)
    {
        var name = Path.GetFileName(path);
        var extension = Path.GetExtension(path);
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length == 0 || info.Length > maxBytes)
            {
                return new SourceFileRecord
                {
                    FullPath = path,
                    Name = name,
                    Extension = extension,
                    Content = null,
                    Length = info.Exists ? info.Length : 0
                };
            }

            var length = (int)info.Length;
            var buffer = new byte[length];
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var offset = 0;
            while (offset < length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);
                if (read == 0)
                    break;

                offset += read;
            }

            if (LooksBinary(buffer.AsSpan(0, offset)))
            {
                return new SourceFileRecord
                {
                    FullPath = path,
                    Name = name,
                    Extension = extension,
                    Content = null,
                    Length = info.Length
                };
            }

            return new SourceFileRecord
            {
                FullPath = path,
                Name = name,
                Extension = extension,
                Content = Encoding.UTF8.GetString(buffer, 0, offset),
                Length = info.Length
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new SourceFileRecord
            {
                FullPath = path,
                Name = name,
                Extension = extension,
                Content = null,
                Length = 0
            };
        }
    }

    private static bool LooksBinary(ReadOnlySpan<byte> data)
    {
        var sample = Math.Min(data.Length, 512);
        for (var i = 0; i < sample; i++)
        {
            if (data[i] == 0)
                return true;
        }

        return false;
    }
}
