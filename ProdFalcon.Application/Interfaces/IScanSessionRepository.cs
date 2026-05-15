using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Interfaces;

public interface IScanSessionRepository
{
    Task AddAsync(ScanSession session);

    Task<List<ScanSession>> GetAllAsync();

    Task<ScanSession?> GetByIdAsync(int id);

    Task UpdateAsync(ScanSession session);
}