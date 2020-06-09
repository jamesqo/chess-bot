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
#if DEBUG
        private static string OutputPath = GetOutputPath();
        private static readonly StreamWriter Output = new StreamWriter(OutputPath, append: false, new UTF8Encoding(false));

        static Log()
        {
            // remove the default trace listener
            Trace.Listeners.Clear();

            Trace.Listeners.Add(new TextWriterTraceListener(Output));
            AppDomain.CurrentDomain.ProcessExit += Destructor;
        }

        private static void Destructor(object sender, EventArgs e)
        {
            Output.Dispose();
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
#endif

        public static int IndentLevel
        {
            get => Trace.IndentLevel;
            set => Trace.IndentLevel = value;
        }

        public static bool IncludeCallerNames { get; set; } = true;

        [Conditional("DEBUG")]
        public static void Debug<T0>(
            string message,
            T0 arg0,
            [CallerMemberName] string callerName = null) => DebugCore(string.Format(message, arg0.ToString()), callerName);

        [Conditional("DEBUG")]
        public static void Debug<T0, T1>(
            string message,
            T0 arg0,
            T1 arg1,
            [CallerMemberName] string callerName = null) => DebugCore(string.Format(message, arg0.ToString(), arg1.ToString()), callerName);

        [Conditional("DEBUG")]
        public static void Debug<T0, T1, T2>(
            string message,
            T0 arg0,
            T1 arg1,
            T2 arg2,
            [CallerMemberName] string callerName = null) => DebugCore(string.Format(message, arg0.ToString(), arg1.ToString(), arg2.ToString()), callerName);

        private static void DebugCore(string message, string callerName)
        {
            if (IncludeCallerNames)
            {
                Trace.Write("[");
                Trace.Write(callerName);
                Trace.Write("] ");
            }
            Trace.WriteLine(message);
        }
    }
}
