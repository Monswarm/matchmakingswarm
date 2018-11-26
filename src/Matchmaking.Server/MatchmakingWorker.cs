using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Matchmaking.Server
{
    public class MatchmakingWorker : BackgroundService
    {
        private readonly MatchmakingTicker _ticker;
        private readonly MatchmakingOptions _options;
        private readonly ILogger<MatchmakingWorker> _logger;

        public MatchmakingWorker(MatchmakingTicker ticker, IOptions<MatchmakingOptions> options, ILogger<MatchmakingWorker> logger)
        {
            _ticker = ticker;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var interval = TimeSpan.FromMilliseconds(_options.TickMilliseconds);
            _logger.LogInformation("Matchmaking every {Interval} ms for regions {Regions}", interval.TotalMilliseconds, string.Join(",", _ticker.Regions));

            var watch = new Stopwatch();
            while (!stoppingToken.IsCancellationRequested)
            {
                watch.Restart();
                try
                {
                    await TickAsync();
                }
                catch (Exception ex)
                {
                    // an exception escaping ExecuteAsync stops the worker without any log
                    _logger.LogError(ex, "Matchmaking tick failed");
                }

                // keep the cadence: the next tick starts one interval after this one started
                var wait = interval - watch.Elapsed;
                if (wait < TimeSpan.Zero)
                {
                    _logger.LogWarning("Matchmaking tick took {Elapsed} ms, longer than the interval", watch.ElapsedMilliseconds);
                    wait = TimeSpan.Zero;
                }

                try
                {
                    await Task.Delay(wait, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private async Task TickAsync()
        {
            var matches = await _ticker.TickAsync();
            foreach (var match in matches)
            {
                _logger.LogInformation("Match {MatchId} [{Region}] {Players} spread={Spread}",
                    match.Id, match.Region, string.Join(" ", match.Players.Select(p => p.PlayerId + "(" + p.Mmr + ")")), match.MmrSpread);
            }
        }
    }
}
