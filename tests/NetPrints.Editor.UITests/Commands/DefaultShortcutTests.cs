using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Commands;

/// <summary>Each default gesture of the built-in commands (FR-034) runs its command where it applies, and the single-key ones stay out of text fields.</summary>
public class DefaultShortcutTests
{
    private static readonly CommandScope[] Scopes = [CommandScope.Global, CommandScope.Graph, CommandScope.ProjectTree];

    private static List<CommandDescriptor> BuiltIns()
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        return [.. registry.Commands.Where(command => command.DefaultGestures is { Count: > 0 })];
    }

    public static TheoryData<string, string, CommandScope> Gestures() => ToData(BuiltIns().SelectMany(command => Scopes.Where(scope => AppliesIn(command, scope))
        .SelectMany(scope => (command.DefaultGestures ?? []).Select(gesture => (command.Id, gesture, scope)))));

    public static TheoryData<string, string, CommandScope> SingleKeyGestures() => ToData(BuiltIns().Where(command => command.Scope != CommandScope.Global)
        .SelectMany(command => Scopes.Where(scope => scope != CommandScope.Global && AppliesIn(command, scope))
            .SelectMany(scope => (command.DefaultGestures ?? []).Where(IsPlain).Select(gesture => (command.Id, gesture, scope)))));

    private static bool AppliesIn(CommandDescriptor command, CommandScope scope) =>
        scope == CommandScope.Global ? command.Scope == CommandScope.Global : command.Scope.HasFlag(scope);

    private static bool IsPlain(string gesture) => CommandGesture.TryParse(gesture, out var parsed) && parsed.IsPlain;

    private static TheoryData<string, string, CommandScope> ToData(IEnumerable<(string Id, string Gesture, CommandScope Scope)> rows)
    {
        var data = new TheoryData<string, string, CommandScope>();
        foreach (var (id, gesture, scope) in rows)
        {
            data.Add(id, gesture, scope);
        }

        return data;
    }

    private static (KeyHost Host, ProbeHandler Probe) Start(string commandId)
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        var probe = new ProbeHandler();
        foreach (var command in BuiltIns())
        {
            registry.AddCommand(command with { Handler = command.Id == commandId ? probe : new ProbeHandler() });
        }

        return (new KeyHost(registry), probe);
    }

    private static InputElementFor Surface(KeyHost host, CommandScope scope) => scope switch
    {
        CommandScope.Graph => new(host.Canvas, host.NodeText),
        CommandScope.ProjectTree => new(host.Tree, host.TreeText),
        _ => new(host.Canvas, host.OutsideText),
    };

    private sealed record InputElementFor(Avalonia.Input.InputElement Surface, TextBox Text);

    [Fact]
    public void EveryFr034GestureOfARegisteredCommandIsBound()
    {
        string[] pending = ["Ctrl+Shift+P", "Ctrl+P", "Alt+Left", "Alt+Right"];
        string[] fr034 =
        [
            "Ctrl+S", "Ctrl+Shift+S", "Ctrl+O", "Ctrl+Shift+N", "F7", "Ctrl+Shift+B", "F5", "Shift+F5", "Ctrl+Z", "Ctrl+Y", "Ctrl+Shift+Z",
            "Delete", "F2", "Ctrl+A", "F", "Home", "Shift+F", "Esc", "Ctrl+Space", "Ctrl+W", "Ctrl+Tab", "Ctrl+Shift+Tab", .. pending,
        ];
        var bound = BuiltIns().SelectMany(command => (command.DefaultGestures ?? []))
            .Select(gesture => CommandGesture.TryParse(gesture, out var parsed) ? parsed.ToString() : gesture).ToHashSet(StringComparer.Ordinal);

        string[] missing = [.. fr034.Except(pending).Where(gesture => !bound.Contains(CommandGesture.TryParse(gesture, out var parsed) ? parsed.ToString() : gesture))];

        Assert.Empty(missing);
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(Gestures))]
    public void AGestureRunsItsCommandWhereItApplies(string commandId, string gesture, CommandScope scope)
    {
        var (host, probe) = Start(commandId);
        using (host)
        {
            host.Focus(Surface(host, scope).Surface);

            host.Press(gesture);

            Assert.Equal(1, probe.Runs);
        }
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(SingleKeyGestures))]
    public void ASingleKeyGestureNeverActsWhileATextFieldHasFocus(string commandId, string gesture, CommandScope scope)
    {
        var (host, probe) = Start(commandId);
        using (host)
        {
            host.Focus(Surface(host, scope).Text);

            host.Press(gesture);

            Assert.Equal(0, probe.Runs);
        }
    }
}
