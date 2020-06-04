using ChessBot.Types;
using System.ComponentModel;

namespace ChessBot
{
    internal class PlayerProperty<T>
    {
        // Avoids the caller having to specify the type parameter
        public static implicit operator PlayerProperty<T>((T, T) tuple)
        {
            return new PlayerProperty<T>(tuple.Item1, tuple.Item2);
        }

        public PlayerProperty(T white, T black)
        {
            White = white;
            Black = black;
        }

        public T White { get; }
        public T Black { get; }

        public T Get(Side side) => side.IsWhite() ? White : Black;
        public PlayerProperty<T> Set(Side side, T value) => side.IsWhite() ? (value, Black) : (White, value);
    }
}
