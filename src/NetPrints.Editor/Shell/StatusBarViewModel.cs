using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Editor.Hosting;

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
