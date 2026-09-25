using NetPrints.Core;
using NetPrints.Editor.Tests.Fakes;
using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Tests.ViewModels;

[TestClass]
public class ReflectionHostTests
{
    [TestMethod]
    [Timeout(120000, CooperativeCancellation = true)]
    public async Task ReloadPublishesTypesAndRaisesReloaded()
    {
        var host = new ReflectionHost(new InlineDispatcher());
        int reloaded = 0;
        host.Reloaded += (_, _) => reloaded++;

        Assert.IsEmpty(host.NonStaticTypes);
        Assert.IsNotNull(host.Provider, "an empty provider exists before the first load");

        await host.ReloadAsync(Project.CreateNew("P", "N"));

        Assert.AreEqual(1, reloaded);
        Assert.IsGreaterThan(4000, host.NonStaticTypes.Count);
        Assert.IsEmpty(host.LastWarnings);
        Assert.IsTrue(host.Provider.GetNonStaticTypes().Contains(TypeSpecifier.FromType<string>()));
    }

    [TestMethod]
    [Timeout(120000, CooperativeCancellation = true)]
    public async Task MissingReferencesAreReportedNotThrown()
    {
        var host = new ReflectionHost(new InlineDispatcher());
        var project = Project.CreateNew("P", "N");
        project.References.Add(new AssemblyReference("/does/not/exist.dll"));
        project.References.Add(new SourceDirectoryReference("/does/not/exist"));

        await host.ReloadAsync(project);

        Assert.HasCount(2, host.LastWarnings);
        Assert.IsGreaterThan(4000, host.NonStaticTypes.Count);
    }

    [TestMethod]
    [Timeout(120000, CooperativeCancellation = true)]
    public async Task ProjectClassesAreVisibleToReflection()
    {
        var host = new ReflectionHost(new InlineDispatcher());
        var project = TestPaths.LoadHelloWorldCopy();
        try
        {
            await host.ReloadAsync(project);
            Assert.IsTrue(host.NonStaticTypes.Any(t => t.Name == "HelloWorld.Program"));
        }
        finally
        {
            TestPaths.TryDelete(project.Path);
        }
    }
}
