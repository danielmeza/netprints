using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.State;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Editor.Tests.Settings;

/// <summary>DD-1: a <c>keyboard</c> binding that omits or nulls a member loads with that member's default, and an unusable entry uses the section's defaults.</summary>
public sealed class KeyboardSettingsLoadTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "netprints-keyboard-settings", Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(folder, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private JsonFileSettingsStore NewStore() => new(FilePath, NullLogger<JsonFileSettingsStore>.Instance);

    private void Write(string text)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(FilePath, text);
    }

    private sealed class NoopHandler : ICommandHandler
    {
        public bool CanExecute(ICommandContext context) => true;

        public Task ExecuteAsync(ICommandContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public void ABindingThatOmitsScopeAddOrRemoveLoadsWithTheirDefaults()
    {
        Write("""{"keyboard":{"bindings":[{"command":"probe.cmd","add":["Ctrl+K"]},{"command":"probe.other","remove":["Ctrl+L"]}]}}""");
        JsonFileSettingsStore store = NewStore();

        KeyboardSettings settings = store.Get(KeyboardSettings.Descriptor);

        Assert.Equal(2, settings.Bindings.Count);
        KeyBindingOverride first = settings.Bindings[0];
        Assert.Equal("Global", first.Scope);
        Assert.Equal(["Ctrl+K"], first.Add);
        Assert.Empty(first.Remove);
        KeyBindingOverride second = settings.Bindings[1];
        Assert.Empty(second.Add);
        Assert.Equal(["Ctrl+L"], second.Remove);
        Assert.Empty(store.Issues);
    }

    [Fact]
    public void TheResolverAcceptsABindingThatOmitsRemove()
    {
        Write("""{"keyboard":{"bindings":[{"command":"probe.cmd","add":["Ctrl+K"]},{"command":"probe.other","remove":["Ctrl+L"]}]}}""");
        JsonFileSettingsStore store = NewStore();

        EffectiveKeyBindings result = KeyBindingResolver.Resolve(
            [new CommandDescriptor("probe.cmd", "probe.cmd", new NoopHandler())],
            store.Get(KeyboardSettings.Descriptor));

        Assert.Equal(["Ctrl+K"], result.For("probe.cmd").Effective.Select(gesture => gesture.ToString()));
    }

    [Fact]
    public void ExplicitNullListsAndScopeLoadAsTheirDefaults()
    {
        Write("""{"keyboard":{"bindings":[{"command":"probe.cmd","scope":null,"add":null,"remove":null}]}}""");
        JsonFileSettingsStore store = NewStore();

        KeyBindingOverride binding = Assert.Single(store.Get(KeyboardSettings.Descriptor).Bindings);

        Assert.Equal("Global", binding.Scope);
        Assert.Empty(binding.Add);
        Assert.Empty(binding.Remove);
        Assert.Empty(store.Issues);
    }

    [Theory]
    [InlineData("""{"keyboard":{"bindings":[null]}}""")]
    [InlineData("""{"keyboard":{"bindings":[{"add":["Ctrl+K"]}]}}""")]
    public void ANullEntryOrAnEntryWithoutCommandMakesTheSectionUseItsDefaults(string json)
    {
        Write(json);
        JsonFileSettingsStore store = NewStore();

        Assert.Empty(store.Get(KeyboardSettings.Descriptor).Bindings);
        SettingsSectionIssue issue = Assert.Single(store.Issues);
        Assert.Equal("keyboard", issue.SectionId);
        Assert.Null(issue.Code);
    }
}
