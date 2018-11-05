using System;

namespace Matchmaking
{
    public enum TicketState
    {
        NotFound,
        Waiting,
        Matched
    }

    public sealed class TicketStatus
    {
        public static readonly TicketStatus NotFound = new TicketStatus(TicketState.NotFound, null, 0, null, null);

        private TicketStatus(TicketState state, string region, int mmr, DateTimeOffset? enqueuedAt, Match match)
        {
            State = state;
            Region = region;
            Mmr = mmr;
            EnqueuedAt = enqueuedAt;
            Match = match;
        }

        public TicketState State { get; }
        public string Region { get; }
        public int Mmr { get; }
        public DateTimeOffset? EnqueuedAt { get; }
        public Match Match { get; }

        public static TicketStatus Waiting(string region, int mmr, DateTimeOffset enqueuedAt)
        {
            return new TicketStatus(TicketState.Waiting, region, mmr, enqueuedAt, null);
        }

        public static TicketStatus Matched(Match match, MatchPlayer player)
        {
            return new TicketStatus(TicketState.Matched, match.Region, player.Mmr, player.EnqueuedAt, match);
        }
    }
}
