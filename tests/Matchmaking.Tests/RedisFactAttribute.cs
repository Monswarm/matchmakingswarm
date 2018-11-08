using System;
using Xunit;

namespace Matchmaking.Tests
{
    // Integration tests only run when MATCHMAKING_TEST_REDIS points to a server (e.g. localhost:6379)
    public sealed class RedisFactAttribute : FactAttribute
    {
        public const string Variable = "MATCHMAKING_TEST_REDIS";

        public RedisFactAttribute()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Variable)))
                Skip = "Set " + Variable + " to run redis tests";
        }
    }
}
