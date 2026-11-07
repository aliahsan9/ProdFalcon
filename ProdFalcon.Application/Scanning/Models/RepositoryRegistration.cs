using ProdFalcon.Domain.Interfaces;

namespace ProdFalcon.Application.Scanning.Models;

public class RepositoryRegistration : ITenantEntity
{
    public int Id { get; set; }

    public Guid TenantId { get; set; }

    public string Owner { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Branch { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public string TokenPrefix { get; set; } = string.Empty;

    public bool FailOnNewCritical { get; set; } = true;

    public bool WarnOnHigh { get; set; } = true;

    public bool AllowAcceptedRisk { get; set; } = true;

    public bool PreventScoreRegression { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
