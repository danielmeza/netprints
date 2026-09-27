using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Search;

/// <summary>Node search list (PAR-52).</summary>
public partial class NodeSearchView : UserControl
{
    /// <summary>Loads the control's XAML.</summary>
    public NodeSearchView()
    {
        InitializeComponent();
    }

    private SuggestionListVM? ViewModel => DataContext as SuggestionListVM;

    /// <summary>The search box is cleared by the view model and focused on open.</summary>
    public void FocusSearchBox() => Dispatcher.UIThread.Post(() =>
    {
        ResultList.ScrollIntoView(0);
        SearchBox.Focus();
    });

    private void OnItemTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is SuggestionItem { IsHeader: false } item && ViewModel is { } viewModel)
        {
            viewModel.SelectCommand.ExecuteAsync(item).Forget(viewModel.Context.LoggerFactory.CreateLogger<NodeSearchView>());
        }
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                // Enter picks the first suggestion.
                if (ViewModel is { } viewModel && viewModel.Items.FirstOrDefault(i => !i.IsHeader) is { } first)
                {
                    viewModel.SelectCommand.ExecuteAsync(first).Forget(viewModel.Context.LoggerFactory.CreateLogger<NodeSearchView>());
                }
                e.Handled = true;
                break;
            case Key.Down:
                ResultList.SelectedItem = ViewModel?.Items.FirstOrDefault(i => !i.IsHeader);
                ResultList.Focus();
                e.Handled = true;
                break;
            case Key.Escape:
                ViewModel?.CloseCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ResultList.SelectedItem is SuggestionItem { IsHeader: false } item && ViewModel is { } viewModel)
        {
            viewModel.SelectCommand.ExecuteAsync(item).Forget(viewModel.Context.LoggerFactory.CreateLogger<NodeSearchView>());
            e.Handled = true;
        }
    }
}
