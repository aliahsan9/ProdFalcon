using Microsoft.AspNetCore.Mvc;
using ProdFalcon.Application.Scanning.Interfaces;
using System.IO.Compression;

namespace ProdFalcon.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScanController : ControllerBase
{
    private readonly IScanService _scanService;

    public ScanController(IScanService scanService)
    {
        _scanService = scanService;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(500_000_000)] // 500 MB
    public async Task<IActionResult> UploadZip(IFormFile file)
    {
        try
        {
            // Validate file
            if (file == null || file.Length == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "No ZIP file uploaded."
                });
            }

            // Validate extension
            var extension = Path.GetExtension(file.FileName);

            if (!extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Only ZIP files are allowed."
                });
            }

            // ============================================
            // SOLUTION ROOT FOLDER (ProdFalcon)
            // ============================================

            // Current:
            // ProdFalcon.API/bin/Debug/net8.0

            // Go back to:
            // ProdFalcon/

            var solutionRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")
              );

            // ============================================
            // STORAGE FOLDERS
            // ============================================

            var storageRoot = Path.Combine(solutionRoot, "ProjectStorage");

            var uploadsFolder = Path.Combine(storageRoot, "UploadedZips");

            var extractedFolder = Path.Combine(storageRoot, "ExtractedProjects");

            Directory.CreateDirectory(storageRoot);
            Directory.CreateDirectory(uploadsFolder);
            Directory.CreateDirectory(extractedFolder);

            // ============================================
            // SESSION
            // ============================================

            var sessionId = Guid.NewGuid().ToString();

            // ============================================
            // ZIP FILE PATH
            // ============================================

            var zipFileName = $"{sessionId}.zip";

            var zipPath = Path.Combine(
                uploadsFolder,
                zipFileName
            );

            // Save ZIP file
            using (var stream = new FileStream(zipPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // ============================================
            // EXTRACTION PATH
            // ============================================

            var extractionPath = Path.Combine(
                extractedFolder,
                sessionId
            );

            Directory.CreateDirectory(extractionPath);

            // Extract ZIP
            ZipFile.ExtractToDirectory(
                zipPath,
                extractionPath,
                true
            );

            // ============================================
            // SCAN PROJECT
            // ============================================

            var scanResult = await _scanService
                .ScanProjectAsync(extractionPath);

            // ============================================
            // RESPONSE
            // ============================================

            return Ok(new
            {
                success = true,
                message = "Project scanned successfully.",
                sessionId,
                uploadedZip = zipPath,
                extractedProject = extractionPath,
                result = scanResult
            });
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new
            {
                success = false,
                message = "Invalid ZIP file.",
                error = ex.Message
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "An unexpected error occurred.",
                error = ex.Message,
                innerError = ex.InnerException?.Message
            });
        }
    }
}