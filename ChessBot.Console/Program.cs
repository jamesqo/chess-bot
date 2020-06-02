using ChessBot.Exceptions;
using ChessBot.Types;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using static System.Console;

namespace ChessBot.Console
{
    class Program
    {
        static Side GetUserSide()
        {
            while (true)
            {
                Write("Pick your color [b, w (default)]: ");
                string input = ReadLine().Trim().ToLower();
                switch (input)
                {
                    case "b": return Side.Black;
                    case "": case "w": return Side.White;
                }
            }
        }

        static AIStrategy GetAIStrategy()
        {
            while (true)
            {
                Write("Pick ai strategy [random, minimax, alphabeta (default)]: ");
                string input = ReadLine().Trim().ToLower();
                switch (input)
                {
                    case "random": return AIStrategy.Random;
                    case "minimax": return AIStrategy.Minimax;
                    case "": case "alphabeta": return AIStrategy.AlphaBeta;
                }
            }
        }

        static string GetStartFen()
        {
            while (true)
            {
                Write("Enter start FEN [optional]: ");
                string input = ReadLine().Trim();
                if (string.IsNullOrEmpty(input)) return State.StartFen;

                try
                {
                    State.ParseFen(input); // make sure it's valid
                    return input;
                }
                catch (InvalidFenException) { }
            }
        }

        static void Main(string[] args)
        {
            WriteLine("Welcome! This is a simple chess bot written in C#.");
            WriteLine();

            var userSide = GetUserSide();
            var aiStrategy = GetAIStrategy();
            var fen = GetStartFen();
            WriteLine();

            var whitePlayer = userSide.IsWhite() ? new HumanPlayer() : GetAIPlayer(aiStrategy);
            var blackPlayer = userSide.IsWhite() ? GetAIPlayer(aiStrategy) : new HumanPlayer();

            WriteLine($"Playing as: {userSide}");
            WriteLine();

            var state = State.ParseFen(fen);
            int turn = 0; // todo

            while (true)
            {
                if (state.WhiteToMove)
                {
                    turn++;
                    WriteLine($"[Turn {turn}]");
                    WriteLine();
                }

                WriteLine(GetDisplayString(state));
                WriteLine();

                WriteLine($"It's {state.ActiveSide}'s turn.");
                var player = state.WhiteToMove ? whitePlayer : blackPlayer;
                var nextMove = player.GetNextMove(state);
                WriteLine($"{state.ActiveSide} played: {nextMove}");
                state = state.Apply(nextMove);
                WriteLine();
                CheckForEnd(state);
            }
        }

        static IPlayer GetAIPlayer(AIStrategy strategy) => strategy switch
        {
            AIStrategy.Random => new RandomAIPlayer(),
            AIStrategy.Minimax => new MinimaxAIPlayer(depth: 3),
            AIStrategy.AlphaBeta => new AlphaBetaAIPlayer(depth: 5),
            _ => throw new ArgumentOutOfRangeException(nameof(strategy))
        };

        static void CheckForEnd(State state)
        {
            if (state.IsCheckmate)
            {
                WriteLine($"{state.OpposingSide} wins!");
                Environment.Exit(0);
            }

            if (state.IsStalemate)
            {
                WriteLine("It's a draw.");
                Environment.Exit(0);
            }

            // todo: Check for 3-fold repetition
        }

        static string GetDisplayString(State state)
        {
            var sb = new StringBuilder();
            // todo: have whichever side the human is on at the bottom
            for (var rank = Rank.Rank8; rank >= Rank.Rank1; rank--)
            {
                for (var file = File.FileA; file <= File.FileH; file++)
                {
                    if (file > File.FileA) sb.Append(' ');
                    sb.Append(GetDisplayChar(state[file, rank]));
                }
                if (rank > Rank.Rank1) sb.AppendLine();
            }
            return sb.ToString();
        }

        static char GetDisplayChar(Tile tile)
            => tile.HasPiece ? tile.Piece.ToDisplayChar() : '.';
    }

    enum AIStrategy
    {
        Random,
        Minimax,
        AlphaBeta,
    }

    interface IPlayer
    {
        Move GetNextMove(State state);
    }

    class HumanPlayer : IPlayer
    {
        public Move GetNextMove(State state)
        {
            while (true)
            {
                Write("> ");
                string input = ReadLine().Trim();
                switch (input.ToLower())
                {
                    case "exit":
                    case "quit":
                        Environment.Exit(0);
                        break;
                    case "help":
                        // todo: have some kind of _commands object that you loop thru
                        WriteLine("List of commands:");
                        WriteLine();
                        WriteLine("exit|quit - exits the program");
                        WriteLine("help - displays this message");
                        WriteLine("list - lists all valid moves");
                        break;
                    case "list":
                        WriteLine("List of valid moves:");
                        WriteLine();
                        WriteLine(string.Join(Environment.NewLine, state.GetMoves()));
                        break;
                    default:
                        try
                        {
                            var move = Move.Parse(input, state);
                            _ = state.Apply(move); // make sure it's valid
                            return move;
                        }
                        catch (Exception e) when (e is AnParseException || e is InvalidMoveException)
                        {
                            Debug.WriteLine(e.ToString());
                            WriteLine("Sorry, try again.");
                        }
                        break;
                }
            }
        }
    }

    class RandomAIPlayer : IPlayer
    {
        private readonly Random _rand = new Random();

        public Move GetNextMove(State state)
        {
            var moves = state.GetMoves().ToArray();
            Debug.WriteLine(string.Join(Environment.NewLine, (object[])moves));
            return moves[_rand.Next(moves.Length)];
        }
    }

