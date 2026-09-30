using System.Threading;
using System.Threading.Tasks;
using NetPrints.Projects;

namespace NetPrints.Cli.Infrastructure;

/// <summary>Runs the user's program for <c>netprints run</c> and reports its exit code; the program's output is not buffered by the tool.</summary>
internal interface IProgramRunner
{
    /// <summary>Starts <paramref name="request"/> and waits for it to exit.</summary>
    /// <param name="request">The process to start.</param>
    /// <param name="cancellationToken">Cancels the wait; the process tree is killed.</param>
    /// <returns>The process exit code.</returns>
    Task<int> RunAsync(ProcessStartRequest request, CancellationToken cancellationToken);
}
