using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Matchmaking.Redis
{
    public class RedisMatchQueue : IMatchQueue
    {
        private readonly IDatabase _db;
        private readonly string _prefix;
        private readonly TimeSpan _assignmentTtl;

        public RedisMatchQueue(IDatabase db, string keyPrefix, TimeSpan assignmentTtl)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _prefix = keyPrefix ?? "";
            _assignmentTtl = assignmentTtl;
        }

        private RedisKey TicketsKey => _prefix + "tickets";
        private RedisKey SequenceKey => _prefix + "match-seq";
        private string AssignPrefix => _prefix + "assign:";

        private RedisKey QueueKey(string region) => _prefix + "queue:" + region;
        private RedisKey AssignKey(string playerId) => AssignPrefix + playerId;

        public async Task<bool> EnqueueAsync(string playerId, string region, int mmr, DateTimeOffset now)
        {
            var result = await _db.ScriptEvaluateAsync(
                LuaScripts.Enqueue,
                new[] { TicketsKey, QueueKey(region), AssignKey(playerId) },
                new RedisValue[] { playerId, region, mmr, now.ToUnixTimeMilliseconds() }).ConfigureAwait(false);

            return (int)result == 1;
        }

        public async Task<bool> CancelAsync(string playerId)
        {
            var ticket = await _db.HashGetAsync(TicketsKey, playerId).ConfigureAwait(false);
            if (ticket.IsNull)
                return false;

            var region = MatchFormat.ParseTicket(ticket).Region;
            var result = await _db.ScriptEvaluateAsync(
                LuaScripts.Cancel,
                new[] { TicketsKey, QueueKey(region) },
                new RedisValue[] { playerId, region }).ConfigureAwait(false);

            return (int)result == 1;
        }

        public async Task<TicketStatus> GetStatusAsync(string playerId)
        {
            // ticket first: if it is gone by the time we look at the assignment we still see the match
            var ticket = await _db.HashGetAsync(TicketsKey, playerId).ConfigureAwait(false);
            if (!ticket.IsNull)
                return MatchFormat.ParseTicket(ticket);

            var assignment = await _db.StringGetAsync(AssignKey(playerId)).ConfigureAwait(false);
            if (assignment.IsNull)
                return TicketStatus.NotFound;

            var match = MatchFormat.ParseMatch(assignment);
            foreach (var player in match.Players)
            {
                if (player.PlayerId == playerId)
                    return TicketStatus.Matched(match, player);
            }

            return TicketStatus.NotFound;
        }

        public async Task<IReadOnlyList<Match>> FindMatchesAsync(string region, DateTimeOffset now, MatchRules rules)
        {
            var result = await _db.ScriptEvaluateAsync(
                LuaScripts.Match,
                new[] { TicketsKey, QueueKey(region), SequenceKey },
                new RedisValue[]
                {
                    now.ToUnixTimeMilliseconds(),
                    rules.InitialTolerance,
                    rules.TolerancePerSecond.ToString(CultureInfo.InvariantCulture),
                    rules.MaxTolerance,
                    rules.PlayersPerMatch,
                    region,
                    AssignPrefix,
                    (long)_assignmentTtl.TotalMilliseconds
                }).ConfigureAwait(false);

            var payloads = (RedisResult[])result;
            var matches = new List<Match>(payloads.Length);
            foreach (var payload in payloads)
                matches.Add(MatchFormat.ParseMatch((string)payload));

            return matches;
        }
    }
}
