namespace ProdFalcon.Application.Scanning.Intelligence;

public static class ArchitectureGraphBuilder
{
    public static ArchitectureGraph Build(string projectPath, ProjectIntelligenceProfile profile)
    {
        var graph = new ArchitectureGraph();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddNode(ArchitectureNode node)
        {
            if (!seen.Add(node.Id))
                return;
            graph.Nodes.Add(node);
        }

        var hasFrontend = profile.Technologies.Any(item =>
            item.Name is "Angular" or "React" or "Vue" or "Next.js");
        if (hasFrontend)
        {
            AddNode(new ArchitectureNode
            {
                Id = "frontend",
                Label = profile.Technologies.First(item => item.Name is "Angular" or "React" or "Vue" or "Next.js").Name + " application",
                Type = "Frontend",
                Description = "A frontend framework was declared in a manifest.",
                FilePath = profile.Technologies.First(item => item.Name is "Angular" or "React" or "Vue" or "Next.js").Evidence
            });
        }

        var hasBackend = profile.Technologies.Any(item => item.Name.Contains("ASP.NET", StringComparison.OrdinalIgnoreCase))
            || profile.Components.Any(component => component.Type is "Controller" or "Database");
        if (hasBackend)
        {
            AddNode(new ArchitectureNode
            {
                Id = "backend",
                Label = "Backend",
                Type = "Backend",
                Description = "Server-side project markers were detected.",
                Kind = "Detected"
            });
        }

        if (graph.Nodes.Any(node => node.Id == "frontend") && graph.Nodes.Any(node => node.Id == "backend"))
        {
            graph.Edges.Add(new ArchitectureEdge
            {
                Source = "frontend",
                Target = "backend",
                Label = "Possible HTTP client",
                Kind = "Uncertain",
                Evidence = "Both a UI framework and a backend were detected. No specific call was matched."
            });
        }

        foreach (var component in profile.Components.Take(80))
        {
            AddNode(new ArchitectureNode
            {
                Id = component.Id,
                Label = component.Name,
                Type = component.Type,
                FilePath = component.FilePath,
                Description = component.Description,
                Kind = component.Kind,
                Risks = component.Risks
            });

            if (component.Type == "Controller" && graph.Nodes.Any(node => node.Id == "backend"))
            {
                graph.Edges.Add(new ArchitectureEdge
                {
                    Source = "backend",
                    Target = component.Id,
                    Label = "hosts",
                    Kind = "Detected",
                    Evidence = component.FilePath
                });
            }
        }

        foreach (var database in profile.Databases.Take(8))
        {
            var id = "db:" + database;
            AddNode(new ArchitectureNode
            {
                Id = id,
                Label = database,
                Type = "Database",
                Description = "Database technology mentioned in source or a package reference.",
                Kind = "Detected"
            });
        }

        LinkByName(projectPath, profile, graph);
        LinkExternal(profile, graph);

        if (graph.Nodes.Count == 0)
        {
            graph.Note = "No architecture components could be inferred from the supported file patterns.";
        }

        return graph;
    }

    private static void LinkByName(string projectPath, ProjectIntelligenceProfile profile, ArchitectureGraph graph)
    {
        var services = profile.Components.Where(component => component.Type == "Service").ToList();
        var repositories = profile.Components.Where(component => component.Type == "Repository").ToList();
        var controllers = profile.Components.Where(component => component.Type == "Controller").ToList();

        foreach (var controller in controllers)
        {
            var content = Read(projectPath, controller.FilePath);
            if (content == null)
                continue;

            foreach (var service in services)
            {
                if (content.Contains(service.Name, StringComparison.Ordinal))
                {
                    graph.Edges.Add(new ArchitectureEdge
                    {
                        Source = controller.Id,
                        Target = service.Id,
                        Label = "references",
                        Kind = "Detected",
                        Evidence = controller.FilePath + " mentions " + service.Name
                    });
                }
            }

            if (content.Contains("DbContext", StringComparison.Ordinal))
            {
                var database = profile.Components.FirstOrDefault(component => component.Type == "Database");
                if (database != null)
                {
                    graph.Edges.Add(new ArchitectureEdge
                    {
                        Source = controller.Id,
                        Target = database.Id,
                        Label = "uses DbContext",
                        Kind = "Detected",
                        Evidence = controller.FilePath + " contains DbContext"
                    });
                    controller.Risks.Add("Controller file references DbContext directly.");
                }
            }
        }

        foreach (var service in services)
        {
            var content = Read(projectPath, service.FilePath);
            if (content == null)
                continue;

            foreach (var repository in repositories)
            {
                if (!content.Contains(repository.Name, StringComparison.Ordinal))
                    continue;

                graph.Edges.Add(new ArchitectureEdge
                {
                    Source = service.Id,
                    Target = repository.Id,
                    Label = "references",
                    Kind = "Detected",
                    Evidence = service.FilePath + " mentions " + repository.Name
                });
            }
        }
    }

    private static void LinkExternal(ProjectIntelligenceProfile profile, ArchitectureGraph graph)
    {
        var index = 0;
        foreach (var integration in profile.Integrations.Take(12))
        {
            var id = "external:" + index++;
            graph.Nodes.Add(new ArchitectureNode
            {
                Id = id,
                Label = integration,
                Type = "External",
                Description = "An external integration keyword was found. The remote system was not contacted.",
                Kind = "Detected"
            });

            var source = graph.Nodes.FirstOrDefault(node => node.Type is "Backend" or "Service" or "Controller");
            if (source != null)
            {
                graph.Edges.Add(new ArchitectureEdge
                {
                    Source = source.Id,
                    Target = id,
                    Label = "integration keyword",
                    Kind = "Uncertain",
                    Evidence = integration
                });
            }
        }
    }

    private static string? Read(string projectPath, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
            return null;

        var path = Path.Combine(projectPath, relative.Replace('/', Path.DirectorySeparatorChar));
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}
