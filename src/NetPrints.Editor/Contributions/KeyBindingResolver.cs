using NetPrints.Editor.State;

namespace NetPrints.Editor.Contributions;

/// <summary>The gestures of one command: what it ships with, what is in force, and the defaults a user binding took.</summary>
/// <param name="CommandId">The id of the command.</param>
/// <param name="Defaults">The gestures the command was registered with.</param>
/// <param name="Effective">The gestures in force.</param>
/// <param name="Overridden">The default gestures a user binding of another command took.</param>
internal sealed record CommandBindings(string CommandId, IReadOnlyList<CommandGesture> Defaults, IReadOnlyList<CommandGesture> Effective, IReadOnlyList<CommandGesture> Overridden)
{
    /// <summary>Gets a value indicating whether the gestures in force differ from the defaults.</summary>
    public bool IsChanged => !Defaults.Order(GestureOrder.Instance).SequenceEqual(Effective.Order(GestureOrder.Instance));
}

/// <summary>A gesture of the <c>keyboard</c> section that was not applied.</summary>
/// <param name="Command">The id of the command the entry names.</param>
/// <param name="Gesture">The gesture text as written in the file.</param>
/// <param name="Reason">Why it was ignored.</param>
internal sealed record KeyBindingIssue(string Command, string Gesture, string Reason);

/// <summary>The effective key bindings of one registry snapshot (research R21).</summary>
internal sealed class EffectiveKeyBindings
{
    private readonly Dictionary<string, CommandBindings> byCommand;

    internal EffectiveKeyBindings(Dictionary<string, CommandBindings> byCommand, IReadOnlyList<KeyBindingOverride> notLoaded, IReadOnlyList<KeyBindingIssue> issues)
    {
        this.byCommand = byCommand;
        NotLoaded = notLoaded;
        Issues = issues;
    }

    /// <summary>Gets the user entries whose command is not registered; they stay in the file.</summary>
    public IReadOnlyList<KeyBindingOverride> NotLoaded { get; }

    /// <summary>Gets the gestures of the file that were reported and ignored.</summary>
    public IReadOnlyList<KeyBindingIssue> Issues { get; }

    /// <summary>Gets the gestures of a command.</summary>
    /// <param name="commandId">The id of a registered command.</param>
    /// <returns>The gestures.</returns>
    /// <exception cref="KeyNotFoundException">No command has the id.</exception>
    public CommandBindings For(string commandId) => byCommand[commandId];

    /// <summary>Gets the gestures of a command.</summary>
    /// <param name="commandId">The id of a command.</param>
    /// <param name="bindings">The gestures when the command is registered.</param>
    /// <returns><see langword="true"/> when the command is registered.</returns>
    public bool TryGet(string commandId, out CommandBindings bindings)
    {
        if (byCommand.TryGetValue(commandId, out CommandBindings? found))
        {
            bindings = found;
            return true;
        }

        bindings = new CommandBindings(commandId, [], [], []);
        return false;
    }
}

/// <summary>Computes the effective gestures: user override, then built-in default, then extension default in load order (FR-050).</summary>
internal static class KeyBindingResolver
{
    private static string GlobalScopeReason => Strings.Settings_Keyboard_GlobalScopeRefusal;

    private static string UnparseableReason => Strings.Settings_Keyboard_NotAGesture;

