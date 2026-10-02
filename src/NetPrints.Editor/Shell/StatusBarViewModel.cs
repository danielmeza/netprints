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

/// <summary>The status bar: a message that expires on the injected clock, and the build state.</summary>
/// <param name="timeProvider">The clock the expiry runs on.</param>
/// <param name="dispatcher">Brings the expiry, which a real clock fires on a pool thread, to the UI thread.</param>
public sealed partial class StatusBarViewModel(TimeProvider timeProvider, IUiDispatcher dispatcher) : ObservableObject, IDisposable
{
    private ITimer? expiryTimer;
    private int version;

    /// <summary>Gets the message shown, or null.</summary>
    [ObservableProperty]
    public partial string? Message { get; private set; }

    /// <summary>Gets the build state.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BuildStateText))]
    public partial BuildState BuildState { get; private set; }

    /// <summary>Gets the build state as text, empty when idle.</summary>
    public string BuildStateText => BuildState switch
    {
        BuildState.Building => "Building…",
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
    public void SetBuildState(BuildState state) => BuildState = state;

    /// <summary>Cancels the pending expiry.</summary>
    public void Dispose()
    {
        expiryTimer?.Dispose();
        expiryTimer = null;
    }

    private void Expire(int shown) => dispatcher.Post(() => Clear(shown));

    private void Clear(int shown)
    {
        if (shown == version)
        {
            Message = null;
        }
    }
}
