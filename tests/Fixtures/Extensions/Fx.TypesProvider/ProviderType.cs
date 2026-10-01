using NetPrints.Extensibility;

namespace Fx.TypesProvider;

/// <summary>The type a consumer extension shares through <c>dependsOn</c>.</summary>
public sealed class ProviderType
{
    /// <summary>Describes the provider.</summary>
    /// <returns>The marker.</returns>
    public static string Describe() => "fx.types-provider";
}

/// <summary>Contributes nothing: its assembly is what the consumer needs.</summary>
public sealed class TypesProviderExtension : INetPrintsExtension
{
    /// <summary>The extension id, also the manifest id.</summary>
    public const string Id = "fx.types-provider";

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
    }
}
