using ChessBot.Helpers;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml;

namespace ChessBot
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Welcome! This is a simple chess bot written in C#.");

            PlayerColor userColor;
            while (true)
            {
                Console.Write("Pick your color [b for black, w for white]: ");
                string input = Console.ReadLine().Trim().ToLower();
                if (input == "b" || input == "w")
                {
                    userColor = (input == "b" ? PlayerColor.Black : PlayerColor.White);
                    break;
                }
            }

            AIStrategy aiStrategy;
            while (true)
            {
                Console.Write("Pick ai strategy [random, minimax]: ");
                string input = Console.ReadLine().Trim().ToLower();
                if (input == "random")
                {
                    aiStrategy = AIStrategy.Random;
                    break;
                }
                else if (input == "minimax")
                {
                    aiStrategy = AIStrategy.Minimax;
                    break;
                }
            }

            var whitePlayer = (userColor == PlayerColor.White) ? new HumanPlayer() : GetAIPlayer(aiStrategy);
            var blackPlayer = (userColor != PlayerColor.White) ? new HumanPlayer() : GetAIPlayer(aiStrategy);

            Console.WriteLine($"Playing as: {userColor}");

            var game = new ChessGame();
            Console.WriteLine();
            Console.WriteLine(GetDisplayString(game.State));
            Console.WriteLine();

            while (true)
            {
                Console.WriteLine($"<< Turn {game.Turn} >>");
                Console.WriteLine();

                Console.WriteLine("It's White's turn.");
                var nextMove = whitePlayer.GetNextMove(game.State);
                Console.WriteLine($"White chose: {nextMove}"); // todo
                game.ApplyMove(nextMove);
                Console.WriteLine();
                Console.WriteLine(GetDisplayString(game.State));
                Console.WriteLine();
                CheckForEnd(game);

                Console.WriteLine("It's Black's turn.");
                nextMove = blackPlayer.GetNextMove(game.State);
                Console.WriteLine($"Black chose: {nextMove}");
                game.ApplyMove(nextMove);
                Console.WriteLine();
                Console.WriteLine(GetDisplayString(game.State));
                Console.WriteLine();
                CheckForEnd(game);
            }
        }

        static IPlayer GetAIPlayer(AIStrategy strategy) => strategy switch
        {
            AIStrategy.Random => new RandomAIPlayer(),
            AIStrategy.Minimax => new MinimaxAIPlayer(depth: 5),
        };

        // todo: HasEnded
        static void CheckForEnd(ChessGame game)
        {
            var state = game.State;
            if (state.IsCheckmate)
            {
                Console.WriteLine($"{state.OpposingColor} wins!");
                Environment.Exit(0);
            }

            if (state.IsStalemate)
            {
                Console.WriteLine("It's a draw.");
                Environment.Exit(0);
            }

            // todo: Check for 3-fold repetition
        }

        static string GetDisplayString(ChessState state)
        {
            var sb = new StringBuilder();
            // todo: have whichever side the human is on at the bottom
            for (int r = 7; r >= 0; r--)
            {
                sb.Append(' ');
                sb.AppendJoin("__", Enumerable.Repeat('.', 9));
                sb.AppendLine();

                sb.Append(r + 1);
                for (int c = 0; c < 8; c++)
                {
                    sb.Append('|');
                    sb.Append(GetDisplayString(state[c, r]));
                }
                sb.Append('|');
                sb.AppendLine();
            }
            sb.Append(' ');
            sb.AppendJoin("__", Enumerable.Repeat('.', 9));
            sb.AppendLine();

            sb.Append("   ");
            sb.AppendJoin("  ", Enumerable.Range(0, 8).Select(i => (char)(i + 'a')));
            return sb.ToString();
        }

        static string GetDisplayString(ChessTile tile)
        {
            if (!tile.HasPiece) return "  ";
            var piece = tile.Piece;

            char kindChar = piece.Kind switch
            {
                PieceKind.Pawn => 'P',
                PieceKind.Knight => 'N',
                PieceKind.Bishop => 'B',
                PieceKind.Rook => 'R',
                PieceKind.Queen => 'Q',
                PieceKind.King => 'K',
                _ => throw new ArgumentOutOfRangeException()
            };
            char colorChar = (piece.Color == PlayerColor.White) ? 'w' : 'b';
            return new string(new[] { kindChar, colorChar });
        }
    }

    enum AIStrategy
    {
        Random,
        Minimax,
        // AlphaBeta,
    }

    interface IPlayer
    {
        ChessMove GetNextMove(ChessState state);
    }

    class HumanPlayer : IPlayer
    {
        public ChessMove GetNextMove(ChessState state)
        {
            while (true)
            {
                Console.Write("> ");
                string input = Console.ReadLine();
                switch (input)
                {
                    case "help":
                        Console.WriteLine("List of commands:");
                        Console.WriteLine();
                        Console.WriteLine("help - displays this message");
                        Console.WriteLine("list - list of all valid moves");
                        break;
                    case "list":
                        Console.WriteLine("List of valid moves:");
                        Console.WriteLine();
                        Console.WriteLine(string.Join(Environment.NewLine, state.GetMoves()));
                        break;
                    default:
                        try
                        {
                            var move = ChessMove.Parse(input, state);
                            _ = state.ApplyMove(move); // make sure it's valid
                            return move;
                        }
                        catch (Exception e) when (e is AlgebraicNotationParseException || e is InvalidChessMoveException)
                        {
                            Debug.WriteLine(e.ToString());
                            Console.WriteLine("Sorry, try again.");
                        }
                        break;
                }
            }
        }
    }

    class RandomAIPlayer : IPlayer
    {
        private readonly Random _rand = new Random();

        public ChessMove GetNextMove(ChessState state)
        {
            var moves = state.GetMoves().ToArray();
            Debug.WriteLine(string.Join(Environment.NewLine, (object[])moves));
            return moves[_rand.Next(moves.Length)];
        }
    }

    class MinimaxAIPlayer : IPlayer
    {
        // todo: refactor so this doesn't break if we switch the order of enum values
        private static readonly int[] PieceValues =
        {
            100,   // pawn
            320,   // knight
            330,   // bishop
            500,   // rook
            900,   // queen
            20000  // king
        };

        // todo: refactor so this doesn't break if we switch the order of enum values
        private static readonly int[][] PieceSquareValues =
        {
            // pawn
            new int[]
            {
                 0,  0,  0,  0,  0,  0,  0,  0,
                50, 50, 50, 50, 50, 50, 50, 50,
                10, 10, 20, 30, 30, 20, 10, 10,
                 5,  5, 10, 25, 25, 10,  5,  5,
                 0,  0,  0, 20, 20,  0,  0,  0,
                 5, -5,-10,  0,  0,-10, -5,  5,
                 5, 10, 10,-20,-20, 10, 10,  5,
                 0,  0,  0,  0,  0,  0,  0,  0
            },
            // knight
            new int[]
            {
                -50,-40,-30,-30,-30,-30,-40,-50,
                -40,-20,  0,  0,  0,  0,-20,-40,
                -30,  0, 10, 15, 15, 10,  0,-30,
                -30,  5, 15, 20, 20, 15,  5,-30,
                -30,  0, 15, 20, 20, 15,  0,-30,
                -30,  5, 10, 15, 15, 10,  5,-30,
                -40,-20,  0,  5,  5,  0,-20,-40,
                -50,-40,-30,-30,-30,-30,-40,-50,
            },
            // bishop
            new int[]
            {
                -20,-10,-10,-10,-10,-10,-10,-20,
                -10,  0,  0,  0,  0,  0,  0,-10,
                -10,  0,  5, 10, 10,  5,  0,-10,
                -10,  5,  5, 10, 10,  5,  5,-10,
                -10,  0, 10, 10, 10, 10,  0,-10,
                -10, 10, 10, 10, 10, 10, 10,-10,
                -10,  5,  0,  0,  0,  0,  5,-10,
                -20,-10,-10,-10,-10,-10,-10,-20,
            },
            // rook
            new int[]
            {
                 0,  0,  0,  0,  0,  0,  0,  0,
                 5, 10, 10, 10, 10, 10, 10,  5,
                -5,  0,  0,  0,  0,  0,  0, -5,
                -5,  0,  0,  0,  0,  0,  0, -5,
                -5,  0,  0,  0,  0,  0,  0, -5,
                -5,  0,  0,  0,  0,  0,  0, -5,
                -5,  0,  0,  0,  0,  0,  0, -5,
                 0,  0,  0,  5,  5,  0,  0,  0
            },
            // queen
            new int[]
            {
                -20,-10,-10, -5, -5,-10,-10,-20,
                -10,  0,  0,  0,  0,  0,  0,-10,
                -10,  0,  5,  5,  5,  5,  0,-10,
                 -5,  0,  5,  5,  5,  5,  0, -5,
                  0,  0,  5,  5,  5,  5,  0, -5,
                -10,  5,  5,  5,  5,  5,  0,-10,
                -10,  0,  5,  0,  0,  0,  0,-10,
                -20,-10,-10, -5, -5,-10,-10,-20
            }
        };

        private static readonly int[] KingMiddlegameValues =
        {
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -20,-30,-30,-40,-40,-30,-30,-20,
            -10,-20,-20,-20,-20,-20,-20,-10,
             20, 20,  0,  0,  0,  0, 20, 20,
             20, 30, 10,  0,  0, 10, 30, 20
        };

        private static readonly int[] KingEndgameValues =
        {
            -50,-40,-30,-20,-20,-30,-40,-50,
            -30,-20,-10,  0,  0,-10,-20,-30,
            -30,-10, 20, 30, 30, 20,-10,-30,
            -30,-10, 30, 40, 40, 30,-10,-30,
            -30,-10, 30, 40, 40, 30,-10,-30,
            -30,-10, 20, 30, 30, 20,-10,-30,
            -30,-30,  0,  0,  0,  0,-30,-30,
            -50,-30,-30,-30,-30,-30,-30,-50
        };

        private readonly int _depth;

        public MinimaxAIPlayer(int depth) => _depth = depth;

        public ChessMove GetNextMove(ChessState state)
        {
            // throw something if it's terminal

            ChessMove bestMove = null;
            int bestValue = state.WhiteToMove ? -int.MaxValue : int.MaxValue;
            foreach (var (move, succ) in state.GetMovesAndSuccessors())
            {
                int value = Minimax(succ, _depth - 1);
                bool better = state.WhiteToMove ? value > bestValue : value < bestValue;
                if (better)
                {
                    bestMove = move;
                    bestValue = value;
                }
            }

            return bestMove;
        }

        private static int Minimax(ChessState state, int d)
        {
            if (d == 0 || state.IsTerminal)
            {
                return Heuristic(state);
            }

            int bestValue = state.WhiteToMove ? -int.MaxValue : int.MaxValue;

            foreach (var succ in state.GetSuccessors())
            {
                int value = Minimax(succ, d - 1);
                bestValue = state.WhiteToMove
                    ? Math.Max(bestValue, value)
                    : Math.Min(bestValue, value);
            }

            return bestValue;
        }

        // Heuristic is always positive / calculated from white's viewpoint
        private static int Heuristic(ChessState state)
        {
            if (state.IsTerminal)
            {
                // todo: give preference to checkmates that occur in fewer moves
                if (state.IsStalemate) return 0;
                return state.WhiteToMove ? int.MaxValue : -int.MaxValue;
            }

            return HeuristicForPlayer(state, PlayerColor.White) - HeuristicForPlayer(state, PlayerColor.Black);
        }

        private static int HeuristicForPlayer(ChessState state, PlayerColor color)
        {
            // temporarily disabling this for perf reasons
            /*
            bool CheckForEndgame(PlayerInfo player)
            {
                var remainingPieces = player.GetOccupiedTiles()
                    .Select(t => t.Piece)
                    .Where(p => p.Kind != PieceKind.Pawn && p.Kind != PieceKind.King);
                if (!remainingPieces.Any(p => p.Kind == PieceKind.Queen)) return true;

                remainingPieces = remainingPieces.Where(p => p.Kind != PieceKind.Queen);
                int count = remainingPieces.Count();
                if (count == 0) return true;
                if (count > 1) return false;
                var piece = remainingPieces.Single();
                return (piece.Kind == PieceKind.Bishop || piece.Kind == PieceKind.Knight);
            }
            bool isEndgame = CheckForEndgame(state.White) && CheckForEndgame(state.Black);
            */
            bool isEndgame = false;

            int result = 0;
            foreach (var tile in state.GetPlayer(color).GetOccupiedTiles())
            {
                result += PieceValues[(int)tile.Piece.Kind];
                int locationInt = 8 * (color == PlayerColor.White ? (7 - tile.Location.Row) : tile.Location.Row) + tile.Location.Column;
                if (tile.Piece.Kind != PieceKind.King)
                {
                    result += PieceSquareValues[(int)tile.Piece.Kind][locationInt];
                }
                else
                {
                    var kingValues = isEndgame ? KingEndgameValues : KingMiddlegameValues;
                    result += kingValues[locationInt];
                }
            }
            return result;
        }
    }
}
