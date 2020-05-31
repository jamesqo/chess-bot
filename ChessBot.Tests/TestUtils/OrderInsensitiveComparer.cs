using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

namespace ChessBot.Tests.TestUtils
{
    internal class OrderInsensitiveComparer<T> : IEqualityComparer<IEnumerable<T>>
    {
        public static IEqualityComparer<IEnumerable<T>> Instance { get; } = new OrderInsensitiveComparer<T>();

        private OrderInsensitiveComparer() { }

        public bool Equals([AllowNull] IEnumerable<T> x, [AllowNull] IEnumerable<T> y)
        {
            return x.Count() == y.Count() && !x.Except(y).Any();
        }

        public int GetHashCode([DisallowNull] IEnumerable<T> obj)
        {
            throw new NotImplementedException();
        }
    }
}
