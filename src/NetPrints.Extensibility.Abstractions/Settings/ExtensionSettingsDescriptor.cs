using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using NetPrints.Core;

namespace NetPrints.Extensibility.Settings;

/// <summary>
/// Names one extension's settings section and its value type (extension-points.md §7). An extension registers
/// at most one descriptor, and its <see cref="ExtensionId"/> is its manifest id.
/// </summary>
/// <param name="ExtensionId">The extension id; the key of the section in the settings file.</param>
/// <param name="ValueType">The CLR type of the section's value.</param>
[Experimental(ExperimentalApiIds.Settings, UrlFormat = ExperimentalApiIds.UrlFormat)]
public abstract record ExtensionSettingsDescriptor(string ExtensionId, Type ValueType)
{
    /// <summary>Gets the source-generated JSON metadata of the section's value type.</summary>
    public abstract JsonTypeInfo SectionTypeInfo { get; }

    /// <summary>Reads the section's current value from <paramref name="store"/> as the JSON object the settings file holds.</summary>
    /// <param name="store">The store to read from.</param>
    /// <returns>A new object the caller may edit.</returns>
    public abstract JsonObject ReadSection(ISettingsStore store);

    /// <summary>Checks an edited section the way the store checks one it has read.</summary>
    /// <param name="section">The edited section.</param>
    /// <returns>The failure messages; empty when the section is valid.</returns>
    public abstract IReadOnlyList<string> ValidateSection(JsonObject section);

    /// <summary>Stores an edited section that <see cref="ValidateSection"/> accepted.</summary>
    /// <param name="store">The store to write to.</param>
    /// <param name="section">The edited section.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes when the file is written and the listeners are notified.</returns>
    /// <exception cref="JsonException"><paramref name="section"/> does not deserialize to the section's value type.</exception>
    public abstract ValueTask WriteSectionAsync(ISettingsStore store, JsonObject section, CancellationToken cancellationToken);
}

/// <summary>
/// A settings section of value type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The section's value type.</typeparam>
/// <param name="ExtensionId">The extension id; the key of the section in the settings file.</param>
/// <param name="TypeInfo">Source-generated JSON metadata for <typeparamref name="T"/>.</param>
/// <param name="Default">The value used when the section is missing or invalid.</param>
[Experimental(ExperimentalApiIds.Settings, UrlFormat = ExperimentalApiIds.UrlFormat)]
public sealed record ExtensionSettingsDescriptor<T>(string ExtensionId, JsonTypeInfo<T> TypeInfo, T Default)
    : ExtensionSettingsDescriptor(ExtensionId, typeof(T))
{
    /// <summary>
    /// Gets the validator the settings store runs on a section it has read. It returns the failure messages; a
    /// non-empty result replaces the section by <see cref="Default"/> and the messages are logged.
    /// <see langword="null"/> accepts every value.
    /// </summary>
    public Func<T, IReadOnlyList<string>>? Validator { get; init; }

    /// <inheritdoc/>
    public override JsonTypeInfo SectionTypeInfo => TypeInfo;

    /// <inheritdoc/>
    public override JsonObject ReadSection(ISettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        return JsonSerializer.SerializeToNode(store.Get(this), TypeInfo) as JsonObject
            ?? throw new InvalidOperationException($"The settings section '{ExtensionId}' is not a JSON object.");
    }

    /// <inheritdoc/>
    public override IReadOnlyList<string> ValidateSection(JsonObject section)
    {
        ArgumentNullException.ThrowIfNull(section);
        try
        {
            T? value = section.Deserialize(TypeInfo);
            return value is null ? ["The section is empty."] : Validator?.Invoke(value) ?? [];
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            return [ex.Message];
        }
    }

    /// <inheritdoc/>
    public override ValueTask WriteSectionAsync(ISettingsStore store, JsonObject section, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(section);
        T value = section.Deserialize(TypeInfo) ?? throw new JsonException($"The settings section '{ExtensionId}' is empty.");
        return store.SetAsync(this, value, cancellationToken);
    }
}
