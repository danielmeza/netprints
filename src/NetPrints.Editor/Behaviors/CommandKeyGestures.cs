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
    private static readonly Dictionary<string, Key> Synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Esc"] = Key.Escape,
        ["Del"] = Key.Delete,
        ["Ins"] = Key.Insert,
        ["Return"] = Key.Enter,
    };

    /// <summary>Converts the descriptor's gesture strings; one that does not name an Avalonia key is skipped.</summary>
    /// <param name="command">The command.</param>
    /// <returns>The gestures.</returns>
    public static IEnumerable<KeyGesture> Of(CommandDescriptor command)
    {
        foreach (string text in command.DefaultGestures ?? [])
        {
            if (CommandGesture.TryParse(text, out CommandGesture gesture) && TryKey(gesture.Key, out Key key))
            {
                yield return new KeyGesture(key, Modifiers(gesture.Modifiers));
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
        if (Synonyms.TryGetValue(name, out key))
        {
            return true;
        }

        string enumName = name.Length == 1 && char.IsDigit(name[0]) ? "D" + name : name;
        return Enum.TryParse(enumName, ignoreCase: true, out key);
    }

    private static KeyModifiers Modifiers(CommandModifiers modifiers)
    {
        var result = KeyModifiers.None;
        result |= modifiers.HasFlag(CommandModifiers.Ctrl) ? KeyModifiers.Control : KeyModifiers.None;
        result |= modifiers.HasFlag(CommandModifiers.Alt) ? KeyModifiers.Alt : KeyModifiers.None;
        result |= modifiers.HasFlag(CommandModifiers.Shift) ? KeyModifiers.Shift : KeyModifiers.None;
        result |= modifiers.HasFlag(CommandModifiers.Meta) ? KeyModifiers.Meta : KeyModifiers.None;
        return result;
    }
}
