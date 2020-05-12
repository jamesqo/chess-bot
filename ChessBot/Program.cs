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

            var whitePlayer = (userColor == PlayerColor.White) ? (IPlayer)new HumanPlayer() : new AIPlayer();
            var blackPlayer = (userColor != PlayerColor.White) ? (IPlayer)new HumanPlayer() : new AIPlayer();

            Console.WriteLine($"Playing as: {userColor}");
            Console.WriteLine("Have fun!");

            var game = new ChessGame();
            while (true)
            {
                Console.WriteLine();
                Console.WriteLine($"<< Turn {game.Turn} >>");
                Console.WriteLine();

                Console.WriteLine(GetDisplayString(game.State));
                Console.WriteLine();

                Console.WriteLine("It's White's turn.");
                game.ApplyMove(whitePlayer.GetNextMove(game.State));
                CheckForEnd(game);

                Console.WriteLine("It's Black's turn.");
                game.ApplyMove(blackPlayer.GetNextMove(game.State));
                CheckForEnd(game);
            }
        }

        static void CheckForEnd(ChessGame game)
        {
            var state = game.State;
            if (state.IsCheckmate)
            {
                var lastPlayer = (state.NextPlayer == PlayerColor.White ? PlayerColor.Black : PlayerColor.White);
                Console.WriteLine($"{lastPlayer} wins!");
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
                sb.AppendJoin("__", Enumerable.Repeat('.', 9));
                sb.AppendLine();
                for (int c = 0; c < 8; c++)
                {
                    sb.Append('|');
                    sb.Append(GetDisplayString(state[r, c]));
                }
                sb.Append('|');
                sb.AppendLine();
            }
            sb.AppendJoin("__", Enumerable.Repeat('.', 9));
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
                Console.Write("Enter your move: ");
                string input = Console.ReadLine();
                try
                {
                    return ChessMove.Parse(input, state);
                }
                catch (AlgebraicNotationParseException e)
                {
                    Debug.WriteLine(e.ToString());
                    Console.WriteLine("Sorry, try again.");
                }
            }
        }
    }

    class AIPlayer : IPlayer
    {
        public ChessMove GetNextMove(ChessState state)
        {
            throw new NotImplementedException();
        }
    }
}
