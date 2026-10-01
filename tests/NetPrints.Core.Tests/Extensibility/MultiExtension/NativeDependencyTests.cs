using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using NetPrints.Extensibility.Loading;
using NetPrints.Testing.Extensions;
using Xunit;

namespace NetPrints.Tests.Extensibility.MultiExtension;

/// <summary>MX-T16: an extension that ships a native library in its own folder can call it.</summary>
[Collection(nameof(RealExtensionLoadCollection))]
public sealed class NativeDependencyTests : IAsyncLifetime
{
    private readonly string root = Directory.CreateTempSubdirectory("netprints-native-").FullName;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        Directory.Delete(root, recursive: true);
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task TheNativeCallOfAnExtensionReturnsAMilestoneAboveZero()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "The fixture ships the Linux native assets of SkiaSharp.");
        string folder = FixtureExtensions.CopyTo(root, FixtureExtensions.Native);

        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync([folder], [], TestContext.Current.CancellationToken);

        Assert.Contains(harness.Registry.Loaded, m => m.Id == FixtureExtensions.Native);
        Assembly assembly = AssemblyLoadContext.All
            .Where(context => context.Name == FixtureExtensions.Native)
            .SelectMany(context => context.Assemblies)
            .Single(a => a.GetName().Name == "Fx.Native" && a.Location.StartsWith(root, StringComparison.Ordinal));
        Type extension = assembly.GetType("Fx.Native.NativeExtension") ?? throw new InvalidOperationException("NativeExtension not found.");
        PropertyInfo property = extension.GetProperty("Milestone") ?? throw new InvalidOperationException("Milestone not found.");
        Assert.True(Assert.IsType<int>(property.GetValue(null)) > 0);
    }
}
