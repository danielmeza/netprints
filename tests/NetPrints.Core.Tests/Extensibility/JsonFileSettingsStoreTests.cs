using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NetPrints.Extensibility.Settings;
using Xunit;

namespace NetPrints.Tests.Extensibility;

public sealed record SampleSettings(string Name, int Count);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(SampleSettings))]
internal sealed partial class SampleSettingsContext : JsonSerializerContext;

/// <summary>EX-T09: the JSON settings file store (extension-points.md §7).</summary>
public class JsonFileSettingsStoreTests : IDisposable
{
    private static readonly ExtensionSettingsDescriptor<SampleSettings> Sample =
        new("test.sample", SampleSettingsContext.Default.SampleSettings, new SampleSettings("default", 1));

    private static readonly ExtensionSettingsDescriptor<SampleSettings> Other =
        new("test.other", SampleSettingsContext.Default.SampleSettings, new SampleSettings("other-default", 2));

    private readonly string directory = ExtensionTestSupport.NewTempDirectory();
    private readonly CollectingLoggerFactory logs = new();

    private string FilePath => Path.Combine(directory, "sub", "settings.json");

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private JsonFileSettingsStore NewStore() => new(FilePath, logs.CreateLogger<JsonFileSettingsStore>());

    [Fact]
    public void MissingFileGivesDefaults()
    {
        JsonFileSettingsStore store = NewStore();

        Assert.Equal(Sample.Default, store.Get(Sample));
        Assert.Empty(store.Get(NetPrintsSettings.Descriptor).ExtensionPaths);
        Assert.Empty(logs.Entries);
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public async Task AnimationsAreOnByDefaultAndTheOffChoiceIsKept()
    {
        JsonFileSettingsStore store = NewStore();
        Assert.True(store.Get(NetPrintsSettings.Descriptor).EnableAnimations);

        await store.SetAsync(NetPrintsSettings.Descriptor, new NetPrintsSettings { EnableAnimations = false }, TestContext.Current.CancellationToken);

        Assert.False(NewStore().Get(NetPrintsSettings.Descriptor).EnableAnimations);
    }

    [Fact]
    public async Task SetThenGetRoundTripsAcrossStores()
    {
        JsonFileSettingsStore store = NewStore();
        var value = new SampleSettings("x", 42);

        await store.SetAsync(Sample, value, TestContext.Current.CancellationToken);

        Assert.Equal(value, store.Get(Sample));
        Assert.Equal(value, NewStore().Get(Sample));
    }

    [Fact]
    public async Task NetPrintsSectionRoundTripsAndFileIsCanonical()
    {
        JsonFileSettingsStore store = NewStore();
        await store.SetAsync(Sample, new SampleSettings("b", 2), TestContext.Current.CancellationToken);
        await store.SetAsync(Other, new SampleSettings("a", 1), TestContext.Current.CancellationToken);
        await store.SetAsync(
            NetPrintsSettings.Descriptor,
            new NetPrintsSettings { ExtensionPaths = ["/ext"], TrustedProjects = ["/p/a.csproj"] },
            TestContext.Current.CancellationToken);

        NetPrintsSettings read = NewStore().Get(NetPrintsSettings.Descriptor);
        Assert.Equal(["/ext"], read.ExtensionPaths);
        Assert.Equal(["/p/a.csproj"], read.TrustedProjects);

        const string expected = """
            {
              "schemaVersion": 1,
              "netprints": {
                "extensionPaths": [
                  "/ext"
                ],
                "trustedProjects": [
                  "/p/a.csproj"
                ],
                "enableAnimations": true
              },
              "extensions": {
                "test.other": {
                  "name": "a",
                  "count": 1
                },
                "test.sample": {
                  "name": "b",
                  "count": 2
                }
              }
            }

            """;
        byte[] bytes = await File.ReadAllBytesAsync(FilePath, TestContext.Current.CancellationToken);
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Equal(expected.Replace("\r\n", "\n"), Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task UnknownSectionsSurviveAWriteByteForByte()
    {
        JsonFileSettingsStore first = NewStore();
        await first.SetAsync(Sample, new SampleSettings("s", 5), TestContext.Current.CancellationToken);
        await first.SetAsync(Other, new SampleSettings("keep", 9), TestContext.Current.CancellationToken);
        string keptSection = ExtractSection(File.ReadAllText(FilePath), "test.other");

        JsonFileSettingsStore second = NewStore();
        await second.SetAsync(Sample, new SampleSettings("changed", 6), TestContext.Current.CancellationToken);

        string after = File.ReadAllText(FilePath);
        Assert.Equal(keptSection, ExtractSection(after, "test.other"));
        Assert.Contains("\"changed\"", after);
    }

    [Fact]
    public async Task UnknownTopLevelAndUnregisteredExtensionSectionsArePreserved()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath) ?? directory);
        await File.WriteAllTextAsync(
            FilePath,
            """{ "zzz": [1, 2], "extensions": { "com.gone": { "a": { "b": true } } } }""",
            TestContext.Current.CancellationToken);

        await NewStore().SetAsync(Sample, new SampleSettings("s", 1), TestContext.Current.CancellationToken);

        string text = File.ReadAllText(FilePath);
        Assert.Contains("\"com.gone\"", text);
        Assert.Contains("\"b\": true", text);
        Assert.Contains("\"zzz\"", text);
        Assert.Equal("{\n  \"schemaVersion\": 1,", text[..text.IndexOf('\n', text.IndexOf('\n') + 1)]);
    }

