using ChessBot.Types;

namespace ChessBot.Search
{
    /// <summary>
    /// Uses iterative deepening with MTD-f search to find the best move.
    /// </summary>
    public class Ids : IMovePicker<Ids.Info>
    {
        public class Info
        {
            internal Info(int utility) => Utility = utility;
            
            public int Utility { get; }
        }

        private readonly Mtdf _inner;

        public Ids(int depth)
        {
            Depth = depth;
            _inner = new Mtdf(1);
        }

        public int Depth { get; set; }

        public Move PickMove(State root, out Info info)
        {
            Move bestMove = default;
            int utility = 0;

            for (int d = 1; d <= Depth; d++)
            {
                _inner.Depth = d;
                _inner.FirstGuess = utility;

                bestMove = _inner.PickMove(root, out var mtdfInfo);
                utility = mtdfInfo.Utility;
            }

            info = new Info(utility: utility);
            return bestMove;
        }
    }
}
