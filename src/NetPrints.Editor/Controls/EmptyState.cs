using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls.Primitives;

namespace NetPrints.Editor.Controls;

/// <summary>
/// The one control that stands in for a panel or list with nothing to show (FR-088): an icon, one sentence and, when
/// one applies, an action that runs a command. Its look is the <c>EmptyState</c> control theme in <c>EditorStyles.axaml</c>.
/// </summary>
public sealed class EmptyState : TemplatedControl
{
    /// <summary>Identifies <see cref="IconId"/>.</summary>
    public static readonly StyledProperty<string?> IconIdProperty = AvaloniaProperty.Register<EmptyState, string?>(nameof(IconId));

    /// <summary>Identifies <see cref="Message"/>.</summary>
    public static readonly StyledProperty<string?> MessageProperty = AvaloniaProperty.Register<EmptyState, string?>(nameof(Message));

    /// <summary>Identifies <see cref="ActionText"/>.</summary>
    public static readonly StyledProperty<string?> ActionTextProperty = AvaloniaProperty.Register<EmptyState, string?>(nameof(ActionText));

    /// <summary>Identifies <see cref="ActionCommand"/>.</summary>
    public static readonly StyledProperty<ICommand?> ActionCommandProperty = AvaloniaProperty.Register<EmptyState, ICommand?>(nameof(ActionCommand));

    /// <summary>Identifies <see cref="HasAction"/>.</summary>
    public static readonly DirectProperty<EmptyState, bool> HasActionProperty =
        AvaloniaProperty.RegisterDirect<EmptyState, bool>(nameof(HasAction), state => state.HasAction);

    private bool hasAction;

    /// <summary>Gets or sets the icon id drawn above the sentence, such as <see cref="Icons.IconIds.EmptyErrors"/>.</summary>
    public string? IconId
    {
        get => GetValue(IconIdProperty);
        set => SetValue(IconIdProperty, value);
    }

    /// <summary>Gets or sets the one sentence that says what is missing and, when it helps, how to get it.</summary>
    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>Gets or sets the label of the action button; the button is shown only with <see cref="ActionCommand"/>.</summary>
    public string? ActionText
    {
        get => GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    /// <summary>Gets or sets the command the action button runs; null shows no button.</summary>
    public ICommand? ActionCommand
    {
        get => GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    /// <summary>Gets a value indicating whether the action button is shown: it has both a label and a command.</summary>
    public bool HasAction
    {
        get => hasAction;
        private set => SetAndRaise(HasActionProperty, ref hasAction, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ActionTextProperty || change.Property == ActionCommandProperty)
        {
            HasAction = !string.IsNullOrEmpty(ActionText) && ActionCommand is not null;
        }
        else if (change.Property == MessageProperty)
        {
            AutomationProperties.SetName(this, Message);
        }
    }
}