    static class Eval
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
            },
            // king middlegame
            new int[]
            {
                -30,-40,-40,-50,-50,-40,-40,-30,
                -30,-40,-40,-50,-50,-40,-40,-30,
                -30,-40,-40,-50,-50,-40,-40,-30,
                -30,-40,-40,-50,-50,-40,-40,-30,
                -20,-30,-30,-40,-40,-30,-30,-20,
                -10,-20,-20,-20,-20,-20,-20,-10,
                 20, 20,  0,  0,  0,  0, 20, 20,
                 20, 30, 10,  0,  0, 10, 30, 20
            },
            // king endgame
            new int[]
            {
                -50,-40,-30,-20,-20,-30,-40,-50,
                -30,-20,-10,  0,  0,-10,-20,-30,
                -30,-10, 20, 30, 30, 20,-10,-30,
                -30,-10, 30, 40, 40, 30,-10,-30,
                -30,-10, 30, 40, 40, 30,-10,-30,
                -30,-10, 20, 30, 30, 20,-10,-30,
                -30,-30,  0,  0,  0,  0,-30,-30,
                -50,-30,-30,-30,-30,-30,-30,-50
            }
        };

        // Heuristic is calculated from white's viewpoint (positive = good for white)
        public static int Heuristic(State state)
        {
            if (state.IsTerminal)
            {
                // todo: give preference to checkmates that occur in fewer moves
                if (state.IsStalemate) return 0;
                return state.WhiteToMove ? int.MinValue : int.MaxValue;
            }

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
            // GetOccupiedTiles() doesn't perform great atm, it's faster to check whether the tiles are occupied manually
            foreach (var tile in state.GetTiles())
            {
                if (!tile.HasPiece) continue;
                var (piece, location) = (tile.Piece, tile.Location);
                int locationInt = 8 * (piece.IsWhite ? (7 - (int)location.Rank) : (int)location.Rank) + (int)location.File;
                int pieceValue = PieceValues[(int)piece.Kind];
                int pieceSquareValue = PieceSquareValues[(int)piece.Kind + Convert.ToInt32(piece.Kind == PieceKind.King && isEndgame)][locationInt];

                if (piece.IsWhite)
                {
                    result += pieceValue;
                    result += pieceSquareValue;
                }
                else
                {
                    result -= pieceValue;
                    result -= pieceSquareValue;
                }
            }
            return result;
        }
    }

    // todo: improve perf by using bitboards to represent ChessState
    class MinimaxAIPlayer : IPlayer
    {
        private readonly int _depth;

        public MinimaxAIPlayer(int depth) => _depth = depth;

        public Move GetNextMove(State state)
        {
            // todo: throw something if it's terminal

            Move bestMove = null;
            int bestValue = state.WhiteToMove ? int.MinValue : int.MaxValue;
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

        private static int Minimax(State state, int d)
        {
            if (d == 0 || state.IsTerminal)
            {
                return Eval.Heuristic(state);
            }

            int bestValue = state.WhiteToMove ? int.MinValue : int.MaxValue;

            foreach (var succ in state.GetSuccessors())
            {
                int value = Minimax(succ, d - 1);
                bestValue = state.WhiteToMove
                    ? Math.Max(bestValue, value)
                    : Math.Min(bestValue, value);
            }

            return bestValue;
        }
    }

    class AlphaBetaAIPlayer : IPlayer
    {
        private readonly int _depth;
        private readonly TranspositionCache _cache;

        public AlphaBetaAIPlayer(int depth)
        {
            Debug.Assert(depth > 0);
            _depth = depth;
            _cache = new TranspositionCache();
        }

        public Move GetNextMove(State state)
        {
            // todo: throw something if it's terminal

            Move bestMove = null;
            int bestValue = state.WhiteToMove ? int.MinValue : int.MaxValue;

            var (alpha, beta) = (int.MinValue, int.MaxValue);

            foreach (var (move, succ) in state.GetMovesAndSuccessors())
            {
                int value = AlphaBeta(succ, _depth - 1, alpha, beta);
                if (state.WhiteToMove)
                {
                    bool better = (value > bestValue);
                    if (better)
                    {
                        bestValue = value;
                        bestMove = move;
                    }
                    alpha = Math.Max(alpha, bestValue);
                }
                else
                {
                    bool better = (value < bestValue);
                    if (better)
                    {
                        bestValue = value;
                        bestMove = move;
                    }
                    beta = Math.Min(beta, bestValue);
                }

                if (alpha >= beta)
                {
                    break;
                }
            }

            return bestMove;
        }

        private int AlphaBeta(State state, int d, int alpha, int beta)
        {
            if (d == 0 || state.IsTerminal)
            {
                return Eval.Heuristic(state);
            }
            if (_cache.TryGetValue(state, out int cachedValue))
            {
                return cachedValue;
            }

            int bestValue = state.WhiteToMove ? int.MinValue : int.MaxValue;

            foreach (var succ in state.GetSuccessors())
            {
                int value = AlphaBeta(succ, d - 1, alpha, beta);
                if (state.WhiteToMove)
                {
                    bestValue = Math.Max(bestValue, value);
                    alpha = Math.Max(alpha, bestValue);
                }
                else
                {
                    bestValue = Math.Min(bestValue, value);
                    beta = Math.Min(beta, bestValue);
                }

                if (alpha >= beta)
                {
                    break;
                }
            }

            _cache.TryAdd(state, bestValue);
            return bestValue;
        }
    }
}
