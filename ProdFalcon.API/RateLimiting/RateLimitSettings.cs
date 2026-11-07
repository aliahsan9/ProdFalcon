namespace ProdFalcon.API.RateLimiting;

public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimiting";

    public int GlobalPermitLimit { get; set; } = 300;

    public int AuthPermitLimit { get; set; } = 10;

    public int ScanPermitLimit { get; set; } = 8;

    public int AiPermitLimit { get; set; } = 20;

    public int WindowSeconds { get; set; } = 60;
}
