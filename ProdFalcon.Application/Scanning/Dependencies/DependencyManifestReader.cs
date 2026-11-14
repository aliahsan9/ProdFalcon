using System.Text.RegularExpressions;
using ProdFalcon.Application.Scanning.Rules;

namespace ProdFalcon.Application.Scanning.Dependencies;

public sealed record ManifestPackage(
    string Name,
    string Version,
    string Declared,
    string Ecosystem,
    string ManifestPath,
    int LineNumber,
    int ColumnNumber,
    bool ExactPin);

public static class VersionBounds
{
    public static bool TryGetMinimum(string spec, out string version)
    {
        version = string.Empty;
        if (string.IsNullOrWhiteSpace(spec))
            return false;

        var text = spec.Trim().Trim('"', '\'', '`');
        if (text is "*" or "latest" or "x")
            return false;

        text = text.TrimStart('^', '~', '=', ' ');
        if (text.StartsWith(">=", StringComparison.Ordinal))
            text = text[2..].Trim();

        text = text.TrimStart('v', 'V');
        var match = Regex.Match(text, @"\d+\.\d+(?:\.\d+)?");
        if (!match.Success)
            return false;

        version = Normalize(match.Value);
        return true;
    }

    public static int Compare(string left, string right)
    {
        var a = Parts(left);
        var b = Parts(right);
        for (var i = 0; i < 3; i++)
        {
            var cmp = a[i].CompareTo(b[i]);
            if (cmp != 0)
                return cmp;
        }

        return 0;
    }

    public static bool Below(string version, string fixedVersion) => Compare(version, fixedVersion) < 0;

    private static string Normalize(string version)
    {
        var parts = version.Split('.');
        if (parts.Length == 2)
            return version + ".0";
        return version;
    }

    private static int[] Parts(string version)
    {
        TryGetMinimum(version, out var minimum);
        var pieces = (string.IsNullOrEmpty(minimum) ? "0.0.0" : minimum).Split('.');
        var result = new int[3];
        for (var i = 0; i < 3 && i < pieces.Length; i++)
            int.TryParse(pieces[i], out result[i]);
        return result;
    }
}

public static class DependencyManifestReader
{
    public static IReadOnlyList<ManifestPackage> Read(string projectPath)
    {
        var packages = new List<ManifestPackage>();
        if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
            return packages;

        ReadByName(projectPath, "*.csproj", ReadCsproj, packages);
        ReadByName(projectPath, "Directory.Packages.props", ReadCsproj, packages);
        ReadByName(projectPath, "packages.config", ReadPackagesConfig, packages);
        ReadByName(projectPath, "package.json", ReadPackageJson, packages);
        ReadByName(projectPath, "requirements.txt", ReadRequirements, packages);
        ReadByName(projectPath, "pom.xml", ReadPom, packages);
        ReadByName(projectPath, "go.mod", ReadGoMod, packages);
        ReadByName(projectPath, "composer.json", ReadComposer, packages);
        ReadByName(projectPath, "Gemfile", ReadGemfile, packages);
        return packages;
    }

