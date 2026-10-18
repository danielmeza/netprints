using System.Text.Json.Nodes;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.Settings;

/// <summary>One settings section as the pages edit it: its exported schema and the descriptor and store that read and write it.</summary>
internal sealed class SettingsSection
{
    private readonly ExtensionSettingsDescriptor descriptor;
    private readonly ISettingsStore store;
    private readonly JsonObject properties;

    /// <summary>Creates the section over a descriptor.</summary>
    /// <param name="descriptor">The descriptor whose schema lists the fields.</param>
    /// <param name="store">The store the section is read from and written to.</param>
    public SettingsSection(ExtensionSettingsDescriptor descriptor, ISettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(store);
        this.descriptor = descriptor;
        this.store = store;
        properties = SettingsSchema.Export(descriptor.SectionTypeInfo)["properties"] as JsonObject ?? [];
    }

    /// <summary>Gets the id of the section, the key it has in <c>settings.json</c>.</summary>
    public string Id => descriptor.ExtensionId;

    /// <summary>Reads one key of the section as it is stored now.</summary>
    /// <param name="key">The JSON name of the key.</param>
    /// <returns>A detached copy of the value, or <see langword="null"/> when the value is null or the key unknown.</returns>
    public JsonNode? Read(string key) => descriptor.ReadSection(store)[key]?.DeepClone();

    /// <summary>Validates a new value for one key and, when the section stays valid, stores it.</summary>
    /// <param name="key">The JSON name of the key.</param>
    /// <param name="value">The new value, owned by no other node.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The validator's messages; empty when the value was stored.</returns>
    public async Task<IReadOnlyList<string>> TrySetAsync(string key, JsonNode? value, CancellationToken cancellationToken)
    {
        JsonObject section = descriptor.ReadSection(store);
        section[key] = value;
        IReadOnlyList<string> failures = descriptor.ValidateSection(section);
        if (failures.Count > 0)
        {
            return failures;
        }

        await descriptor.WriteSectionAsync(store, section, cancellationToken).ConfigureAwait(false);
        return [];
    }

    /// <summary>Creates the fields of the section in schema order.</summary>
    /// <param name="services">What the fields need from the host.</param>
    /// <param name="keys">The keys to create, or <see langword="null"/> for all of them.</param>
    /// <returns>One field per key whose schema has a field kind.</returns>
    public IReadOnlyList<SettingsFieldViewModel> CreateFields(SettingsFieldServices services, IReadOnlyCollection<string>? keys = null) =>
    [
        .. properties
            .Where(property => keys is null || keys.Contains(property.Key))
            .Select(property => property.Value is JsonObject schema ? SettingsFieldFactory.Create(this, property.Key, schema, services) : null)
            .OfType<SettingsFieldViewModel>(),
    ];
}
