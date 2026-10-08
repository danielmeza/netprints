using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Navigation;

/// <summary>
/// Go to anything (FR-061): asks every registered <see cref="IGoToProvider"/> as the user types and lists the results grouped
/// by kind, each group ranked with the research R9 ranking. Text starting with <see cref="GoToKinds.CommandMarker"/> asks only the
/// commands provider. Enter navigates to the selected result's target, or runs its command, and closes.
/// </summary>
public sealed partial class GoToAnythingViewModel : DialogViewModel<object>
{
    private readonly IReadOnlyList<IGoToProvider> providers;
    private readonly Func<NavigationTarget, bool> navigate;
    private readonly Func<string, bool> runCommand;
    private CancellationTokenSource? searching;

    /// <summary>Creates the dialog over a set of providers.</summary>
    /// <param name="providers">The registered providers, in registration order (the order of the groups).</param>
    /// <param name="navigate">Navigates to a target; answers false when it could not.</param>
    /// <param name="runCommand">Runs the command with the given full id; answers false when it is unknown or disabled.</param>
    public GoToAnythingViewModel(IReadOnlyList<IGoToProvider> providers, Func<NavigationTarget, bool> navigate, Func<string, bool> runCommand)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(navigate);
        ArgumentNullException.ThrowIfNull(runCommand);
        this.providers = providers;
        this.navigate = navigate;
        this.runCommand = runCommand;
    }

    /// <summary>Gets the group headers and results, in display order.</summary>
    public ObservableCollection<GoToRow> Rows { get; } = [];

    /// <summary>Gets or sets the text typed.</summary>
    [ObservableProperty]
    public partial string Query { get; set; } = "";

    /// <summary>Gets or sets the highlighted result, never a header; null when nothing is listed.</summary>
    [ObservableProperty]
    public partial GoToRow? SelectedRow { get; set; }

    /// <summary>Gets a value indicating whether nothing is listed.</summary>
    public bool IsEmpty => Rows.Count == 0;

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task SearchAsync()
    {
        var source = new CancellationTokenSource();
        CancellationTokenSource? previous = searching;
        searching = source;
        if (previous is not null)
        {
            await previous.CancelAsync();
            previous.Dispose();
        }

        try
        {
            IReadOnlyList<GoToRow> rows = await CollectAsync(Query.Trim(), source.Token);
            if (!source.IsCancellationRequested)
            {
                Show(rows);
            }
        }
        catch (OperationCanceledException)
        {
            // A newer search replaced this one.
        }
        finally
        {
            if (ReferenceEquals(searching, source))
            {
                searching = null;
            }

            source.Dispose();
        }
    }

    [RelayCommand]
    private void RunSelected()
    {
        if (SelectedRow?.Item is not { } item)
        {
            return;
        }

        bool done = item.CommandId is { } commandId ? runCommand(commandId) : item.Target is { } target && navigate(target);
        if (done)
        {
            RequestClose(null);
        }
    }

    [RelayCommand]
    private void SelectNext() => Move(1);

    [RelayCommand]
    private void SelectPrevious() => Move(-1);

    [RelayCommand]
    private void Close() => RequestClose(null);

    partial void OnQueryChanged(string value) => SearchCommand.Execute(null);

    partial void OnSelectedRowChanged(GoToRow? oldValue, GoToRow? newValue)
    {
        if (newValue is { IsHeader: true })
        {
            SelectedRow = oldValue is not null && Rows.Contains(oldValue) ? oldValue : null;
        }
    }

    private async Task<IReadOnlyList<GoToRow>> CollectAsync(string text, CancellationToken cancellationToken)
    {
        bool commandsOnly = text.StartsWith(GoToKinds.CommandMarker);
        string search = commandsOnly ? text[1..].Trim() : text;
        List<(string Kind, List<GoToItem> Items)> groups = [];
        foreach (IGoToProvider provider in providers.Where(provider => (provider.Kind == GoToKinds.Commands) == commandsOnly))
        {
            List<GoToItem> items = [];
            await foreach (GoToItem item in provider.SearchAsync(search, cancellationToken))
            {
                items.Add(item);
            }

            if (items.Count == 0)
            {
                continue;
            }

            if (groups.Find(group => group.Kind == provider.Kind) is { Items: { } existing })
            {
                existing.AddRange(items);
            }
            else
            {
                groups.Add((provider.Kind, items));
            }
        }

        List<GoToRow> rows = [];
        foreach ((string kind, List<GoToItem> items) in groups)
        {
            rows.Add(new GoToRow(true, kind, "", null));
            rows.AddRange(items
                .OrderBy(item => MatchRanking.SortKey(item.Title, search))
                .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                .Select(item => new GoToRow(false, item.Title, item.Detail, item)));
        }

        return rows;
    }

    private void Show(IReadOnlyList<GoToRow> rows)
    {
        Rows.Clear();
        foreach (GoToRow row in rows)
        {
            Rows.Add(row);
        }

        SelectedRow = Rows.FirstOrDefault(row => !row.IsHeader);
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void Move(int step)
    {
        int index = SelectedRow is null ? -1 : Rows.IndexOf(SelectedRow);
        for (int next = index + step; next >= 0 && next < Rows.Count; next += step)
        {
            if (!Rows[next].IsHeader)
            {
                SelectedRow = Rows[next];
                return;
            }
        }
    }
}
