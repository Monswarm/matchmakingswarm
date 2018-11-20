using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Matchmaking
{
    // Same behaviour as the redis queue (match.lua), kept in process for simulations and tests.
    // Assignments never expire here.
    public class InMemoryMatchQueue : IMatchQueue
    {
        // redis sorts members with the same score by their bytes, hence the ordinal comparison
        private static readonly IComparer<Ticket> MmrOrder = Comparer<Ticket>.Create((a, b) =>
            a.Mmr != b.Mmr ? a.Mmr.CompareTo(b.Mmr) : string.CompareOrdinal(a.PlayerId, b.PlayerId));

        private static readonly IComparer<Ticket> TimeOrder = Comparer<Ticket>.Create((a, b) =>
            a.EnqueuedMs != b.EnqueuedMs ? a.EnqueuedMs.CompareTo(b.EnqueuedMs) : string.CompareOrdinal(a.PlayerId, b.PlayerId));

        private static readonly IReadOnlyList<Match> NoMatches = new Match[0];

        private readonly object _sync = new object();
        private readonly Dictionary<string, Ticket> _tickets = new Dictionary<string, Ticket>(StringComparer.Ordinal);
        private readonly Dictionary<string, Match> _assignments = new Dictionary<string, Match>(StringComparer.Ordinal);
        private readonly Dictionary<string, RegionQueue> _regions = new Dictionary<string, RegionQueue>(StringComparer.Ordinal);
        private long _sequence;

        public Task<bool> EnqueueAsync(string playerId, string region, int mmr, DateTimeOffset now)
        {
            lock (_sync)
            {
                if (_tickets.ContainsKey(playerId))
                    return Task.FromResult(false);

                var ticket = new Ticket(playerId, region, mmr, now.ToUnixTimeMilliseconds());
                RegionQueue queue;
                if (!_regions.TryGetValue(region, out queue))
                {
                    queue = new RegionQueue();
                    _regions.Add(region, queue);
                }

                _assignments.Remove(playerId);
                _tickets.Add(playerId, ticket);
                Insert(queue.ByMmr, ticket, MmrOrder);
                Insert(queue.ByTime, ticket, TimeOrder);
                return Task.FromResult(true);
            }
        }

        public Task<bool> CancelAsync(string playerId)
        {
            lock (_sync)
            {
                Ticket ticket;
                if (!_tickets.TryGetValue(playerId, out ticket))
                    return Task.FromResult(false);

                Remove(ticket);
                return Task.FromResult(true);
            }
        }

        public Task<TicketStatus> GetStatusAsync(string playerId)
        {
            lock (_sync)
            {
                Ticket ticket;
                if (_tickets.TryGetValue(playerId, out ticket))
                    return Task.FromResult(TicketStatus.Waiting(ticket.Region, ticket.Mmr, FromMs(ticket.EnqueuedMs)));

                Match match;
                if (_assignments.TryGetValue(playerId, out match))
                    return Task.FromResult(TicketStatus.Matched(match, match.Players.First(p => p.PlayerId == playerId)));

                return Task.FromResult(TicketStatus.NotFound);
            }
        }

        public Task<IReadOnlyList<Match>> FindMatchesAsync(string region, DateTimeOffset now, MatchRules rules)
        {
            var nowMs = now.ToUnixTimeMilliseconds();
            var size = rules.PlayersPerMatch;

            lock (_sync)
            {
                RegionQueue queue;
                if (!_regions.TryGetValue(region, out queue))
                    return Task.FromResult(NoMatches);

                var matches = new List<Match>();
                foreach (var anchor in queue.ByTime.ToArray())
                {
                    var rank = queue.ByMmr.BinarySearch(anchor, MmrOrder);
                    if (rank < 0)
                        continue;

                    var tolerance = rules.ToleranceAfter(TimeSpan.FromMilliseconds(nowMs - anchor.EnqueuedMs));
                    var picked = new List<Ticket>(size) { anchor };
                    int below = rank - 1, above = rank + 1;

                    while (picked.Count < size)
                    {
                        var lower = below >= 0 ? queue.ByMmr[below] : null;
                        var upper = above < queue.ByMmr.Count ? queue.ByMmr[above] : null;
                        Ticket pick;
                        int distance;

                        if (lower != null && (upper == null || anchor.Mmr - lower.Mmr <= upper.Mmr - anchor.Mmr))
                        {
                            pick = lower;
                            distance = anchor.Mmr - lower.Mmr;
                            below--;
                        }
                        else if (upper != null)
                        {
                            pick = upper;
                            distance = upper.Mmr - anchor.Mmr;
                            above++;
                        }
                        else
                        {
                            break;
                        }

                        if (distance > tolerance)
                            break;
                        picked.Add(pick);
                    }

                    if (picked.Count < size)
                        continue;

                    var players = picked.Select(t => new MatchPlayer(t.PlayerId, t.Mmr, FromMs(t.EnqueuedMs))).ToList();
                    var match = new Match(++_sequence, region, FromMs(nowMs), players);
                    foreach (var ticket in picked)
                    {
                        Remove(ticket);
                        _assignments[ticket.PlayerId] = match;
                    }

                    matches.Add(match);
                }

                return Task.FromResult<IReadOnlyList<Match>>(matches);
            }
        }

        private void Remove(Ticket ticket)
        {
            var queue = _regions[ticket.Region];
            queue.ByMmr.RemoveAt(queue.ByMmr.BinarySearch(ticket, MmrOrder));
            queue.ByTime.RemoveAt(queue.ByTime.BinarySearch(ticket, TimeOrder));
            _tickets.Remove(ticket.PlayerId);
        }

        private static void Insert(List<Ticket> list, Ticket ticket, IComparer<Ticket> comparer)
        {
            var index = list.BinarySearch(ticket, comparer);
            list.Insert(index < 0 ? ~index : index, ticket);
        }

        private static DateTimeOffset FromMs(long ms)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(ms);
        }

        private sealed class Ticket
        {
            public Ticket(string playerId, string region, int mmr, long enqueuedMs)
            {
                PlayerId = playerId;
                Region = region;
                Mmr = mmr;
                EnqueuedMs = enqueuedMs;
            }

            public string PlayerId { get; }
            public string Region { get; }
            public int Mmr { get; }
            public long EnqueuedMs { get; }
        }

        private sealed class RegionQueue
        {
            public readonly List<Ticket> ByMmr = new List<Ticket>();
            public readonly List<Ticket> ByTime = new List<Ticket>();
        }
    }
}
