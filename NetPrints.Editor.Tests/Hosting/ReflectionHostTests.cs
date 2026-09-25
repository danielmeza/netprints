using NetPrints.Core;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Tests.Hosting;

public class ReflectionHostTests
{
    [Fact(Timeout = 120000)]
    public async Task ReloadPublishesTypesAndRaisesReloaded()
    {
        var host = new ReflectionHost(new InlineDispatcher());
        int reloaded = 0;
        host.Reloaded += (_, _) => reloaded++;

        Assert.Empty(host.NonStaticTypes);
        Assert.NotNull(host.Provider);

        await host.ReloadAsync(Project.CreateNew("P", "N"));

        Assert.Equal(1, reloaded);
        Assert.True(host.NonStaticTypes.Count > 4000);
        Assert.Empty(host.LastWarnings);
        Assert.True(host.Provider.GetNonStaticTypes().Contains(TypeSpecifier.FromType<string>()));
    }

    [Fact(Timeout = 120000)]
    public async Task MissingReferencesAreReportedNotThrown()
    {
        var host = new ReflectionHost(new InlineDispatcher());
        var project = Project.CreateNew("P", "N");
        project.References.Add(new AssemblyReference("/does/not/exist.dll"));
        project.References.Add(new SourceDirectoryReference("/does/not/exist"));

        await host.ReloadAsync(project);

        Assert.Equal(2, host.LastWarnings.Count());
        Assert.True(host.NonStaticTypes.Count > 4000);
    }

    [Fact(Timeout = 120000)]
    public async Task ProjectClassesAreVisibleToReflection()
    {
        var host = new ReflectionHost(new InlineDispatcher());
        var project = TestPaths.LoadHelloWorldCopy();
        try
        {
            await host.ReloadAsync(project);
            Assert.True(host.NonStaticTypes.Any(t => t.Name == "HelloWorld.Program"));
        }
        finally
        {
            TestPaths.TryDelete(project.Path);
        }
    }
}
