using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace NetPrints.Analyzers.Tests.Infrastructure;

/// <summary>Loads the analyzer assemblies from their own output folders; the test project never references them (ADR-0024).</summary>
internal static class AnalyzerBinaries
{
    /// <summary>Loads an analyzer assembly by its project name, such as <c>NetPrints.RepoAnalyzers</c>.</summary>
    /// <param name="name">The project and assembly name.</param>
    /// <returns>The loaded assembly.</returns>
    public static Assembly Load(string name) =>
        Assembly.LoadFrom(Path.Combine(Metadata("SourceRoot"), name, "bin", Metadata("Configuration"), "netstandard2.0", name + ".dll"));

    /// <summary>Reads a build-time value the project stamps into the test assembly.</summary>
    /// <param name="key">The metadata key.</param>
    /// <returns>The stamped value.</returns>
    public static string Metadata(string key) =>
        typeof(AnalyzerBinaries).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == key).Value
            ?? throw new InvalidOperationException($"Assembly metadata {key} is missing.");
}
