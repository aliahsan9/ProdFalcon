using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using ProdFalcon.Application.Scanning;
using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Rules;

/// <summary>
/// Enumerates source files while skipping dependencies, build output, and binaries.
/// During a scan, enumeration and reads come from the snapshot loaded once for that scan.
/// </summary>
public static class SourceTree
{
    private static readonly ConcurrentDictionary<string, Regex> SearchPatterns = new(StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<string> EnumerateSourceFiles(string projectPath, string searchPattern = "*.*")
    {
        var snapshot = SourceScanScope.Current;
        if (snapshot != null && SameRoot(snapshot.Root, projectPath))
        {
            foreach (var file in snapshot.Files)
            {
                if (MatchesPattern(file.Name, searchPattern))
                    yield return file.FullPath;
            }

            yield break;
        }

        foreach (var file in DiscoverFiles(projectPath, new ScanLimitsOptions(), CancellationToken.None).Files)
        {
            if (MatchesPattern(Path.GetFileName(file), searchPattern))
                yield return file;
        }
    }

    public static FileDiscovery DiscoverFiles(string projectPath, ScanLimitsOptions limits, CancellationToken cancellationToken)
    {
        var files = new List<string>();
        var skippedDirectories = 0;
        var truncated = false;
        if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
            return new FileDiscovery(files, skippedDirectories, truncated);

        var skippedNames = new HashSet<string>(limits.ExcludedDirectories ?? [], StringComparer.OrdinalIgnoreCase);
        var skippedExtensions = new HashSet<string>(limits.ExcludedExtensions ?? [], StringComparer.OrdinalIgnoreCase);
        var maxFiles = Math.Max(1, limits.MaxFilesScanned);
        var pending = new Stack<string>();
        pending.Push(projectPath);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateDirectories(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var child in children)
            {
                var name = Path.GetFileName(child);
                var parent = Path.GetFileName(Path.GetDirectoryName(child) ?? string.Empty);
                if (skippedNames.Contains(name) ||
                    (name.Equals("lib", StringComparison.OrdinalIgnoreCase) &&
                     parent.Equals("wwwroot", StringComparison.OrdinalIgnoreCase)))
                {
                    skippedDirectories++;
                    continue;
                }

                pending.Push(child);
            }

            IEnumerable<string> directoryFiles;
            try
            {
                directoryFiles = Directory.EnumerateFiles(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in directoryFiles)
            {
                if (skippedExtensions.Contains(Path.GetExtension(file)))
                    continue;

                files.Add(file);
                if (files.Count >= maxFiles)
                {
                    truncated = true;
                    return new FileDiscovery(files, skippedDirectories, truncated);
                }
            }
        }

        return new FileDiscovery(files, skippedDirectories, truncated);
    }

    public static Task<string?> TryReadTextAsync(string path, CancellationToken cancellationToken, int maxBytes = 1_048_576)
    {
        var snapshot = SourceScanScope.Current;
        if (snapshot != null && snapshot.ByPath.TryGetValue(path, out var record))
        {
            if (record.Length > maxBytes)
                return Task.FromResult<string?>(null);

            return Task.FromResult(record.Content);
        }

        return ReadFromDiskAsync(path, cancellationToken, maxBytes);
    }

    public static string? ReadText(string path, int maxBytes = 1_048_576)
    {
        var snapshot = SourceScanScope.Current;
        if (snapshot != null && snapshot.ByPath.TryGetValue(path, out var record))
            return record.Length > maxBytes ? null : record.Content;

        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > maxBytes)
                return null;

            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static List<ScanIssue> SecretsFor(string path, string content)
    {
        var snapshot = SourceScanScope.Current;
        return snapshot == null
            ? SecretsAnalyzer.AnalyzeFile(path, content)
            : snapshot.SecretsFor(path, content);
    }

    public static int CountLines(string content)
    {
        if (string.IsNullOrEmpty(content))
            return 0;

        var lines = 1;
        foreach (var ch in content)
        {
            if (ch == '\n')
                lines++;
        }

        return lines;
    }

    public static int LineNumberAt(string content, int index)
    {
        if (index <= 0)
            return 1;

        var line = 1;
        var end = Math.Min(index, content.Length);
        for (var i = 0; i < end; i++)
        {
            if (content[i] == '\n')
                line++;
        }

        return line;
    }

    public static bool MatchesPattern(string fileName, string pattern)
    {
        if (string.IsNullOrEmpty(pattern) || pattern is "*.*" or "*")
            return true;

        var regex = SearchPatterns.GetOrAdd(pattern, static value =>
        {
            var body = "^" + Regex.Escape(value).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return new Regex(body, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
        });
        return regex.IsMatch(fileName);
    }

    private static bool SameRoot(string left, string right)
    {
        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
            return false;
        }
    }

    private static async Task<string?> ReadFromDiskAsync(string path, CancellationToken cancellationToken, int maxBytes)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > maxBytes)
                return null;

            return await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

public readonly record struct FileDiscovery(IReadOnlyList<string> Files, int SkippedDirectories, bool Truncated);
