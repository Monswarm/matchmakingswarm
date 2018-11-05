using System;

namespace Matchmaking
{
    public class MatchRules
    {
        public int PlayersPerMatch { get; set; } = 2;

        // MMR window when the ticket is created
        public int InitialTolerance { get; set; } = 50;

        // how much the window grows for each second spent in queue
        public double TolerancePerSecond { get; set; } = 5;

        public int MaxTolerance { get; set; } = 500;

        public double ToleranceAfter(TimeSpan waited)
        {
            if (waited < TimeSpan.Zero)
                waited = TimeSpan.Zero;

            return Math.Min(InitialTolerance + TolerancePerSecond * waited.TotalSeconds, MaxTolerance);
        }
    }
}
