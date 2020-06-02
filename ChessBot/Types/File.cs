using System;

namespace ChessBot.Types
{
    /// <summary>
    /// Identifies a file (or column) of the chess board.
    /// </summary>
    public enum File
    {
        FileA,
        FileB,
        FileC,
        FileD,
        FileE,
        FileF,
        FileG,
        FileH,
    }

    public static class FileHelpers
    {
        // note: we leave it up to the caller to check for validity
        public static File FromChar(char ch) => (File)(ch - 'a');

        public static bool IsValid(this File file)
            => file >= File.FileA && file <= File.FileH;

        public static char ToChar(this File file)
            => file.IsValid() ? (char)((int)file + 'a') : throw new ArgumentOutOfRangeException(nameof(file));
    }
}
