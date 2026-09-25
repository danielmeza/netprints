using Avalonia.Headless.XUnit;
using NetPrints.Core;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Hosting;
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

        Assert.Equal(methods[0].ToString(), await page.MethodBox.PropertyAsync("SelectedItem", Token)); // PAR-59
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
        Assert.Equal("True", await page.Message.PropertyAsync("IsReadOnly", Token));
        Assert.Equal("details\nline 2", await page.Message.TextAsync(Token));
        await page.OkButton.ClickAsync(Token);
        Assert.True(closed);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ReferencesDialogListsAndCloses()
    {
        var project = Project.CreateNew("P", "N");
        project.References.Add(new SourceDirectoryReference("/tmp/src"));
        var dispatcher = new NetPrints.Editor.Hosting.Avalonia.AvaloniaUiDispatcher();
        var context = new EditorContext(new QueuedFilePicker(), new RecordingDialogs(), new NoClipboard(), dispatcher, new ReflectionHost(dispatcher),
            new NetPrints.Editor.Hosting.Avalonia.WindowService(), new CapturingProcessLauncher(),
            System.Reactive.Concurrency.DefaultScheduler.Instance, System.Reactive.Concurrency.DefaultScheduler.Instance,
            () => new CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger());
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
}
