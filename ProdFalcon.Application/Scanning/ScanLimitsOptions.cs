namespace ProdFalcon.Application.Scanning;

public class ScanLimitsOptions
{
    public const string SectionName = "Scanning";

    public long MaxUploadBytes { get; set; } = 100L * 1024 * 1024;

    public long MaxExtractedBytes { get; set; } = 500L * 1024 * 1024;

    public int MaxEntries { get; set; } = 8_000;

    public int MaxCompressionRatio { get; set; } = 100;

    public int MaxFileBytes { get; set; } = 1_048_576;

    public int MaxFilesScanned { get; set; } = 4_000;

    public int MaxDependencyQueries { get; set; } = 30;

    public int ScanTimeoutMinutes { get; set; } = 10;

    /// <summary>Upper bound for file reads and for running independent rules together.</summary>
    public int MaxDegreeOfParallelism { get; set; } = 4;

    public string[] ExcludedDirectories { get; set; } =
    [
        "node_modules", "bin", "obj", ".git", "dist", "build", "vendor", "packages",
        ".vs", "coverage", "__pycache__", "target", ".next", ".angular", ".idea",
        "bower_components", "TestResults", ".svn", ".terraform", "Pods"
    ];

    public string[] ExcludedExtensions { get; set; } =
    [
        ".dll", ".exe", ".pdb", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".ico", ".zip",
        ".nupkg", ".woff", ".woff2", ".ttf", ".eot", ".mp4", ".pdf", ".snk", ".pfx", ".so",
        ".dylib", ".wasm", ".map"
    ];
}
