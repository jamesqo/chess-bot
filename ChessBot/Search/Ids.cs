using ChessBot.Helpers;
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
            Log.Debug("Entering {0}.{1}", arg0: nameof(Ids), arg1: nameof(PickMove));

            Move bestMove = default;
            int utility = 0;

            for (int d = 1; d <= Depth; d++)
            {
                Log.Debug("Running mtdf with depth={0}, f={1}", Depth, utility);
                _inner.Depth = d;
                _inner.FirstGuess = utility;

                Log.IndentLevel++;
                bestMove = _inner.PickMove(root, out var mtdfInfo);
                Log.IndentLevel--;
                utility = mtdfInfo.Utility;
            }

            info = new Info(utility: utility);
            Log.Debug("Exiting {0}.{1}", arg0: nameof(Ids), arg1: nameof(PickMove));
            return bestMove;
        }
    }
}