    [Fact]
    public async Task InvalidSectionGivesDefaultLogs2006AndStaysOnDisk()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath) ?? directory);
        const string original = """{ "extensions": { "test.sample": { "name": 7, "count": "x" }, "test.other": { "name": "ok", "count": 3 } } }""";
        await File.WriteAllTextAsync(FilePath, original, TestContext.Current.CancellationToken);
        JsonFileSettingsStore store = NewStore();

        Assert.Equal(Sample.Default, store.Get(Sample));
        Assert.Equal(new SampleSettings("ok", 3), store.Get(Other));
        Assert.Equal(2006, Assert.Single(logs.Entries).EventId.Id);
        Assert.Equal(original, await File.ReadAllTextAsync(FilePath, TestContext.Current.CancellationToken));

        await store.SetAsync(Other, new SampleSettings("ok2", 4), TestContext.Current.CancellationToken);
        Assert.Contains("\"count\": \"x\"", File.ReadAllText(FilePath));
    }

    [Fact]
    public async Task UnparseableFileGivesDefaultsAndLogs2006()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath) ?? directory);
        await File.WriteAllTextAsync(FilePath, "{ not json", TestContext.Current.CancellationToken);
        JsonFileSettingsStore store = NewStore();

        Assert.Equal(Sample.Default, store.Get(Sample));
        Assert.Equal(2006, Assert.Single(logs.Entries).EventId.Id);
    }

    // R1-13: an unreadable file (an IOException/UnauthorizedAccessException, not just malformed JSON)
    // falls back to defaults and logs 2006 the same way — Get must not keep rethrowing forever.
    [Fact]
    public async Task UnreadableFileGivesDefaultsAndLogs2006()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("File permission bits are POSIX-only.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(FilePath) ?? directory);
        await File.WriteAllTextAsync(FilePath, """{ "extensions": { "test.sample": { "name": "s", "count": 1 } } }""", TestContext.Current.CancellationToken);
        File.SetUnixFileMode(FilePath, UnixFileMode.None);
        try
        {
            JsonFileSettingsStore store = NewStore();

            Assert.Equal(Sample.Default, store.Get(Sample));
            Assert.Equal(2006, Assert.Single(logs.Entries).EventId.Id);
        }
        finally
        {
            File.SetUnixFileMode(FilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    [Fact]
    public async Task TheFileIsReadOnceAndCached()
    {
        JsonFileSettingsStore store = NewStore();
        await store.SetAsync(Sample, new SampleSettings("s", 1), TestContext.Current.CancellationToken);
        JsonFileSettingsStore reader = NewStore();
        Assert.Equal("s", reader.Get(Sample).Name);

        await File.WriteAllTextAsync(FilePath, "{}", TestContext.Current.CancellationToken);

        Assert.Equal("s", reader.Get(Sample).Name);
    }

    [Fact]
    public async Task ConcurrentSetsAreSerializedAndNoTempFilesRemain()
    {
        JsonFileSettingsStore store = NewStore();

        await Task.WhenAll(Enumerable.Range(0, 20).Select(i =>
            Task.Run(() => store.SetAsync(Sample, new SampleSettings("n", i), TestContext.Current.CancellationToken).AsTask(), TestContext.Current.CancellationToken)));

        Assert.Equal("n", NewStore().Get(Sample).Name);
        Assert.Equal([FilePath], Directory.GetFiles(Path.GetDirectoryName(FilePath) ?? directory));
    }

    [Fact]
    public void DefaultFilePathEndsWithNetPrintsSettings() =>
        Assert.EndsWith(Path.Combine("NetPrints", "settings.json"), JsonFileSettingsStore.DefaultFilePath());

    private static string ExtractSection(string text, string id)
    {
        int start = text.IndexOf($"\"{id}\": {{", StringComparison.Ordinal);
        Assert.True(start >= 0);
        int end = text.IndexOf("\n    }", start, StringComparison.Ordinal);
        return text[start..end];
    }
}
