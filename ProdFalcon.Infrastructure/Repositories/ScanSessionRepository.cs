using Microsoft.EntityFrameworkCore;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Infrastructure.Data;

namespace ProdFalcon.Infrastructure.Repositories;

public class ScanSessionRepository : IScanSessionRepository
{
    private readonly ApplicationDbContext _context;

    public ScanSessionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ScanSession session)
    {
        await _context.ScanSessions.AddAsync(session);
    }

    public async Task<List<ScanSession>> GetAllAsync()
    {
        return await _context.ScanSessions
            .Include(x => x.Issues)
            .ToListAsync();
    }

    public async Task<ScanSession?> GetByIdAsync(int id)
    {
        return await _context.ScanSessions
            .Include(x => x.Issues)
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public Task UpdateAsync(ScanSession session)
    {
        _context.ScanSessions.Update(session);
        return Task.CompletedTask;
    }
}