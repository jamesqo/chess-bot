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
            var mtdf = new Mtdf()
            {
                Depth = Depth,
            };
            mtdf.Tt = mtdf.MakeTt(TtCapacity);
            mtdf.Search(State.Start);
        }

        [Benchmark]
        public void MtdfIds()
        {
            var ids = new MtdfIds()
            {
                Depth = Depth,
            };
            ids.Tt = ids.MakeTt(TtCapacity);
            ids.Search(State.Start);
        }
    }
}
