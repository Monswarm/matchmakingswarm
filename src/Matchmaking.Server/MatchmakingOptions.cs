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
    }
}
