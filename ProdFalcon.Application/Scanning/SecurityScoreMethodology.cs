namespace ProdFalcon.Application.Scanning;

public static class SecurityScoreMethodology
{
    public const string Summary =
        "The score starts at 100. Only open findings reduce it. " +
        "Critical deducts 25, High 15, Medium 8, Low 3, and Informational deducts 0. " +
        "Confidence scales that deduction: High 100%, Medium 75%, Low 50%. " +
        "The result is clamped between 0 and 100. " +
        "Resolved and accepted-risk findings are excluded. " +
        "Repository size is reported beside the score and does not hide a critical finding. " +
        "This is an indicative assessment of what the scanners detected, not proof that a project is secure.";
}
