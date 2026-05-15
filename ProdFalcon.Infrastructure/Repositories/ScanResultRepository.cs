using ProdFalcon.Application.Scanning.Interfaces;
using ProdFalcon.Application.Scanning.Models;
using ProdFalcon.Infrastructure.Data;

namespace ProdFalcon.Infrastructure.Repositories;

public class ScanResultRepository : IScanResultRepository
{
    private readonly ApplicationDbContext _db;

    public ScanResultRepository(ApplicationDbContext db)
    {
        _db = db;
    } 

    public async Task SaveAsync(ScanResult result, CancellationToken cancellationToken)
    {
        _db.ScanResults.Add(result);
        await _db.SaveChangesAsync(cancellationToken);
    }
}