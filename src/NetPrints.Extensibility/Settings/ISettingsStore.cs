namespace NetPrints.Extensibility.Settings;

/// <summary>
/// Reads and writes per-extension settings sections (extension-points.md §7).
/// </summary>
public interface ISettingsStore
{
    /// <summary>
    /// Gets the current value of a section.
    /// </summary>
    /// <typeparam name="T">The section's value type.</typeparam>
    /// <param name="descriptor">The section.</param>
    /// <returns>The stored value, or <see cref="ExtensionSettingsDescriptor{T}.Default"/> when it is missing or invalid.</returns>
    T Get<T>(ExtensionSettingsDescriptor<T> descriptor);

    /// <summary>
    /// Stores a section's value and persists it.
    /// </summary>
    /// <typeparam name="T">The section's value type.</typeparam>
    /// <param name="descriptor">The section.</param>
    /// <param name="value">The new value.</param>
    /// <param name="cancellationToken">Cancels the write before it starts.</param>
    /// <returns>A task that completes when the value is persisted.</returns>
    ValueTask SetAsync<T>(ExtensionSettingsDescriptor<T> descriptor, T value, CancellationToken cancellationToken);
}
