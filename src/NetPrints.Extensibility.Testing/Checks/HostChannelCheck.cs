using System.Text.Json;
using NetPrints.Extensibility.Hosting;

namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>NPK010: each host channel of the extension is created in a live state, takes and releases a subscriber, is closed by disposal that can repeat, completes its messages within 5 seconds of closing, on whatever thread, and refuses a message once closed (the IHostChannel contract).</summary>
internal sealed class HostChannelCheck : ConformanceCheck
{
    private const string Rule = "a host channel closes when it is disposed";
    private const string ProbeType = "netprints.conformance-probe";

    private static readonly JsonElement ProbePayload = JsonDocument.Parse("{}").RootElement.Clone();
    private static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(5);

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

                var observer = new CompletionObserver();
                using IDisposable subscription = channel.Messages.Subscribe(observer);
                await dispose().ConfigureAwait(false);
                if (stateOf() != HostChannelState.Closed)
                {
                    return $"the channel is {stateOf()} after it is disposed";
                }

                if (!await observer.CompletesWithinAsync(CompletionTimeout, cancellationToken).ConfigureAwait(false))
                {
                    return "Messages did not complete within 5 seconds of the channel closing";
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

    private sealed class CompletionObserver : IObserver<HostMessage>
    {
        private readonly TaskCompletionSource completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<bool> CompletesWithinAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            try
            {
                await completed.Task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        public void OnCompleted() => completed.TrySetResult();

        public void OnError(Exception error)
        {
        }

        public void OnNext(HostMessage value)
        {
        }
    }
}
