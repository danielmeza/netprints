using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace NetPrints.Reflection
{
    /// <summary>
    /// Caches the result of a pure, argument-keyed function for the lifetime of the memoized delegate.
    /// Used by <see cref="MemoizedReflectionProvider"/> to cache reflection queries.
    /// </summary>
    public static class Memoization
    {
        // https://stackoverflow.com/a/2852595/4332314

        // Memoized functions are thread-safe: the editor queries the provider from the UI thread
        // and from background tasks (suggestion lists, reflection reloads).

        /// <summary>
        /// Wraps a parameterless function so it runs at most once: the first call computes and caches
        /// the result (thread-safely; concurrent first calls block on the same computation), every
        /// later call returns the cached result.
        /// </summary>
        /// <typeparam name="R">Result type.</typeparam>
        /// <param name="f">Function to memoize.</param>
        /// <returns>A memoized wrapper around <paramref name="f"/>.</returns>
        public static Func<R> Memoize<R>(this Func<R> f)
        {
            var lazy = new Lazy<R>(f, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
            return () => lazy.Value;
        }

        /// <summary>
        /// Wraps a single-argument function so each distinct argument is computed at most once
        /// (cached in a <see cref="ConcurrentDictionary{TKey, TValue}"/>, thread-safe).
        /// </summary>
        /// <typeparam name="A">Argument type.</typeparam>
        /// <typeparam name="R">Result type.</typeparam>
        /// <param name="f">Function to memoize.</param>
        /// <returns>A memoized wrapper around <paramref name="f"/>.</returns>
        public static Func<A, R> Memoize<A, R>(this Func<A, R> f)
        {
            var d = new ConcurrentDictionary<A, R>();

            return a => d.GetOrAdd(a, f);
        }

        /// <summary>
        /// Wraps a two-argument function so each distinct argument pair is computed at most once, via
        /// <see cref="Tuplify"/> and the single-argument <see cref="Memoize{A, R}"/>.
        /// </summary>
        /// <typeparam name="A">First argument type.</typeparam>
        /// <typeparam name="B">Second argument type.</typeparam>
        /// <typeparam name="R">Result type.</typeparam>
        /// <param name="f">Function to memoize.</param>
        /// <returns>A memoized wrapper around <paramref name="f"/>.</returns>
        public static Func<A, B, R> Memoize<A, B, R>(this Func<A, B, R> f)
        {
            return f.Tuplify().Memoize().Detuplify();
        }

        /// <summary>
        /// Adapts a two-argument function to a single-argument function taking a
        /// <see cref="Tuple{A, B}"/>.
        /// </summary>
        /// <typeparam name="A">First argument type.</typeparam>
        /// <typeparam name="B">Second argument type.</typeparam>
        /// <typeparam name="R">Result type.</typeparam>
        /// <param name="f">Function to adapt.</param>
        /// <returns>A function taking a single tuple argument.</returns>
        public static Func<Tuple<A, B>, R> Tuplify<A, B, R>(this Func<A, B, R> f)
        {
            return t => f(t.Item1, t.Item2);
        }

        /// <summary>
        /// The inverse of <see cref="Tuplify"/>: adapts a single-argument tuple function back to a
        /// two-argument function.
        /// </summary>
        /// <typeparam name="A">First argument type.</typeparam>
        /// <typeparam name="B">Second argument type.</typeparam>
        /// <typeparam name="R">Result type.</typeparam>
        /// <param name="f">Function to adapt.</param>
        /// <returns>A function taking two separate arguments.</returns>
        public static Func<A, B, R> Detuplify<A, B, R>(this Func<Tuple<A, B>, R> f)
        {
            return (a, b) => f(Tuple.Create(a, b));
        }
    }
}
