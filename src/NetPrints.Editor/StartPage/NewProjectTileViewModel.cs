using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.StartPage;

/// <summary>The new tile: "New project".</summary>
/// <param name="actions">The project flows.</param>
internal sealed partial class NewProjectTileViewModel(IProjectActions actions)
{
    [RelayCommand]
    private Task NewAsync(CancellationToken cancellationToken) => actions.NewProjectAsync(cancellationToken);
}
