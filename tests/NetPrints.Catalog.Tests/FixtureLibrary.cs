using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace NetPrints.Catalog.Tests;

/// <summary>Locates the built <c>CatalogFixtureLib</c> (built first, never referenced) and the profile files.</summary>
internal static class FixtureLibrary
{
    private const string OutputMetadataKey = "CatalogFixtureLibOutput";

    private const string ConsumerOutputMetadataKey = "CatalogConsumerLibOutput";

    public static string OutputDirectory { get; } = OutputOf(OutputMetadataKey);

    public static string ConsumerAssemblyPath { get; } = Path.Combine(OutputOf(ConsumerOutputMetadataKey), "CatalogConsumerLib.dll");

    public static string AssemblyPath { get; } = Path.Combine(OutputDirectory, "CatalogFixtureLib.dll");

    public static string DocumentationPath { get; } = Path.Combine(OutputDirectory, "CatalogFixtureLib.xml");

    public static string ProfilePath(string fileName) => Path.Combine(AppContext.BaseDirectory, "Profiles", fileName);

    private static string OutputOf(string key) =>
        typeof(FixtureLibrary).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where(a => a.Key == key)
            .Select(a => a.Value)
            .FirstOrDefault(v => !string.IsNullOrEmpty(v))
        ?? throw new InvalidOperationException($"The test project does not record {key}.");
}
