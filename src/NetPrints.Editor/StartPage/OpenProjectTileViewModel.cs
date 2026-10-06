using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.StartPage;

/// <summary>The open tile: "Open folder or project".</summary>
/// <param name="actions">The project flows.</param>
internal sealed partial class OpenProjectTileViewModel(IProjectActions actions)
{
    [RelayCommand]
    private Task OpenAsync(CancellationToken cancellationToken) => actions.OpenProjectAsync(null, cancellationToken);
}
