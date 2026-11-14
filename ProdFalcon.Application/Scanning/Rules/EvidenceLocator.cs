namespace ProdFalcon.Application.Scanning.Rules;

/// <summary>
/// Turns a match index into a file, line, and column citation. Snippets are trimmed and secrets are replaced.
/// </summary>
public static class EvidenceLocator
{
    public readonly record struct SourceLocation(int Line, int Column);

    public static SourceLocation Locate(string content, int index)
    {
        if (string.IsNullOrEmpty(content) || index <= 0)
            return new SourceLocation(1, 1);

        var end = Math.Min(index, content.Length);
        var line = 1;
        var lineStart = 0;
        for (var i = 0; i < end; i++)
        {
            if (content[i] != '\n')
                continue;

            line++;
            lineStart = i + 1;
        }

        return new SourceLocation(line, end - lineStart + 1);
    }

    public static string Format(string filePath, string content, int index, string? secret = null)
    {
        var location = Locate(content, index);
        var start = index <= 0 ? 0 : content.LastIndexOf('\n', Math.Max(0, index - 1)) + 1;
        var end = content.IndexOf('\n', Math.Min(index, content.Length));
        if (end < 0)
            end = content.Length;

        var line = content[start..end].Trim().TrimEnd('\r');
        if (!string.IsNullOrEmpty(secret))
            line = line.Replace(secret, "[redacted]", StringComparison.Ordinal);

        return FormatAt(filePath, location.Line, location.Column, line);
    }

    public static string FormatAt(string filePath, int line, int column, string snippet)
    {
        var name = string.IsNullOrWhiteSpace(filePath) ? "source" : Path.GetFileName(filePath.Replace('\\', '/'));
        var text = (snippet ?? string.Empty).Trim().Replace('\r', ' ').Replace('\n', ' ');
        if (text.Length > 180)
            text = text[..180] + "...";

        var citedColumn = Math.Max(1, column);
        var citedLine = Math.Max(1, line);
        return $"{name}:{citedLine}:{citedColumn} | {text}";
    }
}

public static class LineFilter
{
    public static bool Skip(string line, ref bool inBlockComment)
    {
        var trimmed = line.TrimStart();
        if (inBlockComment)
        {
            if (trimmed.Contains("*/", StringComparison.Ordinal))
                inBlockComment = false;
            return true;
        }

        if (trimmed.StartsWith("/*", StringComparison.Ordinal))
        {
            if (!trimmed.Contains("*/", StringComparison.Ordinal))
                inBlockComment = true;
            return true;
        }

        return trimmed.StartsWith("//", StringComparison.Ordinal) ||
               trimmed.StartsWith('#') ||
               trimmed.StartsWith('*') ||
               trimmed.StartsWith("--", StringComparison.Ordinal) ||
               trimmed.StartsWith("<!--", StringComparison.Ordinal);
    }
}
