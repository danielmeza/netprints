using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NetPrints.Catalog.Tests.Generator;

/// <summary>The fixture library as the generator sees it: its sources (own-source pipeline) or its built assembly (referenced-assembly pipeline).</summary>
internal static class GeneratorFixtures
{
    public const string FixtureAssemblyName = "CatalogFixtureLib";

    public const string FlagsProfileFile = "fixture-flags.npprofile.json";

    public const string AttributeCopyFile = "AnnotationAttributes.cs";

    private const string VersionSource = "[assembly: System.Reflection.AssemblyVersion(\"1.0.0.0\")]";

    public static IReadOnlyList<string> FixtureSources() =>
    [
        VersionSource,
        .. Directory
            .EnumerateFiles(Path.Combine(TestPaths.RepositoryRoot(), "tests", "Fixtures", "Catalog", FixtureAssemblyName), "*.cs", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path) != AttributeCopyFile)
            .Order(System.StringComparer.Ordinal)
            .Select(File.ReadAllText),
    ];

    public static CSharpCompilation CompileFixtureSources() =>
        GeneratorTestHost.Compile(FixtureAssemblyName, FixtureSources(), LanguageVersion.Latest, FixtureCatalog.FrameworkReferences());

    public static CSharpCompilation CompileConsumer(string source, LanguageVersion languageVersion = LanguageVersion.Latest) =>
        GeneratorTestHost.Compile(
            "Consumer",
            [source],
            languageVersion,
            [.. FixtureCatalog.FrameworkReferences(), MetadataReference.CreateFromFile(FixtureLibrary.AssemblyPath)]);

    public static GeneratorInputs FixtureInputs(string? rootNamespace = "Consumer") =>
        new GeneratorInputs { RootNamespace = rootNamespace }
            .AddFile(Path.Combine("profiles", FlagsProfileFile), File.ReadAllText(FixtureLibrary.ProfilePath(FlagsProfileFile)))
            .AddDocumentation(FixtureLibrary.DocumentationPath, File.ReadAllText(FixtureLibrary.DocumentationPath));
}
