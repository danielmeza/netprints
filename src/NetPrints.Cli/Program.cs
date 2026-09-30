using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetPrints.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        return await CliApplication.RunAsync(args, CliServices.CreateDefault(), cancellation.Token).ConfigureAwait(false);
    }
}
