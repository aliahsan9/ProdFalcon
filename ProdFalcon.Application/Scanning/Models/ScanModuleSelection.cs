namespace ProdFalcon.Application.Scanning.Models;

public class ScanModuleSelection
{
    public bool FullAudit { get; set; } = true;

    public bool Secrets { get; set; } = true;

    public bool Dependencies { get; set; } = true;

    public bool StaticAnalysis { get; set; } = true;

    public bool SqlInjection { get; set; } = true;

    public bool Authentication { get; set; } = true;

    public bool Configuration { get; set; } = true;

    public bool CodeQuality { get; set; } = true;

    public bool IsEnabled(string moduleKey)
    {
        if (FullAudit)
            return true;

        return moduleKey switch
        {
            Rules.ScanModules.Secrets => Secrets,
            Rules.ScanModules.Dependencies => Dependencies,
            Rules.ScanModules.StaticAnalysis => StaticAnalysis,
            Rules.ScanModules.SqlInjection => SqlInjection,
            Rules.ScanModules.Authentication => Authentication,
            Rules.ScanModules.Configuration => Configuration,
            Rules.ScanModules.Quality => CodeQuality,
            _ => true
        };
    }
}
