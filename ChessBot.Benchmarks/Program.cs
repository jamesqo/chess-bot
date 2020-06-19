using BenchmarkDotNet.Running;
using System;

namespace ChessBot.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<SearchBenchmarks>();
        }
    }
}
