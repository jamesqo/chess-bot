using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using ChessBot.Search;

namespace ChessBot.Benchmarks
{
    [SimpleJob(RuntimeMoniker.NetCoreApp31, baseline: true)]
    //[SimpleJob(RuntimeMoniker.Net48)]
    [RPlotExporter]
    public class SearchBenchmarks
    {
        [Params(5, 6, 7)]
        public int Depth;

        [Params(1 << 8, 1 << 12, 1 << 16)]
        public int TtCapacity;

        // todo: test on states besides startpos

        [Benchmark]
        public void Mtdf()
        {
            new Mtdf()
            {
                Depth = Depth,
                TtCapacity = TtCapacity
            }
            .Search(State.Start);
        }

        [Benchmark]
        public void MtdfIds()
        {
            new MtdfIds()
            {
                Depth = Depth,
                TtCapacity = TtCapacity
            }
            .Search(State.Start);
        }
    }
}
