using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Threats;

public static class ThreatModelEngine
{
    public static Intelligence.ThreatModelDocument Build(
        Intelligence.ProjectIntelligenceProfile profile,
        Intelligence.ArchitectureGraph architecture,
        IReadOnlyList<ScanIssue> issues)
    {
        var document = new Intelligence.ThreatModelDocument
        {
            Diagram = BuildDiagram(architecture, issues)
        };

        var index = 1;
        void Add(Intelligence.ThreatItem threat)
        {
            threat.Id = "T-" + index.ToString("00");
            threat.ExploitConfirmed = false;
            document.Threats.Add(threat);
            index++;
        }

        if (profile.Authentication.Count == 0 && profile.Endpoints.Count > 0)
        {
            Add(new Intelligence.ThreatItem
            {
                Stride = "Spoofing",
                Title = "No authentication mechanism was detected",
                Description = "Endpoints were found and no authentication API was detected in the scanned files.",
                Component = "API",
                Scenario = "If these endpoints are exposed, a caller who can reach them may not have to prove an identity. That depends on a gateway or host configuration that was not in the repository.",
                Impact = "Unauthenticated access to whatever the endpoints do.",
                Likelihood = "Unknown",
                Evidence = profile.Endpoints.Take(3).Select(item => item.FilePath).FirstOrDefault() ?? "Endpoint inventory",
                Mitigation = "Require authentication on state-changing and private endpoints, and document any endpoint that is intentionally public.",
                Classification = "Architectural risk"
            });
        }
        else if (profile.Authentication.Count > 0)
        {
            Add(new Intelligence.ThreatItem
            {
                Stride = "Spoofing",
                Title = "Authentication boundary detected",
                Description = "The scan found authentication-related code. Token lifetime, issuer checks, and key storage still need a configuration review.",
                Component = "Authentication",
                Scenario = "A stolen or forged credential could be accepted if validation is incomplete. This review did not test a login flow.",
                Impact = "Access as another user if credential checks are weak.",
                Likelihood = "Unknown",
                Evidence = string.Join("; ", profile.Authentication.Take(4)),
                Mitigation = "Validate issuer, audience, and lifetime, and keep signing keys outside source control.",
                Classification = "Architectural risk"
            });
        }

        foreach (var issue in issues.Where(item =>
                     item.RuleId.Contains("SQL", StringComparison.OrdinalIgnoreCase) ||
                     item.Title.Contains("SQL", StringComparison.OrdinalIgnoreCase))
                     .Take(5))
        {
            Add(new Intelligence.ThreatItem
            {
                Stride = "Tampering",
                Title = "Query construction may allow tampering",
                Description = "A static SQL rule matched source that builds a command with interpolation or concatenation.",
                Component = issue.FilePath,
                Scenario = "If that command runs with a string influenced by a request, the caller may change the query. The scanner did not send a payload.",
                Impact = "Unauthorized reads or writes in the database, if the path is reachable.",
                Likelihood = issue.Confidence,
                Evidence = $"{issue.FilePath}:{issue.LineNumber} {issue.Title}",
                Mitigation = issue.Remediation,
                Classification = "Supported by a static finding"
            });
        }

        if (issues.Any(item => item.RuleId.Contains("CORS", StringComparison.OrdinalIgnoreCase) || item.Title.Contains("CORS", StringComparison.OrdinalIgnoreCase)))
        {
            var cors = issues.First(item => item.Title.Contains("CORS", StringComparison.OrdinalIgnoreCase) || item.RuleId.Contains("CORS", StringComparison.OrdinalIgnoreCase));
            Add(new Intelligence.ThreatItem
            {
                Stride = "Information Disclosure",
                Title = "Browser origin boundary is wide open",
                Description = "A CORS rule allows any origin. Browsers on other sites may be allowed to read responses.",
                Component = cors.FilePath,
                Scenario = "A malicious site could call the API with a victim's browser credentials if credentials are also allowed. Credential settings were not fully evaluated.",
                Impact = "Cross-origin reads of API responses.",
                Likelihood = "Medium",
                Evidence = $"{cors.FilePath}:{cors.LineNumber}",
                Mitigation = "Allow an explicit list of origins.",
                Classification = "Supported by a static finding"
            });
        }

        foreach (var issue in issues.Where(item => item.Category == "Security" && item.RuleId.StartsWith("SEC-", StringComparison.Ordinal)).Take(8))
        {
            if (document.Threats.Any(threat => threat.Evidence.Contains(issue.FilePath, StringComparison.OrdinalIgnoreCase) && threat.Title.Contains(issue.Title, StringComparison.OrdinalIgnoreCase)))
                continue;

            Add(new Intelligence.ThreatItem
            {
                Stride = MapStride(issue),
                Title = issue.Title,
                Description = issue.Description,
                Component = issue.FilePath,
                Scenario = "The dangerous pattern is present in source. ProdFalcon did not confirm that an attacker can reach it.",
                Impact = string.IsNullOrWhiteSpace(issue.Impact) ? "See the finding." : issue.Impact,
                Likelihood = issue.Confidence,
                Evidence = $"{issue.FilePath}:{issue.LineNumber}",
                Mitigation = issue.Remediation,
                Classification = "Supported by a static finding"
            });
        }

        if (profile.Integrations.Count > 0)
        {
            Add(new Intelligence.ThreatItem
            {
                Stride = "Repudiation",
                Title = "External calls need an audit trail",
                Description = "Outbound integration keywords were detected. Whether those calls are logged with actor and target was not established.",
                Component = "External integrations",
                Scenario = "A disputed change that depends on a third party cannot be reconstructed if the call is not logged.",
                Impact = "Weak evidence after an incident.",
                Likelihood = "Unknown",
                Evidence = string.Join("; ", profile.Integrations.Take(4)),
                Mitigation = "Log the actor, target, and result of security-sensitive calls without logging secrets.",
                Classification = "Architectural risk"
            });
        }

        if (issues.Any(item => item.RuleId == "SEC-UPLOAD-NAME"))
        {
            var upload = issues.First(item => item.RuleId == "SEC-UPLOAD-NAME");
            Add(new Intelligence.ThreatItem
            {
                Stride = "Tampering",
                Title = "File upload trust boundary",
                Description = "A client file name is combined into a storage path.",
                Component = upload.FilePath,
                Scenario = "A crafted file name may write outside the upload directory. This was not executed against a running app.",
                Impact = "Overwrite of local files if the process can write them.",
                Likelihood = upload.Confidence,
                Evidence = $"{upload.FilePath}:{upload.LineNumber}",
                Mitigation = upload.Remediation,
                Classification = "Supported by a static finding"
            });
        }

        if (document.Threats.Count == 0)
        {
            Add(new Intelligence.ThreatItem
            {
                Stride = "Information Disclosure",
                Title = "No security-sensitive boundary was confirmed",
                Description = "The detectors did not see authentication, SQL construction, uploads, or secrets. That is not a statement that the system is safe.",
                Component = "Repository",
                Scenario = "Sensitive behavior may live in generated code, infrastructure, or a language this pass does not parse.",
                Impact = "Unknown.",
                Likelihood = "Unknown",
                Evidence = "Empty result from the supported detectors.",
                Mitigation = "Review hosting, secrets, and authorization outside the repository.",
                Classification = "Architectural risk"
            });
        }

        return document;
    }

