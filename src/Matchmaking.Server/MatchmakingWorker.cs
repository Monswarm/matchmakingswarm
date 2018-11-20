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
            var matches = await _ticker.TickAsync();
            foreach (var match in matches)
            {
                _logger.LogInformation("Match {MatchId} [{Region}] {Players} spread={Spread}",
                    match.Id, match.Region, string.Join(" ", match.Players.Select(p => p.PlayerId + "(" + p.Mmr + ")")), match.MmrSpread);
            }
        }
    }
}
