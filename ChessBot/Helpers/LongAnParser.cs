using ChessBot.Types;

namespace ChessBot.Helpers
{
    internal class LongAnParser
    {
        private readonly string _input;
        private int _index = 0;

        public LongAnParser(string input) => _input = input;

        private bool CanRead(int count) => _index + count <= _input.Length;
        private char Peek() => _input[_index];
        private char Pop() => _input[_index++];

        public Move Parse()
        {
            ReadPieceKind();
            var source = ReadLocation();
            ReadSeparator();
            var destination = ReadLocation();
            var promotionKind = ReadPieceKind();
            return new Move(source, destination, promotionKind);
        }

        private PieceKind? ReadPieceKind()
        {
            if (!CanRead(1)) return null;

            switch (Peek())
            {
                case 'N': Pop(); return PieceKind.Knight;
                case 'B': Pop(); return PieceKind.Bishop;
                case 'R': Pop(); return PieceKind.Rook;
                case 'Q': Pop(); return PieceKind.Queen;
                case 'K': Pop(); return PieceKind.King;
                default: return null;
            }
        }

        private Location ReadLocation()
        {
            if (!CanRead(2)) throw something;

            var locationText = new string(new char[] { Pop(), Pop() });
            return Location.Parse(locationText);
        }

        private void ReadSeparator()
        {
            if (!CanRead(1)) throw something;

            char next = Peek();
            if (next == '-' || next == 'x') Pop();
        }
    }
}
