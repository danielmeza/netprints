using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Main;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>ED-T11 at view-model level: extension load failures are listed once and the editor stays usable.</summary>
public class ExtensionFailureReportTests : IDisposable
{
    private readonly TestEditor testEditor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly List<string> cleanup = [];

    public void Dispose() => cleanup.ForEach(TestPaths.TryDelete);

    private ExtensionHost HostWithBrokenExtension()
    {
        string folder = TestPaths.CreateTempDirectory();
        cleanup.Add(folder);
        File.WriteAllText(Path.Combine(folder, ExtensionManifest.FileName), "{ this is not a manifest");
        return new ExtensionHost(new ExtensionLoaderOptions([], [folder], [BuiltInExtension.InProcessEntry]), NullLoggerFactory.Instance);
    }

    [Fact]
    public async Task FailuresAreListedInOneDialogAndNotRepeated()
    {
        await using ExtensionHost extensions = HostWithBrokenExtension();
        var vm = new MainEditorViewModel(testEditor.Context with { Extensions = extensions });

        await vm.ReportExtensionFailuresAsync();
        await vm.ReportExtensionFailuresAsync();

        (string title, IReadOnlyList<CodeDiagnostic> issues) = Assert.Single(testEditor.Dialogs.IssueDialogs);
        Assert.Equal("Extensions failed to load", title);
        CodeDiagnostic issue = Assert.Single(issues);
        Assert.Equal(ExtensionDiagnosticCodes.InvalidManifest, issue.Id);
        Assert.Equal(CodeDiagnosticSeverity.Error, issue.Severity);
        Assert.Contains("was not loaded", issue.Message);
        Assert.NotNull(issue.SourcePath);
    }

    [Fact]
    public async Task NoFailuresShowNoDialog()
    {
        var vm = new MainEditorViewModel(testEditor.Context);

        await vm.ReportExtensionFailuresAsync();

        Assert.Empty(testEditor.Dialogs.IssueDialogs);
    }

    private static ExtensionManifest Manifest(string id, params string[] dependsOn) =>
        new(id, id, "1.0.0", string.Empty, "1.0", dependsOn);

    private ExtensionHost HostWithTestExtensionAnd(string failure)
    {
        string folder = TestPaths.CreateTempDirectory();
        cleanup.Add(folder);
        List<string> folders = [TestExtensionFolder.Folder];
        List<(ExtensionManifest, INetPrintsExtension)> inProcess = [BuiltInExtension.InProcessEntry];

        switch (failure)
        {
            case ExtensionDiagnosticCodes.InvalidManifest:
                File.WriteAllText(Path.Combine(folder, ExtensionManifest.FileName), "{ this is not a manifest");
                folders.Add(folder);
                break;
            case ExtensionDiagnosticCodes.ApiVersion:
                File.WriteAllText(Path.Combine(folder, ExtensionManifest.FileName),
                    """{ "id": "bad.api", "name": "Bad API", "version": "1.0.0", "assembly": "bad.dll", "netprintsApi": "2.0", "dependsOn": [] }""");
                folders.Add(folder);
                break;
            case ExtensionDiagnosticCodes.Dependency:
                inProcess.Add((Manifest("bad.dependency", "absent.extension"), new EmptyExtension()));
                break;
            case ExtensionDiagnosticCodes.DuplicateId:
                inProcess.Add((Manifest("dup.id"), new EmptyExtension()));
                inProcess.Add((Manifest("dup.id"), new EmptyExtension()));
                break;
            case ExtensionDiagnosticCodes.RegisterFailed:
                inProcess.Add((Manifest("bad.register"), new ThrowingExtension()));
                break;
            case ExtensionDiagnosticCodes.ContributionRejected:
                inProcess.Add((Manifest("bad.contribution"), new DefaultProfileExtension()));
                break;
            case ExtensionDiagnosticCodes.AssemblyLoadFailed:
                File.WriteAllText(Path.Combine(folder, ExtensionManifest.FileName),
                    """{ "id": "bad.assembly", "name": "Bad assembly", "version": "1.0.0", "assembly": "missing.dll", "netprintsApi": "1.0", "dependsOn": [] }""");
                folders.Add(folder);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(failure), failure, null);
        }

        return new ExtensionHost(new ExtensionLoaderOptions([], folders, inProcess), NullLoggerFactory.Instance);
    }

    [Theory]
    [InlineData(ExtensionDiagnosticCodes.InvalidManifest)]
    [InlineData(ExtensionDiagnosticCodes.ApiVersion)]
    [InlineData(ExtensionDiagnosticCodes.Dependency)]
    [InlineData(ExtensionDiagnosticCodes.DuplicateId)]
    [InlineData(ExtensionDiagnosticCodes.RegisterFailed)]
    [InlineData(ExtensionDiagnosticCodes.ContributionRejected)]
    [InlineData(ExtensionDiagnosticCodes.AssemblyLoadFailed)]
    public async Task EachLoadFailureIsOneDialogRowAndTheEditorAndTheTestExtensionStayUsable(string code) // SC-004
    {
        await using ExtensionHost extensions = HostWithTestExtensionAnd(code);
        var editor = TestEditor.Create(TestEditor.CreateReflectionHost, extensions);
        var vm = new MainEditorViewModel(editor.Context);

        await vm.ReportExtensionFailuresAsync();
        await vm.ReportExtensionFailuresAsync();

        (string title, IReadOnlyList<CodeDiagnostic> issues) = Assert.Single(editor.Dialogs.IssueDialogs);
        Assert.Equal("Extensions failed to load", title);
        CodeDiagnostic row = Assert.Single(issues);
        Assert.Equal(code, row.Id);
        Assert.Equal(CodeDiagnosticSeverity.Error, row.Severity);
        Assert.Contains(extensions.Current.Loaded, m => m.Id == "netprints.test");
        Assert.Contains(extensions.Current.NodeKinds, k => k.Kind == "netprints.test/Log");

        string csproj = TestPaths.CopyHelloWorldSample();
        cleanup.Add(Path.GetDirectoryName(csproj) ?? csproj);
        await vm.LoadProjectAsync(csproj);

        Assert.NotNull(vm.Project);
        Assert.Empty(editor.Dialogs.Errors);
        await vm.NewClassCommand.ExecuteAsync(null);
        Assert.Equal("MyClass", vm.Project?.Classes[^1].Name);
    }

    private sealed class EmptyExtension : INetPrintsExtension
    {
        public void Register(IExtensionBuilder builder)
        {
        }
    }

    private sealed class ThrowingExtension : INetPrintsExtension
    {
        public void Register(IExtensionBuilder builder) => throw new InvalidOperationException("boom");
    }

    private sealed class DefaultProfileExtension : INetPrintsExtension
    {
        public void Register(IExtensionBuilder builder) => builder.AddProjectProfile(DefaultProjectProfile.Instance);
    }
}
