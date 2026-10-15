using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Commands.CommandPalette;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Shell;

namespace NetPrints.Editor.Tests.Commands;

public sealed class CommandPaletteViewModelTests
{
    private sealed class Probe(bool enabled) : ICommandHandler
    {
        public bool Enabled { get; } = enabled;

        public int Runs { get; private set; }

        public bool CanExecute(CommandContext context) => Enabled;

        public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
        {
            Runs++;
            return Task.CompletedTask;
        }
    }

    [Theory]
    [InlineData("File", "Ctrl+S", true, "File, Ctrl+S")]
    [InlineData("File", "Ctrl+S", false, "File, Ctrl+S, unavailable")]
    [InlineData("", "", false, "unavailable")]
    [InlineData("", "", true, "")]
    public void TheDescriptionNamesTheMenuTheShortcutsAndWhetherTheCommandIsUnavailable(string menu, string shortcuts, bool enabled, string expected)
    {
        var item = new PaletteItem(new CommandDescriptor("netprints.command.test", "Test", new Probe(enabled)), "Test", shortcuts, menu, enabled);

        Assert.Equal(expected, item.Description);
    }

    private sealed class Contexts : ICommandContextProvider
    {
        public event EventHandler? CommandStatesChanged
        {
            add { }
            remove { }
        }

        public CommandContext Create(object? parameter = null, CommandScope scope = CommandScope.Global) =>
            new(new FakeShell(), null, null, null, CommandSelection.None, parameter, scope);
    }

    private readonly Dictionary<string, Probe> probes = [];
    private readonly ContributionRegistry registry = new(NullLogger<ContributionRegistry>.Instance);

    private void Add(string name, string label, bool enabled = true, string? menu = null, params string[] gestures)
    {
        var probe = new Probe(enabled);
        probes[name] = probe;
        registry.AddCommand(new CommandDescriptor(ContributionIds.CommandPrefix + name, label, probe, DefaultGestures: gestures,
            Menu: menu is null ? null : new MenuPlacement(menu, "g", 0)));
    }

    private CommandPaletteViewModel Open()
    {
        registry.Freeze();
        return new CommandPaletteViewModel(registry, new CommandInvoker(registry, new Contexts(), exception => throw exception));
    }

    [Fact]
    public void ItListsEveryRegisteredCommandWithItsShortcutsAndMenu()
    {
        Add("save", "Save", true, "File", "Ctrl+S");
        Add("redo", "Redo", true, "Edit", "Ctrl+Y", "Ctrl+Shift+Z");
        Add("hidden", "Hidden");

        CommandPaletteViewModel palette = Open();

        Assert.Equal(["Hidden", "Redo", "Save"], palette.Items.Select(item => item.Label));
        PaletteItem redo = palette.Items.Single(item => item.Label == "Redo");
        Assert.Equal("Ctrl+Y, Ctrl+Shift+Z", redo.Shortcuts);
        Assert.Equal("Edit", redo.MenuPath);
        Assert.Equal("", palette.Items.Single(item => item.Label == "Hidden").MenuPath);
        Assert.False(palette.IsEmpty);
    }

    [Fact]
    public void TheQueryFiltersWithThePrefixWordStartSubstringRanking()
    {
        Add("resave", "Resave");
        Add("undo", "Undo save");
        Add("saveall", "Save all");
        Add("close", "Close tab");
        Add("save", "Save");
        CommandPaletteViewModel palette = Open();

        palette.Query = "sa";

        Assert.Equal(["Save", "Save all", "Undo save", "Resave"], palette.Items.Select(item => item.Label));
        Assert.Equal("Save", palette.SelectedItem?.Label);
        palette.Query = "zzz";
        Assert.True(palette.IsEmpty);
        Assert.Null(palette.SelectedItem);
    }

    [Fact]
    public void ADisabledCommandIsListedButEnterDoesNotRunIt()
    {
        Add("run", "Run", enabled: false);
        CommandPaletteViewModel palette = Open();
        bool closed = false;
        palette.CloseRequested += (_, _) => closed = true;

        PaletteItem item = Assert.Single(palette.Items);
        Assert.False(item.IsEnabled);
        palette.RunSelectedCommand.Execute(null);

        Assert.Equal(0, probes["run"].Runs);
        Assert.False(closed);
    }

    [Fact]
    public void EnterRunsTheSelectedCommandAndCloses()
    {
        Add("a", "Alpha");
        Add("b", "Beta");
        CommandPaletteViewModel palette = Open();
        bool closed = false;
        palette.CloseRequested += (_, _) => closed = true;

        palette.SelectNextCommand.Execute(null);
        Assert.Equal("Beta", palette.SelectedItem?.Label);
        palette.RunSelectedCommand.Execute(null);

        Assert.Equal(0, probes["a"].Runs);
        Assert.Equal(1, probes["b"].Runs);
        Assert.True(closed);
    }

    [Fact]
    public void EscapeClosesWithoutRunningAndTheSelectionStaysInRange()
    {
        Add("a", "Alpha");
        Add("b", "Beta");
        CommandPaletteViewModel palette = Open();
        bool closed = false;
        palette.CloseRequested += (_, _) => closed = true;

        palette.SelectPreviousCommand.Execute(null);
        Assert.Equal("Alpha", palette.SelectedItem?.Label);
        palette.SelectNextCommand.Execute(null);
        palette.SelectNextCommand.Execute(null);
        Assert.Equal("Beta", palette.SelectedItem?.Label);
        palette.CloseCommand.Execute(null);

        Assert.True(closed);
        Assert.Equal(0, probes["a"].Runs + probes["b"].Runs);
    }
}
