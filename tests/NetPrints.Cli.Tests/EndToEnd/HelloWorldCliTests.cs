using System;
using System.Threading.Tasks;
using NetPrints.Cli.Tests.Support;
using Xunit;

namespace NetPrints.Cli.Tests.EndToEnd;

/// <summary>CL-T11: the whole tool against the real SDK: a temp copy of HelloWorld generates, builds and runs.</summary>
public sealed class HelloWorldCliTests : IDisposable
{
    private readonly SampleCopy _sample = new();

    public void Dispose() => _sample.Dispose();

    [Fact]
    public async Task RunPrintsHelloWorldAndExitsZero()
    {
        var host = new CliTestHost(_sample.Directory);

        int exitCode = await host.RunRealAsync("run", _sample.Project);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Hello, World!", host.Output, StringComparison.Ordinal);
    }
}
