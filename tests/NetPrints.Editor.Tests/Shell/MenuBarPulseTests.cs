using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Tests.Shell;

public sealed class MenuBarPulseTests
{
    private sealed class Pulses : ICommandContextProvider
    {
        private EventHandler? changed;

        public int Created { get; private set; }

        public event EventHandler? CommandStatesChanged
        {
            add => changed += value;
            remove => changed -= value;
        }

        public CommandContext Create(object? parameter = null, CommandScope scope = CommandScope.Global)
        {
            Created++;
            return new CommandContext(new FakeShell(), null, null, null, CommandSelection.None, parameter, scope);
        }

        public void Raise() => changed?.Invoke(this, EventArgs.Empty);
    }

    private sealed class Switchable : ICommandHandler
    {
        public bool Enabled { get; set; } = true;

        public bool CanExecute(ICommandContext context) => Enabled;

        public Task ExecuteAsync(ICommandContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static (MenuBarViewModel Menu, Pulses Pulses, Switchable First) Open(int commands)
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        var first = new Switchable();
        for (int index = 0; index < commands; index++)
        {
            registry.AddCommand(new CommandDescriptor(ContributionIds.CommandPrefix + "test" + index, "Test " + index, index == 0 ? first : new Switchable(), Menu: new MenuPlacement("File", "g", index)));
        }

        registry.Freeze();
        var pulses = new Pulses();
        return (new MenuBarViewModel(registry, new CommandInvoker(registry, pulses, exception => throw exception)), pulses, first);
    }

    [Fact]
    public void AStatePulseBuildsOneContextForAllTheGlobalEntries()
    {
        (MenuBarViewModel menu, Pulses pulses, _) = Open(commands: 12);
        pulses.Raise();
        int before = pulses.Created;

        pulses.Raise();

        Assert.Equal(1, pulses.Created - before);
        menu.Dispose();
    }

    [Fact]
    public void AStatePulseStillRefreshesTheEnabledState()
    {
        (MenuBarViewModel menu, Pulses pulses, Switchable first) = Open(commands: 3);
        CommandEntryViewModel entry = menu.Menus.Single(m => m.Header == "File").Items.First(item => item.Id.EndsWith("test0", StringComparison.Ordinal));
        Assert.True(entry.IsEnabled);

        first.Enabled = false;
        pulses.Raise();

        Assert.False(entry.IsEnabled);
        menu.Dispose();
    }
}
