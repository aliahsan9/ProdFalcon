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
            var text = File.ReadAllText(file);
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

        var packageJson = Directory.EnumerateFiles(projectPath, "package.json", SearchOption.AllDirectories)
            .FirstOrDefault(path => !path.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
        if (packageJson != null)
        {
            var text = File.ReadAllText(packageJson);
            found.Add("Node.js");
            if (text.Contains("\"next\"", StringComparison.OrdinalIgnoreCase))
                found.Add("Next.js");
            else if (text.Contains("\"react\"", StringComparison.OrdinalIgnoreCase))
                found.Add("React");
            if (text.Contains("\"@angular/core\"", StringComparison.OrdinalIgnoreCase))
                found.Add("Angular");
        }

        foreach (var requirements in Directory.EnumerateFiles(projectPath, "requirements.txt", SearchOption.AllDirectories).Take(5))
        {
            var text = File.ReadAllText(requirements);
            found.Add("Python");
            if (text.Contains("django", StringComparison.OrdinalIgnoreCase))
                found.Add("Django");
            if (text.Contains("flask", StringComparison.OrdinalIgnoreCase))
                found.Add("Flask");
        }

        foreach (var pom in Directory.EnumerateFiles(projectPath, "pom.xml", SearchOption.AllDirectories).Take(5))
        {
            var text = File.ReadAllText(pom);
            found.Add("Java");
            if (text.Contains("spring-boot", StringComparison.OrdinalIgnoreCase))
                found.Add("Spring Boot");
        }

        foreach (var composer in Directory.EnumerateFiles(projectPath, "composer.json", SearchOption.AllDirectories).Take(3))
        {
            var text = File.ReadAllText(composer);
            found.Add("PHP");
            if (text.Contains("laravel", StringComparison.OrdinalIgnoreCase))
                found.Add("Laravel");
        }

        foreach (var manifest in Directory.EnumerateFiles(projectPath, "*.yml", SearchOption.AllDirectories).Take(40))
        {
            if (manifest.Contains($"{Path.DirectorySeparatorChar}node_modules{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;

            string text;
            try
            {
                text = File.ReadAllText(manifest);
            }
            catch (IOException)
            {
                continue;
            }

            if (text.Contains("kind: Deployment", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("kind: Service", StringComparison.OrdinalIgnoreCase) && text.Contains("apiVersion:", StringComparison.Ordinal))
                found.Add("Kubernetes");
        }
    }

    public static string ToJson(IReadOnlyList<string> technologies) =>
        JsonSerializer.Serialize(technologies);
}
