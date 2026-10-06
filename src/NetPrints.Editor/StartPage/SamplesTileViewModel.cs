using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.StartPage;

/// <summary>The samples tile: the bundled samples, each opened as a copy.</summary>
internal sealed partial class SamplesTileViewModel
{
    private readonly IProjectActions actions;

    /// <summary>Creates the tile.</summary>
    /// <param name="catalog">The bundled samples.</param>
    /// <param name="actions">Opens the chosen sample.</param>
    public SamplesTileViewModel(SampleCatalog catalog, IProjectActions actions)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(actions);
        this.actions = actions;
        Samples = catalog.Samples;
    }

    /// <summary>Gets the samples.</summary>
    public IReadOnlyList<SampleDescriptor> Samples { get; }

    [RelayCommand]
    private async Task OpenAsync(SampleDescriptor sample, CancellationToken cancellationToken)
    {
        if (await actions.ConfirmUnloadAsync(cancellationToken).ConfigureAwait(true))
        {
            await actions.OpenSampleAsync(sample.Name, cancellationToken).ConfigureAwait(true);
        }
    }
}
