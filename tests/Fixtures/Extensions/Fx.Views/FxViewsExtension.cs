using NetPrints.Extensibility;

namespace Fx.Views;

/// <summary>Declares the view resources of its own assembly by URI: a data templates file with a view that throws, an icon dictionary, a styles file, a view that is none of them and a file that does not exist.</summary>
public sealed class FxViewsExtension : INetPrintsExtension
{
    /// <summary>The extension id, also the manifest id.</summary>
    public const string Id = "fx.views";

    /// <summary>The URI of the <c>DataTemplates</c> file.</summary>
    public const string TemplatesUri = "avares://Fx.Views/Templates.axaml";

    /// <summary>The URI of the <c>ResourceDictionary</c> file.</summary>
    public const string IconsUri = "avares://Fx.Views/Icons.axaml";

    /// <summary>The URI of the <c>Styles</c> file.</summary>
    public const string StylesUri = "avares://Fx.Views/Styles.axaml";

    /// <summary>The URI of a view that is not a resource document.</summary>
    public const string ViewUri = "avares://Fx.Views/Other.axaml";

    /// <summary>The URI of a file the assembly does not have.</summary>
    public const string MissingUri = "avares://Fx.Views/Missing.axaml";

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Editor
            .AddViewResources(TemplatesUri)
            .AddViewResources(IconsUri)
            .AddViewResources(StylesUri)
            .AddViewResources(ViewUri)
            .AddViewResources(MissingUri);
    }
}
