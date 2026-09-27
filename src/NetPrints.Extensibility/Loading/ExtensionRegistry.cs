using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using NetPrints.Core;
using NetPrints.Extensibility.Nodes;
using NetPrints.Reflection;
using NetPrints.Serialization.Mapping;
using NetPrints.Translator;

namespace NetPrints.Extensibility.Loading;

/// <summary>
/// Everything the loaded extensions contribute, in registry order (extension-points.md §8). Owns the contributed
/// objects and disposes those that are <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/>.
/// </summary>
public sealed class ExtensionRegistry : IDisposable
{
    private readonly List<object> owned;
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
        IReadOnlyList<ExtensionContributionIssue> issues,
        TranslationEnvironment translation,
        NodeDocumentConverterRegistry nodeConverters,
        List<object> owned,
        ILogger logger)
    {
        Results = results;
        Loaded = loaded;
        NodeKinds = nodeKinds;
        ClassEmitters = classEmitters;
        MemberEmitters = memberEmitters;
        TypeCatalogs = typeCatalogs;
        Profiles = profiles;
        JsonTypeInfoResolvers = jsonResolvers;
        ProjectProperties = projectProperties;
        Issues = issues;
        Translation = translation;
        NodeConverters = nodeConverters;
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
    public IReadOnlyList<IClassEmitter> ClassEmitters { get; }

    /// <summary>
    /// The member emitters, in registry order.
    /// </summary>
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
    /// The contributions that were rejected (<c>NPX006</c>) while their extension stayed loaded.
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
    /// Finds a project profile.
    /// </summary>
    /// <param name="id">The profile id.</param>
    /// <returns>The profile, or <see langword="null"/> when none has that id.</returns>
    public IProjectProfile? FindProfile(string id) => Profiles.FirstOrDefault(profile => profile.Id == id);

    /// <summary>
    /// Disposes the contributed objects that are <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/>;
    /// a failing one is logged and does not stop the others. Idempotent.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        foreach (object item in owned.Distinct(ReferenceEqualityComparer.Instance))
        {
            try
            {
                switch (item)
                {
                    case IDisposable disposable:
                        disposable.Dispose();
                        break;
                    case IAsyncDisposable asyncDisposable:
                        asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
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
