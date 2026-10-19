using Microsoft.Extensions.DependencyInjection;
using NetPrints.Core;
using NetPrints.Extensibility.Editor;
using NetPrints.Extensibility.Loading;

namespace NetPrints.Extensibility.Testing.Tests;

/// <summary>FR-076, FR-077: the rules of the checks that a failing fixture does not reach, driven with extensions made in the test.</summary>
public sealed class ConformanceRuleTests
{
    private const string Id = "test.rules";

    private static async Task<ConformanceCheckResult> RunAsync(INetPrintsExtension extension, string code, ConformanceOptions? options = null)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string api = $"{ExtensionApi.Version.Major}.{ExtensionApi.Version.Minor}";
        await using ExtensionHarness harness = await ExtensionHarness.CreateWithManifestsAsync(
            [], [(new ExtensionManifest(Id, Id, "1.0.0", Id + ".dll", api, []), extension)], cancellationToken);
        return await ConformanceSuite.RunCheckAsync(harness, options ?? new ConformanceOptions(), code, cancellationToken);
    }

    private static string Messages(ConformanceCheckResult result) => string.Join('\n', result.Findings.Select(finding => finding.ToString()));

    [Fact]
    public async Task AnEditorContributionThatFollowsTheRulesPasses()
    {
        ConformanceCheckResult result = await RunAsync(new EditorExtension(Id + ".run", "Run", Id + ".mark", [CommandGesture.Parse("Ctrl+R", null)], CommandScope.Global), NpkDiagnosticCodes.EditorContributions);

        Assert.Equal(ConformanceStatus.Passed, result.Status);
    }

    [Theory]
    [InlineData("A", CommandScope.Global, "no modifier")]
    [InlineData("Ctrl+R", CommandScope.Graph, "")]
    [InlineData("F5", CommandScope.Global, "")]
    [InlineData("A", CommandScope.Graph, "")]
    public async Task AGestureWithoutAModifierNeedsANarrowerScopeThanGlobal(string gesture, CommandScope scope, string expected)
    {
        ConformanceCheckResult result = await RunAsync(new EditorExtension(Id + ".run", "Run", null, [CommandGesture.Parse(gesture, null)], scope), NpkDiagnosticCodes.EditorContributions);

        Assert.Equal(expected.Length == 0 ? ConformanceStatus.Passed : ConformanceStatus.Failed, result.Status);
        Assert.Contains(expected, Messages(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ALabelWithoutAKeyOrADefaultTextIsReported()
    {
        ConformanceCheckResult result = await RunAsync(new EditorExtension(Id + ".run", new LocalizedText("Run.Label"), null, [], CommandScope.Global), NpkDiagnosticCodes.EditorContributions);

        Assert.Equal(ConformanceStatus.Failed, result.Status);
        Assert.Contains("Run.Label", Messages(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnIconIdOfAnotherExtensionIsReported()
    {
        ConformanceCheckResult result = await RunAsync(new EditorExtension(Id + ".run", "Run", "other.mark", [], CommandScope.Global), NpkDiagnosticCodes.EditorContributions);

        Assert.Equal(ConformanceStatus.Failed, result.Status);
        Assert.Contains("other.mark", Messages(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnIconThatNoViewSourceDefinesIsReported()
    {
        string directory = Directory.CreateTempSubdirectory("npk015").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "Icons.axaml"), "<ResourceDictionary xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"><StreamGeometry x:Key=\"Icon.test.rules.other\">M0,0</StreamGeometry></ResourceDictionary>", TestContext.Current.CancellationToken);

            ConformanceCheckResult result = await RunAsync(
                new EditorExtension(Id + ".run", "Run", Id + ".mark", [], CommandScope.Global), NpkDiagnosticCodes.EditorContributions, new ConformanceOptions { SourceDirectory = directory });

            Assert.Equal(ConformanceStatus.Failed, result.Status);
            Assert.Contains("Icon.test.rules.mark", Messages(result), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ASingletonThatCapturesAScopedServiceFailsTheProviderRule()
    {
        ConformanceCheckResult result = await RunAsync(new ServicesExtension(captive: true), NpkDiagnosticCodes.Services);

        Assert.Equal(ConformanceStatus.Failed, result.Status);
        Assert.Contains("builds, validates", Messages(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AServiceThatReplacesAHostExportFailsTheProviderRule()
    {
        ConformanceCheckResult result = await RunAsync(new ServicesExtension(captive: false), NpkDiagnosticCodes.Services);

        Assert.Equal(ConformanceStatus.Failed, result.Status);
        Assert.Contains("TimeProvider", Messages(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AScopedServiceThatIsReleasedPassesTheLeakTest()
    {
        ConformanceCheckResult result = await RunAsync(new ServicesExtension(captive: null), NpkDiagnosticCodes.Services);

        Assert.True(result.Status == ConformanceStatus.Passed, Messages(result));
    }

    [Fact]
    public async Task ViewsThatUseTokensAndThemeDictionariesPass()
    {
        string directory = Directory.CreateTempSubdirectory("npk016").FullName;
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory, "Good.axaml"),
                "<UserControl xmlns=\"https://github.com/avaloniaui\"><Border Background=\"{DynamicResource Panel.Background}\" BorderBrush=\"Transparent\"><TextBlock Classes=\"title\" FontSize=\"{DynamicResource Font.Title}\" /></Border></UserControl>",
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(directory, "Tokens.axaml"),
                "<ResourceDictionary xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"><ResourceDictionary.ThemeDictionaries><ResourceDictionary x:Key=\"Dark\"><SolidColorBrush x:Key=\"Mine\" Color=\"#202020\" /></ResourceDictionary></ResourceDictionary.ThemeDictionaries></ResourceDictionary>",
                TestContext.Current.CancellationToken);

            ConformanceCheckResult result = await RunAsync(new EditorExtension(Id + ".run", "Run", null, [], CommandScope.Global), NpkDiagnosticCodes.ContributedViews, new ConformanceOptions { SourceDirectory = directory });

            Assert.True(result.Status == ConformanceStatus.Passed, Messages(result));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ASettingsSectionThatPassesIsExercised()
    {
        string folder = KitFixtures.AlphaFolder();
        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync([folder], [], TestContext.Current.CancellationToken);

        ConformanceCheckResult result = await ConformanceSuite.RunCheckAsync(harness, new ConformanceOptions(), NpkDiagnosticCodes.Settings, TestContext.Current.CancellationToken);

        Assert.Equal(ConformanceStatus.Passed, result.Status);
        Assert.Empty(result.Note);
    }
}

/// <summary>An extension with one command.</summary>
public sealed class EditorExtension(string commandId, LocalizedText label, string? iconId, IReadOnlyList<CommandGesture> gestures, CommandScope scope) : INetPrintsExtension
{
    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Editor.AddCommand(new CommandDescriptor(commandId, label, new NoopHandler(), iconId, gestures, scope));
    }
}

/// <summary>A command that does nothing.</summary>
public sealed class NoopHandler : ICommandHandler
{
    /// <inheritdoc />
    public bool CanExecute(ICommandContext context) => true;

    /// <inheritdoc />
    public Task ExecuteAsync(ICommandContext context, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>A scoped service.</summary>
public sealed class ScopedDependency;

/// <summary>A singleton that needs a scoped service.</summary>
/// <param name="dependency">The scoped service.</param>
public sealed class CaptiveSingleton(ScopedDependency dependency)
{
    /// <summary>Gets the captured service.</summary>
    public ScopedDependency Dependency { get; } = dependency;
}

/// <summary>An extension that registers services in one of three ways.</summary>
public sealed class ServicesExtension(bool? captive) : INetPrintsExtension
{
    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddScoped<ScopedDependency>();
        switch (captive)
        {
            case true:
                builder.Services.AddSingleton<CaptiveSingleton>();
                break;
            case false:
                builder.Services.AddSingleton(TimeProvider.System);
                break;
        }
    }
}
