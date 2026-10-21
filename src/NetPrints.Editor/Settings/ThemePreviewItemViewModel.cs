using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Editor.Theming;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.Settings;

/// <summary>The block of the Appearance page that shows the canvas, card, pins and selected wire of the chosen Dark and Light presets, and follows the settings.</summary>
internal sealed partial class ThemePreviewItemViewModel : SettingsItemViewModel, IDisposable
{
    private readonly ISettingsStore store;
    private readonly Func<string, ThemeBase, object?> baseLookup;
    private readonly IDisposable? subscription;

    /// <summary>Creates the preview over the running application's tokens.</summary>
    /// <param name="store">The settings the chosen presets are read from.</param>
    public ThemePreviewItemViewModel(ISettingsStore store)
        : this(store, ThemePreviewRows.FromApplication, ThemePreviewRows.PostToUi)
    {
    }

    /// <summary>Creates the preview with its sources supplied.</summary>
    /// <param name="store">The settings the chosen presets are read from.</param>
    /// <param name="baseLookup">Resolves a token in the XAML of a variant.</param>
    /// <param name="post">Runs a refresh on the thread that owns the rows.</param>
    internal ThemePreviewItemViewModel(ISettingsStore store, Func<string, ThemeBase, object?> baseLookup, Action<Action> post)
        : base(Strings.Settings_ThemePreview_Label, Strings.Settings_ThemePreview_Description)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(baseLookup);
        ArgumentNullException.ThrowIfNull(post);
        this.store = store;
        this.baseLookup = baseLookup;
        Rows = ThemePreviewRows.Build(store, baseLookup);
        subscription = (store as ISettingsChangeSource)?.OnSectionChanged(State.EditorSettings.SectionId, () => post(Refresh));
    }

    /// <summary>Gets the preview of the Dark and the Light variant.</summary>
    [ObservableProperty]
    public partial ImmutableArray<ThemePreviewRow> Rows { get; private set; }

    /// <inheritdoc/>
    public void Dispose() => subscription?.Dispose();

    private void Refresh() => Rows = ThemePreviewRows.Build(store, baseLookup);
}
