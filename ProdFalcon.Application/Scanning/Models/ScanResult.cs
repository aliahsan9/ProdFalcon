using ProdFalcon.Domain.Entities;

namespace ProdFalcon.Application.Scanning.Models;

public class ScanResult : BaseEntity
{
    public string ProjectPath { get; set; } = string.Empty;
    public int Score { get; set; }

    public List<ScanIssue> Issues { get; set; } = new();
}