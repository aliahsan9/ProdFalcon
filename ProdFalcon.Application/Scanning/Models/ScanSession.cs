namespace ProdFalcon.Application.Scanning.Models;
public class ScanSession
{
    public int Id { get; set; }

    public string FileName { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; }

    public string Status { get; set; } = string.Empty;   // Processing / Completed / Failed

    public int Score { get; set; }

    public string ExtractedPath { get; set; } = string.Empty;

    public List<ScanIssue> Issues { get; set; } = [];
}