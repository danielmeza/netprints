using System.Diagnostics;

namespace NetPrints.Testing;

/// <summary>Where the run fixtures under <c>tests/Fixtures/RunProfiles</c> are built, and the host that starts them.</summary>
public static class RunFixtures
{
    /// <summary>The echo tool's assembly: it reports its arguments, working directory and chosen variables, and its behaviour follows <c>ECHO_TOOL_*</c> variables.</summary>
    /// <returns>The path of <c>EchoTool.dll</c>, in the configuration of the running tests.</returns>
    public static string EchoToolDll() => Path.Combine(
        LocalSdkLayout.FindRepositoryRoot(),
        "tests",
        "Fixtures",
        "RunProfiles",
        "EchoTool",
        "bin",
        LocalSdkLayout.DetectConfiguration(),
        "EchoTool.dll");

    /// <summary>The program that starts a managed assembly: the <c>dotnet</c> host.</summary>
    /// <returns>The running host's path when it is <c>dotnet</c>, else <c>DOTNET_HOST_PATH</c> when set, else the bare name <c>dotnet</c>. Never throws.</returns>
    public static string DotnetHost()
    {
        string? current = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(current) && string.Equals(Path.GetFileNameWithoutExtension(current), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return current;
        }

        string? configured = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        return string.IsNullOrEmpty(configured) ? "dotnet" : configured;
    }
}
