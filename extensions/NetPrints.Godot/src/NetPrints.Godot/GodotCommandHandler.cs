using NetPrints.Extensibility.Editor;

namespace NetPrints.Godot;

/// <summary>Writes where the Godot executable comes from, and whether it exists, to the editor output.</summary>
public sealed class GodotCommandHandler : ICommandHandler
{
    /// <summary>The id of the command.</summary>
    public const string CommandId = GodotExtension.Id + ".checkSetup";

    /// <inheritdoc />
    public bool CanExecute(ICommandContext context) => true;

    /// <inheritdoc />
    public Task ExecuteAsync(ICommandContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        GodotSettings settings = GodotStatus.SettingsOf(context.Services);
        string? variable = Environment.GetEnvironmentVariable(GodotStatus.Variable);
        context.Output.WriteLine(GodotStatus.Text(variable, settings));
        string path = settings.ExecutablePath.Length > 0 ? settings.ExecutablePath : variable ?? string.Empty;
        if (path.Length > 0)
        {
            context.Output.WriteLine(File.Exists(path) ? "The Godot executable exists." : "The Godot executable was not found.");
        }

        return Task.CompletedTask;
    }
}
