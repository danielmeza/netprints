using Avalonia.Headless.XUnit;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.UITests.ClassEditor;
using NetPrints.Graph;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Editor.UITests.Graph;

/// <summary>
/// OWN-06 (owner report): typing Space in the Console.WriteLine node's "Hello, World!" value box
/// showed numbers on the nodes instead of inserting a space. Root cause: Nodify's connector
/// "Connect"/"Disconnect" gestures default to Space/Delete as keyboard alternates to their mouse
/// gestures; a KeyDown bubbling up from the pin's text box reaches the pin's connector before it
/// reaches anything of NetPrints' own, so Space moved keyboard focus off the text box onto the
/// connector and started a keyboard-driven pending connection (Nodify's own "numbered hotkey
/// badge" feature), and Delete disconnected the pin. Real input, not synthetic command calls: a
/// click to focus the box, then a raw KeyDown plus the platform's separate TextInput event for the
/// same keystroke (see HeadlessDriver.PressAsync/TypeAsync), matching how a physical keypress
/// reaches Avalonia.
/// </summary>
public class PinValueTextEditingTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task SpaceInsertsATextSpaceWithoutMovingFocusOffTheBox()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var valuePin = session.GraphVM.Nodes.Single(n => n.Node is CallMethodNode).InputDataPins.Single();
        var valueBox = session.Graph.Node("CallMethodNode").Input(valuePin.Pin.Name).ValueBox;

        await valueBox.ClickAsync(Token);
        await session.Driver.PressAsync("End", Token);
        Assert.Equal("True", await valueBox.PropertyAsync(AutomationPropertyNames.IsFocused, Token));

        // The physical keystroke: a KeyDown (which Nodify's connector could treat as a gesture)
        // followed by the platform's TextInput for the same key.
        await session.Driver.PressAsync("Space", Token);
        Assert.Equal("True", await valueBox.PropertyAsync(AutomationPropertyNames.IsFocused, Token)); // OWN-06: focus stayed on the box
        await session.Driver.TypeAsync(" ", Token);

        Assert.Equal("Hello, World! ", await valueBox.TextAsync(Token));
        Assert.False(valuePin.IsConnected); // no accidental pending/keyboard connection was started
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task DeleteAndBackspaceStayInTheBoxInsteadOfHittingTheCanvas()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var valuePin = session.GraphVM.Nodes.Single(n => n.Node is CallMethodNode).InputDataPins.Single();
        var valueBox = session.Graph.Node("CallMethodNode").Input(valuePin.Pin.Name).ValueBox;
        var node = session.Graph.Node("CallMethodNode");

        await valueBox.ClickAsync(Token);

        // Delete is also the connector's "Disconnect" keyboard gesture (PAR-48), and both Delete and
        // the window's Delete key binding (PAR-37) remove the selected node: neither should reach the
        // pin or the node while the box has focus, whatever it does to the box's own text/caret.
        await session.Driver.PressAsync("Delete", Token);
        await session.Driver.PressAsync("Back", Token);
        Assert.Equal("True", await valueBox.PropertyAsync(AutomationPropertyNames.IsFocused, Token));
        Assert.False(valuePin.IsConnected); // Delete didn't disconnect the pin
        Assert.Contains("CallMethodNode", await session.Graph.NodeNamesAsync(Token)); // nothing deleted the node
        Assert.True(await node.ExistsAsync(Token));

        // The box still composes text normally afterward (letters aren't a Nodify/window gesture, PAR-44).
        await session.Driver.PressAsync("End", Token);
        string before = await valueBox.TextAsync(Token) ?? "";
        await session.Driver.TypeAsync("!", Token);
        Assert.Equal(before + "!", await valueBox.TextAsync(Token));
    }
}
