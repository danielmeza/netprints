namespace NetPrints.Core;

/// <summary>
/// The diagnostic ids of the <see cref="System.Diagnostics.CodeAnalysis.ExperimentalAttribute"/> that marks
/// unstable extension APIs (ADR-0010, ADR-0017). A project that uses a marked API opts in to its id.
/// </summary>
public static class ExperimentalApiIds
{
    /// <summary>The host channel API: <c>IHostChannel</c>, <c>IHostChannelFactory</c>, <c>HostMessage</c>.</summary>
    public const string HostChannel = "NPXE0001";

    /// <summary>The extension settings API: <c>ExtensionSettingsDescriptor</c>, <c>ISettingsStore</c>.</summary>
    public const string Settings = "NPXE0002";

    /// <summary>The class and member emitter API: <c>IClassEmitter</c>, <c>IMemberEmitter</c>.</summary>
    public const string Emitters = "NPXE0003";

    /// <summary>The catalog engine and profile API.</summary>
    public const string CatalogProfiles = "NPXE0004";

    /// <summary>The editor contributions API: the typed contexts (<c>ICommandContext</c>, <c>IProjectSession</c>, <c>IGraphEditor</c>) and the editor descriptors.</summary>
    public const string EditorContributions = "NPXE0005";

    /// <summary>The node search provider API: <c>INodeSuggestionProvider</c>, <c>NodeSearchContext</c>, <c>NodeSearchEntry</c>, <c>IExtensionBuilder.AddSuggestionProvider</c>.</summary>
    public const string NodeSearchProviders = "NPXE0006";

    /// <summary>The C# feature API: <c>CSharpFeature</c>, <c>CSharpFeatureId</c>, <c>Availability</c> and <c>NodeSearchEntry.Requires</c>.</summary>
    public const string CSharpFeatures = "NPXE0007";

    /// <summary>The extension services API: <c>IExtensionBuilder.Services</c>, <c>HostExports</c> and the per-extension providers and sessions of <c>ExtensionRegistry</c>.</summary>
    public const string ExtensionServices = "NPXE0008";

    /// <summary>The settings metadata API: <c>PathKind</c>, <c>RequiresRestartAttribute</c>, <c>SettingPathAttribute</c> and <c>SettingChoicesAttribute</c>. The page descriptor is part of the editor contributions (<c>NPXE0005</c>).</summary>
    public const string SettingsPages = "NPXE0009";

    /// <summary>The extension string resources API: <c>IExtensionBuilder.AddStringResources</c> and the registry's <c>StringResources</c>.</summary>
    public const string StringResources = "NPXE0011";

    /// <summary>The project template files API: <c>TemplateFile</c>, <c>ProjectTemplateDescriptor</c> and <c>IEditorContributions.AddProjectTemplate</c>.</summary>
    public const string ProjectTemplates = "NPXE0012";

    /// <summary>The node-data migration API: <c>INodeDataMigration</c>, <c>NodeKindDescriptor.DataVersion</c> and <c>IExtensionBuilder.AddDocumentMigration</c>.</summary>
    public const string NodeDataMigrations = "NPXE0013";

    /// <summary>The guide section that explains the stability promise and how to opt in; the diagnostics link to it.</summary>
    public const string UrlFormat = "https://danielmeza.github.io/netprints/guide/extensions#api-stability";
}
