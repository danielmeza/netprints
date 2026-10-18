using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NetPrints.Editor.Commands.CommandPalette;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Settings;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Extensibility.Settings;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Editor.UITests.Settings;

/// <summary>US5 scenarios 1, 3, 4 and 5: the Settings document opens from every entry point, searches, validates and asks for a restart.</summary>
public class SettingsDocumentTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static IReadOnlyList<Control> Find(ShellApp app, string automationId, string? name = null) =>
        [.. app.Ui.Tree.FindControls(new AutomationQuery(automationId) { Name = name }).Select(pair => pair.Control)];

    private static IReadOnlyList<Control> Visible(ShellApp app, string automationId) =>
        [.. Find(app, automationId).Where(control => control.IsEffectivelyVisible)];

    private static async Task<T> OpenAsync<T>(ShellApp app, string commandName, Func<T> read)
    {
        Assert.True(app.Commands.TryRun(commandName));
        HeadlessDriver.Pump();
        await UiWait.UntilAsync(app.Driver, () => Task.FromResult(app.Api.OpenDocuments.Contains(DocumentId.Settings)), "settings open", Token);
        HeadlessDriver.Pump();
        return read();
    }

    private static SettingsDocumentViewModel Document(ShellApp app) =>
        Assert.IsType<SettingsDocumentViewModel>(app.Shell.FindDocument(DocumentId.Settings));

    private static async Task CommitAsync(ShellApp app, TextBox box, string text)
    {
        box.Focus();
        box.Text = text;
        HeadlessDriver.Pump();
        await app.Driver.PressAsync("Enter", Token);
        HeadlessDriver.Pump();
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheOpenSettingsCommandOpensOneDocumentAndReopeningActivatesIt()
    {
        await using ShellApp app = ShellApp.Start();

        await OpenAsync(app, "openSettings", () => 0);
        Assert.Single(app.Api.OpenDocuments, id => id == DocumentId.Settings);
        Assert.Equal(DocumentId.Settings, app.Api.ActiveDocument);

        Assert.True(app.Commands.TryRun("openSettings"));
        HeadlessDriver.Pump();

        Assert.Single(app.Api.OpenDocuments, id => id == DocumentId.Settings);
        Assert.Equal(DocumentId.Settings, app.Api.ActiveDocument);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task CtrlCommaOpensTheDocument()
    {
        await using ShellApp app = ShellApp.Start();

        await app.Driver.PressAsync("Ctrl+OemComma", Token);
        HeadlessDriver.Pump();

        Assert.Single(app.Api.OpenDocuments, id => id == DocumentId.Settings);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheCommandIsInTheFileMenuAndThePaletteAndRunsWithoutAProject()
    {
        await using ShellApp app = ShellApp.Start();
        CommandDescriptor command = app.Command("openSettings");

        Assert.Equal("File", command.Menu?.Path);
        PaletteItem item = Assert.Single(new CommandPaletteViewModel(app.Registry, app.Commands).Items, candidate => candidate.Command.Id == command.Id);
        Assert.True(item.IsEnabled);
        Assert.Null(app.Shell.Session);
        await OpenAsync(app, "openSettings", () => 0);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ThePageListShowsThePagesInOrderAndSelectingOneShowsItsFields()
    {
        await using ShellApp app = ShellApp.Start();
        SettingsDocumentViewModel document = await OpenAsync(app, "openSettings", () => Document(app));

        Assert.Equal(["General", "Appearance", "Keyboard shortcuts", "Run", "Extensions"], Find(app, AutomationIds.SettingsPageItem).Select(Avalonia.Automation.AutomationProperties.GetName));
        Assert.Single(Find(app, AutomationIds.SettingsFieldInput, "Language"));

        document.SelectedPage = document.Pages.Single(page => page.Title == "Run");
        HeadlessDriver.Pump();

        Assert.Single(Find(app, AutomationIds.SettingsFieldInput, "Stop timeout seconds"));
        Assert.Empty(Find(app, AutomationIds.SettingsFieldInput, "Language"));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task SearchHidesPagesWithoutAMatch()
    {
        await using ShellApp app = ShellApp.Start();
        await OpenAsync(app, "openSettings", () => 0);
        var search = Assert.IsType<TextBox>(Assert.Single(Find(app, AutomationIds.SettingsSearch)));

        search.Text = "timeout";
        HeadlessDriver.Pump();

        Assert.Equal(["Run"], Find(app, AutomationIds.SettingsPageItem).Select(Avalonia.Automation.AutomationProperties.GetName));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AnInvalidValueShowsTheMessageUnderTheFieldAndIsNotSavedAndAValidOneIs()
    {
        await using ShellApp app = ShellApp.Start();
        SettingsDocumentViewModel document = await OpenAsync(app, "openSettings", () => Document(app));
        document.SelectedPage = document.Pages.Single(page => page.Title == "Run");
        HeadlessDriver.Pump();
        var box = Assert.IsType<TextBox>(Assert.Single(Find(app, AutomationIds.SettingsFieldInput, "Stop timeout seconds")));

        await CommitAsync(app, box, "0");

        Control error = Assert.Single(Visible(app, AutomationIds.SettingsFieldError));
        Assert.NotEmpty(Assert.IsType<TextBlock>(error).Text ?? "");
        Assert.Equal(5, app.Composition.Context.Settings.Get(RunSettings.Descriptor).StopTimeoutSeconds);

        await CommitAsync(app, box, "30");
        SettingsFieldViewModel field = document.FindField("run.stopTimeoutSeconds");
        await UiWait.UntilAsync(app.Driver, () => Task.FromResult(!field.HasError && app.Composition.Context.Settings.Get(RunSettings.Descriptor).StopTimeoutSeconds == 30), "saved", Token);
        HeadlessDriver.Pump();

        Assert.Empty(Visible(app, AutomationIds.SettingsFieldError));
        Assert.Equal(30, app.Composition.Context.Settings.Get(RunSettings.Descriptor).StopTimeoutSeconds);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task ARestartFieldShowsTheRequiresRestartNoteAndTheBarWithRestartNow()
    {
        await using ShellApp app = ShellApp.Start();
        await OpenAsync(app, "openSettings", () => 0);
        Assert.Empty(Visible(app, AutomationIds.SettingsRestartBar));
        Assert.Single(Visible(app, AutomationIds.SettingsRequiresRestart));
        var language = Assert.IsType<TextBox>(Assert.Single(Find(app, AutomationIds.SettingsFieldInput, "Language")));

        await CommitAsync(app, language, "es");
        await UiWait.UntilAsync(app.Driver, () => Task.FromResult(Document(app).RestartPending), "restart pending", Token);
        HeadlessDriver.Pump();

        Assert.Single(Visible(app, AutomationIds.SettingsRestartBar));
        Assert.Single(Visible(app, AutomationIds.SettingsRestartNow));
    }
}
