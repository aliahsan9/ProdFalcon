using System.Text.Json;
using ProdFalcon.Application.Scanning.Rules;

namespace ProdFalcon.Application.Scanning.Services;

public static class ProjectTechnologyDetector
{
    public static IReadOnlyList<string> Detect(string projectPath)
    {
        var found = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(projectPath) || !Directory.Exists(projectPath))
            return found.ToList();

        foreach (var file in SourceTree.EnumerateSourceFiles(projectPath))
        {
            var name = Path.GetFileName(file);
            var extension = Path.GetExtension(file);

            switch (extension.ToLowerInvariant())
            {
                case ".cs": found.Add("C#"); break;
                case ".java": found.Add("Java"); break;
                case ".php": found.Add("PHP"); break;
                case ".py": found.Add("Python"); break;
                case ".go": found.Add("Go"); break;
                case ".rs": found.Add("Rust"); break;
                case ".tf": found.Add("Terraform"); break;
            }

            if (name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Dockerfile.", StringComparison.OrdinalIgnoreCase))
                found.Add("Docker");

            if (name.Equals("angular.json", StringComparison.OrdinalIgnoreCase))
                found.Add("Angular");

            if (name.Equals("go.mod", StringComparison.OrdinalIgnoreCase))
                found.Add("Go");

            if (name.Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase))
                found.Add("Rust");

            if (name.Equals("manage.py", StringComparison.OrdinalIgnoreCase))
                found.Add("Django");

            if (name.Equals("docker-compose.yml", StringComparison.OrdinalIgnoreCase) || name.Equals("compose.yaml", StringComparison.OrdinalIgnoreCase))
                found.Add("Docker");
        }

        InspectManifests(projectPath, found);
        return found.ToList();
    }

    private static void InspectManifests(string projectPath, ISet<string> found)
    {
        foreach (var file in SourceTree.EnumerateSourceFiles(projectPath, "*.csproj"))
        {
            var text = SourceTree.ReadText(file);
            if (text == null)
                continue;
            found.Add(".NET");
            if (text.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase))
                found.Add("ASP.NET Core");
            if (text.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
                found.Add("SQL Server");
            if (text.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                found.Add("PostgreSQL");
            if (text.Contains("MySql", StringComparison.OrdinalIgnoreCase) || text.Contains("Pomelo", StringComparison.OrdinalIgnoreCase))
                found.Add("MySQL");
        }

        var packageJson = SourceTree.EnumerateSourceFiles(projectPath, "package.json").FirstOrDefault();
        if (packageJson != null)
        {
            var text = SourceTree.ReadText(packageJson);
            if (text != null)
            {
                found.Add("Node.js");
                if (text.Contains("\"next\"", StringComparison.OrdinalIgnoreCase))
                    found.Add("Next.js");
                else if (text.Contains("\"react\"", StringComparison.OrdinalIgnoreCase))
                    found.Add("React");
                if (text.Contains("\"@angular/core\"", StringComparison.OrdinalIgnoreCase))
                    found.Add("Angular");
            }
        }

        foreach (var requirements in SourceTree.EnumerateSourceFiles(projectPath, "requirements.txt").Take(5))
        {
            var text = SourceTree.ReadText(requirements);
            if (text == null)
                continue;
            found.Add("Python");
            if (text.Contains("django", StringComparison.OrdinalIgnoreCase))
                found.Add("Django");
            if (text.Contains("flask", StringComparison.OrdinalIgnoreCase))
                found.Add("Flask");
        }

        foreach (var pom in SourceTree.EnumerateSourceFiles(projectPath, "pom.xml").Take(5))
        {
            var text = SourceTree.ReadText(pom);
            if (text == null)
                continue;
            found.Add("Java");
            if (text.Contains("spring-boot", StringComparison.OrdinalIgnoreCase))
                found.Add("Spring Boot");
        }

        foreach (var composer in SourceTree.EnumerateSourceFiles(projectPath, "composer.json").Take(3))
        {
            var text = SourceTree.ReadText(composer);
            if (text == null)
                continue;
            found.Add("PHP");
            if (text.Contains("laravel", StringComparison.OrdinalIgnoreCase))
                found.Add("Laravel");
        }

        foreach (var manifest in SourceTree.EnumerateSourceFiles(projectPath, "*.yml").Take(40))
        {
            var text = SourceTree.ReadText(manifest);
            if (text == null)
                continue;

            if (text.Contains("kind: Deployment", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("kind: Service", StringComparison.OrdinalIgnoreCase) && text.Contains("apiVersion:", StringComparison.Ordinal))
                found.Add("Kubernetes");
        }
    }

    public static string ToJson(IReadOnlyList<string> technologies) =>
        JsonSerializer.Serialize(technologies);
}
