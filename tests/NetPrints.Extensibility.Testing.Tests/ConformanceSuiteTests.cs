namespace NetPrints.Extensibility.Testing.Tests;

/// <summary>FR-076, FR-078, SC-011: each conformance check passes a good extension and fails its fixture with a message naming the code, the rule and the item.</summary>
public sealed class ConformanceSuiteTests
{
    public static TheoryData<string, string, string, string, string> FailingFixtures => new()
    {
        { NpkDiagnosticCodes.Manifest, "Fx.Fail.Manifest", "fx.fail.manifest", "an API version the host implements", "netprintsApi 9.0" },
        { NpkDiagnosticCodes.Packaging, "Fx.Fail.Packaging", "fx.fail.packaging", "no copy of a host assembly", "NetPrints.Core.dll" },
        { NpkDiagnosticCodes.TypeIdentity, "Fx.Fail.TypeIdentity", "fx.fail.typeidentity", "host's assemblies", "NetPrints.Generation" },
        { NpkDiagnosticCodes.Registration, "Fx.Fail.Registration", "fx.fail.registration", "registering twice", "FxFailRegistration" },
        { NpkDiagnosticCodes.RegistryIssues, "Fx.Fail.RegistryIssues", "fx.fail.registryissues", "without registry issues", "fx.fail.registryissues/Ping" },
        { NpkDiagnosticCodes.NodeRoundTrip, "Fx.Fail.RoundTrip", "fx.fail.roundtrip", "saved and loaded unchanged", "fx.fail.roundtrip/Ping" },
        { NpkDiagnosticCodes.TranslationCompiles, "Fx.Fail.Translation", "fx.fail.translation", "generated C# compiles", "fx.fail.translation/Ping" },
        { NpkDiagnosticCodes.DeterministicEmitters, "Fx.Fail.Determinism", "fx.fail.determinism", "two translations", "fx.fail.determinism/Ping" },
    };

    private static ConformanceOptions OptionsFor(string folder) => new()
    {
        ExtensionFolder = folder,
        HarnessFactory = token => ExtensionHarness.CreateAsync([folder], [], token),
    };

    [Fact]
    public async Task AGoodExtensionPassesEveryCheck()
    {
        string folder = KitFixtures.AlphaFolder();
        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync([folder], [], TestContext.Current.CancellationToken);

        ConformanceReport report = await ConformanceSuite.RunAsync(harness, OptionsFor(folder), TestContext.Current.CancellationToken);

        Assert.Equal(NpkDiagnosticCodes.All, report.Results.Select(result => result.Code));
        Assert.All(report.Results, result => Assert.True(result.Status == ConformanceStatus.Passed, report.Format()));
        Assert.True(report.Passed);
    }

    [Fact]
    public async Task ChecksThatNeedAFolderOrAFactoryAreSkippedWithoutThem()
    {
        string folder = KitFixtures.AlphaFolder();
        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync([folder], [], TestContext.Current.CancellationToken);

        ConformanceReport report = await ConformanceSuite.RunAsync(harness, new ConformanceOptions(), TestContext.Current.CancellationToken);

        foreach (string code in new[] { NpkDiagnosticCodes.Manifest, NpkDiagnosticCodes.Packaging, NpkDiagnosticCodes.TypeIdentity, NpkDiagnosticCodes.Registration })
        {
            Assert.Equal(ConformanceStatus.Skipped, report.Find(code)?.Status);
        }

        Assert.True(report.Passed, report.Format());
    }

    [Theory]
    [MemberData(nameof(FailingFixtures))]
    public async Task EachCheckFailsItsFixtureNamingTheCodeTheRuleAndTheItem(string code, string project, string id, string rule, string item)
    {
        string folder = KitFixtures.FailFolder(project, id);
        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync([folder], [], TestContext.Current.CancellationToken);

        ConformanceCheckResult result = await ConformanceSuite.RunCheckAsync(harness, OptionsFor(folder), code, TestContext.Current.CancellationToken);

        Assert.Equal(ConformanceStatus.Failed, result.Status);
        string message = string.Join('\n', result.Findings.Select(finding => finding.ToString()));
        Assert.Contains(code, message, StringComparison.Ordinal);
        Assert.Contains(rule, message, StringComparison.Ordinal);
        Assert.Contains(item, message, StringComparison.Ordinal);
    }
}
