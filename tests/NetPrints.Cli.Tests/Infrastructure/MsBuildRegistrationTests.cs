using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Workspace;
using Xunit;

namespace NetPrints.Cli.Tests.Infrastructure;

/// <summary>Test classes that call <c>RunRealAsync</c> run in parallel, so the first registrations of a process race; every caller must still see a registered instance.</summary>
public sealed class MsBuildRegistrationTests
{
    private const int Callers = 16;

    [Fact]
    public async Task ConcurrentFirstCallsAllSeeARegisteredInstanceAndNoneThrows()
    {
        using var barrier = new Barrier(Callers);

        bool[] results = await Task.WhenAll(Enumerable.Range(0, Callers).Select(_ => Task.Factory.StartNew(
            () =>
            {
                barrier.SignalAndWait(TestContext.Current.CancellationToken);
                return MsBuildRegistration.EnsureRegistered(NullLogger.Instance);
            },
            TestContext.Current.CancellationToken,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default)));

        Assert.All(results, Assert.True);
        Assert.NotNull(MsBuildRegistration.RegisteredInstance);
    }
}
