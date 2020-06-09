using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ChessBot.Helpers
{
    internal static class Log
    {
        [Conditional("DEBUG")]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Debug(string message, params object[] formatArgs)
        {
            var sf = new StackFrame(1);
            var method = sf.GetMethod();
            var (methodName, className) = (method.Name, method.ReflectedType.Name);
            System.Diagnostics.Debug.WriteLine($"[{className}.{methodName}] {message}", formatArgs);
        }
    }
}
