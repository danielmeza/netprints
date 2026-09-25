using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace NetPrints.Reflection
{
    public static class Memoization
    {
        // https://stackoverflow.com/a/2852595/4332314

        // Memoized functions are thread-safe: the editor queries the provider from the UI thread
        // and from background tasks (suggestion lists, reflection reloads).

        public static Func<R> Memoize<R>(this Func<R> f)
        {
            var lazy = new Lazy<R>(f, System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
            return () => lazy.Value;
        }

        public static Func<A, R> Memoize<A, R>(this Func<A, R> f)
        {
            var d = new ConcurrentDictionary<A, R>();

            return a => d.GetOrAdd(a, f);
        }

        public static Func<A, B, R> Memoize<A, B, R>(this Func<A, B, R> f)
        {
            return f.Tuplify().Memoize().Detuplify();
        }

        public static Func<Tuple<A, B>, R> Tuplify<A, B, R>(this Func<A, B, R> f)
        {
            return t => f(t.Item1, t.Item2);
        }

        public static Func<A, B, R> Detuplify<A, B, R>(this Func<Tuple<A, B>, R> f)
        {
            return (a, b) => f(Tuple.Create(a, b));
        }
    }
}
