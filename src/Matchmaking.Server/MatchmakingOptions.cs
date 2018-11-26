using System;
using System.Collections.Generic;

namespace Matchmaking.Server
{
    public class MatchmakingOptions
    {
        public string Redis { get; set; } = "localhost:6379";

        public string KeyPrefix { get; set; } = "mm:";

        public string GrpcHost { get; set; } = "0.0.0.0";

        public int GrpcPort { get; set; } = 5050;

        public int TickMilliseconds { get; set; } = 1000;

        // how long a match assignment stays readable after it is created
        public int AssignmentTtlSeconds { get; set; } = 300;

        public List<string> Regions { get; set; } = new List<string>();

        public MatchRules Rules { get; set; } = new MatchRules();

        public void Validate()
        {
            if (Regions == null || Regions.Count == 0)
                throw new InvalidOperationException("Matchmaking:Regions is empty");
            if (TickMilliseconds <= 0)
                throw new InvalidOperationException("Matchmaking:TickMilliseconds must be positive");
            if (Rules.PlayersPerMatch < 2)
                throw new InvalidOperationException("Matchmaking:Rules:PlayersPerMatch must be at least 2");
            if (Rules.InitialTolerance < 0 || Rules.TolerancePerSecond < 0 || Rules.MaxTolerance < Rules.InitialTolerance)
                throw new InvalidOperationException("Matchmaking:Rules tolerances are not valid");
        }
    }
}
