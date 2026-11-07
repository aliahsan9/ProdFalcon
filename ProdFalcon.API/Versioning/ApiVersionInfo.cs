using System.Reflection;

namespace ProdFalcon.API.Versioning;

public static class ApiVersionInfo
{
    public const string Current = "1.0";

    public static string ProductVersion { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
}
