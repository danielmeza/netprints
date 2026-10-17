using NetPrints.Extensibility;
using NetPrints.Extensibility.Editor;

namespace Fx.RegisterFault;

/// <summary>Registers a command and then throws from <see cref="Register"/>, so it must contribute nothing.</summary>
public sealed class FxRegisterFaultExtension : INetPrintsExtension
{
    /// <summary>The extension id, also the manifest id.</summary>
    public const string Id = "fx.registerfault";

    /// <summary>The command registered before the failure.</summary>
    public const string CommandId = "fx.registerfault.hello";

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Editor.AddCommand(new CommandDescriptor(CommandId, "Hello", new HelloHandler()));
        throw new InvalidOperationException("register");
    }

    private sealed class HelloHandler : ICommandHandler
    {
        public bool CanExecute(ICommandContext context) => true;

        public Task ExecuteAsync(ICommandContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
