using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProdFalcon.Application.Interfaces;
using ProdFalcon.Application.Scanning;
using ProdFalcon.Application.Scanning.Security;

namespace ProdFalcon.Infrastructure.Services;

public class ProjectStorageService : IProjectStorageService
{
    private readonly ILogger<ProjectStorageService> _logger;
    private readonly ITenantProvider _tenantProvider;
    private readonly ZipSafetyLimits _zipLimits;
    private readonly long _maxUploadBytes;

    public ProjectStorageService(
        IConfiguration configuration,
        ILogger<ProjectStorageService> logger,
        ITenantProvider tenantProvider,
        IOptions<ScanLimitsOptions> limits)
    {
        _logger = logger;
        _tenantProvider = tenantProvider;
        _maxUploadBytes = limits.Value.MaxUploadBytes;
        _zipLimits = new ZipSafetyLimits
        {
            MaxExtractedBytes = limits.Value.MaxExtractedBytes,
            MaxEntries = limits.Value.MaxEntries,
            MaxCompressionRatio = limits.Value.MaxCompressionRatio
        };
        StorageRoot = ResolveStorageRoot(configuration);
        Directory.CreateDirectory(StorageRoot);
    }

    public string StorageRoot { get; }

    public string GetProjectDirectory(Guid projectId)
    {
        var tenantId = _tenantProvider.TenantId;
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("Cannot resolve storage path without a tenant context.");

        return Path.Combine(StorageRoot, tenantId.ToString("N"), projectId.ToString("N"));
    }

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

        try
        {
            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await zipStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                total += read;
                if (total > _maxUploadBytes)
                    throw new InvalidDataException("The archive exceeds the upload size limit.");

                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
        }
        catch
        {
            fileStream.Dispose();
            if (File.Exists(zipPath))
                File.Delete(zipPath);

            throw;
        }

        return zipPath;
    }

    public async Task<string> ExtractZipAsync(Guid projectId, string zipPath, CancellationToken cancellationToken = default)
    {
        var extractPath = Path.Combine(GetProjectDirectory(projectId), "extracted");
        if (Directory.Exists(extractPath))
            Directory.Delete(extractPath, recursive: true);

        Directory.CreateDirectory(extractPath);

        await Task.Run(() => SafeZipExtractor.Extract(zipPath, extractPath, _zipLimits, cancellationToken), cancellationToken);
        return extractPath;
    }

    public async Task<bool> ValidateZipAsync(string zipPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(zipPath))
            return false;

        try
        {
            await Task.Run(() => SafeZipExtractor.Validate(zipPath, _zipLimits), cancellationToken);
            return true;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ZIP validation failed");
            return false;
        }
    }

    public Task CleanupProjectAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_tenantProvider.TenantId != Guid.Empty)
        {
            var projectDir = GetProjectDirectory(projectId);
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, recursive: true);
            return Task.CompletedTask;
        }

        // Background cleanup without tenant context: search all tenant folders.
        foreach (var tenantDir in Directory.Exists(StorageRoot) ? Directory.GetDirectories(StorageRoot) : [])
        {
            var projectDir = Path.Combine(tenantDir, projectId.ToString("N"));
            if (Directory.Exists(projectDir))
                Directory.Delete(projectDir, recursive: true);
        }

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
