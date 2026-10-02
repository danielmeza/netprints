using Microsoft.Extensions.Time.Testing;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;

namespace NetPrints.Editor.Tests.Shell;

/// <summary>The status bar's message expiry runs on the injected clock; the build state has its own text.</summary>
public class StatusBarViewModelTests
{
    private readonly FakeTimeProvider time = new();

    [Fact]
    public void AMessageWithAnExpiryDisappearsWhenItIsOver()
    {
        var bar = new StatusBarViewModel(time, new InlineDispatcher());

        bar.Show("Undid: Add node", TimeSpan.FromSeconds(4));
        time.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal("Undid: Add node", bar.Message);

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(bar.Message);
    }

    [Fact]
    public void AMessageWithoutAnExpiryStaysUntilReplaced()
    {
        var bar = new StatusBarViewModel(time, new InlineDispatcher());

        bar.Show("Build succeeded");
        time.Advance(TimeSpan.FromHours(1));

        Assert.Equal("Build succeeded", bar.Message);
    }

    [Fact]
    public void ANewMessageRestartsTheExpiryAndTheOldTimerNeverClearsIt()
    {
        var bar = new StatusBarViewModel(time, new InlineDispatcher());
        bar.Show("first", TimeSpan.FromSeconds(4));
        time.Advance(TimeSpan.FromSeconds(3));

        bar.Show("second", TimeSpan.FromSeconds(4));
        time.Advance(TimeSpan.FromSeconds(3));
        Assert.Equal("second", bar.Message);

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(bar.Message);
    }

    [Fact]
    public void AMessageWithoutAnExpiryCancelsThePreviousExpiry()
    {
        var bar = new StatusBarViewModel(time, new InlineDispatcher());
        bar.Show("first", TimeSpan.FromSeconds(4));

        bar.Show("kept");
        time.Advance(TimeSpan.FromSeconds(10));

        Assert.Equal("kept", bar.Message);
    }

    [Fact]
    public void TheBuildStateHasTextAndRaisesChanges()
    {
        var bar = new StatusBarViewModel(time, new InlineDispatcher());
        var changed = new List<string?>();
        bar.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.Equal("", bar.BuildStateText);

        bar.SetBuildState(BuildState.Building);
        Assert.Equal("Building…", bar.BuildStateText);
        bar.SetBuildState(BuildState.Running);
        Assert.Equal("Running", bar.BuildStateText);
        bar.SetBuildState(BuildState.Idle);
        Assert.Equal("", bar.BuildStateText);

        Assert.Contains(nameof(StatusBarViewModel.BuildStateText), changed);
    }
}
