using ChessBot.Search;
using ChessBot.Tests.TestHelpers;
using ChessBot.Types;
using System;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace ChessBot.Tests.Search
{
    public class SearchTests
    {
        const int TtCapacity = 1 << 8;
        static readonly bool EnableOutput = false; // writing to the xUnit console is extremely slow

        public static TheoryData<ISearchAlgorithm> Searchers
        {
            get
            {
                var depths = new[] { 1, 2, 3 };
                var limits = new[] { 10, 100, 1000 };

                var data = new TheoryData<ISearchAlgorithm>();
                foreach (var depth in depths)
                    foreach (var limit in limits)
                    {
                        var mtdf = new Mtdf() { Depth = depth, MaxNodes = limit };
                        mtdf.Tt = mtdf.MakeTt(TtCapacity);
                        data.Add(mtdf);

                        var ids = new MtdfIds() { Depth = depth, MaxNodes = limit };
                        ids.Tt = ids.MakeTt(TtCapacity);
                        data.Add(ids);
                    }

                return data;
            }
        }

        private readonly ITestOutputHelper _output;

        public SearchTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [MemberData(nameof(Searchers))]
        public void Search_StressTest(ISearchAlgorithm searcher)
        {
            var rsg = GetRsg();
            for (int i = 0; i < 100; i++)
            {
                var state = rsg.Next();
                var info = searcher.Search(state);
                LogSearch(state, searcher, info);

                Assert.Equal(searcher.Depth, info.Depth);
                Assert.InRange(info.NodesSearched, 0, searcher.MaxNodes);
                Assert.InRange(info.Pv.Length, 0, searcher.Depth);

                // make sure all of the moves in the pv are valid
                var successor = state;
                foreach (var pvMove in info.Pv)
                {
                    successor = successor.Apply(pvMove);
                }
            }
        }

        [Theory]
        [MemberData(nameof(Searchers))]
        public void Search_TerminalState(ISearchAlgorithm searcher)
        {
            // Checkmate
            var state = State.ParseFen("8/8/p7/8/P7/1Kbk4/1q6/8 w - - 4 59");
            var info = searcher.Search(state);
            LogSearch(state, searcher, info);
            Assert.Empty(info.Pv);
            Assert.InRange(info.Score, int.MinValue, -1); // getting mated should have a negative utility

            // Stalemate
            state = State.ParseFen("rn2k1nr/pp4pp/3p4/q1pP4/P1P2p1b/1b2pPRP/1P1NP1PQ/2B1KBNR w Kkq - 0 1");
            info = searcher.Search(state);
            LogSearch(state, searcher, info);
            Assert.Empty(info.Pv);
            Assert.Equal(0, info.Score);
        }

        [Theory]
        [MemberData(nameof(Searchers))]
        public void PickMove_TerminalState_Fails(ISearchAlgorithm searcher)
        {
            // Checkmate
            var state = State.ParseFen("8/8/p7/8/P7/1Kbk4/1q6/8 w - - 4 59");
            Assert.Throws<ArgumentException>(() => searcher.PickMove(state));

            // Stalemate
            state = State.ParseFen("rn2k1nr/pp4pp/3p4/q1pP4/P1P2p1b/1b2pPRP/1P1NP1PQ/2B1KBNR w Kkq - 0 1");
            Assert.Throws<ArgumentException>(() => searcher.PickMove(state));
        }

        private void LogSearch(State state, ISearchAlgorithm searcher, ISearchInfo info)
        {
            if (!EnableOutput) return;

            _output.WriteLine($"Search() on {state} with {searcher} yielded score={info.Score}, pv={string.Join(' ', info.Pv)}, nodesSearched={info.NodesSearched}");
        }

        private static RandomStateGenerator GetRsg([CallerMemberName] string callerName = null)
        {
            // generate different states for different test methods
            return new RandomStateGenerator(seed: callerName.GetHashCode());
        }
    }
}
