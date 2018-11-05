using System;
using System.Collections.Generic;
using System.Linq;

namespace Matchmaking
{
    public sealed class MatchPlayer
    {
        public MatchPlayer(string playerId, int mmr, DateTimeOffset enqueuedAt)
        {
            PlayerId = playerId;
            Mmr = mmr;
            EnqueuedAt = enqueuedAt;
        }

        public string PlayerId { get; }
        public int Mmr { get; }
        public DateTimeOffset EnqueuedAt { get; }
    }

    public sealed class Match
    {
        public Match(long id, string region, DateTimeOffset createdAt, IReadOnlyList<MatchPlayer> players)
        {
            Id = id;
            Region = region;
            CreatedAt = createdAt;
            Players = players;
        }

        public long Id { get; }
        public string Region { get; }
        public DateTimeOffset CreatedAt { get; }
        public IReadOnlyList<MatchPlayer> Players { get; }

        public int MmrSpread => Players.Max(p => p.Mmr) - Players.Min(p => p.Mmr);
    }
}
