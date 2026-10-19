using NetPrints.Extensibility.Editor;

namespace NetPrints.Godot;

/// <summary>Says which Godot executable the launch profiles and the settings point at.</summary>
public static class GodotStatus
{
    /// <summary>The environment variable the launch profiles read the Godot executable from.</summary>
    public const string Variable = "GODOT4";

    /// <summary>The id of the status bar item.</summary>
    public const string StatusBarItemId = GodotExtension.Id + ".status";

    /// <summary>Describes where the Godot executable comes from.</summary>
    /// <param name="variable">The value of <see cref="Variable"/>, or <see langword="null"/> when it is not set.</param>
    /// <param name="settings">The extension's settings.</param>
    /// <returns>The text the status bar item shows.</returns>
    public static string Text(string? variable, GodotSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.ExecutablePath.Length > 0)
        {
            return $"Godot: {settings.ExecutablePath}";
        }

        return string.IsNullOrEmpty(variable) ? "Godot: set GODOT4 or the executable path in the settings" : "Godot: GODOT4 is set";
    }

    /// <summary>Creates the status bar item, shown for Godot projects.</summary>
    /// <returns>The descriptor.</returns>
    public static StatusBarItemDescriptor CreateItem() => new(
        StatusBarItemId,
        StatusBarAlignment.Right,
        0,
        context => Text(Environment.GetEnvironmentVariable(Variable), SettingsOf(context.Services)),
        context => context.Session?.Project.ProfileId == GodotProfile.ProfileId);

    internal static GodotSettings SettingsOf(IServiceProvider services) =>
        services.GetService(typeof(NetPrints.Extensibility.Settings.ISettingsStore)) is NetPrints.Extensibility.Settings.ISettingsStore store
            ? store.Get(GodotSettings.Descriptor)
            : GodotSettings.Descriptor.Default;
}
