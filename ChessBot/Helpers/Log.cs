using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace ChessBot.Helpers
{
    // todo: shouldn't have to be public
    public static class Log
    {
        // todo: use a different conditional compilation symbol
#if DEBUG
        private static string LogFilePath = GetOutputPath();
        private static readonly StreamWriter LogFile = new StreamWriter(LogFilePath, append: false, new UTF8Encoding(false));

        static Log()
        {
            AppDomain.CurrentDomain.ProcessExit += Destructor;
        }

        private static void Destructor(object sender, EventArgs e)
        {
            LogFile.Dispose();
        }

        private static string GetOutputPath([CallerFilePath] string thisPath = null)
        {
            var solutionFolder = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(thisPath)));
            var logsFolder = Path.Combine(solutionFolder, "logs");
            Directory.CreateDirectory(logsFolder);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            var fileName = $"trace_{timestamp}.log";
            return Path.Combine(logsFolder, fileName);
        }
#else
        private static readonly StreamWriter LogFile = StreamWriter.Null;
#endif

        public static bool Enabled { get; set; } = false;
        public static int IndentLevel { get; set; } = 0;

        public static bool IncludeCallerNames { get; set; } = true;

        [Conditional("DEBUG")]
        public static void Debug(
            string message,
            [CallerMemberName] string callerName = null)
        {
            if (Enabled) DebugCore(message, callerName);
        }

        [Conditional("DEBUG")]
        public static void Debug<T0>(
            string message,
            T0 arg0,
            [CallerMemberName] string callerName = null)
        {
            if (Enabled) DebugCore(string.Format(message, arg0.ToString()), callerName);
        }

        [Conditional("DEBUG")]
        public static void Debug<T0, T1>(
            string message,
            T0 arg0,
            T1 arg1,
            [CallerMemberName] string callerName = null)
        {
            if (Enabled) DebugCore(string.Format(message, arg0.ToString(), arg1.ToString()), callerName);
        }

        [Conditional("DEBUG")]
        public static void Debug<T0, T1, T2>(
            string message,
            T0 arg0,
            T1 arg1,
            T2 arg2,
            [CallerMemberName] string callerName = null)
        {
            if (Enabled) DebugCore(string.Format(message, arg0.ToString(), arg1.ToString(), arg2.ToString()), callerName);
        }

        [Conditional("DEBUG")]
        public static void Debug<T0, T1, T2, T3>(
            string message,
            T0 arg0,
            T1 arg1,
            T2 arg2,
            T3 arg3,
            [CallerMemberName] string callerName = null)
        {
            if (Enabled) DebugCore(string.Format(message, arg0.ToString(), arg1.ToString(), arg2.ToString(), arg3.ToString()), callerName);
        }

        [Conditional("DEBUG")]
        public static void Debug<T0, T1, T2, T3, T4>(
            string message,
            T0 arg0,
            T1 arg1,
            T2 arg2,
            T3 arg3,
            T4 arg4,
            [CallerMemberName] string callerName = null)
        {
            if (Enabled) DebugCore(string.Format(message, arg0.ToString(), arg1.ToString(), arg2.ToString(), arg3.ToString(), arg4.ToString()), callerName);
        }

        private static void DebugCore(string message, string callerName)
        {
            System.Diagnostics.Debug.Assert(Enabled);

            for (int i = 0; i < IndentLevel; i++)
            {
                LogFile.Write("    ");
            }
            if (IncludeCallerNames)
            {
                LogFile.Write("[");
                LogFile.Write(callerName);
                LogFile.Write("] ");
            }
            LogFile.WriteLine(message);
        }
    }
}
