using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Extensibility.Editor;

/// <summary>
/// The editor's contribution surface, reached through <see cref="IExtensionBuilder.Editor"/>. The editor registers its own commands, panels,
/// tiles, menus, providers and views through it as the in-process extension <c>netprints.editor</c>; a host without an editor (the CLI,
/// the SDK generator, the build) hands out a Null Object that accepts and ignores every call. Every method returns the facet for chaining and
/// throws <see cref="ArgumentNullException"/> for a <see langword="null"/> argument.
/// </summary>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public interface IEditorContributions
{
    /// <summary>Adds a command; an invalid or duplicate one is reported and ignored by the editor.</summary>
    /// <param name="command">The command.</param>
    /// <returns>This facet.</returns>
    IEditorContributions AddCommand(CommandDescriptor command);

    /// <summary>Adds a dockable tool panel.</summary>
    /// <param name="panel">The panel.</param>
    /// <returns>This facet.</returns>
    IEditorContributions AddPanel(PanelDescriptor panel);

    /// <summary>Adds a start page tile.</summary>
    /// <param name="tile">The tile.</param>
    /// <returns>This facet.</returns>
    IEditorContributions AddDashboardTile(DashboardTileDescriptor tile);

    /// <summary>Adds a context-menu entry that runs a registered command.</summary>
    /// <param name="item">The entry.</param>
    /// <returns>This facet.</returns>
    IEditorContributions AddContextMenuItem(ContextMenuItemDescriptor item);

    /// <summary>Adds a tooltip provider.</summary>
    /// <param name="provider">The provider.</param>
    /// <returns>This facet.</returns>
    IEditorContributions AddTooltipProvider(ITooltipProvider provider);

    /// <summary>Adds a go-to-anything provider.</summary>
    /// <param name="provider">The provider.</param>
    /// <returns>This facet.</returns>
    IEditorContributions AddGoToProvider(IGoToProvider provider);

    /// <summary>
    /// Adds the resources of this extension's views: an <c>avares</c> URI of a document whose root is a <c>DataTemplates</c> list (view templates),
    /// a <c>ResourceDictionary</c> (its <c>Icon.&lt;extension id&gt;.&lt;name&gt;</c> geometries) or a <c>Styles</c> file. An extension with more than one kind calls this once for each.
    /// </summary>
    /// <param name="resourceUri">The <c>avares://</c> URI of the dictionary.</param>
    /// <returns>This facet.</returns>
    /// <exception cref="ArgumentException"><paramref name="resourceUri"/> is empty or white space.</exception>
    IEditorContributions AddViewResources(string resourceUri);
}
