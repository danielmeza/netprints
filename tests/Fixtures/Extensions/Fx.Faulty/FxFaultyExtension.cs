using NetPrints.Extensibility;
using NetPrints.Extensibility.Editor;

namespace Fx.Faulty;

/// <summary>Registers contributions that fail at runtime or that the registry must report: a throwing handler, tooltip provider, go-to provider and view model factories, a duplicate id, a gesture conflict and an unknown referenced id.</summary>
public sealed class FxFaultyExtension : INetPrintsExtension
{
    /// <summary>The extension id, also the manifest id.</summary>
    public const string Id = "fx.faulty";

    /// <summary>The command whose handler throws when it runs.</summary>
    public const string ThrowingCommandId = "fx.faulty.throws";

    /// <summary>The command id registered twice.</summary>
    public const string DuplicateCommandId = "fx.faulty.duplicate";

    /// <summary>The command that takes a gesture an earlier command owns.</summary>
    public const string ConflictingCommandId = "fx.faulty.conflict";

    /// <summary>The panel whose view model factory throws.</summary>
    public const string PanelId = "fx.faulty.panel";

    /// <summary>The start page tile whose view model factory throws.</summary>
    public const string TileId = "fx.faulty.tile";

    /// <summary>The status bar item whose view model factory throws.</summary>
    public const string StatusItemId = "fx.faulty.status";

    /// <summary>The context menu item that names a command nothing registered.</summary>
    public const string UnknownReferenceItemId = "fx.faulty.menu";

    /// <summary>The command id the context menu item names although nothing registered it.</summary>
    public const string UnknownCommandId = "fx.faulty.nothing";

    /// <summary>The gesture two commands ask for.</summary>
    public const string SharedGesture = "Ctrl+Alt+F9";

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        foreach (string label in new[] { "First", "Second" })
        {
            builder.Editor.AddCommand(new CommandDescriptor(DuplicateCommandId, label, new ThrowingHandler()));
        }

        builder.Editor
            .AddCommand(new CommandDescriptor(ThrowingCommandId, "Throws", new ThrowingHandler()))
            .AddCommand(new CommandDescriptor("fx.faulty.owner", "Owner", new ThrowingHandler(), DefaultGestures: [CommandGesture.Parse(SharedGesture, null)]))
            .AddCommand(new CommandDescriptor(ConflictingCommandId, "Conflicts", new ThrowingHandler(), DefaultGestures: [CommandGesture.Parse(SharedGesture, null)]))
            .AddContextMenuItem(new ContextMenuItemDescriptor(UnknownReferenceItemId, ContextMenuTarget.Connection, UnknownCommandId, "faulty", 0))
            .AddTooltipProvider(new ThrowingTooltips())
            .AddGoToProvider(new ThrowingGoTo())
            .AddPanel(new PanelDescriptor(PanelId, "Faulty panel", _ => throw new InvalidOperationException("panel factory"), PanelDock.Right, 0))
            .AddDashboardTile(new DashboardTileDescriptor(TileId, "Faulty tile", 0, _ => throw new InvalidOperationException("tile factory")))
            .AddStatusBarItem(new StatusBarItemDescriptor(StatusItemId, StatusBarAlignment.Right, 0, _ => throw new InvalidOperationException("status factory")));
    }

    private sealed class ThrowingHandler : ICommandHandler
    {
        public bool CanExecute(ICommandContext context) => true;

        public Task ExecuteAsync(ICommandContext context, CancellationToken cancellationToken) => throw new InvalidOperationException("handler");
    }

    private sealed class ThrowingTooltips : ITooltipProvider
    {
        public int Order => -100;

        public TooltipContent? TryProvide(TooltipTarget target) => throw new InvalidOperationException("tooltip");
    }

    private sealed class ThrowingGoTo : IGoToProvider
    {
        public string Kind => "Faulty";

        public IAsyncEnumerable<GoToItem> SearchAsync(string text, CancellationToken cancellationToken) => throw new InvalidOperationException("go to");
    }
}
