using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.References;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Testing.Ui.Dialogs;
using NetPrints.Testing.Ui.References;

namespace NetPrints.Editor.UITests.Dialogs;

public class DialogTests
{
    private static readonly TypeSpecifier[] Types = [TypeSpecifier.FromType<object>(), TypeSpecifier.FromType<string>(), TypeSpecifier.FromType<int>()];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task SelectTypeDefaultsToObjectAndResolvesText()
    {
        using var ui = HeadlessUi.Create();
        var dialog = ui.Show(new SelectTypeDialog(Types, TypeSpecifier.FromType<object>()));
        var page = new SelectTypeDialogPage(ui.Driver);

        Assert.Equal(TypeSpecifier.FromType<object>(), dialog.ResolveSelection()); // PAR-58
        Assert.Equal("System.Object", await page.TypeBox.TextAsync(Token));

        await page.TypeBox.ClickAsync(Token);
        await ui.Driver.PressAsync("Ctrl+A", Token);
        await ui.Driver.TypeAsync("System.String", Token);
        Assert.Equal(TypeSpecifier.FromType<string>(), dialog.ResolveSelection()); // editable chooser
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task SelectMethodPreselectsFirst()
    {
        var stringType = TypeSpecifier.FromType<string>();
        MethodSpecifier[] methods =
        [
            new("Trim", [], [stringType], MethodModifiers.None, MemberVisibility.Public, stringType, []),
            new("ToUpper", [], [stringType], MethodModifiers.None, MemberVisibility.Public, stringType, []),
        ];
        using var ui = HeadlessUi.Create();
        ui.Show(new SelectMethodDialog(methods));
        var page = new SelectMethodDialogPage(ui.Driver);

        Assert.Equal(methods[0].ToString(), await page.MethodBox.PropertyAsync(AutomationPropertyNames.SelectedItem, Token)); // PAR-59
        Assert.True(await page.SelectButton.IsEnabledAsync(Token));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ErrorDialogShowsCopyableMessage()
    {
        using var ui = HeadlessUi.Create();
        var dialog = ui.Show(new ErrorDialog("Failed", "details\nline 2"));
        var page = new ErrorDialogPage(ui.Driver);
        bool closed = false;
        dialog.Closed += (_, _) => closed = true;

        Assert.Equal("Failed", await page.TextAsync(Token));
        Assert.Equal("True", await page.Message.PropertyAsync(AutomationPropertyNames.IsReadOnly, Token));
        Assert.Equal("details\nline 2", await page.Message.TextAsync(Token));
        await page.OkButton.ClickAsync(Token);
        Assert.True(closed);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ReferencesDialogListsAndCloses()
    {
        var snapshot = new NetPrints.Projects.ProjectSnapshot("/tmp/P.csproj", "P", "N", "P",
            BinaryType.SharedLibrary, "net10.0", DefaultProjectProfile.ProfileId, true, [], [], [],
            [
                new NetPrints.Projects.ProjectReferenceInfo(NetPrints.Projects.DeclaredReferenceKind.Assembly, "System.dll", null, true, true),
                new NetPrints.Projects.ProjectReferenceInfo(NetPrints.Projects.DeclaredReferenceKind.Assembly, "System.Core", null, true, true),
                new NetPrints.Projects.ProjectReferenceInfo(NetPrints.Projects.DeclaredReferenceKind.Assembly, "mscorlib", null, true, true),
                new NetPrints.Projects.ProjectReferenceInfo(NetPrints.Projects.DeclaredReferenceKind.SourceDirectory, "/tmp/src", null, false, true),
            ],
            [], "{}", new Dictionary<string, string>(), []);
        var project = Project.FromSnapshot(snapshot);
        var dispatcher = new NetPrints.Editor.Hosting.Avalonia.AvaloniaUiDispatcher();
        var noSdkProjects = new NoSdkProjectSystem();
        var extensions = new NetPrints.Extensibility.Loading.ExtensionHost(NetPrints.Extensibility.Loading.ExtensionLoaderOptions.BuiltInOnly, NullLoggerFactory.Instance);
        var context = new EditorContext(new QueuedFilePicker(), new RecordingDialogs(), new NoClipboard(), dispatcher,
            new ReflectionHost(dispatcher, extensions, NullLogger<ReflectionHost>.Instance),
            new NetPrints.Editor.Hosting.Avalonia.WindowService(), new CapturingProcessLauncher(),
            System.Reactive.Concurrency.DefaultScheduler.Instance, System.Reactive.Concurrency.DefaultScheduler.Instance,
            () => new CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger(), NullLoggerFactory.Instance,
            noSdkProjects, TestPersistence.Create(noSdkProjects),
            extensions,
            NetPrints.Extensibility.Hosting.NullHostChannel.Instance,
            new NetPrints.Extensibility.Settings.JsonFileSettingsStore(Path.Combine(Path.GetTempPath(), "netprints-unused", "settings.json"),
                NullLogger<NetPrints.Extensibility.Settings.JsonFileSettingsStore>.Instance));
        using var ui = HeadlessUi.Create();
        ui.Show(new ReferencesDialog { DataContext = new ReferenceListVM(project, context) });
        var page = new ReferencesDialogPage(ui.Driver);

        var rows = await page.RowNamesAsync(Token);
        Assert.Equal(4, rows.Count); // PAR-16
        int enabled = 0;
        foreach (string row in rows)
        {
            enabled += await page.IncludeSwitch(row).IsEnabledAsync(Token) ? 1 : 0;
        }

        Assert.Equal(1, enabled); // PAR-19
        Assert.Contains(rows, r => r.Contains("System.dll"));
        await page.CloseAsync(Token); // PAR-21
    }

    private sealed class NoClipboard : IClipboardService
    {
        public Task SetTextAsync(string text) => Task.CompletedTask;
    }

    /// <summary>A real, JSON-backed persistence over a project system this test never calls.</summary>
    private static class TestPersistence
    {
        public static NetPrints.Serialization.ProjectPersistence Create(NetPrints.Projects.IProjectSystem projects)
        {
            var nodeConverters = new NetPrints.Serialization.Mapping.NodeDocumentConverterRegistry(NetPrints.Serialization.Mapping.NodeDocumentConverterRegistry.BuiltIn, []);
            var mapper = new NetPrints.Serialization.Mapping.DocumentMapper(nodeConverters);
            var formats = new NetPrints.Serialization.DocumentFormatRegistry([
                new NetPrints.Serialization.Json.JsonDocumentFormat(
                    new NetPrints.Serialization.Json.NetPrintsJsonOptions(nodeConverters),
                    new NetPrints.Serialization.Migrations.DocumentMigrator([]))]);
            return new NetPrints.Serialization.ProjectPersistence(projects, formats, mapper,
                directory => new NetPrints.Serialization.Stores.FileSystemDocumentStore(directory,
                    System.Reactive.Concurrency.DefaultScheduler.Instance, NullLogger<NetPrints.Serialization.Stores.FileSystemDocumentStore>.Instance),
                NullLogger<NetPrints.Serialization.ProjectPersistence>.Instance);
        }
    }
}
