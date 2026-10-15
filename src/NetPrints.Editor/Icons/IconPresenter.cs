using Avalonia;
using Avalonia.Controls.Primitives;

namespace NetPrints.Editor.Icons;

/// <summary>Draws the vector glyph of an icon id in the inherited foreground; the one icon control views use (E9).</summary>
public sealed class IconPresenter : TemplatedControl
{
    /// <summary>Identifies <see cref="IconId"/>.</summary>
    public static readonly StyledProperty<string?> IconIdProperty = AvaloniaProperty.Register<IconPresenter, string?>(nameof(IconId));

    /// <summary>Identifies <see cref="IsActive"/>.</summary>
    public static readonly StyledProperty<bool> IsActiveProperty = AvaloniaProperty.Register<IconPresenter, bool>(nameof(IsActive));

    /// <summary>Identifies <see cref="Glyph"/>.</summary>
    public static readonly DirectProperty<IconPresenter, IconGlyph> GlyphProperty =
        AvaloniaProperty.RegisterDirect<IconPresenter, IconGlyph>(nameof(Glyph), presenter => presenter.Glyph);

    private IconGlyph glyph = IconRegistry.Shared.Resolve(null);

    /// <summary>Gets or sets the icon id, such as <see cref="IconIds.Save"/>.</summary>
    public string? IconId
    {
        get => GetValue(IconIdProperty);
        set => SetValue(IconIdProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether the filled glyph of an outline and filled pair is drawn.</summary>
    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>Gets the glyph the registry resolved for <see cref="IconId"/> and <see cref="IsActive"/>.</summary>
    public IconGlyph Glyph
    {
        get => glyph;
        private set => SetAndRaise(GlyphProperty, ref glyph, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IconIdProperty || change.Property == IsActiveProperty)
        {
            Glyph = IconRegistry.Shared.Resolve(IconId, IsActive);
        }
    }
}
