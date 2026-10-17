using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Editor.Hosting;
using NetPrints.Extensibility.Editor;

namespace NetPrints.Editor.Shell;

/// <summary>What the build state of the status bar says.</summary>
public enum BuildState
{
    /// <summary>Nothing builds or runs.</summary>
    Idle,

    /// <summary>A compile or run is saving and building.</summary>
    Building,

    /// <summary>The program runs.</summary>
    Running,
}

/// <summary>The status bar: a message that expires on the injected clock, the build state and the busy indicator.</summary>
/// <param name="timeProvider">The clock the expiry runs on.</param>
/// <param name="dispatcher">Brings the expiry, which a real clock fires on a pool thread, to the UI thread.</param>
public sealed partial class StatusBarViewModel(TimeProvider timeProvider, IUiDispatcher dispatcher) : ObservableObject, IDisposable
{
    /// <summary>How long an operation must run before the busy indicator appears, so fast ones do not flash it.</summary>
    public static readonly TimeSpan BusyIndicatorDelay = TimeSpan.FromMilliseconds(150);

    private const string BuildingText = "Building…";

    private readonly Lock gate = new();
    private readonly List<BusyScope> busyScopes = [];
    private readonly List<(StatusBarItemViewModel Item, IStatusBarContext Context)> attached = [];
    private ITimer? expiryTimer;
    private int version;

    /// <summary>Gets the message shown, or null.</summary>
    [ObservableProperty]
    public partial string? Message { get; private set; }

    /// <summary>Gets the build state.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BuildStateText))]
    public partial BuildState BuildState { get; private set; }

    /// <summary>Gets a value indicating whether a long operation or a build is in progress.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    /// <summary>Gets what the busy operation is doing, or null when not busy.</summary>
    [ObservableProperty]
    public partial string? BusyText { get; private set; }

    /// <summary>Gets the build state as text, empty when idle.</summary>
    public string BuildStateText => BuildState switch
    {
        BuildState.Building => BuildingText,
        BuildState.Running => "Running",
        _ => "",
    };

    /// <summary>Gets the segments on the left, after the message, in order; only those that apply.</summary>
    public ObservableCollection<StatusBarItemViewModel> LeftItems { get; } = [];

    /// <summary>Gets the segments on the right, in order; only those that apply.</summary>
    public ObservableCollection<StatusBarItemViewModel> RightItems { get; } = [];

    /// <summary>
    /// Creates the view model of each item with its context and shows the items that apply. Replaces the items of an earlier call;
    /// an item whose descriptor an earlier call attached keeps its view model.
    /// </summary>
    /// <param name="items">The registered items.</param>
    /// <param name="context">Creates the context an item is created and asked with.</param>
    public void Attach(IReadOnlyList<StatusBarItemDescriptor> items, Func<StatusBarItemDescriptor, IStatusBarContext> context)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(context);
        List<(StatusBarItemViewModel Item, IStatusBarContext Context)> previous = [.. attached];
        attached.Clear();
        foreach (StatusBarItemDescriptor descriptor in items)
        {
            (StatusBarItemViewModel Item, IStatusBarContext Context) kept = previous.Find(entry => ReferenceEquals(entry.Item.Descriptor, descriptor));
            if (kept.Item is not null)
            {
                attached.Add(kept);
                continue;
            }

            IStatusBarContext itemContext = context(descriptor);
            attached.Add((new StatusBarItemViewModel(descriptor, descriptor.CreateViewModel(itemContext)), itemContext));
        }

