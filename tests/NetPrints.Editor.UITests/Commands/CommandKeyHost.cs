using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Behaviors;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Commands;

internal sealed class ProbeHandler(bool enabled = true) : ICommandHandler
{
    public bool Enabled { get; set; } = enabled;

    public int Runs { get; private set; }

    public bool CanExecute(CommandContext context) => Enabled;

    public Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        Runs++;
        return Task.CompletedTask;
    }
}

internal sealed class KeyContexts : ICommandContextProvider
{
    public event EventHandler? CommandStatesChanged;

    public void RaiseCommandStatesChanged() => CommandStatesChanged?.Invoke(this, EventArgs.Empty);

    public CommandContext Create(object? parameter = null, CommandScope scope = CommandScope.Global) => new(new KeyStubShell(), null, null, null, CommandSelection.None, parameter, scope);
}

internal sealed class KeyStubShell : IShell
{
    public IProjectActions ProjectActions => throw new NotSupportedException();

    public DocumentId? ActiveDocument => null;

    public IReadOnlyList<DocumentId> OpenDocuments => [];

    public bool IsPanelVisible(string panelId) => false;

    public bool IsFloating(DocumentId id) => false;

    public void OpenDocument(DocumentId id)
    {
    }

    public void ActivateDocument(DocumentId id)
    {
    }

    public void CloseDocument(DocumentId id)
    {
    }

    public void ShowPanel(string panelId)
    {
    }

    public void HidePanel(string panelId)
    {
    }

    public void FloatDocument(DocumentId id)
    {
    }

    public void DockDocument(DocumentId id)
    {
    }

    public void ResetLayout()
    {
    }
}

internal sealed class KeyHost : IDisposable
{
    private readonly HeadlessUi ui = HeadlessUi.Create();

    public KeyHost(params (string Id, string Gesture, CommandScope Scope, ProbeHandler Handler)[] commands)
        : this(RegistryOf(commands))
    {
    }

    public KeyHost(ContributionRegistry registry)
    {
        var invoker = new CommandInvoker(registry, new KeyContexts(), exception => throw exception);
        Interaction.GetBehaviors(Window).Add(new CommandKeyBindingsBehavior { Invoker = invoker });
        Interaction.GetBehaviors(Canvas).Add(new ScopedCommandKeysBehavior { Invoker = invoker, Scope = CommandScope.Graph });
        Interaction.GetBehaviors(Tree).Add(new ScopedCommandKeysBehavior { Invoker = invoker, Scope = CommandScope.ProjectTree });
        Canvas.Child = NodeText;
        Tree.Child = TreeText;
        Window.Content = new StackPanel { Children = { OutsideText, Canvas, Tree } };
        ui.Show(Window);
    }

    public Window Window { get; } = new() { Width = 400, Height = 300 };

    public TextBox OutsideText { get; } = new() { Text = "outside" };

    public TextBox NodeText { get; } = new() { Text = "node" };

    public TextBox TreeText { get; } = new() { Text = "tree" };

    public Border Canvas { get; } = new() { Focusable = true, Width = 200, Height = 60 };

    public Border Tree { get; } = new() { Focusable = true, Width = 200, Height = 60 };

    private static ContributionRegistry RegistryOf((string Id, string Gesture, CommandScope Scope, ProbeHandler Handler)[] commands)
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        foreach (var (id, gesture, scope, handler) in commands)
        {
            registry.AddCommand(new CommandDescriptor(ContributionIds.CommandPrefix + id, id, handler, DefaultGestures: [gesture], Scope: scope));
        }

        return registry;
    }

    public void Press(string chord)
    {
        var (key, modifiers) = HeadlessDriver.ParseChord(chord);
        Window.KeyPress(key, modifiers, PhysicalKey.None, null);
        Window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        HeadlessDriver.Pump();
    }

    public void Focus(InputElement element)
    {
        Assert.True(element.Focus());
        HeadlessDriver.Pump();
    }

    public void Dispose() => ui.Dispose();
}
