using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Compilation;
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
        using ExtensionHost extensions = HostWithBrokenExtension();
        var vm = new MainEditorVM(testEditor.Context with { Extensions = extensions });

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
        var vm = new MainEditorVM(testEditor.Context);

        await vm.ReportExtensionFailuresAsync();

        Assert.Empty(testEditor.Dialogs.IssueDialogs);
    }
}
