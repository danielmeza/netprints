using NetPrints.Editor;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.ClassEditor;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.References;

namespace NetPrints.Testing.Ui.Main;

/// <summary>Screen object of the main window.</summary>
public sealed class MainWindowPage(IUiDriver driver) : UiElement(driver, new AutomationQuery(AutomationIds.MainWindow))
{
    public UiElement ProjectButton => Find(AutomationIds.MainProjectButton);
    public UiElement ReferencesButton => Find(AutomationIds.MainReferencesButton);
    public UiElement SettingsButton => Find(AutomationIds.MainSettingsButton);
    public UiElement CompileButton => Find(AutomationIds.MainCompileButton);
    public UiElement RunButton => Find(AutomationIds.MainRunButton);
    public UiElement ProjectPane => Find(AutomationIds.MainProjectPane);
    public UiElement SettingsPane => Find(AutomationIds.MainSettingsPane);
    public UiElement CreateProjectButton => Find(AutomationIds.MainCreateProjectButton);
    public UiElement OpenProjectButton => Find(AutomationIds.MainOpenProjectButton);
    public UiElement SaveProjectButton => Find(AutomationIds.MainSaveProjectButton);
    public UiElement NewClassButton => Find(AutomationIds.MainNewClassButton);
    public UiElement ExistingClassButton => Find(AutomationIds.MainExistingClassButton);
    public UiElement OutputChooser => Find(AutomationIds.MainOutputChooser);
    public UiElement BinaryTypeChooser => Find(AutomationIds.MainBinaryTypeChooser);
    public UiElement BusyOverlay => Find(AutomationIds.MainBusyOverlay);

    /// <summary>The toolbar's round buttons, in order.</summary>
    public IReadOnlyList<UiElement> ToolbarButtons => [ProjectButton, ReferencesButton, SettingsButton, CompileButton, RunButton];

    public UiElement ClassButton(string fullName) => Find(AutomationIds.MainOpenClassButton, text: fullName);

    public UiElement RemoveClassButton(string fullName) => Find(AutomationIds.MainRemoveClassButton, name: fullName);

    public async Task<string?> TitleAsync(CancellationToken cancellationToken) => await TextAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> ClassNamesAsync(CancellationToken cancellationToken) =>
        (await Driver.FindAllAsync(new AutomationQuery(AutomationIds.MainOpenClassButton) { Within = Query }, cancellationToken))
            .Select(e => e.Text ?? "").ToList();

    /// <summary>Waits until a project with the given title is open and loaded.</summary>
    public async Task<MainWindowPage> WaitForProjectAsync(string title, CancellationToken cancellationToken)
    {
        await WaitUntilAsync(e => e.Text == title, $"title '{title}'", cancellationToken, TimeSpan.FromSeconds(60));
        await BusyOverlay.WaitHiddenAsync(cancellationToken, TimeSpan.FromSeconds(60));
        return this;
    }

    public async Task<MainWindowPage> ShowProjectPaneAsync(CancellationToken cancellationToken)
    {
        if (!await ProjectPane.IsVisibleAsync(cancellationToken))
        {
            await ProjectButton.ClickAsync(cancellationToken);
            await ProjectPane.WaitVisibleAsync(cancellationToken);
        }

        return this;
    }

    public async Task<MainWindowPage> ShowSettingsPaneAsync(CancellationToken cancellationToken)
    {
        if (!await SettingsPane.IsVisibleAsync(cancellationToken))
        {
            await SettingsButton.ClickAsync(cancellationToken);
            await SettingsPane.WaitVisibleAsync(cancellationToken);
        }

        return this;
    }

    /// <summary>Clicks a class in the class list and returns its editor window.</summary>
    public async Task<ClassEditorPage> OpenClassAsync(string fullName, CancellationToken cancellationToken)
    {
        await ClassButton(fullName).ClickAsync(cancellationToken);
        var page = new ClassEditorPage(Driver, fullName);
        await page.GetAsync(cancellationToken);
        return page;
    }

    public async Task<ReferencesDialogPage> OpenReferencesAsync(CancellationToken cancellationToken)
    {
        await ReferencesButton.ClickAsync(cancellationToken);
        var page = new ReferencesDialogPage(Driver);
        await page.GetAsync(cancellationToken);
        return page;
    }
}
