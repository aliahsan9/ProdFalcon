using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Scanning.Interfaces;

public interface IScanResultRepository
{
    Task SaveAsync(ScanResult result, CancellationToken cancellationToken);
}