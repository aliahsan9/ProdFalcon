namespace ProdFalcon.Application.Scanning.Models;

public class ScanIssue
{
    public int Id { get; set; }

    public int ScanSessionId { get; set; }
    public ScanSession? ScanSession { get; set; }
    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;

    public string RuleId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Severity { get; set; } = "Info";

    public string FilePath { get; set; } = string.Empty;

    public string RuleName { get; set; } = string.Empty;
}