using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace NetPrints.Catalog.Tests;

/// <summary>Locates the built <c>CatalogFixtureLib</c> (built first, never referenced) and the profile files.</summary>
internal static class FixtureLibrary
{
    private const string OutputMetadataKey = "CatalogFixtureLibOutput";

    public static string OutputDirectory { get; } =
        typeof(FixtureLibrary).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Where(a => a.Key == OutputMetadataKey)
            .Select(a => a.Value)
            .FirstOrDefault(v => !string.IsNullOrEmpty(v))
        ?? throw new InvalidOperationException("The test project does not record the fixture output folder.");

    public static string AssemblyPath { get; } = Path.Combine(OutputDirectory, "CatalogFixtureLib.dll");

    public static string DocumentationPath { get; } = Path.Combine(OutputDirectory, "CatalogFixtureLib.xml");

    public static string ProfilePath(string fileName) => Path.Combine(AppContext.BaseDirectory, "Profiles", fileName);
}
