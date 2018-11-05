using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Matchmaking.Server
{
    public class MatchmakingWorker : BackgroundService
    {
        private readonly IMatchQueue _queue;
        private readonly IClock _clock;
        private readonly MatchmakingOptions _options;
        private readonly ILogger<MatchmakingWorker> _logger;

        public MatchmakingWorker(IMatchQueue queue, IClock clock, IOptions<MatchmakingOptions> options, ILogger<MatchmakingWorker> logger)
        {
            _queue = queue;
            _clock = clock;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromMilliseconds(_options.TickMilliseconds);
            _logger.LogInformation("Matchmaking every {Interval} ms for regions {Regions}", interval.TotalMilliseconds, string.Join(",", _options.Regions));

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await TickAsync();
                }
                catch (RedisException ex)
                {
                    _logger.LogError(ex, "Matchmaking tick failed");
                }

                try
                {
                    await Task.Delay(interval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task TickAsync()
        {
            var now = _clock.UtcNow;
            foreach (var region in _options.Regions)
            {
                var matches = await _queue.FindMatchesAsync(region, now, _options.Rules);
                foreach (var match in matches)
                {
                    _logger.LogInformation("Match {MatchId} [{Region}] {Players} spread={Spread}",
                        match.Id, match.Region, string.Join(" ", match.Players.Select(p => p.PlayerId + "(" + p.Mmr + ")")), match.MmrSpread);
                }
            }
        }
    }
}
