using Microsoft.Extensions.DependencyInjection;
using NetPrints.Extensibility;

namespace Fx.Fail.Services;

/// <summary>A scoped service that keeps every instance alive in a static list.</summary>
public sealed class LeakyService
{
    private static readonly List<LeakyService> Instances = [];

    /// <summary>Creates the service and remembers it for good.</summary>
    public LeakyService()
    {
        lock (Instances)
        {
            Instances.Add(this);
        }
    }
}

/// <summary>An extension whose scoped service is captured by a static field.</summary>
public sealed class FailServicesExtension : INetPrintsExtension
{
    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddScoped<LeakyService>();
    }
}
