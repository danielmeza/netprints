using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetPrints.Core;
using NetPrints.Extensibility.Loading;
using NetPrints.Projects;

namespace NetPrints.Extensibility.Settings;

/// <summary>
/// Keeps the settings in one JSON file (extension-points.md §7): the built-in section under <c>netprints</c>, every
/// extension's section under <c>extensions.&lt;id&gt;</c>. The file is read once and cached; a write rewrites the whole
/// file atomically and keeps sections it does not know.
/// </summary>
[Experimental(ExperimentalApiIds.Settings, UrlFormat = ExperimentalApiIds.UrlFormat)]
public sealed class JsonFileSettingsStore : ISettingsStore, ISettingsChangeSource
{
    private const string FileSection = "$file";
    private const string NetPrintsKey = NetPrintsSettings.SectionId;
    private const string KeyboardKey = "keyboard";
    private const string ExtensionsKey = "extensions";
    private const string BuiltInPrefix = NetPrintsKey + ".";
    private const string SchemaVersionKey = "schemaVersion";
    private const int SchemaVersion = 1;
    private const string NullSectionReason = "the section is null";
    private const string NotAnObjectReason = "the file is not a JSON object";

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        // A duplicate key (e.g. a merge conflict resolved by keeping both lines) must fail as a
        // JsonException Load already catches, not parse silently (R1-05).
        AllowDuplicateProperties = false,
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
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private readonly Lock stateLock = new();
    private readonly List<Subscription> listeners = [];
    private readonly List<SettingsSectionIssue> issues = [];
    private readonly Lazy<State> initial;
    private State? state;

    /// <summary>
    /// Creates a store over <paramref name="filePath"/>. The file is not touched until the first read or write.
    /// </summary>
    /// <param name="filePath">The settings file; it and its folder need not exist.</param>
    /// <param name="logger">Logger for <c>SettingsSectionInvalid</c> (2006).</param>
    /// <param name="timeProvider">The clock behind the name of the <c>.unreadable-</c> copy; the system clock when <see langword="null"/>.</param>
    public JsonFileSettingsStore(string filePath, ILogger<JsonFileSettingsStore> logger, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(logger);
        this.filePath = filePath;
        this.logger = logger;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        initial = new Lazy<State>(Load, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// The per-user settings file: <c>NetPrints/settings.json</c> under the application-data folder
    /// (<c>$XDG_CONFIG_HOME</c> or <c>~/.config</c> on Linux, <c>~/Library/Application Support</c> on macOS, <c>%APPDATA%</c> on Windows).
    /// </summary>
    /// <returns>The full path.</returns>
    public static string DefaultFilePath() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NetPrints", "settings.json");

    /// <summary>
    /// Gets the sections that failed validation and were replaced by their defaults, one entry per section, in the order they were read.
    /// </summary>
    public IReadOnlyList<SettingsSectionIssue> Issues
    {
        get
        {
            lock (stateLock)
            {
                return [.. issues];
            }
        }
    }

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
    public IDisposable OnSectionChanged(string sectionId, Action listener)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionId);
        ArgumentNullException.ThrowIfNull(listener);
        var subscription = new Subscription(this, sectionId, listener);
        lock (stateLock)
        {
            listeners.Add(subscription);
        }

        return subscription;
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
            byte[] bytes = Render(root, current.SchemaVersion);

            if (current.CopyPending)
            {
                CopyUnreadableFile();
                current.CopyPending = false;
            }

            await WriteAtomicAsync(bytes, cancellationToken).ConfigureAwait(false);

            var values = new ConcurrentDictionary<string, object?>(current.Values, StringComparer.Ordinal)
            {
                [descriptor.ExtensionId] = value,
            };
            lock (stateLock)
            {
                state = new State(root, values, current.SchemaVersion, copyPending: false);
            }
        }
        finally
        {
            writeGate.Release();
        }

