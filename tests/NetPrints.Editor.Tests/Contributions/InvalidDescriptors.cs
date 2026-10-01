using System.Runtime.CompilerServices;
using NetPrints.Editor.Contributions;

namespace NetPrints.Editor.Tests.Contributions;

/// <summary>Descriptors with a null the nullable annotations forbid, as an untrusted extension could pass them.</summary>
internal static class InvalidDescriptors
{
    private static T Null<T>()
        where T : class => Unsafe.As<T>(null);

    public static CommandDescriptor CommandWithoutHandler() => new("netprints.command.save", "Save", Null<ICommandHandler>());

    public static PanelDescriptor PanelWithoutFactory() => new("netprints.panel.errors", "Errors", Null<Func<IServiceProvider, object>>(), PanelDock.Bottom, 0);

    public static DashboardTileDescriptor TileWithoutFactory() => new("netprints.tile.recent", "Recent", 0, Null<Func<IServiceProvider, object>>());

    public static ContextMenuItemDescriptor MenuItemWithoutGroup() => new("netprints.menu.save", ContextMenuTarget.Node, "netprints.command.save", Null<string>(), 0);
}
