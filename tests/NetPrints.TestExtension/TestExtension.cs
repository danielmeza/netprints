using NetPrints.Extensibility;

namespace NetPrints.TestExtension;

/// <summary>
/// The extension the loader tests load from a folder through its own <c>AssemblyLoadContext</c>: one node kind, both
/// emitters, a catalog, a profile, a settings section, a host channel factory and a project property.
/// </summary>
public sealed class TestExtension : INetPrintsExtension
{
    /// <summary>The extension id, also the manifest id.</summary>
    public const string Id = "netprints.test";

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder
            .AddNodeLibrary(new TestNodeLibrary())
            .AddJsonTypeInfoResolver(TestJsonContext.Default)
            .AddClassEmitter(new TestClassEmitter())
            .AddMemberEmitter(new TestMemberEmitter())
            .AddTypeCatalog(TestCatalog.Create())
            .AddProjectProfile(new TestProfile())
            .AddSettings(TestSettings.Descriptor)
            .AddHostChannel(new TestHostChannelFactory())
            .AddProjectProperty(TestProfile.ModeProperty);
    }
}
