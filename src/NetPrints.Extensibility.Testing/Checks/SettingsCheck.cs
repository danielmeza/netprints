using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using NetPrints.Extensibility.Settings;

namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>NPK009: each settings section of the extension serializes its default, validates it, writes it back unchanged and exports a schema.</summary>
internal sealed class SettingsCheck : ConformanceCheck
{
    private const string Rule = "a settings section serializes, validates and exports a schema";

    public override string Code => NpkDiagnosticCodes.Settings;

    public override string Name => "Settings";

    public override async Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        if (context.SubjectId is not { } id)
        {
            return CheckOutcome.Skip("no extension besides the built-in one was loaded");
        }

        ExtensionSettingsDescriptor[] sections = [.. context.Harness.Registry.Settings.Where(section => section.ExtensionId == id)];
        if (sections.Length == 0)
        {
            return CheckOutcome.Passed("no settings section");
        }

        var findings = new List<ConformanceFinding>();
        foreach (ExtensionSettingsDescriptor section in sections)
        {
            string item = $"{section.ExtensionId} ({section.ValueType.Name})";
            try
            {
                var store = new MemorySettingsStore();
                JsonObject node = section.ReadSection(store);
                foreach (string problem in section.ValidateSection(node))
                {
                    findings.Add(Finding(Rule, $"{item}: the default does not validate: {problem}"));
                }

                await section.WriteSectionAsync(store, node, cancellationToken).ConfigureAwait(false);
                JsonObject again = section.ReadSection(store);
                if (!JsonNode.DeepEquals(node, again))
                {
                    findings.Add(Finding(Rule, $"{item}: the section differs after it is written and read again"));
                }

                if (JsonSchemaExporter.GetJsonSchemaAsNode(section.SectionTypeInfo) is not JsonObject)
                {
                    findings.Add(Finding(Rule, $"{item}: the exported schema is not an object"));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                findings.Add(Finding(Rule, $"{item}: {ex.GetType().Name}: {ex.Message}"));
            }
        }

        return CheckOutcome.FromFindings(findings);
    }

    private sealed class MemorySettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, object?> values = new(StringComparer.Ordinal);

        public T Get<T>(ExtensionSettingsDescriptor<T> descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            return values.TryGetValue(descriptor.ExtensionId, out object? value) && value is T typed ? typed : descriptor.Default;
        }

        public ValueTask SetAsync<T>(ExtensionSettingsDescriptor<T> descriptor, T value, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            values[descriptor.ExtensionId] = value;
            return ValueTask.CompletedTask;
        }
    }
}
