namespace ProdFalcon.Application.Scanning.Models;

public class ScanResultDto
{
    public int SessionId { get; set; }
    public string ProjectPath { get; set; } = string.Empty;
    public int Score { get; set; }
    public int TotalIssues { get; set; }
    public string Status { get; set; } = string.Empty;
}