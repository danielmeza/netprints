using Avalonia.Controls;
using Avalonia.Threading;

namespace NetPrints.Editor.Search;

/// <summary>Node search list (PAR-52).</summary>
public partial class NodeSearchView : UserControl
{
    /// <summary>Loads the control's XAML.</summary>
    public NodeSearchView()
    {
        InitializeComponent();
    }

    /// <summary>The search box is cleared by the view model and focused on open.</summary>
    public void FocusSearchBox() => Dispatcher.UIThread.Post(() =>
    {
        ResultList.ScrollIntoView(0);
        SearchBox.Focus();
    });
}
