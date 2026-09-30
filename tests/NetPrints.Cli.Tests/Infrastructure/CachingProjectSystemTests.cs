using System;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Cli.Infrastructure;
using NetPrints.Cli.Tests.Support;
using NetPrints.Projects;
using Xunit;

namespace NetPrints.Cli.Tests.Infrastructure;

/// <summary>The per-run cache that lets the catalog command and the source resolver share one project load.</summary>
public sealed class CachingProjectSystemTests
{
    [Fact]
    public async Task ALoadOfTheSamePathIsAnsweredFromTheFirstOne()
    {
        var inner = new FakeProjectSystem();
        var cache = new CachingProjectSystem(inner);

        ProjectSnapshot first = await cache.LoadAsync("/p/App.csproj", TestContext.Current.CancellationToken);
        ProjectSnapshot second = await cache.LoadAsync("/p/App.csproj", TestContext.Current.CancellationToken);

        Assert.Same(first, second);
        Assert.Equal(["/p/App.csproj"], inner.LoadedProjects);
    }

    [Fact]
    public async Task DifferentPathsAreLoadedSeparately()
    {
        var inner = new FakeProjectSystem();
        var cache = new CachingProjectSystem(inner);

        await cache.LoadAsync("/p/A.csproj", TestContext.Current.CancellationToken);
        await cache.LoadAsync("/p/B.csproj", TestContext.Current.CancellationToken);

        Assert.Equal(2, inner.LoadedProjects.Count);
    }

    [Fact]
    public async Task AFailedLoadIsNotCached()
    {
        var inner = new FakeProjectSystem { ThrowOnLoad = new InvalidOperationException("boom") };
        var cache = new CachingProjectSystem(inner);

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.LoadAsync("/p/App.csproj", TestContext.Current.CancellationToken));
        inner.ThrowOnLoad = null;
        await cache.LoadAsync("/p/App.csproj", TestContext.Current.CancellationToken);

        Assert.Equal(2, inner.LoadedProjects.Count);
    }

    [Fact]
    public async Task ACanceledLoadIsNotCached()
    {
        var inner = new FakeProjectSystem { ThrowOnLoad = new OperationCanceledException() };
        var cache = new CachingProjectSystem(inner);

        await Assert.ThrowsAsync<OperationCanceledException>(() => cache.LoadAsync("/p/App.csproj", CancellationToken.None));
        inner.ThrowOnLoad = null;
        await cache.LoadAsync("/p/App.csproj", CancellationToken.None);

        Assert.Equal(2, inner.LoadedProjects.Count);
    }
}
