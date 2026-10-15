using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.StartPage;

/// <summary>The open tile: "Open folder or project", with one button per route.</summary>
/// <param name="actions">The project flows.</param>
internal sealed partial class OpenProjectTileViewModel(IProjectActions actions)
{
    [RelayCommand]
    private async Task OpenAsync(CancellationToken cancellationToken)
    {
        if (await actions.ConfirmUnloadAsync(cancellationToken).ConfigureAwait(true))
        {
            await actions.OpenProjectAsync(null, cancellationToken).ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private async Task OpenFolderAsync(CancellationToken cancellationToken)
    {
        if (await actions.ConfirmUnloadAsync(cancellationToken).ConfigureAwait(true))
        {
            await actions.OpenFolderAsync(cancellationToken).ConfigureAwait(true);
        }
    }
}
