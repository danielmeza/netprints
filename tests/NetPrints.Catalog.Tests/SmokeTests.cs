using System.Reflection;
using Xunit;

namespace NetPrints.Catalog.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void ProjectLoads()
    {
        Assert.Equal("NetPrints.Catalog", Assembly.Load(new AssemblyName("NetPrints.Catalog")).GetName().Name);
    }
}
