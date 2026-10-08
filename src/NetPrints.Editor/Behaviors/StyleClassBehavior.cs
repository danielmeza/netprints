using Avalonia;
using Avalonia.Xaml.Interactivity;

namespace NetPrints.Editor.Behaviors;

/// <summary>
/// Keeps one style class on the attached element in step with a bound name (D5: a state maps to a class, and the
/// style sets the theme token). Used for the role of a node header and the kind of a cable, where one name picks
/// among several classes.
/// </summary>
public sealed class StyleClassBehavior : StyledElementBehavior<StyledElement>
{
    /// <summary>Identifies <see cref="ClassName"/>.</summary>
    public static readonly StyledProperty<string?> ClassNameProperty =
        AvaloniaProperty.Register<StyleClassBehavior, string?>(nameof(ClassName));

    /// <summary>Gets or sets the class to put on the attached element; null or empty puts none.</summary>
    public string? ClassName
    {
        get => GetValue(ClassNameProperty);
        set => SetValue(ClassNameProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();
        Apply(null, ClassName);
    }

    /// <inheritdoc/>
    protected override void OnDetaching()
    {
        Apply(ClassName, null);
        base.OnDetaching();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ClassNameProperty)
        {
            Apply(change.GetOldValue<string?>(), change.GetNewValue<string?>());
        }
    }

    private void Apply(string? oldName, string? newName)
    {
        if (AssociatedObject is not { } element)
        {
            return;
        }

        if (!string.IsNullOrEmpty(oldName))
        {
            element.Classes.Remove(oldName);
        }

        if (!string.IsNullOrEmpty(newName))
        {
            element.Classes.Add(newName);
        }
    }
}
