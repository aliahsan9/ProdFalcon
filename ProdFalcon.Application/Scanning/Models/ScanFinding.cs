
namespace ProdFalcon.Application.Scanning.Models;

public class ScanFinding
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Severity { get; set; }
    public int LineNumber { get; set; }
}