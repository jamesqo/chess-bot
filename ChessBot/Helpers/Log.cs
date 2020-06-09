using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace ChessBot.Helpers
{
    internal static class Log
    {
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

        public static int IndentLevel
        {
            get => Trace.IndentLevel;
            set => Trace.IndentLevel = value;
        }

        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Debug(string message, object arg0) => DebugCore(string.Format(message, arg0), new StackFrame(1));

        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Debug(string message, object arg0, object arg1) => DebugCore(string.Format(message, arg0, arg1), new StackFrame(1));

        private static void DebugCore(string message, StackFrame sf)
        {
            var method = sf.GetMethod();
            var (methodName, className) = (method.Name, method.ReflectedType.Name);

            Trace.Write("[");
            Trace.Write(className);
            Trace.Write(".");
            Trace.Write(methodName);
            Trace.Write("] ");
            Trace.WriteLine(message);
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
    }
}
