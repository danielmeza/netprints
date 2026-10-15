using NetPrints.Editor.Navigation;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Tests.Shell;

/// <summary>An <see cref="INavigation"/> that records every call, for command handler tests.</summary>
public sealed class FakeNavigation : INavigation
{
    /// <summary>Gets the calls made, in order, as <c>Method</c> or <c>Method:argument</c>.</summary>
    public List<string> Calls { get; } = [];

    /// <inheritdoc/>
    public bool CanGoBack { get; set; }

    /// <inheritdoc/>
    public bool CanGoForward { get; set; }

    /// <inheritdoc/>
    public bool NavigateTo(NavigationTarget target)
    {
        Calls.Add($"NavigateTo:{target.Document}:{target.NodeId}");
        return true;
    }

    /// <inheritdoc/>
    public void RecordCurrent() => Calls.Add("RecordCurrent");

    /// <inheritdoc/>
    public void GoBack() => Calls.Add("GoBack");

    /// <inheritdoc/>
    public void GoForward() => Calls.Add("GoForward");
}
