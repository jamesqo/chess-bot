using System;
using System.Buffers;
using System.Diagnostics;

namespace ChessBot.Helpers
{
    internal struct PooledList<T> : IDisposable
    {
        public static PooledList<T> Get(int capacity) => new PooledList<T>(capacity);

        private T[] _array;
        private int _count;
        private int _capacity;

        private PooledList(int capacity)
        {
            Debug.Assert(capacity > 0);

            //Log.Debug("Renting buffer, capacity={0}", capacity);
            _array = ArrayPool<T>.Shared.Rent(capacity);
            _count = 0;
            _capacity = capacity;
        }

        public readonly int Count => _count;
        public readonly bool WasDisposed => _array == null;

        public readonly T this[int index]
            => (index >= 0 && index < Count) ? _array[index] : throw new ArgumentOutOfRangeException(nameof(index));

        public void Add(T item)
        {
            Debug.Assert(_array != null, "Don't use the default constructor");

            if (_count == _array.Length)
            {
                Resize();
            }
            _array[_count++] = item;
        }

        public void Dispose()
        {
            Debug.Assert(_array != null);

            //Log.Debug("Returning buffer, capacity={0} len={1}", _capacity, _array.Length);
            ArrayPool<T>.Shared.Return(_array);

            // note: since this is a mutable struct, zeroing out the fields is just a best attempt.
            // the caller could copy this if they wanted to and continue using the returned array.
            _array = null;
            _count = 0;
            _capacity = 0;
        }

        private void Resize()
        {
            int newCapacity = _capacity;
            while (newCapacity <= _array.Length) newCapacity *= 2;

            Log.Debug("Resizing buffer, capacity={0} len={1} newCapacity={2}", _capacity, _array.Length, newCapacity);
            ArrayPool<T>.Shared.Return(_array);
            _array = ArrayPool<T>.Shared.Rent(newCapacity);
            _capacity = newCapacity;
        }
    }
}
