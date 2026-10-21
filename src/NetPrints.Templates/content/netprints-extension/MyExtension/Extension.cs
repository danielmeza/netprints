using NetPrints.Extensibility;

namespace MyExtension;

public sealed class Extension : INetPrintsExtension
{
    public const string Id = "my.extension";

    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
    }
}
