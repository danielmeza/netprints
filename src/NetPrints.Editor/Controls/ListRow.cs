using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace NetPrints.Editor.Controls;

/// <summary>
/// The one row of an editable list: the row's content on the left, optional <see cref="Actions"/> beside it and a remove button
/// in a fixed right-aligned column, so the buttons of every list line up (variables, event arguments, references). Its look is
/// the <c>ListRow</c> control theme in <c>EditorStyles.axaml</c>.
/// </summary>
public sealed class ListRow : ContentControl
{
    /// <summary>Identifies <see cref="Actions"/>.</summary>
    public static readonly StyledProperty<object?> ActionsProperty = AvaloniaProperty.Register<ListRow, object?>(nameof(Actions));

    /// <summary>Identifies <see cref="RemoveCommand"/>.</summary>
    public static readonly StyledProperty<ICommand?> RemoveCommandProperty = AvaloniaProperty.Register<ListRow, ICommand?>(nameof(RemoveCommand));

    /// <summary>Identifies <see cref="RemoveCommandParameter"/>.</summary>
    public static readonly StyledProperty<object?> RemoveCommandParameterProperty = AvaloniaProperty.Register<ListRow, object?>(nameof(RemoveCommandParameter));

    /// <summary>Identifies <see cref="RemoveText"/>.</summary>
    public static readonly StyledProperty<string?> RemoveTextProperty = AvaloniaProperty.Register<ListRow, string?>(nameof(RemoveText));

    /// <summary>Identifies <see cref="RemoveAutomationId"/>.</summary>
    public static readonly StyledProperty<string?> RemoveAutomationIdProperty = AvaloniaProperty.Register<ListRow, string?>(nameof(RemoveAutomationId));

    /// <summary>Identifies <see cref="IsRemoveVisible"/>.</summary>
    public static readonly StyledProperty<bool> IsRemoveVisibleProperty = AvaloniaProperty.Register<ListRow, bool>(nameof(IsRemoveVisible), true);

    private const string RemoveButtonName = "PART_Remove";

    private Button? removeButton;

    /// <summary>Gets or sets the content shown between the row's content and the remove button, such as move buttons.</summary>
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    /// <summary>Gets or sets the command the remove button runs.</summary>
    public ICommand? RemoveCommand
    {
        get => GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    /// <summary>Gets or sets the parameter passed to <see cref="RemoveCommand"/>.</summary>
    public object? RemoveCommandParameter
    {
        get => GetValue(RemoveCommandParameterProperty);
        set => SetValue(RemoveCommandParameterProperty, value);
    }

    /// <summary>Gets or sets the tooltip and accessible name of the remove button, such as "Remove variable".</summary>
    public string? RemoveText
    {
        get => GetValue(RemoveTextProperty);
        set => SetValue(RemoveTextProperty, value);
    }

    /// <summary>Gets or sets the automation id of the remove button, one of <see cref="AutomationIds"/>.</summary>
    public string? RemoveAutomationId
    {
        get => GetValue(RemoveAutomationIdProperty);
        set => SetValue(RemoveAutomationIdProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether the remove button is shown; the column stays, so rows keep their alignment.</summary>
    public bool IsRemoveVisible
    {
        get => GetValue(IsRemoveVisibleProperty);
        set => SetValue(IsRemoveVisibleProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        removeButton = e.NameScope.Find<Button>(RemoveButtonName);
        ApplyRemoveAutomationId();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RemoveAutomationIdProperty)
        {
            ApplyRemoveAutomationId();
        }
    }

    private void ApplyRemoveAutomationId()
    {
        if (removeButton is not null)
        {
            AutomationProperties.SetAutomationId(removeButton, RemoveAutomationId);
        }
    }
}
