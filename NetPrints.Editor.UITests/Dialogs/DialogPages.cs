using Avalonia.Controls;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.References;

namespace NetPrints.Editor.UITests.Dialogs;

/// <summary>Page objects of the dialogs.</summary>
public sealed class ErrorDialogPage(ErrorDialog dialog)
{
    public TextBox Message => dialog.ById<TextBox>(AutomationIds.ErrorMessage);

    public void ClickOk() => dialog.Click(dialog.ById<Button>(AutomationIds.ErrorOkButton).CenterIn(dialog));
}

public sealed class SelectTypeDialogPage(SelectTypeDialog dialog)
{
    public AutoCompleteBox TypeBox => dialog.ById<AutoCompleteBox>(AutomationIds.SelectTypeBox);
}

public sealed class SelectMethodDialogPage(SelectMethodDialog dialog)
{
    public ComboBox MethodBox => dialog.ById<ComboBox>(AutomationIds.SelectMethodBox);
}

public sealed class ReferencesDialogPage(ReferencesDialog dialog)
{
    public IReadOnlyList<ToggleSwitch> IncludeSwitches => dialog.Descendants<ToggleSwitch>().ToList();

    public IEnumerable<string> RowTexts => dialog.Descendants<TextBlock>().Select(t => t.Text ?? "");

    public void ClickClose() => dialog.Click(dialog.ById<Button>(AutomationIds.ReferencesCloseButton).CenterIn(dialog));
}
