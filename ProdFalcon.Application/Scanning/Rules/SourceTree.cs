namespace ProdFalcon.Application.Scanning.Rules;

/// <summary>
/// Enumerates source files while skipping dependencies, build output, and binaries.
/// </summary>
public static class SourceTree
{
    private static readonly HashSet<string> SkippedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", "bin", "obj", ".git", "dist", "build", "vendor", "packages",
        ".vs", "coverage", "__pycache__", "target", ".next", ".angular", ".idea", "wwwroot/lib"
    };

    private static readonly HashSet<string> SkippedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dll", ".exe", ".pdb", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".zip",
        ".nupkg", ".woff", ".woff2", ".ttf", ".eot", ".mp4", ".pdf", ".snk", ".pfx", ".so", ".dylib"
    };

    public static IEnumerable<string> EnumerateSourceFiles(string projectPath, string searchPattern = "*.*")
    {
        if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
            yield break;

        var yielded = 0;
        foreach (var file in EnumerateSafe(projectPath, searchPattern))
        {
            if (yielded >= 4_000)
                yield break;

            yielded++;
            yield return file;
        }
    }

    public static async Task<string?> TryReadTextAsync(string path, CancellationToken cancellationToken, int maxBytes = 1_048_576)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > maxBytes)
                return null;

            return await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
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

    private static IEnumerable<string> EnumerateSafe(string root, string searchPattern)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();
            IEnumerable<string> children;
            try
            {
                children = Directory.EnumerateDirectories(directory);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var child in children)
            {
                var name = Path.GetFileName(child);
                if (SkippedDirectories.Contains(name))
                    continue;

                pending.Push(child);
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory, searchPattern);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                if (SkippedExtensions.Contains(Path.GetExtension(file)))
                    continue;

                yield return file;
            }
        }
    }
}
