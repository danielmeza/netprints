using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using NetPrints.Core;
using NetPrints.Extensibility.Hosting;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Nodes;
using NetPrints.Extensibility.Settings;
using NetPrints.Reflection;
using NetPrints.Translator;

namespace NetPrints.Extensibility;

/// <summary>
/// The contributions one extension buffered during <see cref="INetPrintsExtension.Register"/>.
/// </summary>
internal sealed record ExtensionContributions(
    IReadOnlyList<INodeLibrary> NodeLibraries,
    IReadOnlyList<IClassEmitter> ClassEmitters,
    IReadOnlyList<IMemberEmitter> MemberEmitters,
    IReadOnlyList<ITypeCatalog> TypeCatalogs,
    IReadOnlyList<IProjectProfile> Profiles,
    IReadOnlyList<IJsonTypeInfoResolver> JsonResolvers,
    IReadOnlyList<string> ProjectProperties,
    IReadOnlyList<IHostChannelFactory> HostChannels,
    IReadOnlyList<ExtensionSettingsDescriptor> Settings);

/// <summary>
/// The builder handed to <see cref="INetPrintsExtension.Register"/>: it buffers, and the loader commits the
/// buffer only if <c>Register</c> returns normally.
/// </summary>
internal sealed class ExtensionBuilder(ExtensionManifest manifest, ILoggerFactory loggerFactory) : IExtensionBuilder
{
    private readonly List<INodeLibrary> nodeLibraries = [];
    private readonly List<IClassEmitter> classEmitters = [];
    private readonly List<IMemberEmitter> memberEmitters = [];
    private readonly List<ITypeCatalog> typeCatalogs = [];
    private readonly List<IProjectProfile> profiles = [];
    private readonly List<IJsonTypeInfoResolver> jsonResolvers = [];
    private readonly List<string> projectProperties = [];
    private readonly List<IHostChannelFactory> hostChannels = [];
    private readonly List<ExtensionSettingsDescriptor> settings = [];
    private bool sealedBuilder;

    public ExtensionManifest Manifest { get; } = manifest;

    public ILoggerFactory LoggerFactory { get; } = loggerFactory;

    public IExtensionBuilder AddNodeLibrary(INodeLibrary library) => Add(nodeLibraries, library);

    public IExtensionBuilder AddClassEmitter(IClassEmitter emitter) => Add(classEmitters, emitter);

    public IExtensionBuilder AddMemberEmitter(IMemberEmitter emitter) => Add(memberEmitters, emitter);

    public IExtensionBuilder AddTypeCatalog(ITypeCatalog catalog) => Add(typeCatalogs, catalog);

    public IExtensionBuilder AddProjectProfile(IProjectProfile profile) => Add(profiles, profile);

    public IExtensionBuilder AddJsonTypeInfoResolver(IJsonTypeInfoResolver resolver) => Add(jsonResolvers, resolver);

    public IExtensionBuilder AddProjectProperty(string msbuildPropertyName)
    {
        ArgumentNullException.ThrowIfNull(msbuildPropertyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(msbuildPropertyName);
        return Add(projectProperties, msbuildPropertyName);
    }

    public IExtensionBuilder AddHostChannel(IHostChannelFactory factory) => Add(hostChannels, factory);

    public IExtensionBuilder AddSettings(ExtensionSettingsDescriptor descriptor) => Add(settings, descriptor);

    /// <summary>
    /// Ends registration: later builder calls throw. Returns what was buffered.
    /// </summary>
    public ExtensionContributions Seal()
    {
        sealedBuilder = true;
        return new ExtensionContributions(
            [.. nodeLibraries], [.. classEmitters], [.. memberEmitters], [.. typeCatalogs], [.. profiles], [.. jsonResolvers], [.. projectProperties],
            [.. hostChannels], [.. settings]);
    }

    private ExtensionBuilder Add<T>(List<T> list, T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (sealedBuilder)
        {
            throw new InvalidOperationException($"Extension '{Manifest.Id}' already returned from Register; contributions can no longer be added.");
        }

        list.Add(item);
        return this;
    }
}
