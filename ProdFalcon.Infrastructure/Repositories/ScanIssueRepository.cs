using Microsoft.EntityFrameworkCore;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Infrastructure.Data;

namespace ProdFalcon.Infrastructure.Repositories;

public class ScanIssueRepository : IScanIssueRepository
{
    private readonly ApplicationDbContext _context;

    public ScanIssueRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddRangeAsync(List<ScanIssue> issues)
    {
        await _context.ScanIssues.AddRangeAsync(issues);
    }

    public async Task<List<ScanIssue>> GetBySessionIdAsync(int sessionId)
    {
        return await _context.ScanIssues
            .Where(x => x.ScanSessionId == sessionId)
            .ToListAsync();
    }
}