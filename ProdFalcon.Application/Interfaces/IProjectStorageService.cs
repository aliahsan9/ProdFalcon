namespace ProdFalcon.Application.Interfaces;

public interface IProjectStorageService
{
    string StorageRoot { get; }

    string GetProjectDirectory(Guid projectId);

    Task<string> SaveZipAsync(Guid projectId, Stream zipStream, CancellationToken cancellationToken = default);

    Task<string> ExtractZipAsync(Guid projectId, string zipPath, CancellationToken cancellationToken = default);

    Task<bool> ValidateZipAsync(string zipPath, CancellationToken cancellationToken = default);

    Task CleanupProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
}