        Notify(descriptor.ExtensionId);
    }

    private static void SetSection(JsonObject root, string id, JsonNode? section)
    {
        if (IsHostSection(id))
        {
            root[id] = section;
            return;
        }

        if (root[ExtensionsKey] is not JsonObject extensions)
        {
            extensions = [];
            root[ExtensionsKey] = extensions;
        }

        extensions[id] = section;
    }

    private static byte[] Render(JsonObject root, int schemaVersion)
    {
        var ordered = new JsonObject { [SchemaVersionKey] = schemaVersion };
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

    private static bool IsHostSection(string id) => id is NetPrintsKey or RunSettings.SectionId or KeyboardKey;

    private static bool IsBuiltInSection(string id) =>
        IsHostSection(id) || id.StartsWith(BuiltInPrefix, StringComparison.Ordinal);

    private static JsonNode? FindSection(JsonObject root, string id) =>
        IsHostSection(id) ? root[id] : (root[ExtensionsKey] as JsonObject)?[id];

    private void Report(string sectionId, string reason, Exception? exception)
    {
        string? code = IsBuiltInSection(sectionId) ? null : ExtensionDiagnosticCodes.SettingsInvalid;
        issues.Add(new SettingsSectionIssue(sectionId, code, reason));
        Log.SettingsSectionInvalid(logger, exception, sectionId, filePath, code is null ? reason : $"{code}: {reason}");
    }

    private T Read<T>(JsonObject root, ExtensionSettingsDescriptor<T> descriptor)
    {
        JsonNode? section = FindSection(root, descriptor.ExtensionId);
        if (section is null)
        {
            return descriptor.Default;
        }

        try
        {
            T? value = OverlayOnDefaults(descriptor.Default, section, descriptor.TypeInfo).Deserialize(descriptor.TypeInfo);
            if (value is null)
            {
                Report(descriptor.ExtensionId, NullSectionReason, null);
                return descriptor.Default;
            }

            IReadOnlyList<string>? failures = descriptor.Validator?.Invoke(value);
            if (failures is null || failures.Count == 0)
            {
                return value;
            }

            Report(descriptor.ExtensionId, string.Join("; ", failures), null);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            Report(descriptor.ExtensionId, ex.Message, ex);
        }

        return descriptor.Default;
    }

    /// <summary>
    /// Overlays the stored section on the descriptor's default, so a member the file omits keeps the default. The
    /// source-generated deserializer synthesizes constructor parameters from init-only properties and supplies
    /// <c>default(T)</c> for a missing member, which would erase a collection or string initializer.
    /// </summary>
    /// <typeparam name="T">The section type.</typeparam>
    /// <param name="default">The descriptor's default section.</param>
    /// <param name="section">The stored section.</param>
    /// <param name="typeInfo">The section's JSON metadata.</param>
    /// <returns>The stored section with the default filling its missing members.</returns>
    private static JsonNode OverlayOnDefaults<T>(T @default, JsonNode section, JsonTypeInfo<T> typeInfo)
    {
        if (section is not JsonObject stored)
        {
            return section;
        }

        JsonNode? defaultsNode;
        try
        {
            defaultsNode = JsonSerializer.SerializeToNode(@default, typeInfo);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            return section;
        }

        if (defaultsNode is not JsonObject defaults)
        {
            return section;
        }

        foreach (KeyValuePair<string, JsonNode?> property in stored)
        {
            defaults[property.Key] = property.Value?.DeepClone();
        }

        return defaults;
    }

    private State Load()
    {
        var values = new ConcurrentDictionary<string, object?>(StringComparer.Ordinal);
        if (!File.Exists(filePath))
        {
            return new State([], values, SchemaVersion, copyPending: false);
        }

        bool copyPending = false;
        try
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            if (JsonNode.Parse(bytes, documentOptions: DocumentOptions) is JsonObject root)
            {
                int found = root[SchemaVersionKey] is JsonValue value && value.TryGetValue(out int number) ? number : SchemaVersion;
                if (found > SchemaVersion)
                {
                    Log.SettingsSchemaNewer(logger, filePath, found, SchemaVersion);
                }

                return new State(root, values, Math.Max(found, SchemaVersion), copyPending: false);
            }

            Log.SettingsSectionInvalid(logger, null, FileSection, filePath, NotAnObjectReason);
            copyPending = true;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // R1-13: a locked or unreadable settings.json (not just malformed JSON) must not be cached
            // by the Lazy above as a permanent failure — every Get would then rethrow from editor startup.
            Log.SettingsSectionInvalid(logger, ex, FileSection, filePath, ex.Message);
            copyPending = ex is JsonException;
        }

        return new State([], values, SchemaVersion, copyPending);
    }

    private void CopyUnreadableFile()
    {
        string stamp = timeProvider.GetUtcNow().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        string copyPath = $"{filePath}.unreadable-{stamp}";
        File.Copy(filePath, copyPath, overwrite: true);
        Log.SettingsFileCopied(logger, filePath, copyPath);
    }

    private void Notify(string sectionId)
    {
        Action[] targets;
        lock (stateLock)
        {
            targets = [.. listeners.Where(entry => entry.SectionId == sectionId).Select(entry => entry.Listener)];
        }

        foreach (Action target in targets)
        {
            try
            {
                target();
            }
            catch (Exception ex)
            {
                Log.SettingsListenerFailed(logger, ex, sectionId);
            }
        }
    }

    private async Task WriteAtomicAsync(byte[] bytes, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        await ProjectFiles.WriteAtomicAsync(filePath, bytes, cancellationToken).ConfigureAwait(false);
    }

    private sealed class State(JsonObject root, ConcurrentDictionary<string, object?> values, int schemaVersion, bool copyPending)
    {
        public JsonObject Root { get; } = root;

        public ConcurrentDictionary<string, object?> Values { get; } = values;

        public int SchemaVersion { get; } = schemaVersion;

        public bool CopyPending { get; set; } = copyPending;
    }

    private sealed class Subscription(JsonFileSettingsStore store, string sectionId, Action listener) : IDisposable
    {
        public string SectionId { get; } = sectionId;

        public Action Listener { get; } = listener;

        public void Dispose()
        {
            lock (store.stateLock)
            {
                store.listeners.Remove(this);
            }
        }
    }
}
