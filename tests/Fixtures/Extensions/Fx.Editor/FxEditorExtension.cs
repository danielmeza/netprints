using NetPrints.Extensibility;
using NetPrints.Extensibility.Editor;

namespace Fx.Editor;

/// <summary>Registers one of every editor contribution kind of the facet, for hosts that have no editor and must ignore them.</summary>
public sealed class FxEditorExtension : INetPrintsExtension
{
    /// <summary>The extension id, also the manifest id.</summary>
    public const string Id = "fx.editor";

    /// <summary>The id of the command the extension registers.</summary>
    public const string CommandId = "fx.editor.hello";

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Editor
            .AddCommand(new CommandDescriptor(CommandId, "Hello", new HelloHandler()))
            .AddPanel(new PanelDescriptor("fx.editor.panel", "Hello panel", context => context, PanelDock.Right, 0))
            .AddDashboardTile(new DashboardTileDescriptor("fx.editor.tile", "Hello tile", 0, context => context))
            .AddContextMenuItem(new ContextMenuItemDescriptor("fx.editor.menu", ContextMenuTarget.Canvas, CommandId, "hello", 0))
            .AddViewResources("avares://Fx.Editor/Views.axaml");
    }

    private sealed class HelloHandler : ICommandHandler
    {
        public bool CanExecute(ICommandContext context) => true;

        public Task ExecuteAsync(ICommandContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
