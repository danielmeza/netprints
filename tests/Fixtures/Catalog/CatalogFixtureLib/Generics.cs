using System;
using System.Collections.Generic;

namespace Fixture.Generics;

/// <summary>A generic box with constraints and a nested type.</summary>
/// <typeparam name="T">The boxed type.</typeparam>
public class Box<T>
    where T : notnull
{
    /// <summary>Gets or sets the value.</summary>
    public T? Value { get; set; }

    /// <summary>Maps the value with a generic method.</summary>
    /// <typeparam name="TResult">The mapped type.</typeparam>
    /// <param name="selector">The mapping.</param>
    /// <returns>The mapped value.</returns>
    public TResult Map<TResult>(Func<T, TResult> selector)
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(selector);
        return selector(Value ?? throw new InvalidOperationException("Empty box."));
    }

    /// <summary>Lists the value as a sequence.</summary>
    /// <returns>A sequence with the value when set.</returns>
    public IEnumerable<T> AsSequence() => Value is null ? [] : [Value];

    /// <summary>A nested type of a generic type.</summary>
    public sealed class Handle
    {
        /// <summary>Gets or sets the identifier.</summary>
        public int Id { get; set; }
    }
}

/// <summary>A generic interface with a covariant parameter.</summary>
/// <typeparam name="TOut">The produced type.</typeparam>
public interface IProducer<out TOut>
{
    /// <summary>Produces a value.</summary>
    /// <returns>The value.</returns>
    TOut Produce();
}
