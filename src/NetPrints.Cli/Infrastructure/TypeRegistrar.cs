using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Infrastructure;

/// <summary>Adapts an <see cref="IServiceCollection"/> to Spectre's <see cref="ITypeRegistrar"/>; owns the providers it builds.</summary>
internal sealed class TypeRegistrar(IServiceCollection services) : ITypeRegistrar, IDisposable
{
    private readonly List<ServiceProvider> _providers = [];

    /// <inheritdoc/>
    public ITypeResolver Build()
    {
        ServiceProvider provider = services.BuildServiceProvider();
        _providers.Add(provider);
        return new TypeResolver(provider);
    }

    /// <inheritdoc/>
    public void Register(Type service, Type implementation) => services.AddSingleton(service, implementation);

    /// <inheritdoc/>
    public void RegisterInstance(Type service, object implementation) => services.AddSingleton(service, implementation);

    /// <inheritdoc/>
    public void RegisterLazy(Type service, Func<object> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        services.AddSingleton(service, _ => factory());
    }

    /// <summary>Disposes every provider <see cref="Build"/> created.</summary>
    public void Dispose()
    {
        foreach (ServiceProvider provider in _providers)
        {
            provider.Dispose();
        }
    }
}
