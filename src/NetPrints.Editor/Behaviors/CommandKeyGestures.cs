using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using NetPrints.Editor.Contributions;
using Nodify.Avalonia.Connections;

namespace NetPrints.Editor.Behaviors;

/// <summary>View-layer helpers of the command key behaviors: descriptor gestures to <see cref="KeyGesture"/>, and the text input test.</summary>
internal static class CommandKeyGestures
{
    /// <summary>Converts the descriptor's gesture strings for the running platform; <c>Ctrl</c> becomes Command on macOS.</summary>
    /// <param name="command">The command.</param>
    /// <returns>The gestures.</returns>
    public static IEnumerable<KeyGesture> Of(CommandDescriptor command) => Of(command, OperatingSystem.IsMacOS());

    /// <summary>Converts the descriptor's gesture strings; one that does not name an Avalonia key is skipped.</summary>
    /// <param name="command">The command.</param>
    /// <param name="isMacOS">Whether <c>Ctrl</c> maps to the Meta (Command) modifier instead of Control.</param>
    /// <returns>The gestures.</returns>
    public static IEnumerable<KeyGesture> Of(CommandDescriptor command, bool isMacOS)
    {
        foreach (string text in command.DefaultGestures ?? [])
        {
            if (CommandGesture.TryParse(text, out CommandGesture gesture) && TryKey(gesture.Key, out Key key))
            {
                yield return new KeyGesture(key, Modifiers(gesture.Modifiers, isMacOS));
            }
        }
    }

    /// <summary>Whether a key press comes from a value editor (text box, check box or combo box) that keeps its own keys.</summary>
    /// <param name="source">The event source.</param>
    /// <returns><see langword="true"/> when the source sits inside such an editor.</returns>
    public static bool IsInsideValueEditor(object? source)
    {
        for (var visual = source as Visual; visual is not null; visual = visual.GetVisualParent())
        {
            if (visual is TextBox or CheckBox or ComboBox)
            {
                return true;
            }

            if (visual is Connector)
            {
                return false;
            }
        }

        return false;
    }

    private static bool TryKey(string name, out Key key)
    {
        if (name.Length == 1 && char.IsDigit(name[0]))
        {
            return Enum.TryParse("D" + name, out key);
        }

        return Enum.TryParse(name, out key);
    }

    private static KeyModifiers Modifiers(CommandModifiers modifiers, bool isMacOS)
    {
        var result = KeyModifiers.None;
        result |= modifiers.HasFlag(CommandModifiers.Ctrl) ? (isMacOS ? KeyModifiers.Meta : KeyModifiers.Control) : KeyModifiers.None;
        result |= modifiers.HasFlag(CommandModifiers.Alt) ? KeyModifiers.Alt : KeyModifiers.None;
        result |= modifiers.HasFlag(CommandModifiers.Shift) ? KeyModifiers.Shift : KeyModifiers.None;
        result |= modifiers.HasFlag(CommandModifiers.Meta) ? KeyModifiers.Meta : KeyModifiers.None;
        return result;
    }
}
