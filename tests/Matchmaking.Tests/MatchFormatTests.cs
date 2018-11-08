using System;
using Matchmaking.Redis;
using Xunit;

namespace Matchmaking.Tests
{
    public class MatchFormatTests
    {
        [Fact]
        public void Parses_ticket()
        {
            var status = MatchFormat.ParseTicket("eu|1520|1541443630000");

            Assert.Equal(TicketState.Waiting, status.State);
            Assert.Equal("eu", status.Region);
            Assert.Equal(1520, status.Mmr);
            Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1541443630000), status.EnqueuedAt);
        }

        [Fact]
        public void Parses_match()
        {
            var match = MatchFormat.ParseMatch("7|na|1541443640000|p1:1500:1541443630000,p2:1580.0:1541443635000");

            Assert.Equal(7, match.Id);
            Assert.Equal("na", match.Region);
            Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1541443640000), match.CreatedAt);
            Assert.Equal(2, match.Players.Count);
            Assert.Equal("p2", match.Players[1].PlayerId);
            Assert.Equal(1580, match.Players[1].Mmr);
            Assert.Equal(80, match.MmrSpread);
        }

        [Fact]
        public void Rejects_broken_ticket()
        {
            Assert.Throws<FormatException>(() => MatchFormat.ParseTicket("eu|1500"));
        }

        [Theory]
        [InlineData("1|eu|0")]
        [InlineData("1|eu|0|p1:1500")]
        public void Rejects_broken_match(string value)
        {
            Assert.Throws<FormatException>(() => MatchFormat.ParseMatch(value));
        }
    }
}
