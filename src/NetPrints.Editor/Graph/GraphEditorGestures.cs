using Avalonia.Input;
using Nodify.Avalonia;
using Nodify.Avalonia.Helpers.Gestures;

namespace NetPrints.Editor.Graph;

/// <summary>
/// Configures Nodify's global connector gestures for the whole app (OWN-06, PAR-46, PAR-48). Called
/// once from <see cref="EditorApp.Initialize"/> (REVIEW-NOTE, PR #6): a view's static constructor is
/// the wrong place for a one-time, process-wide mutation of <see cref="EditorGestures.Mappings"/> (it
/// runs the first time <em>anything</em> constructs a <see cref="GraphEditorView"/>, not at a
/// predictable point in startup, and nothing outside that view could call it again to test it).
/// </summary>
public static class GraphEditorGestures
{
    /// <summary>
    /// Pins are only ever connected or disconnected by dragging in this app: Nodify's default
    /// keyboard alternates for the connector's "Connect" and "Disconnect" gestures are bare Space and
    /// Delete, and a <c>KeyDown</c> that bubbles up from a pin's value or name text box reaches the
    /// pin's connector before it reaches anything of ours, so Space moved focus off the text box onto
    /// the connector (and showed Nodify's keyboard-connect hotkey badges) instead of inserting a
    /// space, and Delete disconnected the pin instead of deleting a character. Ctrl+Enter and
    /// Ctrl+Delete replace them: modifier chords nobody types while editing a pin's value, so a
    /// keyboard-only path to connect and disconnect stays reachable (a11y) without that conflict. The
    /// editor is never panned or selected by keyboard, so those two alternates are dropped outright.
    /// </summary>
    public static void Configure()
    {
        EditorGestures.Mappings.Connector.Connect.Value = new AnyGesture(
            new PointerGesture(MouseAction.LeftClick),
            new KeyboardGesture(Key.Enter, KeyModifiers.Control));
        EditorGestures.Mappings.Connector.Disconnect.Value = new AnyGesture(
            new PointerGesture(MouseAction.LeftClick, KeyModifiers.Alt),
            new KeyboardGesture(Key.Delete, KeyModifiers.Control));
        EditorGestures.Mappings.Editor.Keyboard.ToggleSelected.Unbind();
        EditorGestures.Mappings.Editor.Keyboard.Pan.Unbind();
    }
}
