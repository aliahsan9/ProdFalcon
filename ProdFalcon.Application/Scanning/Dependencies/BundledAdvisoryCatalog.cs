namespace ProdFalcon.Application.Scanning.Dependencies;

public sealed record BundledAdvisoryMatch(string Cve, string Cwe, string Severity, string Summary, string FixedVersion);

/// <summary>
/// A small reviewed snapshot of published advisories. It is used when a live OSV response is missing
/// and as a second source when OSV is reachable. It is not a vulnerability database.
/// </summary>
public static class BundledAdvisoryCatalog
{
    public static IReadOnlyList<BundledAdvisoryMatch> Match(ManifestPackage package)
    {
        var hits = new List<BundledAdvisoryMatch>();
        var name = package.Name;
        var version = package.Version;
        var ecosystem = package.Ecosystem;

        if (ecosystem.Equals("Maven", StringComparison.OrdinalIgnoreCase) &&
            name.Contains("log4j-core", StringComparison.OrdinalIgnoreCase) &&
            IsLog4Shell(version))
        {
            hits.Add(new BundledAdvisoryMatch(
                "CVE-2021-44228",
                "CWE-917",
                "Critical",
                "log4j-core before 2.17.1 is in the Log4Shell family. The 2.12.2 and 2.3.1 backport lines are treated as fixed.",
                "2.17.1"));
        }

        if (ecosystem.Equals("npm", StringComparison.OrdinalIgnoreCase) &&
            name.Equals("lodash", StringComparison.OrdinalIgnoreCase) &&
            VersionBounds.Below(version, "4.17.21"))
        {
            hits.Add(new BundledAdvisoryMatch(
                "CVE-2021-23337",
                "CWE-94",
                "High",
                "lodash before 4.17.21 has a command-injection issue in template.",
                "4.17.21"));
        }

        if (ecosystem.Equals("npm", StringComparison.OrdinalIgnoreCase) &&
            name.Equals("minimist", StringComparison.OrdinalIgnoreCase) &&
            IsMinimistVulnerable(version))
        {
            hits.Add(new BundledAdvisoryMatch(
                "CVE-2021-44906",
                "CWE-1321",
                "High",
                "minimist before 1.2.6, and the 0.x line before 0.2.4, has a prototype-pollution issue.",
                "1.2.6"));
        }

        if (ecosystem.Equals("npm", StringComparison.OrdinalIgnoreCase) &&
            name.Equals("jsonwebtoken", StringComparison.OrdinalIgnoreCase) &&
            VersionBounds.Below(version, "9.0.0"))
        {
            hits.Add(new BundledAdvisoryMatch(
                "CVE-2022-23529",
                "CWE-347",
                "High",
                "jsonwebtoken before 9.0.0 accepts an insecure key type. Upgrade and confirm the secret is a string.",
                "9.0.0"));
        }

        if (ecosystem.Equals("npm", StringComparison.OrdinalIgnoreCase) &&
            name.Equals("semver", StringComparison.OrdinalIgnoreCase) &&
            VersionBounds.Compare(version, "7.0.0") >= 0 &&
            VersionBounds.Below(version, "7.5.2"))
        {
            hits.Add(new BundledAdvisoryMatch(
                "CVE-2022-25883",
                "CWE-1333",
                "Medium",
                "semver 7.x before 7.5.2 has a regular-expression denial of service. This snapshot does not score the 6.x line.",
                "7.5.2"));
        }

        if (ecosystem.Equals("npm", StringComparison.OrdinalIgnoreCase) &&
            name.Equals("node-fetch", StringComparison.OrdinalIgnoreCase) &&
            VersionBounds.Compare(version, "2.0.0") >= 0 &&
            VersionBounds.Compare(version, "3.0.0") < 0 &&
            VersionBounds.Below(version, "2.6.7"))
        {
            hits.Add(new BundledAdvisoryMatch(
                "CVE-2022-0235",
                "CWE-918",
                "Medium",
                "node-fetch 2.x before 2.6.7 can follow a redirect to an unintended host.",
                "2.6.7"));
        }

        return hits;
    }

    private static bool IsLog4Shell(string version)
    {
        if (VersionBounds.Compare(version, "2.0.0") < 0 || VersionBounds.Compare(version, "2.17.1") >= 0)
            return false;

        if (VersionBounds.Compare(version, "2.12.2") >= 0 && VersionBounds.Compare(version, "2.13.0") < 0)
            return false;

        if (VersionBounds.Compare(version, "2.3.1") >= 0 && VersionBounds.Compare(version, "2.4.0") < 0)
            return false;

        return true;
    }

    private static bool IsMinimistVulnerable(string version)
    {
        if (VersionBounds.Compare(version, "1.0.0") >= 0)
            return VersionBounds.Below(version, "1.2.6");

        return VersionBounds.Below(version, "0.2.4");
    }
}
