using System;
using System.Threading;
using System.Threading.Tasks;

namespace NetPrints.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        return await CliApplication.RunAsync(args, CliServices.CreateDefault(), cancellation.Token).ConfigureAwait(false);
    }
}
