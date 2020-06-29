using ChessBot.Search;
using ChessBot.Types;
using Xunit;

namespace ChessBot.Tests
{
    public class MutStateTests
    {
        [Fact]
        public void GetPseudoLegalMoves_BadKiller_Ignores()
        {
            var state = State.ParseFen("rnb1kbnr/pppp1ppp/1q6/1B6/4P3/2p2N2/PP3PPP/RNBQK2R w KQkq - 0 6").ToMutable();
            var killer = new Move(Location.Parse("c4"), Location.Parse("b3"));
            var killers = Killers.Empty.Add(killer);

            Assert.DoesNotContain(killer, state.GetPseudoLegalMoves(killers: killers));
        }
    }
}
