using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace NetPrints.Extensibility.Loading;

/// <summary>Answers an extension node document type from the resolvers of the extension that owns it, so another extension's resolver cannot shadow it.</summary>
internal sealed class OwnerRoutingJsonTypeInfoResolver(IReadOnlyDictionary<Type, IReadOnlyList<IJsonTypeInfoResolver>> ownerResolvers) : IJsonTypeInfoResolver
{
    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        if (!ownerResolvers.TryGetValue(type, out IReadOnlyList<IJsonTypeInfoResolver>? candidates))
        {
            return null;
        }

        foreach (IJsonTypeInfoResolver resolver in candidates)
        {
            JsonTypeInfo? info = resolver.GetTypeInfo(type, options);
            if (info is not null)
            {
                return info;
            }
        }

        return null;
    }
}
