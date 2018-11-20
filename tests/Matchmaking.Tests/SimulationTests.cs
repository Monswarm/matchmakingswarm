using System;
using System.Linq;
using System.Threading.Tasks;
using Matchmaking.Redis;
using StackExchange.Redis;
using Xunit;
using Xunit.Abstractions;

namespace Matchmaking.Tests
{
    public class SimulationTests
    {
        private readonly ITestOutputHelper _output;

        public SimulationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Ten_thousand_players_during_five_minutes()
        {
            var settings = new SimulationSettings();

            var report = await Simulation.RunAsync(new InMemoryMatchQueue(), settings);
            _output.WriteLine(report.ToString());

            Assert.Equal(report.Matched, report.Matches.SelectMany(m => m.Players).Select(p => p.PlayerId).Distinct().Count());
            Assert.All(report.Matches, m => Assert.Equal(settings.Rules.PlayersPerMatch, m.Players.Count));
            Assert.True(report.MaxMmrSpread <= settings.Rules.MaxTolerance, "spread over the max tolerance");
            Assert.True(report.Matched > settings.Players * 0.95, "too many players left in queue");
            Assert.True(report.Elapsed < TimeSpan.FromMilliseconds(200), "simulation took " + report.Elapsed.TotalMilliseconds + " ms");
        }

        [Fact]
        public async Task Same_seed_gives_same_matches()
        {
            var settings = new SimulationSettings { Players = 3000, Duration = TimeSpan.FromMinutes(2) };

            var first = await Simulation.RunAsync(new InMemoryMatchQueue(), settings);
            var second = await Simulation.RunAsync(new InMemoryMatchQueue(), settings);

            Assert.Equal(Describe(first), Describe(second));
            Assert.Equal(first.AverageWait, second.AverageWait);
            Assert.Equal(first.AverageMmrSpread, second.AverageMmrSpread);
        }

        [Fact]
        public async Task Larger_groups()
        {
            var settings = new SimulationSettings
            {
                Rules = new MatchRules { PlayersPerMatch = 10, InitialTolerance = 50, TolerancePerSecond = 5, MaxTolerance = 500 }
            };

            var report = await Simulation.RunAsync(new InMemoryMatchQueue(), settings);
            _output.WriteLine(report.ToString());

            Assert.All(report.Matches, m => Assert.Equal(10, m.Players.Count));
            Assert.True(report.MaxMmrSpread <= 2 * settings.Rules.MaxTolerance);
        }

        // Runs the same simulation through match.lua and through the in-memory queue
        [RedisFact]
        public async Task Redis_and_memory_queues_agree()
        {
            var settings = new SimulationSettings { Players = 1500, Duration = TimeSpan.FromMinutes(1) };
            var prefix = "sim:" + Guid.NewGuid().ToString("N") + ":";

            using (var redis = ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable(RedisFactAttribute.Variable)))
            {
                var db = redis.GetDatabase();
                try
                {
                    var expected = await Simulation.RunAsync(new InMemoryMatchQueue(), settings);
                    var actual = await Simulation.RunAsync(new RedisMatchQueue(db, prefix, TimeSpan.FromMinutes(1)), settings);
                    _output.WriteLine("redis run: " + actual.Elapsed.TotalMilliseconds + " ms");

                    Assert.NotEmpty(expected.Matches);
                    Assert.Equal(Describe(expected), Describe(actual));
                }
                finally
                {
                    var server = redis.GetServer(redis.GetEndPoints().First());
                    var keys = server.Keys(db.Database, prefix + "*").ToArray();
                    if (keys.Length > 0)
                        db.KeyDelete(keys);
                }
            }
        }

        private static string[] Describe(SimulationReport report)
        {
            return report.Matches
                .Select(m => m.Id + " " + m.Region + " " + m.CreatedAt.ToUnixTimeMilliseconds() + " " +
                             string.Join(",", m.Players.Select(p => p.PlayerId + ":" + p.Mmr + ":" + p.EnqueuedAt.ToUnixTimeMilliseconds())))
                .ToArray();
        }
    }
}
