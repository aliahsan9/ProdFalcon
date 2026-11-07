namespace ProdFalcon.Application.Scanning.Rules;

public interface IScanModule
{
    string ModuleKey { get; }
}

public static class ScanModules
{
    public const string Secrets = "secrets";
    public const string SqlInjection = "sql";
    public const string Authentication = "auth";
    public const string Dependencies = "dependencies";
    public const string Configuration = "configuration";
    public const string Quality = "quality";
    public const string StaticAnalysis = "static";
}
