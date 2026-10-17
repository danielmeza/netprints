using System.Diagnostics.CodeAnalysis;
using System.Resources;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetPrints.Catalog;
using NetPrints.Core;
using NetPrints.Extensibility.Editor;
using NetPrints.Extensibility.Hosting;
using NetPrints.Extensibility.Nodes;
using NetPrints.Extensibility.Settings;
using NetPrints.Reflection;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Translator;

namespace NetPrints.Extensibility.Loading;

/// <summary>
/// Everything the loaded extensions contribute, in registry order (extension-points.md §8). Owns the contributed
/// objects and disposes those that are <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/>, awaiting an
/// owned <see cref="IAsyncDisposable"/> directly through <see cref="DisposeAsync"/>.
/// </summary>
public sealed class ExtensionRegistry : IAsyncDisposable
{
    private static readonly ServiceProvider NoServices = new ServiceCollection().BuildServiceProvider();

    private readonly List<object> owned;
    private readonly Dictionary<string, ServiceProvider> providers;
    private readonly List<IServiceScope> sessions = [];
    private readonly ILogger logger;
    private bool disposed;

    internal ExtensionRegistry(
        IReadOnlyList<ExtensionLoadResult> results,
        IReadOnlyList<ExtensionManifest> loaded,
        IReadOnlyList<NodeKindDescriptor> nodeKinds,
        IReadOnlyList<IClassEmitter> classEmitters,
        IReadOnlyList<IMemberEmitter> memberEmitters,
        IReadOnlyList<ITypeCatalog> typeCatalogs,
        IReadOnlyList<IProjectProfile> profiles,
        IReadOnlyList<IJsonTypeInfoResolver> jsonResolvers,
        IReadOnlyList<string> projectProperties,
        IReadOnlyList<IHostChannelFactory> hostChannels,
        IReadOnlyList<ExtensionSettingsDescriptor> settings,
        IReadOnlyList<CatalogProfile> catalogProfiles,
        IReadOnlyList<ResourceManager> stringResources,
        IReadOnlyList<INodeSuggestionProvider> suggestionProviders,
        IReadOnlyList<ProjectTemplateDescriptor> projectTemplates,
        IReadOnlyList<ExtensionContributionIssue> issues,
        TranslationEnvironment translation,
        NodeDocumentConverterRegistry nodeConverters,
        NodeDataMigrator nodeMigrator,
        List<object> owned,
        Dictionary<string, ServiceProvider> providers,
        ILogger logger)
    {
        this.providers = providers;
        Results = results;
        Loaded = loaded;
        NodeKinds = nodeKinds;
        ClassEmitters = classEmitters;
        MemberEmitters = memberEmitters;
        TypeCatalogs = typeCatalogs;
        Profiles = profiles;
        JsonTypeInfoResolvers = jsonResolvers;
        ProjectProperties = projectProperties;
        HostChannels = hostChannels;
        Settings = settings;
        CatalogProfiles = catalogProfiles;
        StringResources = stringResources;
        SuggestionProviders = suggestionProviders;
        ProjectTemplates = projectTemplates;
        Issues = issues;
        Translation = translation;
        NodeConverters = nodeConverters;
        NodeMigrator = nodeMigrator;
        this.owned = owned;
        this.logger = logger;
    }

    /// <summary>
    /// Every manifest found: the loaded extensions in load order, then the failures.
    /// </summary>
    public IReadOnlyList<ExtensionLoadResult> Results { get; }

    /// <summary>
    /// The manifests of the extensions that loaded, in load order.
    /// </summary>
    public IReadOnlyList<ExtensionManifest> Loaded { get; }

    /// <summary>
    /// The accepted node kinds, in registry order.
    /// </summary>
    public IReadOnlyList<NodeKindDescriptor> NodeKinds { get; }

