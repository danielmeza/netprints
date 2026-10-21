using NetPrints.Extensibility;
using NetPrints.Extensibility.Editor;

namespace NetPrints.Godot;

/// <summary>The NetPrints extension for Godot: the <c>godot</c> profile, a project template with launch profiles, a command, a settings section and a status bar item.</summary>
public sealed class GodotExtension : INetPrintsExtension
{
    /// <summary>The extension id, also the manifest id.</summary>
    public const string Id = "netprints.godot";

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder
            .AddProjectProfile(new GodotProfile())
            .AddProjectTemplate(GodotTemplate.Create())
            .AddSettings(GodotSettings.Descriptor);
        builder.Editor
            .AddCommand(new CommandDescriptor(GodotCommandHandler.CommandId, "Check Godot setup", new GodotCommandHandler(), Menu: new MenuPlacement("Project", "godot", 0)))
            .AddStatusBarItem(GodotStatus.CreateItem())
            .AddSettingsPage(new SettingsPageDescriptor(GodotExtension.Id + ".settings", "Godot", Id, 100));
    }
}
