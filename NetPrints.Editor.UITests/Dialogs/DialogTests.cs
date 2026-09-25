using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using NetPrints.Core;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.References;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Dialogs;

public class DialogTests
{
    private static readonly TypeSpecifier[] Types = [TypeSpecifier.FromType<object>(), TypeSpecifier.FromType<string>(), TypeSpecifier.FromType<int>()];

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task SelectTypeDefaultsToObjectAndResolvesText()
    {
        var dialog = new SelectTypeDialog(Types, TypeSpecifier.FromType<object>());
        dialog.Show();
        HeadlessInput.Pump();
        var page = new SelectTypeDialogPage(dialog);

        Assert.Equal(TypeSpecifier.FromType<object>(), dialog.ResolveSelection()); // PAR-58

        page.TypeBox.SelectedItem = null;
        page.TypeBox.Text = "System.String";
        Assert.Equal(TypeSpecifier.FromType<string>(), dialog.ResolveSelection()); // editable chooser
        dialog.Close();
        await Task.CompletedTask;
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
        var dialog = new SelectMethodDialog(methods);
        dialog.Show();
        HeadlessInput.Pump();

        Assert.Equal(methods[0], new SelectMethodDialogPage(dialog).MethodBox.SelectedItem); // PAR-59
        Assert.NotNull(dialog.CaptureRenderedFrame());
        dialog.Close();
        await Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ErrorDialogShowsCopyableMessage()
    {
        var dialog = new ErrorDialog("Failed", "details\nline 2");
        dialog.Show();
        HeadlessInput.Pump();
        var page = new ErrorDialogPage(dialog);
        bool closed = false;
        dialog.Closed += (_, _) => closed = true;

        Assert.Equal("Failed", dialog.Title);
        Assert.True(page.Message.IsReadOnly);
        Assert.Equal("details\nline 2", page.Message.Text);
        page.ClickOk();
        Assert.True(closed);
        await Task.CompletedTask;
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ReferencesDialogListsAndCloses()
    {
        var project = Project.CreateNew("P", "N");
        project.References.Add(new SourceDirectoryReference("/tmp/src"));
        var context = new EditorContext(new NoFilePicker(), new RecordingDialogs(), new NoClipboard(),
            new NetPrints.Editor.Hosting.Avalonia.AvaloniaUiDispatcher(), new ReflectionHost(new NetPrints.Editor.Hosting.Avalonia.AvaloniaUiDispatcher()),
            new NetPrints.Editor.Hosting.Avalonia.WindowService(), new RecordingProcessLauncher());
        var dialog = new ReferencesDialog { DataContext = new ReferenceListVM(project, context) };
        dialog.Show();
        HeadlessInput.Pump();
        var page = new ReferencesDialogPage(dialog);
        bool closed = false;
        dialog.Closed += (_, _) => closed = true;

        Assert.Equal(4, page.IncludeSwitches.Count); // PAR-16
        Assert.Single(page.IncludeSwitches, s => s.IsEffectivelyEnabled); // PAR-19
        Assert.Contains(page.RowTexts, t => t.Contains("System.dll"));
        page.ClickClose(); // PAR-21
        Assert.True(closed);
        await Task.CompletedTask;
    }

    private sealed class NoFilePicker : IFilePickerService
    {
        public Task<string?> OpenFileAsync(string title, IReadOnlyList<FileFilter> filters) => Task.FromResult<string?>(null);
        public Task<string?> SaveFileAsync(string title, string suggestedName, string defaultExtension, IReadOnlyList<FileFilter> filters) => Task.FromResult<string?>(null);
        public Task<string?> OpenFolderAsync(string title) => Task.FromResult<string?>(null);
    }

    private sealed class NoClipboard : IClipboardService
    {
        public Task SetTextAsync(string text) => Task.CompletedTask;
    }
}
