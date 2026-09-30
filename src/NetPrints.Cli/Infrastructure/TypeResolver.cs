using System;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Infrastructure;

/// <summary>Adapts an <see cref="IServiceProvider"/> to Spectre's <see cref="ITypeResolver"/>.</summary>
internal sealed class TypeResolver(IServiceProvider provider) : ITypeResolver
{
    /// <inheritdoc/>
    public object? Resolve(Type? type) => type is null ? null : provider.GetService(type);
}
