using System;
using System.Collections;
using System.Collections.Generic;

namespace NetPrints.Annotations
{
    /// <summary>An immutable array that compares by element, so a value that holds one is a valid incremental pipeline model.</summary>
    /// <typeparam name="T">The element type; compared with its default equality.</typeparam>
    internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T>
    {
        private readonly T[]? items;

        public EquatableArray(T[]? items)
        {
            this.items = items;
        }

        public int Length => items?.Length ?? 0;

        public IReadOnlyList<T> Items => items ?? Array.Empty<T>();

        public bool Equals(EquatableArray<T> other)
        {
            IReadOnlyList<T> left = Items;
            IReadOnlyList<T> right = other.Items;
            if (left.Count != right.Count)
            {
                return false;
            }

            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            for (int index = 0; index < left.Count; index++)
            {
                if (!comparer.Equals(left[index], right[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

        public override int GetHashCode()
        {
            int hash = Length;
            foreach (T item in Items)
            {
                hash = unchecked((hash * 31) + (item is null ? 0 : item.GetHashCode()));
            }

            return hash;
        }

        public IEnumerator<T> GetEnumerator() => Items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
