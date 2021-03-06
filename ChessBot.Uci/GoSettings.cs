﻿using ChessBot.Types;
using System;
using System.Collections.Immutable;

namespace ChessBot.Uci
{
    class GoSettings
    {
        public ImmutableArray<Move> SearchMoves { get; set; } = default;
        public int? Depth { get; set; }
        public int? Nodes { get; set; }
        public int? Mate { get; set; }
        public TimeSpan? MoveTime { get; set; }
        public bool Infinite { get; set; } = false;
    }
}
