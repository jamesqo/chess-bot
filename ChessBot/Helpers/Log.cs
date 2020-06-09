﻿using System;
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
        public static void Debug<T0>(
            string message,
            T0 arg0,
            [CallerMemberName] string memberName = null) => DebugCore(string.Format(message, arg0.ToString()), memberName);

        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Debug<T0, T1>(
            string message,
            T0 arg0,
            T1 arg1,
            [CallerMemberName] string memberName = null) => DebugCore(string.Format(message, arg0.ToString(), arg1.ToString()), memberName);

        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Debug<T0, T1, T2>(
            string message,
            T0 arg0,
            T1 arg1,
            T2 arg2,
            [CallerMemberName] string memberName = null) => DebugCore(string.Format(message, arg0.ToString(), arg1.ToString(), arg2.ToString()), memberName);

        private static void DebugCore(string message, string memberName)
        {
            Trace.Write("[");
            Trace.Write(memberName);
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
