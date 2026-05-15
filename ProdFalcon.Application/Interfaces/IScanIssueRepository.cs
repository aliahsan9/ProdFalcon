using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Interfaces;

public interface IScanIssueRepository
{
    Task AddRangeAsync(List<ScanIssue> issues);

    Task<List<ScanIssue>> GetBySessionIdAsync(int sessionId);
}