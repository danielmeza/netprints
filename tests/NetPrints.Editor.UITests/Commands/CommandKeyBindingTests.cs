using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Xaml.Interactivity;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Behaviors;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Commands;

/// <summary>Registered gestures run their commands from the window and from the scoped canvas and tree, never from a text input.</summary>
public class CommandKeyBindingTests
{
    private const string GlobalGesture = "Ctrl+Shift+B";

    private sealed class Handler(bool enabled = true) : ICommandHandler
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

    private sealed class Contexts : ICommandContextProvider
    {
        public event EventHandler? CommandStatesChanged;

        public void RaiseCommandStatesChanged() => CommandStatesChanged?.Invoke(this, EventArgs.Empty);

        public CommandContext Create(object? parameter = null) => new(new StubShell(), null, null, null, CommandSelection.None, parameter);
    }

    private sealed class StubShell : IShell
    {
        public IProjectActions ProjectActions => throw new NotSupportedException();

        public DocumentId? ActiveDocument => null;

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

    private sealed class Host : IDisposable
    {
        private readonly HeadlessUi ui = HeadlessUi.Create();

        public Host(params (string Id, string Gesture, CommandScope Scope, Handler Handler)[] commands)
        {
            var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
            foreach (var (id, gesture, scope, handler) in commands)
            {
                registry.AddCommand(new CommandDescriptor(ContributionIds.CommandPrefix + id, id, handler, DefaultGestures: [gesture], Scope: scope));
            }

            var invoker = new CommandInvoker(registry, new Contexts(), exception => throw exception);
            Interaction.GetBehaviors(Window).Add(new CommandKeyBindingsBehavior { Invoker = invoker });
            Interaction.GetBehaviors(Canvas).Add(new ScopedCommandKeysBehavior { Invoker = invoker, Scope = CommandScope.Graph });
            Interaction.GetBehaviors(Tree).Add(new ScopedCommandKeysBehavior { Invoker = invoker, Scope = CommandScope.ProjectTree });
            Canvas.Child = NodeText;
            Window.Content = new StackPanel { Children = { OutsideText, Canvas, Tree } };
            ui.Show(Window);
        }

        public Window Window { get; } = new() { Width = 400, Height = 300 };

        public TextBox OutsideText { get; } = new() { Text = "outside" };

        public TextBox NodeText { get; } = new() { Text = "node" };

        public Border Canvas { get; } = new() { Focusable = true, Width = 200, Height = 60 };

        public Border Tree { get; } = new() { Focusable = true, Width = 200, Height = 60 };

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

    public static TheoryData<string> ChordsOfATextField() => ["Ctrl+A", "Delete", "F2"];

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AGlobalGestureRunsWhereverTheFocusIs()
    {
        var handler = new Handler();
        using var host = new Host(("global", GlobalGesture, CommandScope.Global, handler));

        host.Focus(host.OutsideText);
        host.Press(GlobalGesture);
        host.Focus(host.NodeText);
        host.Press(GlobalGesture);
        host.Focus(host.Canvas);
        host.Press(GlobalGesture);

        Assert.Equal(3, handler.Runs);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AGraphGestureRunsWhileTheCanvasHasFocus()
    {
        var handler = new Handler();
        using var host = new Host(("graph", "Delete", CommandScope.Graph, handler));

        host.Focus(host.Canvas);
        host.Press("Delete");

        Assert.Equal(1, handler.Runs);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AGraphGestureDoesNotRunOutsideTheCanvas()
    {
        var handler = new Handler();
        using var host = new Host(("graph", "Delete", CommandScope.Graph, handler));

        host.Focus(host.OutsideText);
        host.Press("Delete");
        host.Focus(host.Tree);
        host.Press("Delete");

        Assert.Equal(0, handler.Runs);
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(ChordsOfATextField))]
    public void AGraphGestureStaysWithATextInputInsideANode(string chord)
    {
        var handler = new Handler();
        using var host = new Host(("graph", chord, CommandScope.Graph, handler));

        host.Focus(host.NodeText);
        host.Press(chord);

        Assert.Equal(0, handler.Runs);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ADeleteInANodeTextBoxEditsTheText()
    {
        var handler = new Handler();
        using var host = new Host(("graph", "Delete", CommandScope.Graph, handler));
        host.Focus(host.NodeText);
        host.NodeText.CaretIndex = 0;

        host.Press("Delete");

        Assert.Equal("ode", host.NodeText.Text);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AScopeRunsOnlyItsOwnCommands()
    {
        var graph = new Handler();
        var both = new Handler();
        var tree = new Handler();
        using var host = new Host(
            ("graph", "Ctrl+G", CommandScope.Graph, graph),
            ("both", "Ctrl+H", CommandScope.Graph | CommandScope.ProjectTree, both),
            ("tree", "Ctrl+J", CommandScope.ProjectTree, tree));

        host.Focus(host.Tree);
        host.Press("Ctrl+G");
        host.Press("Ctrl+H");
        host.Press("Ctrl+J");
        host.Focus(host.Canvas);
        host.Press("Ctrl+G");
        host.Press("Ctrl+H");
        host.Press("Ctrl+J");

        Assert.Equal((1, 2, 1), (graph.Runs, both.Runs, tree.Runs));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ADisabledCommandDoesNotRunAndTheStateIsReadAtInvocation()
    {
        var global = new Handler(enabled: false);
        var graph = new Handler(enabled: false);
        using var host = new Host(("global", GlobalGesture, CommandScope.Global, global), ("graph", "Ctrl+G", CommandScope.Graph, graph));
        host.Focus(host.Canvas);

        host.Press(GlobalGesture);
        host.Press("Ctrl+G");
        Assert.Equal((0, 0), (global.Runs, graph.Runs));

        global.Enabled = true;
        graph.Enabled = true;
        host.Press(GlobalGesture);
        host.Press("Ctrl+G");

        Assert.Equal((1, 1), (global.Runs, graph.Runs));
    }
}
