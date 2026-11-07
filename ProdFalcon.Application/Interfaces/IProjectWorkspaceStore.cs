using ProdFalcon.Application.Scanning.Models;

namespace ProdFalcon.Application.Interfaces;

public interface IProjectWorkspaceStore
{
    Task<List<TrackedFinding>> GetTrackedAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task SaveTrackedAsync(IReadOnlyList<TrackedFinding> findings, CancellationToken cancellationToken = default);

    Task AddCopilotMessageAsync(CopilotMessage message, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CopilotMessage>> GetCopilotMessagesAsync(int scanResultId, CancellationToken cancellationToken = default);

    Task<RepositoryRegistration> AddRegistrationAsync(RepositoryRegistration registration, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RepositoryRegistration>> ListRegistrationsAsync(CancellationToken cancellationToken = default);

    Task<RepositoryRegistration?> FindRegistrationByRepositoryAsync(string owner, string name, CancellationToken cancellationToken = default);

    Task<RepositoryRegistration?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
}
