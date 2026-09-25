using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using NetPrints.Editor.ViewModels;

namespace NetPrints.Editor.Views.Graph;

/// <summary>Node search list (PAR-52).</summary>
public partial class NodeSearchView : UserControl
{
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
        if ((sender as Control)?.DataContext is SuggestionItem { IsHeader: false } item)
        {
            _ = ViewModel?.SelectCommand.ExecuteAsync(item);
        }
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                // Enter picks the first suggestion.
                if (ViewModel?.Items.FirstOrDefault(i => !i.IsHeader) is { } first)
                {
                    _ = ViewModel.SelectCommand.ExecuteAsync(first);
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
        if (e.Key == Key.Enter && ResultList.SelectedItem is SuggestionItem { IsHeader: false } item)
        {
            _ = ViewModel?.SelectCommand.ExecuteAsync(item);
            e.Handled = true;
        }
    }
}