    private static void ReadByName(string root, string pattern, Action<string, string, string, List<ManifestPackage>> read, List<ManifestPackage> packages)
    {
        foreach (var file in SourceTree.EnumerateSourceFiles(root, pattern))
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            read(root, file, text, packages);
        }
    }

    private static void ReadCsproj(string root, string file, string text, List<ManifestPackage> packages)
    {
        foreach (Match match in Regex.Matches(text, @"<PackageReference\s+Include\s*=\s*""([^""]+)""[^>]*Version\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase))
            Add(packages, root, file, text, match.Groups[1].Value, match.Groups[2].Value, match.Index, "NuGet");

        foreach (Match match in Regex.Matches(text, @"<PackageVersion\s+Include\s*=\s*""([^""]+)""[^>]*Version\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase))
            Add(packages, root, file, text, match.Groups[1].Value, match.Groups[2].Value, match.Index, "NuGet");
    }

    private static void ReadPackagesConfig(string root, string file, string text, List<ManifestPackage> packages)
    {
        foreach (Match match in Regex.Matches(text, @"<package[^>]*id\s*=\s*""([^""]+)""[^>]*version\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase))
            Add(packages, root, file, text, match.Groups[1].Value, match.Groups[2].Value, match.Index, "NuGet");
    }

    private static void ReadPackageJson(string root, string file, string text, List<ManifestPackage> packages)
    {
        foreach (var section in new[] { "dependencies", "devDependencies", "optionalDependencies" })
            ReadJsonSection(root, file, text, section, "npm", packages);
    }

    private static void ReadComposer(string root, string file, string text, List<ManifestPackage> packages)
    {
        foreach (var section in new[] { "require", "require-dev" })
            ReadJsonSection(root, file, text, section, "Packagist", packages);
    }

    private static void ReadJsonSection(string root, string file, string text, string property, string ecosystem, List<ManifestPackage> packages)
    {
        if (!TryExtractObject(text, property, out var start, out var body))
            return;

        foreach (Match match in Regex.Matches(body, @"""([^""]+)""\s*:\s*""([^""]+)"""))
        {
            var name = match.Groups[1].Value;
            if (name.StartsWith("php", StringComparison.OrdinalIgnoreCase) || name.StartsWith("ext-", StringComparison.Ordinal))
                continue;

            Add(packages, root, file, text, name, match.Groups[2].Value, start + match.Index, ecosystem);
        }
    }

    private static void ReadRequirements(string root, string file, string text, List<ManifestPackage> packages)
    {
        var offset = 0;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim().TrimEnd('\r');
            var index = offset;
            offset += raw.Length + 1;
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var match = Regex.Match(line, @"^([A-Za-z0-9_.\-]+)\s*(?:==|>=)\s*([^\s;#]+)");
            if (!match.Success)
                continue;

            Add(packages, root, file, text, match.Groups[1].Value, match.Groups[2].Value, index + match.Groups[1].Index, "PyPI");
        }
    }

    private static void ReadPom(string root, string file, string text, List<ManifestPackage> packages)
    {
        foreach (Match match in Regex.Matches(
                     text,
                     @"<dependency>\s*<groupId>([^<]+)</groupId>\s*<artifactId>([^<]+)</artifactId>\s*<version>([^<]+)</version>",
                     RegexOptions.Singleline))
        {
            var name = match.Groups[1].Value.Trim() + ":" + match.Groups[2].Value.Trim();
            Add(packages, root, file, text, name, match.Groups[3].Value.Trim(), match.Index, "Maven");
        }
    }

    private static void ReadGoMod(string root, string file, string text, List<ManifestPackage> packages)
    {
        foreach (Match match in Regex.Matches(text, @"(?m)^require\s+(\S+)\s+(v?\S+)"))
            Add(packages, root, file, text, match.Groups[1].Value, match.Groups[2].Value, match.Index, "Go");

        foreach (Match block in Regex.Matches(text, @"require\s*\((.*?)\)", RegexOptions.Singleline))
        {
            foreach (Match row in Regex.Matches(block.Groups[1].Value, @"(?m)^\s*(\S+)\s+(v?\S+)"))
            {
                if (row.Groups[1].Value.StartsWith("//", StringComparison.Ordinal))
                    continue;

                Add(packages, root, file, text, row.Groups[1].Value, row.Groups[2].Value, block.Groups[1].Index + row.Index, "Go");
            }
        }
    }

    private static void ReadGemfile(string root, string file, string text, List<ManifestPackage> packages)
    {
        foreach (Match match in Regex.Matches(text, @"gem\s+['""]([^'""]+)['""]\s*,\s*['""]([^'""]+)['""]"))
            Add(packages, root, file, text, match.Groups[1].Value, match.Groups[2].Value, match.Index, "RubyGems");
    }

    private static void Add(List<ManifestPackage> packages, string root, string file, string text, string name, string declared, int index, string ecosystem)
    {
        if (!VersionBounds.TryGetMinimum(declared, out var version))
            return;

        var location = EvidenceLocator.Locate(text, index);
        var exact = Regex.IsMatch(declared.Trim().TrimStart('v', 'V'), @"^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.]+)?$") ||
                    Regex.IsMatch(declared.Trim(), @"^v\d+\.\d+\.\d+");
        packages.Add(new ManifestPackage(
            name.Trim(),
            version,
            declared.Trim(),
            ecosystem,
            Relative(root, file),
            location.Line,
            location.Column,
            exact));
    }

    private static bool TryExtractObject(string text, string property, out int start, out string body)
    {
        start = 0;
        body = string.Empty;
        var marker = "\"" + property + "\"";
        var at = 0;
        while ((at = text.IndexOf(marker, at, StringComparison.Ordinal)) >= 0)
        {
            var brace = text.IndexOf('{', at + marker.Length);
            if (brace < 0)
                return false;

            var between = text[(at + marker.Length)..brace];
            if (!Regex.IsMatch(between, @"^\s*:\s*$"))
            {
                at += marker.Length;
                continue;
            }

            var end = FindBrace(text, brace);
            if (end < 0)
                return false;

            start = brace;
            body = text[brace..(end + 1)];
            return true;
        }

        return false;
    }

    private static int FindBrace(string text, int open)
    {
        var depth = 0;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '{')
                depth++;
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return i;
            }
        }

        return -1;
    }

    private static string Relative(string root, string path)
    {
        try
        {
            return Path.GetRelativePath(root, path).Replace('\\', '/');
        }
        catch (Exception ex) when (ex is IOException or ArgumentException)
        {
            return path;
        }
    }
}
