using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Matchmaking
{
    // One matchmaking pass over every region using the time of the injected clock.
    public class MatchmakingTicker
    {
        private readonly IMatchQueue _queue;
        private readonly IClock _clock;
        private readonly MatchRules _rules;
        private readonly IReadOnlyList<string> _regions;

        public MatchmakingTicker(IMatchQueue queue, IClock clock, MatchRules rules, IEnumerable<string> regions)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _regions = regions.ToList();
        }

        public IReadOnlyList<string> Regions => _regions;

        public async Task<IReadOnlyList<Match>> TickAsync()
        {
            var now = _clock.UtcNow;
            var matches = new List<Match>();
            foreach (var region in _regions)
                matches.AddRange(await _queue.FindMatchesAsync(region, now, _rules).ConfigureAwait(false));

            return matches;
        }
    }
}
