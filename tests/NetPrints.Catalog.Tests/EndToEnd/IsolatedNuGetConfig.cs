using System.IO;
using System.Linq;

namespace NetPrints.Catalog.Tests.EndToEnd;

/// <summary>The <c>NuGet.config</c> of the throwaway projects the package tests build: one local feed, an isolated global package folder, everything else from nuget.org.</summary>
internal static class IsolatedNuGetConfig
{
    public static void Write(string directory, string feed, string packagesFolder, params string[] localPatterns)
    {
        string patterns = string.Join("\n      ", localPatterns.Select(pattern => $"<package pattern=\"{pattern}\" />"));
        File.WriteAllText(
            Path.Combine(directory, "NuGet.config"),
        $"""
        <configuration>
          <packageSources>
            <clear />
            <add key="local" value="{feed}" />
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
          </packageSources>
          <packageSourceMapping>
            <clear />
            <packageSource key="local">
              {patterns}
            </packageSource>
            <packageSource key="nuget.org">
              <package pattern="*" />
            </packageSource>
          </packageSourceMapping>
          <config>
            <add key="globalPackagesFolder" value="{packagesFolder}" />
          </config>
        </configuration>
        """);
    }
}
