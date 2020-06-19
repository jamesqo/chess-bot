﻿using ChessBot.Exceptions;
using ChessBot.Search;
using ChessBot.Tests.TestHelpers;
using System;
using System.Runtime.CompilerServices;
using Xunit;

namespace ChessBot.Tests.Search
{
    public class SearchTests
    {
        const int TtCapacity = 1 << 16;

        public static TheoryData<ISearchAlgorithm> Searchers
        {
            get
            {
                var data = new TheoryData<ISearchAlgorithm>();
                for (int d = 1; d <= 3; d++)
                {
                    data.Add(new Mtdf(TtCapacity) { Depth = d });
                    data.Add(new MtdfIds(TtCapacity) { Depth = d });
                }
                return data;
            }
        }

        [Theory]
        [MemberData(nameof(Searchers))]
        public void PickMove_StressTest(ISearchAlgorithm searcher)
        {
            var rsg = GetRsg();
            for (int i = 0; i < 20; i++)
            {
                var state = rsg.NextNonTerminal();
                var move = searcher.PickMove(state);
                state.TryApply(move, out var error);
                Assert.Equal(InvalidMoveReason.None, error);
            }
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
