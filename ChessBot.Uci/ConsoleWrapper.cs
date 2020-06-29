using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace ChessBot.Uci
{
    static class ConsoleWrapper
    {
#if UCI_TRACE
        private static string LogFilePath = GetLogFilePath();
        private static readonly StreamWriter LogFile = new StreamWriter(LogFilePath, append: false, new UTF8Encoding(false));

        static ConsoleWrapper()
        {
            AppDomain.CurrentDomain.ProcessExit += Destructor;
        }

        private static void Destructor(object sender, EventArgs e)
        {
            LogFile.Dispose();
        }

        private static string GetLogFilePath([CallerFilePath] string thisPath = null)
        {
            var solutionFolder = Path.GetDirectoryName(Path.GetDirectoryName(thisPath));
            var logsFolder = Path.Combine(solutionFolder, "logs");
            Directory.CreateDirectory(logsFolder);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            var fileName = $"uci_{timestamp}.log";
            return Path.Combine(logsFolder, fileName);
        }
#else
        private static readonly StreamWriter LogFile = StreamWriter.Null;
#endif

        public static class Error
        {
            public static void WriteLine(object value)
            {
                LogFile.WriteLine(value);
                Console.Error.WriteLine(value);
            }
        }

        public static void WriteLine(object value)
        {
            LogFile.WriteLine(value);
            Console.WriteLine(value);
        }

        public static string ReadLine()
        {
            string input = Console.ReadLine();
            LogFile.WriteLine(input);
            return input;
        }
    }
}
