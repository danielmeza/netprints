using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Extensibility.Settings;
using NetPrints.Extensibility.Testing.Xunit;
using NetPrints.Graph;
using NetPrints.Projects;
using NetPrints.TestExtension;
using NetPrints.Testing;

namespace NetPrints.Extensibility.Testing.Tests.TestExtensions;

/// <summary>US10 scenario 4: the real test extension passes every conformance check, built folder and in-process.</summary>
public sealed class TestExtensionConformanceTests : ConformanceTests<NetPrints.TestExtension.TestExtension>
{
    /// <inheritdoc />
    protected override string? ExtensionFolder => TestExtensionPaths.BuildOutput;

    /// <inheritdoc />
    protected override string? SourceDirectory => TestExtensionPaths.Sources;
}

/// <summary>What the test extension contributes, checked through the kit only (SC-004, EX-T04 and EX-T09 with the real asset).</summary>
public sealed class TestExtensionTests
{
    [Fact]
    public async Task TheTestEmittersMakeAPartialClassItsPropertiesPartialAndLeaveOtherClassesAlone()
    {
        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync([TestExtensionPaths.BuildOutput], [], TestContext.Current.CancellationToken);
        Project project = Project.FromSnapshot(new ProjectSnapshot(
            Path.Combine(Path.GetTempPath(), "PsEm.csproj"), "PsEm", "PsEm", "PsEm", BinaryType.SharedLibrary, "net10.0", DefaultProjectProfile.ProfileId,
            ReferencesNetPrintsSdk: true, GraphFiles: [], ExtensionFolders: [], References: [], DeclaredReferences: [], OtherSources: [],
            CompilationOptionsJson: "{}", Properties: new Dictionary<string, string>(), Messages: []));
        ClassGraph Class(string name)
        {
            var cls = new ClassGraph { Name = name, Namespace = "PsEm", Visibility = MemberVisibility.Public, Project = project };
            project.Classes.Add(cls);
            cls.Variables.Add(new Variable(cls, "Size", TypeSpecifier.FromType<int>(), new MethodGraph("get_Size") { Class = cls }, null, VariableModifiers.None));
            return cls;
        }

        string partial = Regex.Replace(harness.Translate(Class("PartialWidget")), @"\s+", " ");
        string plain = Regex.Replace(harness.Translate(Class("Ordinary")), @"\s+", " ");

        Assert.Contains("using System.Linq;", partial, StringComparison.Ordinal);
        Assert.Contains("[System.Obsolete(\"test\")] public partial class PartialWidget", partial, StringComparison.Ordinal);
        Assert.Contains("private partial System.Int32 Size { get; }", partial, StringComparison.Ordinal);
        Assert.Contains("[System.Obsolete(\"test\")] public class Ordinary", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("partial", plain, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheSettingsSectionIsLoadedDefaultsRoundTripsAndSurvivesARestart()
    {
        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync([TestExtensionPaths.BuildOutput], [], TestContext.Current.CancellationToken);
        Assert.Single(harness.Registry.Settings, section => section.ExtensionId == NetPrints.TestExtension.TestExtension.Id);
        string directory = Directory.CreateTempSubdirectory("netprints-settings-").FullName;
        try
        {
            string file = Path.Combine(directory, "settings.json");
            Assert.Equal("hello", new JsonFileSettingsStore(file, NullLogger<JsonFileSettingsStore>.Instance).Get(TestSettings.Descriptor).Greeting);

            await new JsonFileSettingsStore(file, NullLogger<JsonFileSettingsStore>.Instance).SetAsync(TestSettings.Descriptor, new TestSettings("world"), TestContext.Current.CancellationToken);

            Assert.Equal("world", new JsonFileSettingsStore(file, NullLogger<JsonFileSettingsStore>.Instance).Get(TestSettings.Descriptor).Greeting);
            Assert.Contains("\"netprints.test\"", await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

/// <summary>Where the built test extension and its sources are.</summary>
internal static class TestExtensionPaths
{
    public static string Sources { get; } = Path.Combine(LocalSdkLayout.FindRepositoryRoot(), "tests", "NetPrints.TestExtension");

    public static string BuildOutput { get; } = Path.Combine(Sources, "bin", LocalSdkLayout.DetectConfiguration(), "extensions", NetPrints.TestExtension.TestExtension.Id);
}
