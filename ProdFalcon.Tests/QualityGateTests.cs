using ProdFalcon.Application.Scanning.GitHub;
using Xunit;

namespace ProdFalcon.Tests;

public class QualityGateTests
{
    [Fact]
    public void Clean_scan_passes_the_default_gate()
    {
        var result = QualityGateEvaluator.Evaluate(
            QualityGateSettings.Default,
            new QualityGateFacts(90, 90, 88, 0, 1, 0, 0, 0));

        Assert.True(result.Passed);
        Assert.Contains("Quality gate passed.", result.Messages);
    }

    [Fact]
    public void Open_critical_finding_fails_when_the_limit_is_zero()
    {
        var result = QualityGateEvaluator.Evaluate(
            QualityGateSettings.Default,
            new QualityGateFacts(80, 70, null, 1, 0, 0, 0, 0));

        Assert.False(result.Passed);
        Assert.Contains(result.Messages, message => message.Contains("open critical"));
    }

    [Fact]
    public void Disabled_gate_does_not_fail_a_scan()
    {
        var settings = QualityGateSettings.Default with { Enabled = false };
        var result = QualityGateEvaluator.Evaluate(
            settings,
            new QualityGateFacts(10, 10, 90, 4, 9, 2, 2, 3));

        Assert.True(result.Passed);
    }

    [Fact]
    public void Score_regression_and_new_secrets_are_separate_failures()
    {
        var settings = QualityGateSettings.Default with { PreventScoreRegression = true, MinSecurityScore = 80 };
        var result = QualityGateEvaluator.Evaluate(
            settings,
            new QualityGateFacts(70, 60, 85, 0, 0, 0, 0, 1));

        Assert.False(result.Passed);
        Assert.Contains(result.Messages, message => message.Contains("regressed"));
        Assert.Contains(result.Messages, message => message.Contains("secret"));
        Assert.Contains(result.Messages, message => message.Contains("Security score"));
    }

    [Fact]
    public void Secret_titles_are_recognized_without_matching_ordinary_findings()
    {
        Assert.True(QualityGateEvaluator.LooksLikeSecret("Hardcoded API key", "Secrets", "Security"));
        Assert.False(QualityGateEvaluator.LooksLikeSecret("Missing null check", "Quality", "Maintainability"));
    }
}
