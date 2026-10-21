using NetPrints.Editor.Contributions;
using NetPrints.Editor.State;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.Settings;

/// <summary>What happened when a gesture was added to a command.</summary>
/// <param name="Added">Whether the gesture is now bound to the command.</param>
/// <param name="Refusal">Why the gesture was refused (not a gesture, or against the scope rules); <see langword="null"/> when it was not.</param>
/// <param name="Conflict">The command that uses the gesture in an overlapping scope; <see langword="null"/> when none does.</param>
internal sealed record GestureAddResult(bool Added, string? Refusal, CommandDescriptor? Conflict);

/// <summary>Writes the user's key bindings as differences from the defaults (FR-046, FR-049, research R21) and applies them to the registry at once.</summary>
/// <param name="registry">The registry whose snapshot says what the defaults and the effective gestures are.</param>
/// <param name="store">The settings store that keeps the <c>keyboard</c> section.</param>
internal sealed class KeyBindingEditor(IContributionRegistry registry, ISettingsStore store)
{
    private static string NotAGestureReason => Strings.Settings_Keyboard_NotAGesture;

    /// <summary>Adds a gesture to a command unless it is refused or another command uses it.</summary>
    /// <param name="command">The command.</param>
    /// <param name="gestureText">The gesture text.</param>
    /// <param name="acceptConflict">Whether a command that uses the gesture loses it.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The outcome; nothing changed unless <see cref="GestureAddResult.Added"/>.</returns>
    public async Task<GestureAddResult> AddAsync(CommandDescriptor command, string gestureText, bool acceptConflict, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(gestureText);
        if (!CommandGesture.TryParse(gestureText, out CommandGesture? gesture))
        {
            return new GestureAddResult(false, NotAGestureReason, null);
        }

        if (KeyBindingResolver.Refusal(command.Scope, gesture) is { } refusal)
        {
            return new GestureAddResult(false, refusal, null);
        }

        EffectiveKeyBindings bindings = registry.Snapshot.KeyBindings;
        if (bindings.For(command.Id).Effective.Contains(gesture))
        {
            return new GestureAddResult(true, null, null);
        }

        CommandDescriptor? other = registry.Snapshot.Commands.FirstOrDefault(c =>
            c.Id != command.Id && KeyBindingResolver.ScopesOverlap(c.Scope, command.Scope) && bindings.For(c.Id).Effective.Contains(gesture));
        if (other is not null && !acceptConflict)
        {
            return new GestureAddResult(false, null, other);
        }

        KeyboardSettings settings = store.Get(KeyboardSettings.Descriptor);
        if (other is not null)
        {
            settings = Without(settings, other, bindings.For(other.Id), gesture);
        }

        settings = With(settings, command, bindings.For(command.Id), gesture);
        await ApplyAsync(settings, cancellationToken).ConfigureAwait(true);
        return new GestureAddResult(true, null, other);
    }

    /// <summary>Removes a gesture from a command.</summary>
    /// <param name="command">The command.</param>
    /// <param name="gesture">The gesture; one the command does not have changes nothing.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the change is stored and applied.</returns>
    public Task RemoveAsync(CommandDescriptor command, CommandGesture gesture, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(gesture);
        return ApplyAsync(Without(store.Get(KeyboardSettings.Descriptor), command, registry.Snapshot.KeyBindings.For(command.Id), gesture), cancellationToken);
    }

    /// <summary>Resets one command to its defaults by deleting its entry.</summary>
    /// <param name="commandId">The id of the command.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the change is stored and applied.</returns>
    public Task ResetAsync(string commandId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commandId);
        KeyboardSettings settings = store.Get(KeyboardSettings.Descriptor);
        return ApplyAsync(settings with { Bindings = [.. settings.Bindings.Where(entry => entry.Command != commandId)] }, cancellationToken);
    }

    /// <summary>Deletes one entry of the file, whatever its command, and leaves every other entry as it is.</summary>
    /// <param name="entry">The entry, as listed by the registry; matched by instance.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the change is stored and applied.</returns>
    public Task RemoveEntryAsync(KeyBindingOverride entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        KeyboardSettings settings = store.Get(KeyboardSettings.Descriptor);
        int index = -1;
        for (int i = 0; i < settings.Bindings.Count && index < 0; i++)
        {
            index = ReferenceEquals(settings.Bindings[i], entry) ? i : index;
        }

        return index < 0
            ? Task.CompletedTask
            : ApplyAsync(settings with { Bindings = [.. settings.Bindings.Where((_, position) => position != index)] }, cancellationToken);
    }

    /// <summary>Resets every command, including the entries of commands that are not loaded.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the change is stored and applied.</returns>
    public Task ResetAllAsync(CancellationToken cancellationToken) => ApplyAsync(new KeyboardSettings(), cancellationToken);

    private static KeyboardSettings With(KeyboardSettings settings, CommandDescriptor command, CommandBindings current, CommandGesture gesture)
    {
        string text = gesture.ToString();
        return Edit(settings, command, entry => current.Defaults.Contains(gesture)
            ? entry with { Remove = [.. entry.Remove.Where(removed => removed != text)] }
            : entry with { Add = entry.Add.Contains(text) ? entry.Add : [.. entry.Add, text] });
    }

    private static KeyboardSettings Without(KeyboardSettings settings, CommandDescriptor command, CommandBindings current, CommandGesture gesture)
    {
        string text = gesture.ToString();
        return Edit(settings, command, entry => entry.Add.Contains(text)
            ? entry with { Add = [.. entry.Add.Where(added => added != text)] }
            : current.Defaults.Contains(gesture) && !entry.Remove.Contains(text) ? entry with { Remove = [.. entry.Remove, text] } : entry);
    }

    private static KeyboardSettings Edit(KeyboardSettings settings, CommandDescriptor command, Func<KeyBindingOverride, KeyBindingOverride> change)
    {
        List<KeyBindingOverride> entries = [.. settings.Bindings];
        int index = entries.FindIndex(entry => entry.Command == command.Id);
        KeyBindingOverride edited = change(index >= 0 ? entries[index] : new KeyBindingOverride { Command = command.Id, Scope = command.Scope.ToString() });
        if (index >= 0)
        {
            entries.RemoveAt(index);
        }

        if (edited.Add.Count > 0 || edited.Remove.Count > 0)
        {
            entries.Insert(index >= 0 ? index : entries.Count, edited);
        }

        return settings with { Bindings = entries };
    }

    private async Task ApplyAsync(KeyboardSettings settings, CancellationToken cancellationToken)
    {
        await store.SetAsync(KeyboardSettings.Descriptor, settings, cancellationToken).ConfigureAwait(true);
        registry.SetUserBindings(settings);
    }
}
