using System;
using System.Linq;
using System.Threading.Tasks;
using Matchmaking.Redis;
using StackExchange.Redis;
using Xunit;

namespace Matchmaking.Tests
{
    public class RedisMatchQueueTests : IDisposable
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2018, 11, 1, 18, 0, 0, TimeSpan.Zero);

        private static readonly Lazy<ConnectionMultiplexer> Connection = new Lazy<ConnectionMultiplexer>(
            () => ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable(RedisFactAttribute.Variable)));

        private readonly string _prefix = "test:" + Guid.NewGuid().ToString("N") + ":";
        private readonly MatchRules _rules = new MatchRules
        {
            PlayersPerMatch = 2,
            InitialTolerance = 50,
            TolerancePerSecond = 5,
            MaxTolerance = 400
        };

        private RedisMatchQueue CreateQueue()
        {
            return new RedisMatchQueue(Connection.Value.GetDatabase(), _prefix, TimeSpan.FromMinutes(1));
        }

        [RedisFact]
        public async Task Player_cannot_queue_twice()
        {
            var queue = CreateQueue();

            Assert.True(await queue.EnqueueAsync("p1", "eu", 1500, T0));
            Assert.False(await queue.EnqueueAsync("p1", "na", 1500, T0));

            var status = await queue.GetStatusAsync("p1");
            Assert.Equal(TicketState.Waiting, status.State);
            Assert.Equal("eu", status.Region);
            Assert.Equal(T0, status.EnqueuedAt);
        }

        [RedisFact]
        public async Task Cancel_removes_ticket()
        {
            var queue = CreateQueue();
            await queue.EnqueueAsync("p1", "eu", 1500, T0);
            await queue.EnqueueAsync("p2", "eu", 1510, T0);

            Assert.True(await queue.CancelAsync("p1"));
            Assert.False(await queue.CancelAsync("p1"));
            Assert.Equal(TicketState.NotFound, (await queue.GetStatusAsync("p1")).State);

            var matches = await queue.FindMatchesAsync("eu", T0.AddSeconds(30), _rules);
            Assert.Empty(matches);
        }

        [RedisFact]
        public async Task Matches_close_players()
        {
            var queue = CreateQueue();
            await queue.EnqueueAsync("p1", "eu", 1500, T0);
            await queue.EnqueueAsync("p2", "eu", 1540, T0);
            await queue.EnqueueAsync("p3", "na", 1520, T0);

            var matches = await queue.FindMatchesAsync("eu", T0.AddSeconds(1), _rules);

            var match = Assert.Single(matches);
            Assert.Equal(new[] { "p1", "p2" }, match.Players.Select(p => p.PlayerId).OrderBy(p => p));

            var status = await queue.GetStatusAsync("p2");
            Assert.Equal(TicketState.Matched, status.State);
            Assert.Equal(match.Id, status.Match.Id);
            Assert.Equal(TicketState.Waiting, (await queue.GetStatusAsync("p3")).State);
        }

        [RedisFact]
        public async Task Window_grows_with_waiting_time()
        {
            var queue = CreateQueue();
            await queue.EnqueueAsync("p1", "eu", 1500, T0);
            await queue.EnqueueAsync("p2", "eu", 1600, T0);

            // 50 + 5/s -> 100 needs 10 seconds
            Assert.Empty(await queue.FindMatchesAsync("eu", T0.AddSeconds(1), _rules));
            Assert.Empty(await queue.FindMatchesAsync("eu", T0.AddSeconds(9), _rules));
            Assert.Single(await queue.FindMatchesAsync("eu", T0.AddSeconds(10), _rules));
        }

        [RedisFact]
        public async Task Player_waiting_long_is_not_stuck_between_newcomers()
        {
            var queue = CreateQueue();
            await queue.EnqueueAsync("old", "eu", 1500, T0);
            await queue.EnqueueAsync("low", "eu", 1440, T0.AddSeconds(20));
            await queue.EnqueueAsync("high", "eu", 1600, T0.AddSeconds(20));

            var match = Assert.Single(await queue.FindMatchesAsync("eu", T0.AddSeconds(20), _rules));

            Assert.Equal(new[] { "low", "old" }, match.Players.Select(p => p.PlayerId).OrderBy(p => p));
        }

        [RedisFact]
        public async Task Oldest_ticket_picks_first()
        {
            var queue = CreateQueue();
            await queue.EnqueueAsync("a", "eu", 1500, T0.AddSeconds(5));
            await queue.EnqueueAsync("b", "eu", 1530, T0);
            await queue.EnqueueAsync("c", "eu", 1550, T0.AddSeconds(5));

            var match = Assert.Single(await queue.FindMatchesAsync("eu", T0.AddSeconds(5), _rules));

            // b is the oldest, c is closer to it than a
            Assert.Equal(new[] { "b", "c" }, match.Players.Select(p => p.PlayerId).OrderBy(p => p));
            Assert.Equal(TicketState.Waiting, (await queue.GetStatusAsync("a")).State);
        }

        [RedisFact]
        public async Task Groups_of_more_than_two()
        {
            var queue = CreateQueue();
            var rules = new MatchRules { PlayersPerMatch = 4, InitialTolerance = 100, TolerancePerSecond = 0, MaxTolerance = 100 };
            var mmrs = new[] { 1200, 1450, 1480, 1500, 1530, 1560, 1900 };
            for (var i = 0; i < mmrs.Length; i++)
                await queue.EnqueueAsync("p" + mmrs[i], "na", mmrs[i], i == 3 ? T0 : T0.AddSeconds(1));

            var matches = await queue.FindMatchesAsync("na", T0.AddSeconds(1), rules);

            var match = Assert.Single(matches);
            Assert.Equal(new[] { 1450, 1480, 1500, 1530 }, match.Players.Select(p => p.Mmr).OrderBy(m => m));
            Assert.Equal(T0, match.Players.Single(p => p.Mmr == 1500).EnqueuedAt);
        }

        [RedisFact]
        public async Task Queueing_again_clears_previous_match()
        {
            var queue = CreateQueue();
            await queue.EnqueueAsync("p1", "eu", 1500, T0);
            await queue.EnqueueAsync("p2", "eu", 1500, T0);
            await queue.FindMatchesAsync("eu", T0.AddSeconds(1), _rules);

            Assert.True(await queue.EnqueueAsync("p1", "eu", 1500, T0.AddMinutes(1)));
            Assert.Equal(TicketState.Waiting, (await queue.GetStatusAsync("p1")).State);
        }

        public void Dispose()
        {
            if (!Connection.IsValueCreated)
                return;

            var db = Connection.Value.GetDatabase();
            foreach (var endpoint in Connection.Value.GetEndPoints())
            {
                var keys = Connection.Value.GetServer(endpoint).Keys(db.Database, _prefix + "*").ToArray();
                if (keys.Length > 0)
                    db.KeyDelete(keys);
            }
        }
    }
}
