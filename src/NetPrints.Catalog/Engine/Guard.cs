using System;

namespace NetPrints.Catalog;

/// <summary>Argument checks that compile on every target (the catalog sources are also built for netstandard2.0).</summary>
internal static class Guard
{
    /// <summary>Returns <paramref name="value"/>, or throws <see cref="ArgumentNullException"/> when it is null.</summary>
    public static T NotNull<T>(T? value, string paramName)
        where T : class =>
        value ?? throw new ArgumentNullException(paramName);
}
