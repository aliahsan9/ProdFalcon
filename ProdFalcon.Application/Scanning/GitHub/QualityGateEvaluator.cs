namespace ProdFalcon.Application.Scanning.GitHub;

public sealed record QualityGateSettings(
    bool Enabled,
    bool FailOnCritical,
    bool FailOnHigh,
    int MaxOpenCritical,
    int MaxOpenHigh,
    int MinOverallScore,
    int MinSecurityScore,
    bool PreventScoreRegression,
    bool FailOnNewSecrets)
{
    public static QualityGateSettings Default { get; } = new(
        Enabled: true,
        FailOnCritical: true,
        FailOnHigh: false,
        MaxOpenCritical: 0,
        MaxOpenHigh: 25,
        MinOverallScore: 0,
        MinSecurityScore: 0,
        PreventScoreRegression: false,
        FailOnNewSecrets: true);
}

public sealed record QualityGateFacts(
    int OverallScore,
    int SecurityScore,
    int? PreviousScore,
    int OpenCritical,
    int OpenHigh,
    int NewCritical,
    int NewHigh,
    int NewSecrets);

public sealed record QualityGateResult(bool Passed, IReadOnlyList<string> Messages);

public static class QualityGateEvaluator
{
    public static QualityGateResult Evaluate(QualityGateSettings settings, QualityGateFacts facts)
    {
        if (!settings.Enabled)
            return new QualityGateResult(true, ["Quality gate is turned off."]);

        var failures = new List<string>();

        if (settings.FailOnCritical && facts.NewCritical > 0)
            failures.Add($"{facts.NewCritical} new or recurring critical finding(s).");

        if (settings.FailOnHigh && facts.NewHigh > 0)
            failures.Add($"{facts.NewHigh} new or recurring high finding(s).");

        if (facts.OpenCritical > settings.MaxOpenCritical)
            failures.Add($"{facts.OpenCritical} open critical finding(s). The gate allows {settings.MaxOpenCritical}.");

        if (facts.OpenHigh > settings.MaxOpenHigh)
            failures.Add($"{facts.OpenHigh} open high finding(s). The gate allows {settings.MaxOpenHigh}.");

        if (settings.MinOverallScore > 0 && facts.OverallScore < settings.MinOverallScore)
            failures.Add($"Overall score {facts.OverallScore} is below {settings.MinOverallScore}.");

        if (settings.MinSecurityScore > 0 && facts.SecurityScore < settings.MinSecurityScore)
            failures.Add($"Security score {facts.SecurityScore} is below {settings.MinSecurityScore}.");

        if (settings.PreventScoreRegression && facts.PreviousScore.HasValue && facts.OverallScore < facts.PreviousScore.Value)
            failures.Add($"Overall score regressed from {facts.PreviousScore.Value} to {facts.OverallScore}.");

        if (settings.FailOnNewSecrets && facts.NewSecrets > 0)
            failures.Add($"{facts.NewSecrets} new or recurring secret finding(s).");

        if (failures.Count == 0)
            failures.Add("Quality gate passed.");

        return new QualityGateResult(failures.Count == 1 && failures[0] == "Quality gate passed.", failures);
    }

    public static IReadOnlyList<string> Describe(QualityGateSettings settings)
    {
        if (!settings.Enabled)
            return ["Scans are reported, and this workspace gate does not fail them."];

        var rules = new List<string>();
        if (settings.FailOnCritical)
            rules.Add("Fail when a scan introduces a critical finding.");
        if (settings.FailOnHigh)
            rules.Add("Fail when a scan introduces a high finding.");
        rules.Add($"Fail when open critical findings exceed {settings.MaxOpenCritical}.");
        rules.Add($"Fail when open high findings exceed {settings.MaxOpenHigh}.");
        if (settings.MinOverallScore > 0)
            rules.Add($"Fail when the overall score is below {settings.MinOverallScore}.");
        if (settings.MinSecurityScore > 0)
            rules.Add($"Fail when the security score is below {settings.MinSecurityScore}.");
        if (settings.PreventScoreRegression)
            rules.Add("Fail when the overall score drops compared with the previous scan.");
        if (settings.FailOnNewSecrets)
            rules.Add("Fail when a new secret, password, API key, or JWT finding appears.");
        return rules;
    }

    public static bool LooksLikeSecret(string? title, string? ruleName, string? category)
    {
        var text = $"{title} {ruleName} {category}".ToLowerInvariant();
        return text.Contains("secret")
            || text.Contains("password")
            || text.Contains("api key")
            || text.Contains("apikey")
            || text.Contains("jwt")
            || text.Contains("token");
    }

    public static bool IsOpen(string? status) =>
        !string.Equals(status, "Resolved", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(status, "AcceptedRisk", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(status, "Verified", StringComparison.OrdinalIgnoreCase);
}
