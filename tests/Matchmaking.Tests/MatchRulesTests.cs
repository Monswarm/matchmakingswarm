using System;
using Xunit;

namespace Matchmaking.Tests
{
    public class MatchRulesTests
    {
        private readonly MatchRules _rules = new MatchRules
        {
            InitialTolerance = 50,
            TolerancePerSecond = 5,
            MaxTolerance = 300
        };

        [Fact]
        public void Starts_with_initial_tolerance()
        {
            Assert.Equal(50, _rules.ToleranceAfter(TimeSpan.Zero));
        }

        [Fact]
        public void Grows_per_second()
        {
            Assert.Equal(55, _rules.ToleranceAfter(TimeSpan.FromSeconds(1)));
            Assert.Equal(52.5, _rules.ToleranceAfter(TimeSpan.FromMilliseconds(500)));
            Assert.Equal(200, _rules.ToleranceAfter(TimeSpan.FromSeconds(30)));
        }

        [Fact]
        public void Is_capped()
        {
            Assert.Equal(300, _rules.ToleranceAfter(TimeSpan.FromMinutes(10)));
        }

        [Fact]
        public void Negative_wait_counts_as_zero()
        {
            Assert.Equal(50, _rules.ToleranceAfter(TimeSpan.FromSeconds(-3)));
        }
    }
}
