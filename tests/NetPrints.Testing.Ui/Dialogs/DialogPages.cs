using NetPrints.Editor;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Testing.Ui.Dialogs;

/// <summary>Screen object of the error dialog.</summary>
public sealed class ErrorDialogPage(IUiDriver driver) : UiElement(driver, new AutomationQuery(AutomationIds.ErrorDialog))
{
    public UiElement Message => Find(AutomationIds.ErrorMessage);
    public UiElement OkButton => Find(AutomationIds.ErrorOkButton);
}

/// <summary>Screen object of the type chooser dialog.</summary>
public sealed class SelectTypeDialogPage(IUiDriver driver) : UiElement(driver, new AutomationQuery(AutomationIds.SelectTypeDialog))
{
    public UiElement TypeBox => Find(AutomationIds.SelectTypeBox);
    public UiElement SelectButton => Find(AutomationIds.SelectTypeButton);
}

/// <summary>Screen object of the method chooser dialog.</summary>
public sealed class SelectMethodDialogPage(IUiDriver driver) : UiElement(driver, new AutomationQuery(AutomationIds.SelectMethodDialog))
{
    public UiElement MethodBox => Find(AutomationIds.SelectMethodBox);
    public UiElement SelectButton => Find(AutomationIds.SelectMethodButton);
}

/// <summary>Screen object of the issues dialog (at startup: the extensions that failed to load).</summary>
public sealed class IssuesDialogPage(IUiDriver driver) : UiElement(driver, new AutomationQuery(AutomationIds.IssuesDialog))
{
    public UiElement OkButton => Find(AutomationIds.IssuesOkButton);

    public async Task<IReadOnlyList<string>> RowsAsync(CancellationToken cancellationToken) =>
        (await Driver.FindAllAsync(new AutomationQuery(AutomationIds.IssueRow) { Within = Find(AutomationIds.ExtensionLoadErrors).Query }, cancellationToken))
            .Select(e => e.Name ?? "").ToList();
}

/// <summary>Screen object of the dialog that asks whether a project's extensions may load.</summary>
public sealed class TrustDialogPage(IUiDriver driver) : UiElement(driver, new AutomationQuery(AutomationIds.TrustDialog))
{
    public UiElement Prompt => Find(AutomationIds.TrustPrompt);
    public UiElement TrustButton => Find(AutomationIds.TrustButton);
    public UiElement DontLoadButton => Find(AutomationIds.TrustDontLoadButton);
}

/// <summary>Screen object of the dialog that asks what to do with unsaved files before the project is unloaded.</summary>
public sealed class UnsavedChangesDialogPage(IUiDriver driver) : UiElement(driver, new AutomationQuery(AutomationIds.UnsavedDialog))
{
    public UiElement Files => Find(AutomationIds.UnsavedFiles);
    public UiElement SaveAllButton => Find(AutomationIds.UnsavedSaveAllButton);
    public UiElement DontSaveButton => Find(AutomationIds.UnsavedDontSaveButton);
    public UiElement CancelButton => Find(AutomationIds.UnsavedCancelButton);
}

/// <summary>Screen object of the dialog that offers to restore the backed-up files of the project being opened.</summary>
public sealed class RecoverDialogPage(IUiDriver driver) : UiElement(driver, new AutomationQuery(AutomationIds.RecoverDialog))
{
    public UiElement Files => Find(AutomationIds.RecoverFiles);
    public UiElement Row(string path) => Find(AutomationIds.RecoverRow, text: path);
    public UiElement RestoreButton => Find(AutomationIds.RecoverRestoreButton);
    public UiElement DiscardButton => Find(AutomationIds.RecoverDiscardButton);
}

/// <summary>Screen object of the New project dialog.</summary>
public sealed class NewProjectDialogPage(IUiDriver driver) : UiElement(driver, new AutomationQuery(AutomationIds.NewProjectDialog))
{
    public UiElement Templates => Find(AutomationIds.NewProjectTemplates);
    public UiElement NameBox => Find(AutomationIds.NewProjectName);
    public UiElement LocationBox => Find(AutomationIds.NewProjectLocation);
    public UiElement Preview => Find(AutomationIds.NewProjectPreview);
    public UiElement Message => Find(AutomationIds.NewProjectMessage);
    public UiElement CreateButton => Find(AutomationIds.NewProjectCreate);
    public UiElement CancelButton => Find(AutomationIds.NewProjectCancel);

    /// <summary>Types the name and replaces the location, then presses Create; the project goes in <c>location/name</c>.</summary>
    public async Task CreateAsync(string name, string location, CancellationToken cancellationToken)
    {
        await NameBox.ClickAsync(cancellationToken);
        await Driver.TypeAsync(name, cancellationToken);
        await LocationBox.ClickAsync(cancellationToken);
        await Driver.PressAsync("Ctrl+A", cancellationToken);
        await Driver.TypeAsync(location, cancellationToken);
        await CreateButton.WaitUntilAsync(e => e.IsEnabled, "Create enabled", cancellationToken);
        await CreateButton.ClickAsync(cancellationToken);
    }
}
