using System.IO.Compression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProdFalcon.Application.Interfaces;

namespace ProdFalcon.Infrastructure.Services;

public class ProjectStorageService : IProjectStorageService
{
    private readonly ILogger<ProjectStorageService> _logger;

    public ProjectStorageService(IConfiguration configuration, ILogger<ProjectStorageService> logger)
    {
        _logger = logger;
        StorageRoot = ResolveStorageRoot(configuration);
        Directory.CreateDirectory(StorageRoot);
    }

    public string StorageRoot { get; }

    public string GetProjectDirectory(Guid projectId) =>
        Path.Combine(StorageRoot, projectId.ToString("N"));

    public async Task<string> SaveZipAsync(Guid projectId, Stream zipStream, CancellationToken cancellationToken = default)
    {
        var projectDir = GetProjectDirectory(projectId);
        Directory.CreateDirectory(projectDir);

        var zipPath = Path.Combine(projectDir, "upload.zip");

        await using var fileStream = new FileStream(
            zipPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None);

        await zipStream.CopyToAsync(fileStream, cancellationToken);
        return zipPath;
    }

    public async Task<string> ExtractZipAsync(Guid projectId, string zipPath, CancellationToken cancellationToken = default)
    {
        var extractPath = Path.Combine(GetProjectDirectory(projectId), "extracted");
        if (Directory.Exists(extractPath))
            Directory.Delete(extractPath, recursive: true);

        Directory.CreateDirectory(extractPath);

        await Task.Run(() => ZipFile.ExtractToDirectory(zipPath, extractPath, overwriteFiles: true), cancellationToken);
        return extractPath;
    }

    public async Task<bool> ValidateZipAsync(string zipPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(zipPath))
            return false;

        try
        {
            await using var stream = File.OpenRead(zipPath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            return archive.Entries.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ZIP validation failed for {ZipPath}", zipPath);
            return false;
        }
    }

    public Task CleanupProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var projectDir = GetProjectDirectory(projectId);

        if (Directory.Exists(projectDir))
            Directory.Delete(projectDir, recursive: true);

        return Task.CompletedTask;
    }

    private static string ResolveStorageRoot(IConfiguration configuration)
    {
        var configured = configuration["Storage:RootPath"];
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.GetFullPath(configured);

        var solutionRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

        return Path.Combine(solutionRoot, "ProdFalcon_Storage");
    }
}
