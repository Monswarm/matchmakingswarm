using System;

namespace Matchmaking.Tests
{
    public sealed class ManualClock : IClock
    {
        public ManualClock(DateTimeOffset start)
        {
            UtcNow = start;
        }

        public DateTimeOffset UtcNow { get; private set; }

        public void Advance(TimeSpan by)
        {
            if (by < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(by), "The clock cannot go back");
            UtcNow += by;
        }

        public void AdvanceTo(DateTimeOffset time)
        {
            Advance(time - UtcNow);
        }
    }
}
