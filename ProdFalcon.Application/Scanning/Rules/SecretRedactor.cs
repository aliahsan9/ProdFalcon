namespace ProdFalcon.Application.Scanning.Rules;

public static class SecretRedactor
{
    public static string Describe(string category, string? secret)
    {
        var length = string.IsNullOrEmpty(secret) ? 0 : secret.Trim().Length;
        return $"{category} redacted ({length} characters). The value is not stored.";
    }

    public static bool IsPlaceholder(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        var normalized = value.Trim().Trim('"', '\'', '`');
        if (normalized.Length < 6)
            return true;

        var lower = normalized.ToLowerInvariant();
        string[] placeholders =
        [
            "password", "changeme", "change_me", "secret", "example", "placeholder",
            "your-", "your_", "xxx", "redacted", "todo", "fixme", "null", "undefined",
            "test", "sample", "dummy", "not-a-real", "insert", "replace"
        ];

        if (placeholders.Any(p => lower == p || lower.Contains(p, StringComparison.Ordinal)))
            return true;

        if (normalized.Contains("${", StringComparison.Ordinal) ||
            normalized.Contains("process.env", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("{{", StringComparison.Ordinal) ||
            normalized.All(c => c == '*' || c == 'x' || c == 'X'))
            return true;

        return false;
    }
}
