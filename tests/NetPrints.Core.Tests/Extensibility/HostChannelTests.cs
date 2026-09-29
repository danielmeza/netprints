using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using NetPrints.Extensibility.Hosting;
using Xunit;

namespace NetPrints.Tests.Extensibility;

/// <summary>Host channel types (extension-points.md §6).</summary>
public class HostChannelTests
{
    private static HostMessage Message(string type) => new(type, JsonDocument.Parse("{}").RootElement.Clone());

    private sealed class Collector : IObserver<HostMessage>
    {
        public List<HostMessage> Received { get; } = [];

        public bool Completed { get; private set; }

        public bool Errored { get; private set; }

        public void OnNext(HostMessage value) => Received.Add(value);

        public void OnError(Exception error) => Errored = true;

        public void OnCompleted() => Completed = true;
    }

    [Fact]
    public async Task InMemoryPairDeliversBothWays()
    {
        (InMemoryHostChannel editor, InMemoryHostChannel host) = InMemoryHostChannel.CreatePair("test");
        var atEditor = new Collector();
        var atHost = new Collector();
        using IDisposable s1 = editor.Messages.Subscribe(atEditor);
        using IDisposable s2 = host.Messages.Subscribe(atHost);

        await host.SendAsync(Message(HostMessageTypes.TypesChanged), TestContext.Current.CancellationToken);
        await editor.SendAsync(Message(HostMessageTypes.FocusDocument), TestContext.Current.CancellationToken);

        Assert.Equal("test", editor.Id);
        Assert.Equal(HostChannelState.Open, editor.State);
        Assert.Equal(HostMessageTypes.TypesChanged, Assert.Single(atEditor.Received).Type);
        Assert.Equal(HostMessageTypes.FocusDocument, Assert.Single(atHost.Received).Type);
    }

    [Fact]
    public async Task DisposingClosesBothEndsCompletesMessagesAndSendThrows()
    {
        (InMemoryHostChannel editor, InMemoryHostChannel host) = InMemoryHostChannel.CreatePair("test");
        var atHost = new Collector();
        var atEditor = new Collector();
        using IDisposable s1 = host.Messages.Subscribe(atHost);
        using IDisposable s2 = editor.Messages.Subscribe(atEditor);

        await editor.DisposeAsync();

        Assert.Equal(HostChannelState.Closed, editor.State);
        Assert.Equal(HostChannelState.Closed, host.State);
        Assert.True(atHost.Completed);
        Assert.True(atEditor.Completed);
        Assert.False(atHost.Errored);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await editor.SendAsync(Message("x"), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await host.SendAsync(Message("x"), TestContext.Current.CancellationToken));
        await editor.DisposeAsync();
    }

    [Fact]
    public async Task NullChannelIsOpenSilentAndDropsMessages()
    {
        NullHostChannel channel = NullHostChannel.Instance;
        var received = new Collector();
        using IDisposable subscription = channel.Messages.Subscribe(received);

        await channel.SendAsync(Message("x"), TestContext.Current.CancellationToken);
        await channel.DisposeAsync();

        Assert.Equal(HostChannelState.Open, channel.State);
        Assert.Empty(received.Received);
        Assert.False(received.Completed);
        Assert.Same(NullHostChannel.Instance, NullHostChannel.Instance);
    }
}
