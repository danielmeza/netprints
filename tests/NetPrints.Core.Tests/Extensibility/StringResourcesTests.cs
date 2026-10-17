using System.Resources;
using System.Threading.Tasks;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using Xunit;
using static NetPrints.Tests.Extensibility.ExtensionTestSupport;

namespace NetPrints.Tests.Extensibility;

/// <summary>FR-027, NPXE0011: string resources an extension contributes reach the registry, in registry order.</summary>
public class StringResourcesTests
{
    [Fact]
    public async Task ResourcesAnExtensionAddsAreInTheRegistry()
    {
        var resources = new ResourceManager("Test.Strings", typeof(StringResourcesTests).Assembly);
        var extension = InProcess("test.ext", builder => builder.AddStringResources(resources));

        await using ExtensionRegistry registry = Load(Options([BuiltInExtension.InProcessEntry, extension]));

        Assert.Same(resources, Assert.Single(registry.StringResources));
        Assert.Empty(registry.Issues);
    }

    [Fact]
    public async Task ARegistryWithoutContributionsHasNoResources()
    {
        await using ExtensionRegistry registry = Load(Options([BuiltInExtension.InProcessEntry]));

        Assert.Empty(registry.StringResources);
    }
}
