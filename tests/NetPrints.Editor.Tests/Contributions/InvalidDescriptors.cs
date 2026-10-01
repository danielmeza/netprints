using System.Runtime.CompilerServices;
using NetPrints.Editor.Contributions;

namespace NetPrints.Editor.Tests.Contributions;

/// <summary>Descriptors with a null the nullable annotations forbid, as an untrusted extension could pass them.</summary>
internal static class InvalidDescriptors
{
    public static CommandDescriptor CommandWithoutHandler() => Uninitialized<CommandDescriptor>() with { Id = "netprints.command.save", Label = "Save" };

    public static PanelDescriptor PanelWithoutFactory() =>
        Uninitialized<PanelDescriptor>() with { Id = "netprints.panel.errors", Title = "Errors", DefaultDock = PanelDock.Bottom };

    public static DashboardTileDescriptor TileWithoutFactory() => Uninitialized<DashboardTileDescriptor>() with { Id = "netprints.tile.recent", Title = "Recent" };

    public static ContextMenuItemDescriptor MenuItemWithoutGroup() =>
        Uninitialized<ContextMenuItemDescriptor>() with { Id = "netprints.menu.save", Target = ContextMenuTarget.Node, CommandId = "netprints.command.save" };

    private static T Uninitialized<T>()
        where T : class => (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
}