    /// <summary>Resolves the gestures of a snapshot's commands against the user's overrides.</summary>
    /// <param name="commands">The registered commands in registration order, built-in first and extensions in load order; their gestures are the defaults.</param>
    /// <param name="user">The <c>keyboard</c> section.</param>
    /// <returns>The effective set.</returns>
    public static EffectiveKeyBindings Resolve(IReadOnlyList<CommandDescriptor> commands, KeyboardSettings user)
    {
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(user);
        Dictionary<string, CommandDescriptor> byId = [];
        foreach (CommandDescriptor command in commands)
        {
            byId.TryAdd(command.Id, command);
        }

        Dictionary<string, List<CommandGesture>> effective = byId.ToDictionary(pair => pair.Key, pair => (pair.Value.DefaultGestures ?? []).ToList(), StringComparer.Ordinal);
        Dictionary<string, List<CommandGesture>> added = byId.Keys.ToDictionary(id => id, _ => new List<CommandGesture>(), StringComparer.Ordinal);
        List<KeyBindingOverride> notLoaded = [];
        List<KeyBindingIssue> issues = [];

        foreach (KeyBindingOverride entry in user.Bindings)
        {
            if (entry.Problem is { } problem)
            {
                issues.Add(new KeyBindingIssue(KeyboardSettings.SectionId + ".bindings", entry.RawJson ?? "", problem));
            }

            if (!byId.TryGetValue(entry.Command, out CommandDescriptor? command))
            {
                notLoaded.Add(entry);
                continue;
            }

            foreach (string text in entry.Remove)
            {
                if (CommandGesture.TryParse(text, out CommandGesture? gesture))
                {
                    effective[command.Id].Remove(gesture);
                }
                else
                {
                    issues.Add(new KeyBindingIssue(command.Id, text, UnparseableReason));
                }
            }

            foreach (string text in entry.Add)
            {
                if (!CommandGesture.TryParse(text, out CommandGesture? gesture))
                {
                    issues.Add(new KeyBindingIssue(command.Id, text, UnparseableReason));
                }
                else if (Refusal(command.Scope, gesture) is { } reason)
                {
                    issues.Add(new KeyBindingIssue(command.Id, text, reason));
                }
                else if (TakenByUser(byId, added, command, gesture) is { } other)
                {
                    issues.Add(new KeyBindingIssue(command.Id, text, TextFormat.Of(Strings.Settings_Keyboard_TakenByUser, other)));
                }
                else if (!effective[command.Id].Contains(gesture))
                {
                    effective[command.Id].Add(gesture);
                    added[command.Id].Add(gesture);
                }
            }
        }

        Dictionary<string, List<CommandGesture>> overridden = byId.Keys.ToDictionary(id => id, _ => new List<CommandGesture>(), StringComparer.Ordinal);
        foreach ((string id, List<CommandGesture> gestures) in added)
        {
            foreach (CommandGesture gesture in gestures)
            {
                foreach (CommandDescriptor other in byId.Values.Where(other => other.Id != id && ScopesOverlap(other.Scope, byId[id].Scope)))
                {
                    if (effective[other.Id].Remove(gesture))
                    {
                        overridden[other.Id].Add(gesture);
                    }
                }
            }
        }

        Dictionary<string, CommandBindings> result = byId.ToDictionary(
            pair => pair.Key,
            pair => new CommandBindings(pair.Key, [.. pair.Value.DefaultGestures ?? []], [.. effective[pair.Key]], [.. overridden[pair.Key]]),
            StringComparer.Ordinal);
        return new EffectiveKeyBindings(result, notLoaded, issues);
    }

    /// <summary>Tells why a gesture may not be bound in a scope.</summary>
    /// <param name="scope">The scope of the command.</param>
    /// <param name="gesture">The gesture.</param>
    /// <returns>The reason, or <see langword="null"/> when the scope rules (ADR-0020) allow it.</returns>
    public static string? Refusal(CommandScope scope, CommandGesture gesture)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        return scope == CommandScope.Global && gesture.IsPlain && !gesture.IsFunctionKey ? GlobalScopeReason : null;
    }

    /// <summary>Tells whether the scopes of two commands can be active at once.</summary>
    /// <param name="a">The first scope.</param>
    /// <param name="b">The second scope.</param>
    /// <returns><see langword="true"/> when a key press in one can reach the other.</returns>
    public static bool ScopesOverlap(CommandScope a, CommandScope b) =>
        a == CommandScope.Global || b == CommandScope.Global || (a & b) != 0;

    private static string? TakenByUser(Dictionary<string, CommandDescriptor> byId, Dictionary<string, List<CommandGesture>> added, CommandDescriptor command, CommandGesture gesture) =>
        added.FirstOrDefault(pair => pair.Key != command.Id && pair.Value.Contains(gesture) && ScopesOverlap(byId[pair.Key].Scope, command.Scope)).Key;
}

/// <summary>Orders gestures by their text, so two sets compare equal whatever their order.</summary>
internal sealed class GestureOrder : IComparer<CommandGesture>
{
    /// <summary>Gets the shared instance.</summary>
    public static GestureOrder Instance { get; } = new();

    /// <inheritdoc/>
    public int Compare(CommandGesture? x, CommandGesture? y) => string.CompareOrdinal(x?.ToString(), y?.ToString());
}
