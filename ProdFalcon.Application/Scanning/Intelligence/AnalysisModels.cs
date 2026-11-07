namespace ProdFalcon.Application.Scanning.Intelligence;

public sealed class EvidenceNote
{
    public string Statement { get; set; } = string.Empty;

    /// <summary>Detected fact, or an inference that names the evidence it depends on.</summary>
    public string Kind { get; set; } = "Detected";

    public string Evidence { get; set; } = string.Empty;
}

public sealed class TechnologyFact
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Kind { get; set; } = "Detected";
    public string Evidence { get; set; } = string.Empty;
}

public sealed class DependencyFact
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Ecosystem { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
}

public sealed class EndpointFact
{
    public string Verb { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string Kind { get; set; } = "Detected";
}

public sealed class ComponentFact
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Kind { get; set; } = "Detected";
    public List<string> Dependencies { get; set; } = [];
    public List<string> Risks { get; set; } = [];
}

public sealed class ProjectIntelligenceProfile
{
    public string Purpose { get; set; } = string.Empty;
    public string PurposeKind { get; set; } = "Inferred";
    public string Organization { get; set; } = string.Empty;
    public string OrganizationKind { get; set; } = "Detected";
    public List<string> Languages { get; set; } = [];
    public List<TechnologyFact> Technologies { get; set; } = [];
    public List<string> EntryPoints { get; set; } = [];
    public List<EndpointFact> Endpoints { get; set; } = [];
    public List<string> Databases { get; set; } = [];
    public List<string> Authentication { get; set; } = [];
    public List<string> Integrations { get; set; } = [];
    public List<string> ConfigurationFiles { get; set; } = [];
    public List<DependencyFact> Dependencies { get; set; } = [];
    public List<ComponentFact> Components { get; set; } = [];
    public List<EvidenceNote> Facts { get; set; } = [];
    public List<EvidenceNote> Inferences { get; set; } = [];
    public List<EvidenceNote> Weaknesses { get; set; } = [];
    public int FilesConsidered { get; set; }
    public bool Truncated { get; set; }
    public List<string> Answers { get; set; } = [];
}

public sealed class ArchitectureNode
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Kind { get; set; } = "Detected";
    public List<string> Risks { get; set; } = [];
}

public sealed class ArchitectureEdge
{
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;

    /// <summary>Detected, Inferred, or Uncertain. Uncertain edges are not treated as confirmed calls.</summary>
    public string Kind { get; set; } = "Uncertain";
    public string Evidence { get; set; } = string.Empty;
}

public sealed class ArchitectureGraph
{
    public List<ArchitectureNode> Nodes { get; set; } = [];
    public List<ArchitectureEdge> Edges { get; set; } = [];
    public string Note { get; set; } =
        "Nodes come from files and manifests that were actually present. " +
        "An uncertain edge means a relationship could not be confirmed from the source.";
}

public sealed class ThreatItem
{
    public string Id { get; set; } = string.Empty;
    public string Stride { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Component { get; set; } = string.Empty;
    public string Scenario { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
    public string Likelihood { get; set; } = "Unknown";
    public string Evidence { get; set; } = string.Empty;
    public string Mitigation { get; set; } = string.Empty;

    /// <summary>Architectural risk, or supported by a static finding. Never a confirmed exploit.</summary>
    public string Classification { get; set; } = "Architectural risk";

    public bool ExploitConfirmed { get; set; }
}

public sealed class ThreatModelDocument
{
    public List<ThreatItem> Threats { get; set; } = [];
    public ArchitectureGraph Diagram { get; set; } = new();
    public string Note { get; set; } =
        "This is a static STRIDE review. ProdFalcon did not execute attacks. " +
        "ExploitConfirmed is false unless a controlled proof was run, which this engine does not do.";
}

public sealed class ComparisonItem
{
    public string Fingerprint { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Change { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class ScanComparison
{
    public int? PreviousScanResultId { get; set; }
    public string? PreviousBranch { get; set; }
    public string? CurrentBranch { get; set; }
    public string? PreviousCommit { get; set; }
    public string? CurrentCommit { get; set; }
    public int NewCount { get; set; }
    public int RecurringCount { get; set; }
    public int VerifiedCount { get; set; }
    public int NotObservedCount { get; set; }
    public int UnchangedCount { get; set; }
    public int AcceptedRiskCount { get; set; }
    public List<ComparisonItem> Items { get; set; } = [];
}

public sealed class ProjectAnalysisDocument
{
    public ProjectIntelligenceProfile Intelligence { get; set; } = new();
    public ArchitectureGraph Architecture { get; set; } = new();
    public ThreatModelDocument ThreatModel { get; set; } = new();
    public ScanComparison Comparison { get; set; } = new();
    public List<string> UnverifiedAreas { get; set; } = [];
    public string ScoreMethodology { get; set; } = SecurityScoreMethodology.Summary;
    public string PerformanceNote { get; set; } =
        "Performance and reliability findings are static predictions. No benchmark was executed.";
}

public sealed class ProjectAnalysis
{
    public ProjectIntelligenceProfile Profile { get; init; } = new();
    public ArchitectureGraph Architecture { get; init; } = new();
    public ThreatModelDocument Threats { get; init; } = new();
    public List<Models.ScanIssue> AdditionalIssues { get; init; } = [];
    public List<string> UnverifiedAreas { get; init; } = [];
}