        Refresh();
    }

    /// <summary>Asks every item again whether it applies, refreshes the segments that follow editor state and shows the ones that apply.</summary>
    public void Refresh()
    {
        foreach ((StatusBarItemViewModel item, _) in attached)
        {
            (item.Content as IStatusSegment)?.Refresh();
        }

        Reconcile(LeftItems, StatusBarAlignment.Left);
        Reconcile(RightItems, StatusBarAlignment.Right);
    }

    private void Reconcile(ObservableCollection<StatusBarItemViewModel> shown, StatusBarAlignment side)
    {
        List<StatusBarItemViewModel> wanted =
        [
            .. attached
                .Where(entry => entry.Item.Alignment == side && (entry.Item.Descriptor.AppliesTo?.Invoke(entry.Context) ?? true))
                .Select(entry => entry.Item)
                .OrderBy(item => item.Order),
        ];
        for (int index = shown.Count - 1; index >= 0; index--)
        {
            if (!wanted.Contains(shown[index]))
            {
                shown.RemoveAt(index);
            }
        }

        for (int index = 0; index < wanted.Count; index++)
        {
            if (index >= shown.Count || shown[index] != wanted[index])
            {
                shown.Insert(index, wanted[index]);
            }
        }
    }

    /// <summary>Shows a message, replacing the current one and restarting its expiry.</summary>
    /// <param name="message">The text.</param>
    /// <param name="expiry">How long it stays, or null to keep it until replaced.</param>
    public void Show(string message, TimeSpan? expiry = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        expiryTimer?.Dispose();
        expiryTimer = null;
        int shown = ++version;
        Message = message;
        if (expiry is { } lifetime)
        {
            expiryTimer = timeProvider.CreateTimer(_ => Expire(shown), null, lifetime, Timeout.InfiniteTimeSpan);
        }
    }

    /// <summary>Sets the build state.</summary>
    /// <param name="state">The new state.</param>
    public void SetBuildState(BuildState state)
    {
        BuildState = state;
        UpdateBusy();
        Refresh();
    }

    /// <summary>
    /// Starts a long operation: the busy indicator shows <paramref name="text"/> once the operation has run for
    /// <see cref="BusyIndicatorDelay"/>, and goes when the returned scope is disposed.
    /// </summary>
    /// <param name="text">What the operation is doing.</param>
    /// <returns>The scope that ends the busy state when disposed; disposing it again does nothing.</returns>
    public IDisposable BeginBusy(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var scope = new BusyScope(this, text, timeProvider);
        lock (gate)
        {
            busyScopes.Add(scope);
        }

        return scope;
    }

    /// <summary>Cancels the pending expiry and the pending busy indicators.</summary>
    public void Dispose()
    {
        expiryTimer?.Dispose();
        expiryTimer = null;
        foreach (BusyScope scope in Snapshot())
        {
            scope.Dispose();
        }
    }

    private void Expire(int shown) => dispatcher.Post(() => Clear(shown));

    private void Clear(int shown)
    {
        if (shown == version)
        {
            Message = null;
        }
    }

    private void Reveal(BusyScope scope) => dispatcher.Post(() => RevealOnUi(scope));

    private void RevealOnUi(BusyScope scope)
    {
        bool active;
        lock (gate)
        {
            active = busyScopes.Contains(scope);
        }

        if (active)
        {
            scope.IsRevealed = true;
            UpdateBusy();
        }
    }

    private void End(BusyScope scope)
    {
        bool removed;
        lock (gate)
        {
            removed = busyScopes.Remove(scope);
        }

        if (removed)
        {
            UpdateBusy();
        }
    }

    private List<BusyScope> Snapshot()
    {
        lock (gate)
        {
            return [.. busyScopes];
        }
    }

    private void UpdateBusy()
    {
        string? text = Snapshot().LastOrDefault(scope => scope.IsRevealed)?.Text
            ?? (BuildState == BuildState.Building ? BuildingText : null);
        BusyText = text;
        IsBusy = text is not null;
    }

    private sealed class BusyScope : IDisposable
    {
        private readonly StatusBarViewModel owner;
        private readonly ITimer timer;

        public BusyScope(StatusBarViewModel owner, string text, TimeProvider timeProvider)
        {
            this.owner = owner;
            Text = text;
            timer = timeProvider.CreateTimer(_ => owner.Reveal(this), null, BusyIndicatorDelay, Timeout.InfiniteTimeSpan);
        }

        public string Text { get; }

        public bool IsRevealed { get; set; }

        public void Dispose()
        {
            timer.Dispose();
            owner.End(this);
        }
    }
}
