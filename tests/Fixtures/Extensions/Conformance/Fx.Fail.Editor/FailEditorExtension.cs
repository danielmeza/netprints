using NetPrints.Extensibility;
using NetPrints.Extensibility.Editor;

namespace Fx.Fail.Editor;

/// <summary>A command that does nothing.</summary>
public sealed class NoopHandler : ICommandHandler
{
    /// <inheritdoc />
    public bool CanExecute(ICommandContext context) => true;

    /// <inheritdoc />
    public Task ExecuteAsync(ICommandContext context, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>An extension whose command id does not start with the extension id.</summary>
public sealed class FailEditorExtension : INetPrintsExtension
{
    /// <summary>The command id, which breaks the rule.</summary>
    public const string CommandId = "other.hello";

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Editor.AddCommand(new CommandDescriptor(CommandId, "Hello", new NoopHandler()));
    }
}
