using NetPrints.Editor.Hosting;
using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;

namespace NetPrints.Editor.Tests.Search;

/// <summary>A <see cref="GraphTestBase"/> over a <see cref="FixtureReflectionHost"/>; it disposes the editor it creates.</summary>
public abstract class FixtureGraphTestBase : GraphTestBase, IAsyncDisposable
{
    private readonly TestEditor owned;

    protected FixtureGraphTestBase(IReflectionHost sharedReflection)
        : this(new TestEditor(new FixtureReflectionHost(sharedReflection)))
    {
    }

    private FixtureGraphTestBase(TestEditor owned)
        : base(owned)
    {
        this.owned = owned;
    }

    public async ValueTask DisposeAsync()
    {
        Dispose();
        await owned.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
