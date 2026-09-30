using Xunit;

namespace NetPrints.Cli.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void ProjectLoads()
    {
        Assert.Equal("NetPrints.Cli", typeof(NetPrintsCLI.Program).Assembly.GetName().Name);
    }
}
