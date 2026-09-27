using System.Collections.Concurrent;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace NetPrints.Extensibility.Settings;

/// <summary>
/// Keeps the settings in one JSON file (extension-points.md §7): the built-in section under <c>netprints</c>, every
/// extension's section under <c>extensions.&lt;id&gt;</c>. The file is read once and cached; a write rewrites the whole
/// file atomically and keeps sections it does not know.
/// </summary>
public sealed class JsonFileSettingsStore : ISettingsStore
{
    private const string FileSection = "$file";
    private const string NetPrintsKey = NetPrintsSettings.SectionId;
    private const string ExtensionsKey = "extensions";
    private const string SchemaVersionKey = "schemaVersion";
    private const int SchemaVersion = 1;

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        IndentSize = 2,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly string filePath;
    private readonly ILogger<JsonFileSettingsStore> logger;
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private readonly Lock stateLock = new();
    private readonly Lazy<State> initial;
    private State? state;

    /// <summary>
    /// Creates a store over <paramref name="filePath"/>. The file is not touched until the first read or write.
    /// </summary>
    /// <param name="filePath">The settings file; it and its folder need not exist.</param>
    /// <param name="logger">Logger for <c>SettingsSectionInvalid</c> (2006).</param>
    public JsonFileSettingsStore(string filePath, ILogger<JsonFileSettingsStore> logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(logger);
        this.filePath = filePath;
        this.logger = logger;
        initial = new Lazy<State>(Load, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// The per-user settings file: <c>NetPrints/settings.json</c> under the application-data folder
    /// (<c>$XDG_CONFIG_HOME</c> or <c>~/.config</c> on Linux and macOS, <c>%APPDATA%</c> on Windows).
    /// </summary>
    /// <returns>The full path.</returns>
    public static string DefaultFilePath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetPrints", "settings.json");

    /// <inheritdoc/>
    public T Get<T>(ExtensionSettingsDescriptor<T> descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        lock (stateLock)
        {
            State current = state ?? initial.Value;
            if (current.Values.TryGetValue(descriptor.ExtensionId, out object? cached) && cached is T typed)
            {
                return typed;
            }

            T value = Read(current.Root, descriptor);
            current.Values[descriptor.ExtensionId] = value;
            return value;
        }
    }

    /// <inheritdoc/>
    public async ValueTask SetAsync<T>(ExtensionSettingsDescriptor<T> descriptor, T value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(value);

        await writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            State current;
            lock (stateLock)
            {
                current = state ?? initial.Value;
            }

            JsonNode? section = JsonSerializer.SerializeToNode(value, descriptor.TypeInfo);
            JsonObject root = current.Root.DeepClone().AsObject();
            SetSection(root, descriptor.ExtensionId, section);
            byte[] bytes = Render(root);

            await WriteAtomicAsync(bytes, cancellationToken).ConfigureAwait(false);

            var values = new ConcurrentDictionary<string, object?>(current.Values, StringComparer.Ordinal)
            {
                [descriptor.ExtensionId] = value,
            };
            lock (stateLock)
            {
                state = new State(root, values);
            }
        }
        finally
        {
            writeGate.Release();
        }
    }

    private static void SetSection(JsonObject root, string id, JsonNode? section)
    {
        if (id == NetPrintsKey)
        {
            root[NetPrintsKey] = section;
            return;
        }

        if (root[ExtensionsKey] is not JsonObject extensions)
        {
            extensions = [];
            root[ExtensionsKey] = extensions;
        }

        extensions[id] = section;
    }

    private static byte[] Render(JsonObject root)
    {
        var ordered = new JsonObject { [SchemaVersionKey] = SchemaVersion };
        if (root[NetPrintsKey] is JsonNode netprints)
        {
            ordered[NetPrintsKey] = netprints.DeepClone();
        }

        if (root[ExtensionsKey] is JsonObject extensions)
        {
            var sorted = new JsonObject();
            foreach (KeyValuePair<string, JsonNode?> entry in extensions.OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                sorted[entry.Key] = entry.Value?.DeepClone();
            }

            ordered[ExtensionsKey] = sorted;
        }

        foreach (KeyValuePair<string, JsonNode?> entry in root.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (entry.Key is not (SchemaVersionKey or NetPrintsKey or ExtensionsKey))
            {
                ordered[entry.Key] = entry.Value?.DeepClone();
            }
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            ordered.WriteTo(writer);
        }

        stream.WriteByte((byte)'\n');
        return stream.ToArray();
    }

    private static JsonNode? FindSection(JsonObject root, string id) =>
        id == NetPrintsKey ? root[NetPrintsKey] : (root[ExtensionsKey] as JsonObject)?[id];

    private T Read<T>(JsonObject root, ExtensionSettingsDescriptor<T> descriptor)
    {
        JsonNode? section = FindSection(root, descriptor.ExtensionId);
        if (section is null)
        {
            return descriptor.Default;
        }

        try
        {
            T? value = section.Deserialize(descriptor.TypeInfo);
            if (value is not null)
            {
                return value;
            }

            Log.SettingsSectionInvalid(logger, null, descriptor.ExtensionId, filePath);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            Log.SettingsSectionInvalid(logger, ex, descriptor.ExtensionId, filePath);
        }

        return descriptor.Default;
    }

    private State Load()
    {
        var values = new ConcurrentDictionary<string, object?>(StringComparer.Ordinal);
        if (!File.Exists(filePath))
        {
            return new State([], values);
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            if (JsonNode.Parse(bytes, documentOptions: DocumentOptions) is JsonObject root)
            {
                return new State(root, values);
            }

            Log.SettingsSectionInvalid(logger, null, FileSection, filePath);
        }
        catch (JsonException ex)
        {
            Log.SettingsSectionInvalid(logger, ex, FileSection, filePath);
        }

        return new State([], values);
    }

    private async Task WriteAtomicAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = $"{filePath}.tmp-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllBytesAsync(tempPath, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, filePath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    private sealed record State(JsonObject Root, ConcurrentDictionary<string, object?> Values);
}
