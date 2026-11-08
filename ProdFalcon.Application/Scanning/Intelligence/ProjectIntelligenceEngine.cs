using System.Text.Json;
using System.Text.RegularExpressions;
using ProdFalcon.Application.Scanning.Rules;

namespace ProdFalcon.Application.Scanning.Intelligence;

public static class ProjectIntelligenceEngine
{
    private static readonly Dictionary<string, string> LanguageByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".cs"] = "C#",
        [".cshtml"] = "Razor",
        [".ts"] = "TypeScript",
        [".tsx"] = "TypeScript",
        [".js"] = "JavaScript",
        [".jsx"] = "JavaScript",
        [".py"] = "Python",
        [".java"] = "Java",
        [".go"] = "Go",
        [".php"] = "PHP",
        [".rb"] = "Ruby",
        [".sql"] = "SQL",
        [".json"] = "JSON",
        [".yml"] = "YAML",
        [".yaml"] = "YAML",
        [".xml"] = "XML"
    };

    public static ProjectIntelligenceProfile Analyze(string projectPath)
    {
        var profile = new ProjectIntelligenceProfile();
        if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
        {
            profile.Purpose = "No source tree was available to analyze.";
            profile.PurposeKind = "Detected";
            return profile;
        }

        var files = SourceTree.EnumerateSourceFiles(projectPath).ToList();
        profile.FilesConsidered = files.Count;
        profile.Truncated = files.Count >= 4_000;

        var languageCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var extension = Path.GetExtension(file);
            if (LanguageByExtension.TryGetValue(extension, out var language) && language is not "JSON" and not "YAML" and not "XML")
                languageCounts[language] = languageCounts.GetValueOrDefault(language) + 1;
        }

        profile.Languages = languageCounts
            .OrderByDescending(pair => pair.Value)
            .Select(pair => pair.Key)
            .ToList();

        if (profile.Languages.Count > 0)
        {
            profile.Facts.Add(new EvidenceNote
            {
                Statement = "Languages were counted from source file extensions.",
                Evidence = string.Join(", ", languageCounts.Select(pair => $"{pair.Key} ({pair.Value} files)"))
            });
        }

        InspectManifests(projectPath, files, profile);
        InspectSource(projectPath, files, profile);
        DescribeOrganization(profile);
        profile.Purpose = InferPurpose(projectPath, profile);
        profile.Answers = BuildAnswers(profile);
        return profile;
    }

    private static void InspectManifests(string projectPath, List<string> files, ProjectIntelligenceProfile profile)
    {
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            var relative = Relative(projectPath, file);

            if (name.Equals("package.json", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("requirements.txt", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("go.mod", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("pom.xml", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("composer.json", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("angular.json", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("docker-compose.yml", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("web.config", StringComparison.OrdinalIgnoreCase) ||
                name.Equals(".env", StringComparison.OrdinalIgnoreCase) ||
                name.Equals(".env.example", StringComparison.OrdinalIgnoreCase))
            {
                profile.ConfigurationFiles.Add(relative);
            }

            if (name.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                ReadProjectFile(file, relative, profile);
            else if (name.Equals("package.json", StringComparison.OrdinalIgnoreCase))
                ReadPackageJson(file, relative, profile);
            else if (name.Equals("angular.json", StringComparison.OrdinalIgnoreCase))
                AddTechnology(profile, "Angular", "", relative);
            else if (name.Equals("requirements.txt", StringComparison.OrdinalIgnoreCase) ||
                     name.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase))
                AddTechnology(profile, "Python", "", relative);
        }

        profile.ConfigurationFiles = profile.ConfigurationFiles.Distinct(StringComparer.OrdinalIgnoreCase).Take(40).ToList();
    }

    private static void ReadProjectFile(string file, string relative, ProjectIntelligenceProfile profile)
    {
        string text;
        try
        {
            text = SourceTree.ReadText(file) ?? string.Empty;
            if (text.Length == 0 && !File.Exists(file))
                return;
        }
        catch (IOException)
        {
            return;
        }

        var framework = Regex.Match(text, @"<TargetFramework>([^<]+)</TargetFramework>");
        if (framework.Success)
            AddTechnology(profile, framework.Groups[1].Value.Trim(), "", relative);

        if (text.Contains("Microsoft.NET.Sdk.Web", StringComparison.Ordinal))
            AddTechnology(profile, "ASP.NET Core", "", relative);

        foreach (Match match in Regex.Matches(text, @"<PackageReference\s+Include=""([^""]+)""(?:\s+Version=""([^""]+)"")?", RegexOptions.IgnoreCase))
        {
            var package = match.Groups[1].Value;
            var version = match.Groups[2].Success ? match.Groups[2].Value : string.Empty;
            profile.Dependencies.Add(new DependencyFact
            {
                Name = package,
                Version = version,
                Ecosystem = "NuGet",
                Evidence = relative
            });
            NotePackage(profile, package, version, relative);
        }

        foreach (Match match in Regex.Matches(text, @"<ProjectReference\s+Include=""([^""]+)""", RegexOptions.IgnoreCase))
        {
            profile.Facts.Add(new EvidenceNote
            {
                Statement = $"Project reference to {match.Groups[1].Value}.",
                Evidence = relative
            });
        }
    }

    private static void ReadPackageJson(string file, string relative, ProjectIntelligenceProfile profile)
    {
        try
        {
            var packageText = SourceTree.ReadText(file);
            if (packageText == null)
                return;

            using var document = JsonDocument.Parse(packageText);
            if (document.RootElement.TryGetProperty("dependencies", out var dependencies))
                ReadNpmDependencies(profile, dependencies, relative);
            if (document.RootElement.TryGetProperty("devDependencies", out var devDependencies))
                ReadNpmDependencies(profile, devDependencies, relative);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            profile.Inferences.Add(new EvidenceNote
            {
                Kind = "Uncertain",
                Statement = "package.json could not be parsed.",
                Evidence = relative
            });
        }
    }

    private static void ReadNpmDependencies(ProjectIntelligenceProfile profile, JsonElement dependencies, string relative)
    {
        foreach (var property in dependencies.EnumerateObject())
        {
            var version = property.Value.GetString() ?? string.Empty;
            profile.Dependencies.Add(new DependencyFact
            {
                Name = property.Name,
                Version = version,
                Ecosystem = "npm",
                Evidence = relative
            });

            if (property.Name is "@angular/core")
                AddTechnology(profile, "Angular", version, relative);
            else if (property.Name is "react")
                AddTechnology(profile, "React", version, relative);
            else if (property.Name is "vue")
                AddTechnology(profile, "Vue", version, relative);
            else if (property.Name is "express")
                AddTechnology(profile, "Express", version, relative);
            else if (property.Name is "next")
                AddTechnology(profile, "Next.js", version, relative);
        }
    }

    private static void NotePackage(ProjectIntelligenceProfile profile, string package, string version, string relative)
    {
        if (package.Contains("EntityFrameworkCore", StringComparison.OrdinalIgnoreCase))
        {
            AddTechnology(profile, "Entity Framework Core", version, relative);
            if (package.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
                profile.Databases.Add("SQL Server (package reference)");
            else if (package.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
                profile.Databases.Add("SQLite (package reference)");
            else if (package.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) || package.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase))
                profile.Databases.Add("PostgreSQL (package reference)");
        }
        else if (package.Contains("JwtBearer", StringComparison.OrdinalIgnoreCase))
            profile.Authentication.Add($"JWT bearer ({relative})");
        else if (package.StartsWith("Microsoft.AspNetCore.Identity", StringComparison.OrdinalIgnoreCase))
            profile.Authentication.Add($"ASP.NET Core Identity ({relative})");
    }

    private static void InspectSource(string projectPath, List<string> files, ProjectIntelligenceProfile profile)
    {
        var endpointCount = 0;
        foreach (var file in files)
        {
            var extension = Path.GetExtension(file);
            if (extension is not (".cs" or ".ts" or ".js" or ".py"))
                continue;

            string? content;
            try
            {
                content = SourceTree.ReadText(file, 300_000);
                if (content == null)
                    continue;
            }
            catch (IOException)
            {
                continue;
            }

            var relative = Relative(projectPath, file);
            var fileName = Path.GetFileName(file);

            if (fileName.Equals("Program.cs", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("Startup.cs", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("main.ts", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("main.py", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("index.js", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("static void Main", StringComparison.Ordinal) ||
                content.Contains("public static void Main", StringComparison.Ordinal))
            {
                profile.EntryPoints.Add(relative);
            }

            if (extension == ".cs")
                InspectCSharp(profile, relative, content, ref endpointCount);
            else if (extension is ".ts" or ".js")
                InspectScript(profile, relative, content, ref endpointCount);
        }

        profile.EntryPoints = profile.EntryPoints.Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList();
        profile.Databases = profile.Databases.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        profile.Authentication = profile.Authentication.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        profile.Integrations = profile.Integrations.Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList();
        profile.Dependencies = profile.Dependencies
            .GroupBy(item => item.Ecosystem + ":" + item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(80)
            .ToList();
    }

    private static void InspectCSharp(ProjectIntelligenceProfile profile, string relative, string content, ref int endpointCount)
    {
        var classMatch = Regex.Match(content, @"class\s+(\w+)");
        var className = classMatch.Success ? classMatch.Groups[1].Value : Path.GetFileNameWithoutExtension(relative);

        if (content.Contains(": Controller", StringComparison.Ordinal) || content.Contains(": ControllerBase", StringComparison.Ordinal))
        {
            profile.Components.Add(new ComponentFact
            {
                Id = "controller:" + relative,
                Name = className,
                Type = "Controller",
                FilePath = relative,
                Description = "ASP.NET controller class detected from its base type."
            });
        }
        else if (className.EndsWith("Service", StringComparison.Ordinal) && content.Contains("class ", StringComparison.Ordinal))
        {
            profile.Components.Add(new ComponentFact
            {
                Id = "service:" + relative,
                Name = className,
                Type = "Service",
                FilePath = relative,
                Description = "Class name ends with Service. Treat the role as a naming convention, not a confirmed boundary."
            });
        }
        else if (className.EndsWith("Repository", StringComparison.Ordinal))
        {
            profile.Components.Add(new ComponentFact
            {
                Id = "repository:" + relative,
                Name = className,
                Type = "Repository",
                FilePath = relative,
                Description = "Class name ends with Repository."
            });
        }

        if (content.Contains(": DbContext", StringComparison.Ordinal) || content.Contains(": IdentityDbContext", StringComparison.Ordinal))
        {
            profile.Components.Add(new ComponentFact
            {
                Id = "database:" + relative,
                Name = className,
                Type = "Database",
                FilePath = relative,
                Description = "EF Core DbContext class."
            });
            profile.Databases.Add($"EF Core DbContext {className} ({relative})");
        }

        if (content.Contains(": BackgroundService", StringComparison.Ordinal) || content.Contains("IHostedService", StringComparison.Ordinal))
        {
            profile.Components.Add(new ComponentFact
            {
                Id = "worker:" + relative,
                Name = className,
                Type = "Worker",
                FilePath = relative,
                Description = "Background worker base type or IHostedService reference."
            });
        }

        if (content.Contains("AddAuthentication", StringComparison.Ordinal))
            profile.Authentication.Add($"AddAuthentication in {relative}");
        if (content.Contains("JwtBearer", StringComparison.Ordinal))
            profile.Authentication.Add($"JWT bearer reference in {relative}");
        if (content.Contains("[Authorize", StringComparison.Ordinal))
            profile.Authentication.Add($"[Authorize] in {relative}");
        if (content.Contains("UseSqlServer", StringComparison.Ordinal))
            profile.Databases.Add($"UseSqlServer in {relative}");
        if (content.Contains("UseSqlite", StringComparison.Ordinal))
            profile.Databases.Add($"UseSqlite in {relative}");
        if (content.Contains("UseNpgsql", StringComparison.Ordinal))
            profile.Databases.Add($"UseNpgsql in {relative}");
        if (content.Contains("HttpClient", StringComparison.Ordinal))
            profile.Integrations.Add($"HttpClient usage in {relative}");
        if (content.Contains("AddStripe", StringComparison.OrdinalIgnoreCase) || content.Contains("StripeConfiguration", StringComparison.Ordinal))
            profile.Integrations.Add($"Stripe reference in {relative}");

        if (endpointCount >= 60)
            return;

        var lines = content.Split('\n');
        for (var i = 0; i < lines.Length && endpointCount < 60; i++)
        {
            var line = lines[i];
            var http = Regex.Match(line, @"\[Http(Get|Post|Put|Delete|Patch)(?:\(""([^""]*)""\))?\]");
            if (!http.Success)
                continue;

            endpointCount++;
            profile.Endpoints.Add(new EndpointFact
            {
                Verb = http.Groups[1].Value.ToUpperInvariant(),
                Route = string.IsNullOrWhiteSpace(http.Groups[2].Value) ? "(attribute route not on this line)" : http.Groups[2].Value,
                FilePath = relative,
                LineNumber = i + 1
            });
        }
    }

    private static void InspectScript(ProjectIntelligenceProfile profile, string relative, string content, ref int endpointCount)
    {
        if (relative.Contains("app/", StringComparison.OrdinalIgnoreCase) &&
            (relative.EndsWith(".component.ts", StringComparison.OrdinalIgnoreCase) ||
             relative.EndsWith(".component.js", StringComparison.OrdinalIgnoreCase)))
        {
            profile.Components.Add(new ComponentFact
            {
                Id = "frontend:" + relative,
                Name = Path.GetFileNameWithoutExtension(relative),
                Type = "Frontend",
                FilePath = relative,
                Description = "UI component file."
            });
        }

        if (content.Contains("RouterModule", StringComparison.Ordinal) || content.Contains("provideRouter", StringComparison.Ordinal))
            profile.EntryPoints.Add(relative + " (router)");

        if (endpointCount >= 60 || (!content.Contains("express", StringComparison.OrdinalIgnoreCase) && !content.Contains("router.", StringComparison.Ordinal)))
            return;

        foreach (Match match in Regex.Matches(content, @"\.(get|post|put|delete|patch)\(\s*['""]([^'""]+)['""]", RegexOptions.IgnoreCase))
        {
            if (endpointCount >= 60)
                break;
            endpointCount++;
            profile.Endpoints.Add(new EndpointFact
            {
                Verb = match.Groups[1].Value.ToUpperInvariant(),
                Route = match.Groups[2].Value,
                FilePath = relative,
                LineNumber = SourceTree.LineNumberAt(content, match.Index)
            });
        }
    }

    private static void DescribeOrganization(ProjectIntelligenceProfile profile)
    {
        var types = profile.Components.Select(component => component.Type).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (types.Count == 0)
        {
            profile.Organization = "No controllers, services, repositories, or UI components were identified from naming and base types.";
            profile.OrganizationKind = "Detected";
            return;
        }

        profile.Organization = "Detected component kinds: " + string.Join(", ", types) + ".";
        var hasController = types.Contains("Controller", StringComparer.OrdinalIgnoreCase);
        var hasService = types.Contains("Service", StringComparer.OrdinalIgnoreCase);
        var hasRepository = types.Contains("Repository", StringComparer.OrdinalIgnoreCase);
        if (hasController && hasService && hasRepository)
        {
            profile.Inferences.Add(new EvidenceNote
            {
                Kind = "Inferred",
                Statement = "The names suggest a layered API (controllers, services, repositories). Call direction is only confirmed where the architecture graph cites a reference.",
                Evidence = profile.Organization
            });
        }

        if (hasController && profile.Components.Any(component => component.Type == "Database"))
        {
            profile.Weaknesses.Add(new EvidenceNote
            {
                Kind = "Inferred",
                Statement = "Controllers and a DbContext are both present. Whether controllers query the database directly is reported only when a file contains both.",
                Evidence = "Component inventory"
            });
        }
    }

    private static string InferPurpose(string projectPath, ProjectIntelligenceProfile profile)
    {
        var readme = Directory.EnumerateFiles(projectPath, "README.md", SearchOption.AllDirectories)
            .FirstOrDefault(path => !path.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
        if (readme != null)
        {
            try
            {
                var text = SourceTree.ReadText(readme);
                var line = string.IsNullOrWhiteSpace(text)
                    ? null
                    : text.Split('\n').Select(item => item.Trim().TrimStart('#', ' ')).FirstOrDefault(item => item.Length > 40);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    profile.PurposeKind = "Detected";
                    profile.Facts.Add(new EvidenceNote
                    {
                        Statement = "Purpose text was taken from README.md.",
                        Evidence = Relative(projectPath, readme)
                    });
                    return line.Length > 280 ? line[..280] : line;
                }
            }
            catch (IOException)
            {
                // Fall through to an inference.
            }
        }

        profile.PurposeKind = "Inferred";
        if (profile.Components.Any(component => component.Type == "Controller") || profile.Endpoints.Count > 0)
            return "The repository appears to expose an application API. This is inferred from controllers or route declarations, not from a product description.";
        if (profile.Technologies.Any(item => item.Name.Contains("Angular", StringComparison.OrdinalIgnoreCase) || item.Name == "React"))
            return "The repository appears to contain a frontend application. This is inferred from the UI framework manifest.";

        return "The repository purpose could not be determined from a README or recognizable application entry points.";
    }

    private static List<string> BuildAnswers(ProjectIntelligenceProfile profile)
    {
        return
        [
            "What does this project appear to do? " + profile.Purpose + $" ({profile.PurposeKind})",
            "What technologies does it use? " + (profile.Technologies.Count == 0
                ? "No framework manifest markers were detected."
                : string.Join(", ", profile.Technologies.Select(item => item.Name).Distinct())),
            "How is the project organized? " + profile.Organization,
            "What are its main components? " + (profile.Components.Count == 0
                ? "None were identified with the supported detectors."
                : string.Join(", ", profile.Components.Take(12).Select(item => item.Name + " (" + item.Type + ")"))),
            "How do those components communicate? See the architecture graph. Edges marked Uncertain were not confirmed.",
            "What are its major dependencies? " + (profile.Dependencies.Count == 0
                ? "No package manifests were parsed."
                : string.Join(", ", profile.Dependencies.Take(12).Select(item => item.Name))),
            "What are its potential architectural weaknesses? " + (profile.Weaknesses.Count == 0
                ? "No weakness was inferred beyond the separate quality and security findings."
                : string.Join(" ", profile.Weaknesses.Select(item => item.Statement)))
        ];
    }

    private static void AddTechnology(ProjectIntelligenceProfile profile, string name, string version, string evidence)
    {
        if (profile.Technologies.Any(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && item.Evidence == evidence))
            return;

        profile.Technologies.Add(new TechnologyFact
        {
            Name = name,
            Version = version,
            Evidence = evidence
        });
    }

    public static string Relative(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        var relative = fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
            ? fullPath[fullRoot.Length..]
            : fullPath;
        return relative.Replace('\\', '/');
    }
}
