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
