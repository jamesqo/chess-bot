using ChessBot.Search;
using ChessBot.Tests.TestHelpers;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Xunit;
using Xunit.Abstractions;

namespace ChessBot.Tests.Search
{
    public class SearchTests
    {
        const int TtCapacity = 1 << 8;

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
                        data.Add(new Mtdf(TtCapacity) { Depth = depth, MaxNodes = limit });
                        data.Add(new MtdfIds(TtCapacity) { Depth = depth, MaxNodes = limit });
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
            for (int i = 0; i < 20; i++)
            {
                var state = rsg.Next();
                var info = searcher.Search(state);

                Assert.Equal(searcher.Depth, info.Depth);
                Assert.InRange(info.NodesSearched, 0, searcher.MaxNodes);
                Assert.InRange(info.Pv.Length, 0, searcher.Depth);

                // make sure all of the moves in the pv are valid
                var successor = state;
                foreach (var pvMove in info.Pv)
                {
                    try
                    {
                        successor = successor.Apply(pvMove);
                    }
                    catch
                    {
                        _output.WriteLine($"Search() on {state} generated an invalid PV sequence: {string.Join(' ', info.Pv)}");
                        _output.WriteLine($"Successor state: {successor}");
                        _output.WriteLine($"Offending move: {pvMove}");
                        throw;
                    }
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
            Assert.InRange(info.NodesSearched, 0, 5);
            Assert.Empty(info.Pv);
            Assert.InRange(info.Score, int.MinValue, -1); // getting mated should have a negative utility

            // Stalemate
            state = State.ParseFen("rn2k1nr/pp4pp/3p4/q1pP4/P1P2p1b/1b2pPRP/1P1NP1PQ/2B1KBNR w Kkq - 0 1");
            info = searcher.Search(state);
            // For MTD-f this is actually 2, since we do 2 passes to get from [-inf, inf] -> [0, inf] -> [0, 0].
            // So just say it's a reasonably small number without worrying too much about the implementation details of the algorithm.
            Assert.InRange(info.NodesSearched, 0, 5);
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

        private static RandomStateGenerator GetRsg([CallerMemberName] string callerName = null)
        {
            // generate different states for different test methods
            return new RandomStateGenerator(seed: callerName.GetHashCode());
        }
    }
}
