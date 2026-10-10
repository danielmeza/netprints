using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Navigation;

/// <summary>Moves between graphs and keeps the back and forward history of the views left (FR-061, FR-063).</summary>
public interface INavigation
{
    /// <summary>Gets whether <see cref="GoBack"/> has an entry whose graph still exists.</summary>
    bool CanGoBack { get; }

    /// <summary>Gets whether <see cref="GoForward"/> has an entry whose graph still exists.</summary>
    bool CanGoForward { get; }

    /// <summary>Records the active view, opens the target's graph and, when the target names a node, centres and selects it.</summary>
    /// <param name="target">Where to go.</param>
    /// <returns><see langword="false"/> when the target's graph does not exist in the open project (nothing happens).</returns>
    bool NavigateTo(NavigationTarget target);

    /// <summary>Records the active view as the one being left; call it before a navigation that does not go through <see cref="NavigateTo"/>.</summary>
    void RecordCurrent();

    /// <summary>Restores the previous view: its graph, viewport and selection.</summary>
    void GoBack();

    /// <summary>Restores the view Back left.</summary>
    void GoForward();
}
