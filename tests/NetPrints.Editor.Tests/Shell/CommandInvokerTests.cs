using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Tests.Shell;

public sealed class CommandInvokerTests
{
    private sealed class Handler(Func<Task> run) : ICommandHandler
    {
        public bool CanExecute(CommandContext context) => true;

        public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken) => run();
    }

    private sealed class Contexts : ICommandContextProvider
    {
        public CommandContext Create(object? parameter = null) => new(new FakeShell(), null, null, null, CommandSelection.None, parameter);
    }

    private static List<Exception> RunWith(Func<Task> run)
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        var descriptor = new CommandDescriptor(ContributionIds.CommandPrefix + "boom", "boom", new Handler(run));
        registry.AddCommand(descriptor);
        List<Exception> faults = [];
        var invoker = new CommandInvoker(registry, new Contexts(), faults.Add);

        Assert.True(invoker.TryRun(descriptor));
        return faults;
    }

    [Fact]
    public void ASynchronousThrowReachesTheFaultCallbackInsteadOfEscaping()
    {
        var thrown = new InvalidOperationException("sync");

        List<Exception> faults = RunWith(() => throw thrown);

        Assert.Same(thrown, Assert.Single(faults));
    }

    [Fact]
    public async Task AnAsynchronousFaultReachesTheFaultCallback()
    {
        var thrown = new InvalidOperationException("async");

        List<Exception> faults = RunWith(async () =>
        {
            await Task.Yield();
            throw thrown;
        });

        Assert.True(SpinWait.SpinUntil(() => faults.Count > 0, TimeSpan.FromSeconds(10)));
        await Task.Yield();
        Assert.Same(thrown, Assert.Single(faults));
    }
}
