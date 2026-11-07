using System.Security.Cryptography;
using System.Text;

namespace ProdFalcon.Application.Scanning.Rules;

public static class FindingFingerprint
{
    public static string Create(string ruleId, string relativePath, int lineNumber, string title)
    {
        var normalizedPath = relativePath.Replace('\\', '/').Trim().ToLowerInvariant();
        var raw = $"{ruleId.Trim().ToLowerInvariant()}|{normalizedPath}|{lineNumber}|{title.Trim().ToLowerInvariant()}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
    }
}