    /// <summary>
    /// The class emitters, in registry order.
    /// </summary>
    [Experimental(ExperimentalApiIds.Emitters, UrlFormat = ExperimentalApiIds.UrlFormat)]
    public IReadOnlyList<IClassEmitter> ClassEmitters { get; }

    /// <summary>
    /// The member emitters, in registry order.
    /// </summary>
    [Experimental(ExperimentalApiIds.Emitters, UrlFormat = ExperimentalApiIds.UrlFormat)]
    public IReadOnlyList<IMemberEmitter> MemberEmitters { get; }

    /// <summary>
    /// The type catalogs, in registry order.
    /// </summary>
    public IReadOnlyList<ITypeCatalog> TypeCatalogs { get; }

    /// <summary>
    /// The project profiles, <see cref="DefaultProjectProfile"/> first.
    /// </summary>
    public IReadOnlyList<IProjectProfile> Profiles { get; }

    /// <summary>
    /// The JSON resolvers extensions contributed for their node documents, in registry order.
    /// </summary>
    public IReadOnlyList<IJsonTypeInfoResolver> JsonTypeInfoResolvers { get; }

    /// <summary>
    /// The MSBuild property names extensions asked the project system to capture, without duplicates.
    /// </summary>
    public IReadOnlyList<string> ProjectProperties { get; }

    /// <summary>
    /// The host channel factories, in registry order.
    /// </summary>
    [Experimental(ExperimentalApiIds.HostChannel, UrlFormat = ExperimentalApiIds.UrlFormat)]
    public IReadOnlyList<IHostChannelFactory> HostChannels { get; }

    /// <summary>
    /// The settings sections extensions declared, in registry order.
    /// </summary>
    [Experimental(ExperimentalApiIds.Settings, UrlFormat = ExperimentalApiIds.UrlFormat)]
    public IReadOnlyList<ExtensionSettingsDescriptor> Settings { get; }

    /// <summary>
    /// The catalog profiles extensions contributed, in registry order (the built-in ones are not listed).
    /// </summary>
    [Experimental(ExperimentalApiIds.CatalogProfiles, UrlFormat = ExperimentalApiIds.UrlFormat)]
    public IReadOnlyList<CatalogProfile> CatalogProfiles { get; }

    /// <summary>
    /// The string resources extensions contributed, in registry order; a <see cref="LocalizedText"/> resolves through them.
    /// </summary>
    [Experimental(ExperimentalApiIds.StringResources, UrlFormat = ExperimentalApiIds.UrlFormat)]
    public IReadOnlyList<ResourceManager> StringResources { get; }

    /// <summary>
    /// The node search providers extensions contributed, in registry order; node search asks each one.
    /// </summary>
    [Experimental(ExperimentalApiIds.NodeSearchProviders, UrlFormat = ExperimentalApiIds.UrlFormat)]
    public IReadOnlyList<INodeSuggestionProvider> SuggestionProviders { get; }

    /// <summary>
    /// The project templates extensions contributed, in registry order; an editor host adds them to the New project flow.
    /// </summary>
    [Experimental(ExperimentalApiIds.ProjectTemplates, UrlFormat = ExperimentalApiIds.UrlFormat)]
    public IReadOnlyList<ProjectTemplateDescriptor> ProjectTemplates { get; }

    /// <summary>
    /// The contributions that were rejected (<c>NPX006</c>, or <c>NPX004</c> and <c>NPX013</c> for an id problem) while their extension stayed loaded.
    /// </summary>
    public IReadOnlyList<ExtensionContributionIssue> Issues { get; }

    /// <summary>
    /// The node translators and emitters for a <see cref="ClassTranslator"/>.
    /// </summary>
    public TranslationEnvironment Translation { get; }

    /// <summary>
    /// The node document converters and JSON resolvers for the document mapper.
    /// </summary>
    public NodeDocumentConverterRegistry NodeConverters { get; }

