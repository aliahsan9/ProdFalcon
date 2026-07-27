namespace ProdFalcon.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(Guid tenantId, int? userId, string action, string? metadata = null, CancellationToken cancellationToken = default);
}
