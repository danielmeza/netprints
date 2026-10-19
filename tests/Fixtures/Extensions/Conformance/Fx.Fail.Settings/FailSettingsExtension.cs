using System.Text.Json.Serialization;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Settings;

namespace Fx.Fail.Settings;

/// <summary>The settings section: a retry count that must be positive, which its default is not.</summary>
/// <param name="Retries">How often to retry.</param>
public sealed record FailSettings(int Retries);

/// <summary>The source-generated metadata of the section.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(FailSettings))]
public sealed partial class FailSettingsJsonContext : JsonSerializerContext;

/// <summary>An extension whose settings section does not accept its own default.</summary>
public sealed class FailSettingsExtension : INetPrintsExtension
{
    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddSettings(new ExtensionSettingsDescriptor<FailSettings>("fx.fail.settings", FailSettingsJsonContext.Default.FailSettings, new FailSettings(0))
        {
            Validator = value => value.Retries > 0 ? [] : ["Retries must be positive."],
        });
    }
}
