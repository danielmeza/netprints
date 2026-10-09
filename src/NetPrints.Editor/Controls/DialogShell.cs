using Avalonia;
using Avalonia.Controls;

namespace NetPrints.Editor.Controls;

/// <summary>
/// The one frame of a dialog (FR-088): an icon and a title above the content, and the footer buttons below in the
/// platform's order. Its look, widths (<c>Dialog.MinWidth</c> to <c>Dialog.MaxWidth</c>) and spacing are the
/// <c>DialogShell</c> control theme in <c>EditorStyles.axaml</c>. Enter and Esc come from the footer buttons' <c>IsDefault</c>
/// and <c>IsCancel</c>.
/// </summary>
public sealed class DialogShell : ContentControl
{
    /// <summary>Identifies <see cref="Title"/>.</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<DialogShell, string?>(nameof(Title));

    /// <summary>Identifies <see cref="IconId"/>.</summary>
    public static readonly StyledProperty<string?> IconIdProperty = AvaloniaProperty.Register<DialogShell, string?>(nameof(IconId));

    /// <summary>Identifies <see cref="ButtonOrder"/>.</summary>
    public static readonly StyledProperty<DialogButtonOrder> ButtonOrderProperty =
        AvaloniaProperty.Register<DialogShell, DialogButtonOrder>(nameof(ButtonOrder), DialogButtonOrder.ForCurrentPlatform());

    /// <summary>Identifies <see cref="SortedActions"/>.</summary>
    public static readonly DirectProperty<DialogShell, IReadOnlyList<Control>> SortedActionsProperty =
        AvaloniaProperty.RegisterDirect<DialogShell, IReadOnlyList<Control>>(nameof(SortedActions), shell => shell.SortedActions);

    private IReadOnlyList<Control> sortedActions = [];

    /// <summary>Creates a shell with no actions.</summary>
    public DialogShell()
    {
        Actions.CollectionChanged += (_, _) => Arrange();
    }

    /// <summary>Gets or sets the title drawn next to the icon.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets the icon id drawn before the title, such as <see cref="Icons.IconIds.DialogWarning"/>.</summary>
    public string? IconId
    {
        get => GetValue(IconIdProperty);
        set => SetValue(IconIdProperty, value);
    }

    /// <summary>Gets or sets the order of the footer buttons; it defaults to the current platform's and a test sets it.</summary>
    public DialogButtonOrder ButtonOrder
    {
        get => GetValue(ButtonOrderProperty);
        set => SetValue(ButtonOrderProperty, value);
    }

    /// <summary>Gets the footer buttons in the order they are declared; the template shows <see cref="SortedActions"/>.</summary>
    public Avalonia.Controls.Controls Actions { get; } = [];

    /// <summary>Gets the footer buttons in <see cref="ButtonOrder"/>, left to right.</summary>
    public IReadOnlyList<Control> SortedActions
    {
        get => sortedActions;
        private set => SetAndRaise(SortedActionsProperty, ref sortedActions, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ButtonOrderProperty)
        {
            Arrange();
        }
    }

    private void Arrange() => SortedActions = ButtonOrder.Arrange(Actions);
}
