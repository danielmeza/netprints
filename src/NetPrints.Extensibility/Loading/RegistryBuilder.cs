using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using NetPrints.Core;
using NetPrints.Extensibility.Nodes;
using NetPrints.Reflection;
using NetPrints.Serialization.Mapping;
using NetPrints.Translator;

namespace NetPrints.Extensibility.Loading;

/// <summary>
/// Commits the contributions of loaded extensions in load order, rejecting conflicting ones, and builds the
/// <see cref="ExtensionRegistry"/>.
/// </summary>
internal sealed class RegistryBuilder(ILogger logger)
{
    private readonly List<NodeKindDescriptor> nodeKinds = [];
    private readonly HashSet<string> kindIds = new(StringComparer.Ordinal);
    private readonly HashSet<Type> nodeTypes = [];
    private readonly HashSet<Type> documentTypes = [];
    private readonly List<IClassEmitter> classEmitters = [];
    private readonly List<IMemberEmitter> memberEmitters = [];
    private readonly List<ITypeCatalog> typeCatalogs = [];
    private readonly List<IProjectProfile> profiles = [DefaultProjectProfile.Instance];
    private readonly HashSet<string> profileIds = new(StringComparer.Ordinal) { DefaultProjectProfile.ProfileId };
    private readonly List<IJsonTypeInfoResolver> resolvers = [];
    private readonly List<string> projectProperties = [];
    private readonly List<object> owned = [];
    private readonly List<ExtensionContributionIssue> issues = [];

    public void Commit(ExtensionManifest manifest, ExtensionContributions contributions)
    {
        foreach (INodeLibrary library in contributions.NodeLibraries)
        {
            owned.Add(library);
            CommitLibrary(manifest, library);
        }

        classEmitters.AddRange(contributions.ClassEmitters);
        memberEmitters.AddRange(contributions.MemberEmitters);
        typeCatalogs.AddRange(contributions.TypeCatalogs);
        resolvers.AddRange(contributions.JsonResolvers);
        owned.AddRange(contributions.ClassEmitters);
        owned.AddRange(contributions.MemberEmitters);
        owned.AddRange(contributions.TypeCatalogs);
        owned.AddRange(contributions.JsonResolvers);

        foreach (IProjectProfile profile in contributions.Profiles)
        {
            if (profile.Id == DefaultProjectProfile.ProfileId)
            {
                RejectOther(manifest, $"profile {profile.Id}", "the default profile cannot be replaced.");
            }
            else if (!profileIds.Add(profile.Id))
            {
                RejectOther(manifest, $"profile {profile.Id}", "a profile with this id is already registered.");
            }
            else
            {
                profiles.Add(profile);
                owned.Add(profile);
            }
        }

        foreach (string property in contributions.ProjectProperties)
        {
            if (!projectProperties.Contains(property, StringComparer.OrdinalIgnoreCase))
            {
                projectProperties.Add(property);
            }
        }
    }

    public ExtensionRegistry Build(IReadOnlyList<ExtensionLoadResult> results, ILoggerFactory loggerFactory)
    {
        var translation = new TranslationEnvironment(
            new NodeTranslatorRegistry(nodeKinds.ToDictionary(kind => kind.NodeType, kind => kind.Translator)),
            classEmitters,
            memberEmitters);
        var converters = new NodeDocumentConverterRegistry(nodeKinds.ConvertAll(kind => kind.Converter), resolvers);

        return new ExtensionRegistry(
            results,
            [.. results.OfType<ExtensionLoadResult.Loaded>().Select(loaded => loaded.Manifest)],
            nodeKinds,
            classEmitters,
            memberEmitters,
            typeCatalogs,
            profiles,
            resolvers,
            projectProperties,
            issues,
            translation,
            converters,
            owned,
            loggerFactory.CreateLogger<ExtensionRegistry>());
    }

    private void CommitLibrary(ExtensionManifest manifest, INodeLibrary library)
    {
        IReadOnlyList<NodeKindDescriptor> descriptors;
        try
        {
            descriptors = library.NodeKinds;
        }
        catch (Exception ex)
        {
            RejectOther(manifest, $"node library {library.Id}", $"reading NodeKinds threw {ex.GetType().Name}: {ex.Message}");
            return;
        }

        foreach (NodeKindDescriptor? descriptor in descriptors)
        {
            string? reason = Validate(manifest, descriptor);
            if (reason is not null)
            {
                RejectKind(manifest, descriptor?.Kind ?? "<null>", reason);
                continue;
            }

            NodeKindDescriptor accepted = descriptor ?? throw new InvalidOperationException("Validated descriptor is null.");
            kindIds.Add(accepted.Kind);
            nodeTypes.Add(accepted.NodeType);
            documentTypes.Add(accepted.Converter.DocumentType);
            nodeKinds.Add(accepted);
        }
    }

    private string? Validate(ExtensionManifest manifest, NodeKindDescriptor? descriptor)
    {
        if (descriptor is null)
        {
            return "the descriptor is null.";
        }

        if (string.IsNullOrEmpty(descriptor.Kind) || descriptor.NodeType is null || descriptor.Converter is null
            || descriptor.Translator is null || descriptor.Suggestions is null)
        {
            return "the descriptor has a missing member.";
        }

        bool builtIn = manifest.Id == BuiltInNodeLibrary.Id;
        if (builtIn ? descriptor.Kind.Contains('/') : !HasPrefix(descriptor.Kind, manifest.Id))
        {
            return builtIn
                ? "built-in kinds have no '/'."
                : $"the kind must start with '{manifest.Id}/' followed by a name.";
        }

        if (descriptor.Kind != descriptor.Converter.Kind)
        {
            return $"Kind differs from the converter's kind '{descriptor.Converter.Kind}'.";
        }

        if (descriptor.NodeType != descriptor.Converter.NodeType)
        {
            return $"NodeType differs from the converter's node type '{descriptor.Converter.NodeType}'.";
        }

        if (!typeof(Graph.Node).IsAssignableFrom(descriptor.NodeType))
        {
            return "NodeType is not a NetPrints.Graph.Node.";
        }

        if (kindIds.Contains(descriptor.Kind))
        {
            return "another node kind with this Kind is already registered.";
        }

        if (nodeTypes.Contains(descriptor.NodeType))
        {
            return $"another node kind for node type '{descriptor.NodeType}' is already registered.";
        }

        if (documentTypes.Contains(descriptor.Converter.DocumentType))
        {
            return $"another converter for document type '{descriptor.Converter.DocumentType}' is already registered.";
        }

        try
        {
            _ = new NodeDocumentConverterRegistry([descriptor.Converter], []);
        }
        catch (ArgumentException ex)
        {
            return ex.Message;
        }

        return null;
    }

    private static bool HasPrefix(string kind, string extensionId) =>
        kind.Length > extensionId.Length + 1 && kind.StartsWith(extensionId, StringComparison.Ordinal) && kind[extensionId.Length] == '/';

    private void RejectKind(ExtensionManifest manifest, string kind, string reason)
    {
        issues.Add(new ExtensionContributionIssue(manifest.Id, ExtensionDiagnosticCodes.ContributionRejected, $"node kind {kind}", reason));
        Log.NodeKindConflict(logger, kind, manifest.Id, reason);
    }

    private void RejectOther(ExtensionManifest manifest, string contribution, string reason)
    {
        issues.Add(new ExtensionContributionIssue(manifest.Id, ExtensionDiagnosticCodes.ContributionRejected, contribution, reason));
        Log.ContributionRejected(logger, contribution, manifest.Id, reason);
    }
}
