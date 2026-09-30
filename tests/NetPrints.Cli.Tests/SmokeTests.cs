using Xunit;

namespace NetPrints.Cli.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void ProjectLoads()
    {
        Assert.Equal("NetPrints.Cli", typeof(NetPrints.Cli.CliApplication).Assembly.GetName().Name);
    }
}
