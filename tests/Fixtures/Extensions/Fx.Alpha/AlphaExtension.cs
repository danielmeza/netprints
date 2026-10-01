using System.Text.Json.Serialization;
using Fx.Kit;
using NetPrints.Catalog;
using NetPrints.Core;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Hosting;
using NetPrints.Extensibility.Settings;
using NetPrints.Graph;
using NetPrints.Translator;

namespace Fx.Alpha;

/// <summary>The node of kind <see cref="AlphaExtension.PingKind"/>.</summary>
public sealed class AlphaPingNode : Node
{
    /// <summary>Creates the node and adds it to <paramref name="graph"/>.</summary>
    /// <param name="graph">The graph.</param>
    public AlphaPingNode(NodeGraph graph)
        : base(graph)
    {
        AddInputExecPin("Exec");
        AddOutputExecPin("Then");
    }
}

/// <summary>Marks every class it emits with the extension id as a description attribute.</summary>
public sealed class AlphaClassEmitter : IClassEmitter
{
    /// <summary>The marker the emitter adds.</summary>
    public const string Marker = "System.ComponentModel.Description(\"fx.alpha\")";

    /// <inheritdoc />
    public string Id => "fx.alpha/class";

    /// <inheritdoc />
    public void EmitClass(ClassEmitContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Attributes.Add(Marker);
    }
}

/// <summary>The extension's settings section.</summary>
/// <param name="Greeting">A free-text value.</param>
public sealed record AlphaSettings(string Greeting);

/// <summary>The source-generated JSON metadata of <see cref="AlphaSettings"/>.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AlphaSettings))]
public sealed partial class AlphaJsonContext : JsonSerializerContext;

/// <summary>The <c>fx.alpha.profile</c> project profile.</summary>
public sealed class AlphaProfile : IProjectProfile
{
    /// <summary>The profile id.</summary>
    public const string ProfileId = "fx.alpha.profile";

    /// <inheritdoc />
    public string Id => ProfileId;

    /// <inheritdoc />
    public string DisplayName => "Fixture alpha project";

    /// <inheritdoc />
    public string DefaultTargetFramework => "net10.0";

    /// <inheritdoc />
    public string ProjectTemplate => DefaultProjectProfile.Instance.ProjectTemplate;

    /// <inheritdoc />
    public IReadOnlyList<TypeSpecifier> BaseTypes { get; } = [TypeSpecifier.FromType<object>()];

    /// <inheritdoc />
    public IReadOnlyList<ClassTemplate> ClassTemplates => DefaultProjectProfile.Instance.ClassTemplates;

    /// <inheritdoc />
    public string? CatalogProfileId => null;
}

/// <summary>The host channel factory <c>fx.alpha.channel</c>: an in-memory pair.</summary>
public sealed class AlphaHostChannelFactory : IHostChannelFactory
{
    /// <summary>The factory id.</summary>
    public const string FactoryId = "fx.alpha.channel";

    /// <inheritdoc />
    public string Id => FactoryId;

    /// <inheritdoc />
    public IHostChannel Create(HostLaunchContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        (InMemoryHostChannel editor, _) = InMemoryHostChannel.CreatePair(FactoryId);
        return editor;
    }
}

/// <summary>One contribution of every kind: a node kind with its CLR type, document type and JSON resolver, a class emitter, a settings section, a project profile, a host channel, a catalog profile and a project property.</summary>
public sealed class AlphaExtension : INetPrintsExtension
{
    /// <summary>The extension id, also the manifest id.</summary>
    public const string Id = "fx.alpha";

    /// <summary>The kind id of <see cref="AlphaPingNode"/>.</summary>
    public const string PingKind = "fx.alpha/Ping";

    /// <summary>The id of the catalog profile.</summary>
    public const string CatalogProfileId = "fx-alpha";

    /// <summary>The MSBuild property the extension asks the project system to capture.</summary>
    public const string ProjectProperty = "FxAlphaProperty";

    /// <summary>The text the Ping translator writes.</summary>
    public const string PingText = "fx.alpha ping";

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder
            .AddNodeLibrary(new FxNodeLibrary(Id, FxKit.Kind(PingKind, typeof(AlphaPingNode), graph => new AlphaPingNode(graph), () => PingText)))
            .AddJsonTypeInfoResolver(FxKitJsonContext.Default)
            .AddJsonTypeInfoResolver(AlphaJsonContext.Default)
            .AddClassEmitter(new AlphaClassEmitter())
            .AddSettings(new ExtensionSettingsDescriptor<AlphaSettings>(Id, AlphaJsonContext.Default.AlphaSettings, new AlphaSettings("hello")))
            .AddProjectProfile(new AlphaProfile())
            .AddHostChannel(new AlphaHostChannelFactory())
            .AddCatalogProfile(new CatalogProfile(CatalogProfileId))
            .AddProjectProperty(ProjectProperty);
    }
}
