using System;
using System.Diagnostics;
using System.Linq;
using System.Text;

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

            // todo: "Pick ai strategy [random]: "

            var whitePlayer = (userColor == PlayerColor.White) ? (IPlayer)new HumanPlayer() : new AIPlayer();
            var blackPlayer = (userColor != PlayerColor.White) ? (IPlayer)new HumanPlayer() : new AIPlayer();

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

    class AIPlayer : IPlayer
    {
        private readonly Random _rand = new Random();

        public ChessMove GetNextMove(ChessState state)
        {
            var moves = state.GetMoves().ToArray();
            Debug.WriteLine(string.Join(Environment.NewLine, (object[])moves));
            return moves[_rand.Next(moves.Length)];
        }
    }
}