    /// <summary>
    /// The migrator that upgrades the stored data of node kinds that declare a data version, from the extensions' document migrations (<c>NPXE0013</c>).
    /// </summary>
    [Experimental(ExperimentalApiIds.NodeDataMigrations, UrlFormat = ExperimentalApiIds.UrlFormat)]
    public NodeDataMigrator NodeMigrator { get; }

    /// <summary>
    /// Finds a project profile.
    /// </summary>
    /// <param name="id">The profile id.</param>
    /// <returns>The profile, or <see langword="null"/> when none has that id.</returns>
    public IProjectProfile? FindProfile(string id) => Profiles.FirstOrDefault(profile => profile.Id == id);

    /// <summary>
    /// Finds a host channel factory.
    /// </summary>
    /// <param name="id">The factory id.</param>
    /// <returns>The factory, or <see langword="null"/> when none has that id.</returns>
    [Experimental(ExperimentalApiIds.HostChannel, UrlFormat = ExperimentalApiIds.UrlFormat)]
    public IHostChannelFactory? FindHostChannel(string id) => HostChannels.FirstOrDefault(factory => factory.Id == id);

    /// <summary>
    /// Gets the provider of one extension: its own registrations plus <see cref="HostExports"/>.
    /// </summary>
    /// <param name="extensionId">The extension id.</param>
    /// <returns>The extension's provider; an empty provider for an id that registered no services or did not load.</returns>
    [Experimental(ExperimentalApiIds.ExtensionServices, UrlFormat = ExperimentalApiIds.UrlFormat)]
    public IServiceProvider ServicesOf(string extensionId)
    {
        ArgumentNullException.ThrowIfNull(extensionId);
        return providers.GetValueOrDefault(extensionId) ?? NoServices;
    }

    /// <summary>
    /// Opens a session scope of one extension: the scoped services of its provider, disposed before any provider.
    /// </summary>
    /// <param name="extensionId">The extension id.</param>
    /// <returns>The scope; disposing it ends the session. An empty scope for an id that registered no services.</returns>
    [Experimental(ExperimentalApiIds.ExtensionServices, UrlFormat = ExperimentalApiIds.UrlFormat)]
    public IServiceScope OpenSession(string extensionId)
    {
        ArgumentNullException.ThrowIfNull(extensionId);
        ObjectDisposedException.ThrowIf(disposed, this);
        var session = new TrackedSession(providers.GetValueOrDefault(extensionId) ?? NoServices, this);
        lock (sessions)
        {
            sessions.Add(session);
        }

        return session;
    }

    private void Forget(TrackedSession session)
    {
        lock (sessions)
        {
            sessions.Remove(session);
        }
    }

    private sealed class TrackedSession(ServiceProvider provider, ExtensionRegistry registry) : IServiceScope, IAsyncDisposable
    {
        private readonly AsyncServiceScope scope = provider.CreateAsyncScope();

        public IServiceProvider ServiceProvider => scope.ServiceProvider;

        public void Dispose()
        {
            registry.Forget(this);
            scope.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            registry.Forget(this);
            return scope.DisposeAsync();
        }
    }

    /// <summary>
    /// Disposes the contributed objects that are <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/>,
    /// awaiting an owned <see cref="IAsyncDisposable"/> directly; a failing one is logged and does not stop
    /// the others. Idempotent.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        IServiceScope[] open;
        lock (sessions)
        {
            open = [.. sessions];
            sessions.Clear();
        }

        IEnumerable<object> inOrder = [.. open, .. owned.Distinct(ReferenceEqualityComparer.Instance), .. providers.Values];
        foreach (object item in inOrder)
        {
            try
            {
                switch (item)
                {
                    // R1-10: IAsyncDisposable is matched first so a contribution implementing both is
                    // disposed asynchronously, not synchronously.
                    case IAsyncDisposable asyncDisposable:
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                        break;
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.DisposeFailed(logger, ex, item.GetType().FullName ?? item.GetType().Name);
            }
        }
    }
}
