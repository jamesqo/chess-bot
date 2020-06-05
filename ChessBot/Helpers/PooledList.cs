using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace ChessBot.Helpers
{
    // todo: does this have to be public? maybe just delete altogether?
    internal struct PooledList<T> : IEnumerable<T>, IDisposable
    {
        public static PooledList<T> Get(int maxLength) => new PooledList<T>(maxLength);

        private T[] _array;
        private int _count;
        private int _capacity;

        private PooledList(int capacity)
        {
            Debug.Assert(capacity > 0);

            _array = ArrayPool<T>.Shared.Rent(capacity);
            _count = 0;
            _capacity = capacity;
        }

        public int Count => _count;

        public void Add(T item)
        {
            Debug.Assert(_array != null, "Don't use the default constructor!");

            if (_count == _array.Length)
            {
                Resize();
            }
            _array[_count++] = item;
        }

        public void Dispose()
        {
            ArrayPool<T>.Shared.Return(_array);
            // note: since this is a mutable struct, we're not really enforcing anything here.
            // the caller could copy this if they wanted to and continue using the returned array.
            _array = null;
            _count = 0;
            _capacity = 0;
        }

        public Enumerator GetEnumerator() => new Enumerator(_array, _count);

        private void Resize()
        {
            int newCapacity = _capacity;
            while (newCapacity <= _array.Length) newCapacity *= 2;
            Debug.WriteLine($"[{nameof(PooledList<T>)}] Capacity of {_capacity} was hit, increasing to {newCapacity}");

            ArrayPool<T>.Shared.Return(_array);
            _array = ArrayPool<T>.Shared.Rent(newCapacity);
            _capacity = newCapacity;
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<T>
        {
            private readonly T[] _array;
            private readonly int _count;
            private int _index;

            internal Enumerator(T[] array, int count)
            {
                _array = array;
                _count = count;
                _index = -1;
            }

            public T Current => _array[_index];

            public void Dispose() { }

            public bool MoveNext() => ++_index < _count;

            object IEnumerator.Current => Current;

            void IEnumerator.Reset() => throw new NotSupportedException();
        }
    }
}
