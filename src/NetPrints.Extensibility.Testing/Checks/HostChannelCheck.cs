using System.Text.Json;
using NetPrints.Extensibility.Hosting;

namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>NPK010: each host channel of the extension is created in a live state, takes and releases a subscriber, is closed by disposal that can repeat, completes its messages and refuses a message once closed (the IHostChannel contract).</summary>
internal sealed class HostChannelCheck : ConformanceCheck
{
    private const string Rule = "a host channel closes when it is disposed";
    private const string ProbeType = "netprints.conformance-probe";

    private static readonly JsonElement ProbePayload = JsonDocument.Parse("{}").RootElement.Clone();

    public override string Code => NpkDiagnosticCodes.HostChannel;

    public override string Name => "Host channel lifecycle";

    public override async Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        IReadOnlyList<IHostChannelFactory> factories = context.Harness.Registry.HostChannels;
        if (factories.Count == 0)
        {
            return CheckOutcome.Passed("no host channel");
        }

        var findings = new List<ConformanceFinding>();
        foreach (IHostChannelFactory factory in factories)
        {
            string problem = await ExerciseAsync(factory, cancellationToken).ConfigureAwait(false);
            if (problem.Length > 0)
            {
                findings.Add(Finding(Rule, $"{factory.Id}: {problem}"));
            }
        }

        return CheckOutcome.FromFindings(findings);
    }

    private static async Task<string> ExerciseAsync(IHostChannelFactory factory, CancellationToken cancellationToken)
    {
        IHostChannel channel;
        try
        {
            channel = factory.Create(new HostLaunchContext(new Dictionary<string, string>()));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return $"Create failed: {ex.GetType().Name}: {ex.Message}";
        }

        Func<HostChannelState> stateOf = () => channel.State;
        Func<ValueTask> dispose = channel.DisposeAsync;
        try
        {
            await using (channel.ConfigureAwait(false))
            {
                if (stateOf() == HostChannelState.Closed)
                {
                    return "the channel is closed when it is created";
                }

                var observer = new IgnoringObserver();
                using IDisposable subscription = channel.Messages.Subscribe(observer);
                await dispose().ConfigureAwait(false);
                if (stateOf() != HostChannelState.Closed)
                {
                    return $"the channel is {stateOf()} after it is disposed";
                }

                if (!observer.Completed)
                {
                    return "Messages did not complete when the channel closed";
                }

                try
                {
                    await channel.SendAsync(new HostMessage(ProbeType, ProbePayload), cancellationToken).ConfigureAwait(false);
                    return "SendAsync on a closed channel did not throw InvalidOperationException";
                }
                catch (InvalidOperationException)
                {
                    // IHostChannel documents a closed channel as refusing a message.
                }
            }

            await dispose().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return string.Empty;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    private sealed class IgnoringObserver : IObserver<HostMessage>
    {
        public bool Completed { get; private set; }

        public void OnCompleted()
        {
            Completed = true;
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(HostMessage value)
        {
        }
    }
}
