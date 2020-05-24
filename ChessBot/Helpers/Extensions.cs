using System;
using System.Collections.Generic;
using System.Text;

namespace ChessBot.Helpers
{
    internal static class Extensions
    {
        public static K MaxBy<K, V>(this IEnumerable<K> source, Func<K, V> selector, IComparer<V> comparer = null)
        {
            comparer = comparer ?? Comparer<V>.Default;

            bool first = true;
            K maxKey = default;
            V maxValue = default;

            foreach (K key in source)
            {
                V value = selector(key);
                if (first || comparer.Compare(value, maxValue) > 0)
                {
                    (maxKey, maxValue) = (key, value);
                }
                first = false;
            }

            return maxKey;
        }

        public static K MinBy<K, V>(this IEnumerable<K> source, Func<K, V> selector, IComparer<V> comparer = null)
        {
            comparer = comparer ?? Comparer<V>.Default;

            bool first = true;
            K minKey = default;
            V minValue = default;

            foreach (K key in source)
            {
                V value = selector(key);
                if (first || comparer.Compare(value, minValue) < 0)
                {
                    (minKey, minValue) = (key, value);
                }
                first = false;
            }

            return minKey;
        }
    }
}