    private static Intelligence.ArchitectureGraph BuildDiagram(Intelligence.ArchitectureGraph architecture, IReadOnlyList<ScanIssue> issues)
    {
        var diagram = new Intelligence.ArchitectureGraph
        {
            Note = "Threat diagram nodes are the detected architecture nodes plus a trust-boundary marker when security findings exist."
        };
        diagram.Nodes.AddRange(architecture.Nodes.Take(40));
        diagram.Edges.AddRange(architecture.Edges.Take(60));

        if (issues.Any(issue => issue.Category is "Security" or "Injection" or "Authentication"))
        {
            diagram.Nodes.Add(new Intelligence.ArchitectureNode
            {
                Id = "boundary:internet",
                Label = "External callers",
                Type = "External",
                Description = "Assumed trust boundary. The scanner did not map the real network path.",
                Kind = "Inferred"
            });
            var api = diagram.Nodes.FirstOrDefault(node => node.Type is "Backend" or "Controller");
            if (api != null)
            {
                diagram.Edges.Add(new Intelligence.ArchitectureEdge
                {
                    Source = "boundary:internet",
                    Target = api.Id,
                    Label = "untrusted input",
                    Kind = "Inferred",
                    Evidence = "Security-sensitive code exists and an API component was detected."
                });
            }
        }

        return diagram;
    }

    private static string MapStride(ScanIssue issue)
    {
        if (issue.RuleId.Contains("AUTH", StringComparison.OrdinalIgnoreCase))
            return "Elevation of Privilege";
        if (issue.Title.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
            issue.Title.Contains("key", StringComparison.OrdinalIgnoreCase) ||
            issue.Category.Contains("Secret", StringComparison.OrdinalIgnoreCase))
            return "Information Disclosure";
        if (issue.RuleId.Contains("DESER", StringComparison.OrdinalIgnoreCase))
            return "Tampering";
        if (issue.Title.Contains("DoS", StringComparison.OrdinalIgnoreCase) || issue.Cwe == "CWE-400")
            return "Denial of Service";
        return "Elevation of Privilege";
    }
}
