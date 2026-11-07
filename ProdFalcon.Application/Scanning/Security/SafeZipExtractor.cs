using System.IO.Compression;

namespace ProdFalcon.Application.Scanning.Security;

public sealed class ZipSafetyLimits
{
    public long MaxExtractedBytes { get; init; } = 500L * 1024 * 1024;

    public int MaxEntries { get; init; } = 8_000;

    public int MaxCompressionRatio { get; init; } = 100;
}

public static class SafeZipExtractor
{
    public static void Validate(string zipPath, ZipSafetyLimits limits)
    {
        using var archive = Open(zipPath);
        Inspect(archive, limits);
    }

    public static void Extract(string zipPath, string destination, ZipSafetyLimits limits)
    {
        Directory.CreateDirectory(destination);
        var root = Root(destination);

        using var archive = Open(zipPath);
        Inspect(archive, limits);

        long totalUncompressed = 0;
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name) && entry.FullName.EndsWith('/'))
                continue;

            var target = ResolveEntryPath(root, entry.FullName);
            var directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (entry.Length == 0 && string.IsNullOrEmpty(entry.Name))
                continue;

            using var input = entry.Open();
            using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None);
            var buffer = new byte[81920];
            long written = 0;
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                written += read;
                totalUncompressed += read;
                if (written > limits.MaxExtractedBytes || totalUncompressed > limits.MaxExtractedBytes)
                    throw new InvalidDataException("Archive exceeds the extracted size limit.");

                output.Write(buffer, 0, read);
            }
        }
    }

    private static ZipArchive Open(string zipPath)
    {
        var stream = File.OpenRead(zipPath);
        try
        {
            return new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    private static void Inspect(ZipArchive archive, ZipSafetyLimits limits)
    {
        if (archive.Entries.Count == 0)
            throw new InvalidDataException("The archive is empty.");

        if (archive.Entries.Count > limits.MaxEntries)
            throw new InvalidDataException("The archive contains too many entries.");

        long uncompressed = 0;
        long compressed = 0;
        foreach (var entry in archive.Entries)
        {
            ResolveEntryPath(Root(Path.Combine(Path.GetTempPath(), "pf-zip-check")), entry.FullName);
            uncompressed += Math.Max(0, entry.Length);
            compressed += Math.Max(1, entry.CompressedLength);

            if (uncompressed > limits.MaxExtractedBytes)
                throw new InvalidDataException("Archive exceeds the extracted size limit.");

            if (entry.Length > 1_048_576 &&
                entry.CompressedLength > 0 &&
                entry.Length / Math.Max(1, entry.CompressedLength) > limits.MaxCompressionRatio)
                throw new InvalidDataException("Archive compression ratio exceeds the safety limit.");
        }

        if (uncompressed > 1_048_576 && compressed > 0 && uncompressed / compressed > limits.MaxCompressionRatio)
            throw new InvalidDataException("Archive compression ratio exceeds the safety limit.");
    }

    private static string Root(string path)
    {
        var full = Path.GetFullPath(path);
        return full.EndsWith(Path.DirectorySeparatorChar) ? full : full + Path.DirectorySeparatorChar;
    }

    private static string ResolveEntryPath(string rootWithSeparator, string entryName)
    {
        var normalized = entryName.Replace('\\', '/').TrimStart('/');
        if (normalized.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(entryName))
            throw new InvalidDataException("Archive contains an unsafe path.");

        var combined = Path.GetFullPath(Path.Combine(rootWithSeparator, normalized.Replace('/', Path.DirectorySeparatorChar)));
        if (!combined.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Archive contains an unsafe path.");

        return combined;
    }
}
