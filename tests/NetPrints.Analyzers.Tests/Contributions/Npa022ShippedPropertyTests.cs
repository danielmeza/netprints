using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using NetPrints.Analyzers.Tests.Infrastructure;
using NetPrints.Testing;
using Xunit;

namespace NetPrints.Analyzers.Tests.Contributions;

/// <summary>NPA022's owner prefix: the NetPrints.Core package makes the extension id property visible to the compiler, so a real build reads it.</summary>
public class Npa022ShippedPropertyTests
{
    private const string PropsFile = "src/NetPrints.Core/buildTransitive/NetPrints.Core.props";

    private static IReadOnlyList<string> ShippedCompilerVisibleProperties()
    {
        string path = Path.Combine(LocalSdkLayout.FindRepositoryRoot(), PropsFile);
        Assert.True(File.Exists(path), $"The package ships no {PropsFile}, so MSBuild never passes the extension id to the compiler");
        return [.. XDocument.Load(path).Descendants("CompilerVisibleProperty").Select(item => (string?)item.Attribute("Include") ?? string.Empty)];
    }

    [Fact]
    public void ThePackageMakesTheExtensionIdVisibleToTheCompiler()
    {
        Assert.Contains("NetPrintsExtensionId", ShippedCompilerVisibleProperties());
    }

    [Fact]
    public async Task TheOwnerPrefixIsReadFromWhatTheShippedPropsExpose()
    {
        Dictionary<string, string> options = ShippedCompilerVisibleProperties()
            .Where(name => name.Equals("NetPrintsExtensionId", StringComparison.Ordinal))
            .ToDictionary(name => $"build_property.{name}", _ => "acme.tools");

        await PinAnalyzerHarness.AssertDiagnosticsWithOptionsAsync(
            ContributionSamples.Use(
                """
                            _ = new CommandDescriptor("acme.tools.save", "x", handler);
                            _ = new CommandDescriptor({|NPA022:"other.save"|}, "x", handler);
                """),
            options,
            "NPA022");
    }
}
