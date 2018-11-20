using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Matchmaking.Tests
{
    public class SimulationSettings
    {
        public int Players { get; set; } = 10000;
        public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(5);
        public TimeSpan Tick { get; set; } = TimeSpan.FromSeconds(1);
        public int Seed { get; set; } = 1811;

        public int MeanMmr { get; set; } = 1500;
        public int MmrDeviation { get; set; } = 350;

        public string[] Regions { get; set; } = { "eu", "na", "asia" };
        public double[] RegionWeights { get; set; } = { 0.45, 0.35, 0.20 };

        public MatchRules Rules { get; set; } = new MatchRules();
    }

    public class SimulationReport
    {
        public int Players { get; set; }
        public int Matched { get; set; }
        public int StillWaiting { get; set; }
        public IReadOnlyList<Match> Matches { get; set; }

        public TimeSpan AverageWait { get; set; }
        public TimeSpan P95Wait { get; set; }
        public TimeSpan MaxWait { get; set; }

        public double AverageMmrSpread { get; set; }
        public int P95MmrSpread { get; set; }
        public int MaxMmrSpread { get; set; }

        public TimeSpan Elapsed { get; set; }

        public override string ToString()
        {
            var s = new StringBuilder();
            s.AppendLine("players            " + Players);
            s.AppendLine("matches            " + Matches.Count);
            s.AppendLine("matched players    " + Matched + " (" + (100.0 * Matched / Players).ToString("0.0", CultureInfo.InvariantCulture) + "%)");
            s.AppendLine("still waiting      " + StillWaiting);
            s.AppendLine("avg wait           " + Seconds(AverageWait));
            s.AppendLine("p95 wait           " + Seconds(P95Wait));
            s.AppendLine("max wait           " + Seconds(MaxWait));
            s.AppendLine("avg mmr diff       " + AverageMmrSpread.ToString("0.0", CultureInfo.InvariantCulture));
            s.AppendLine("p95 mmr diff       " + P95MmrSpread);
            s.AppendLine("max mmr diff       " + MaxMmrSpread);
            s.AppendLine("run time           " + Elapsed.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture) + " ms");
            return s.ToString();
        }

        private static string Seconds(TimeSpan t)
        {
            return t.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s";
        }
    }

    // Players arrive at random times during Duration and the ticker runs every Tick.
    // Time only moves through the ManualClock, so a run takes as long as the cpu work.
    public static class Simulation
    {
        public static readonly DateTimeOffset Start = new DateTimeOffset(2018, 11, 19, 20, 0, 0, TimeSpan.Zero);

        public static async Task<SimulationReport> RunAsync(IMatchQueue queue, SimulationSettings settings)
        {
            var random = new Random(settings.Seed);
            var arrivals = Enumerable.Range(0, settings.Players)
                .Select(i => new Arrival
                {
                    PlayerId = "p" + i,
                    At = Start.AddMilliseconds(random.Next((int)settings.Duration.TotalMilliseconds)),
                    Region = PickRegion(random, settings),
                    Mmr = NextMmr(random, settings)
                })
                .OrderBy(a => a.At)
                .ThenBy(a => a.PlayerId, StringComparer.Ordinal)
                .ToList();

            var clock = new ManualClock(Start);
            var ticker = new MatchmakingTicker(queue, clock, settings.Rules, settings.Regions);
            var matches = new List<Match>();
            var next = 0;

            var watch = Stopwatch.StartNew();
            for (var tick = Start + settings.Tick; tick <= Start + settings.Duration; tick += settings.Tick)
            {
                while (next < arrivals.Count && arrivals[next].At <= tick)
                {
                    var arrival = arrivals[next++];
                    clock.AdvanceTo(arrival.At);
                    await queue.EnqueueAsync(arrival.PlayerId, arrival.Region, arrival.Mmr, clock.UtcNow);
                }

                clock.AdvanceTo(tick);
                matches.AddRange(await ticker.TickAsync());
            }
            watch.Stop();

            return BuildReport(settings.Players, matches, watch.Elapsed);
        }

        private static SimulationReport BuildReport(int players, List<Match> matches, TimeSpan elapsed)
        {
            var waits = matches
                .SelectMany(m => m.Players.Select(p => m.CreatedAt - p.EnqueuedAt))
                .OrderBy(w => w)
                .ToList();
            var spreads = matches.Select(m => m.MmrSpread).OrderBy(s => s).ToList();

            return new SimulationReport
            {
                Players = players,
                Matched = waits.Count,
                StillWaiting = players - waits.Count,
                Matches = matches,
                AverageWait = waits.Count == 0 ? TimeSpan.Zero : TimeSpan.FromTicks((long)waits.Average(w => w.Ticks)),
                P95Wait = Percentile(waits, 0.95),
                MaxWait = waits.Count == 0 ? TimeSpan.Zero : waits[waits.Count - 1],
                AverageMmrSpread = spreads.Count == 0 ? 0 : spreads.Average(),
                P95MmrSpread = Percentile(spreads, 0.95),
                MaxMmrSpread = spreads.Count == 0 ? 0 : spreads[spreads.Count - 1],
                Elapsed = elapsed
            };
        }

        private static T Percentile<T>(List<T> sorted, double p)
        {
            if (sorted.Count == 0)
                return default(T);
            var index = (int)Math.Ceiling(p * sorted.Count) - 1;
            return sorted[Math.Max(0, index)];
        }

        private static string PickRegion(Random random, SimulationSettings settings)
        {
            var roll = random.NextDouble() * settings.RegionWeights.Sum();
            for (var i = 0; i < settings.Regions.Length; i++)
            {
                roll -= settings.RegionWeights[i];
                if (roll < 0)
                    return settings.Regions[i];
            }
            return settings.Regions[settings.Regions.Length - 1];
        }

        // Box-Muller, good enough for a ladder-like distribution
        private static int NextMmr(Random random, SimulationSettings settings)
        {
            var u1 = 1.0 - random.NextDouble();
            var u2 = random.NextDouble();
            var normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            var mmr = (int)Math.Round(settings.MeanMmr + normal * settings.MmrDeviation);
            return Math.Max(0, Math.Min(5000, mmr));
        }

        private sealed class Arrival
        {
            public string PlayerId;
            public DateTimeOffset At;
            public string Region;
            public int Mmr;
        }
    }
}
