using System;
using System.IO;
using System.Threading.Tasks;
using Matchmaking.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Matchmaking.Server
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.SetBasePath(Directory.GetCurrentDirectory());
                    config.AddJsonFile("appsettings.json", optional: true);
                    config.AddEnvironmentVariables("MATCHMAKING_");
                })
                .ConfigureLogging((context, logging) =>
                {
                    logging.AddConfiguration(context.Configuration.GetSection("Logging"));
                    logging.AddConsole();
                })
                .ConfigureServices((context, services) =>
                {
                    services.Configure<MatchmakingOptions>(context.Configuration.GetSection("Matchmaking"));

                    services.AddSingleton<IClock, SystemClock>();
                    services.AddSingleton<IConnectionMultiplexer>(sp =>
                        ConnectionMultiplexer.Connect(sp.GetRequiredService<IOptions<MatchmakingOptions>>().Value.Redis));
                    services.AddSingleton<IMatchQueue>(sp =>
                    {
                        var options = sp.GetRequiredService<IOptions<MatchmakingOptions>>().Value;
                        var redis = sp.GetRequiredService<IConnectionMultiplexer>();
                        return new RedisMatchQueue(redis.GetDatabase(), options.KeyPrefix, TimeSpan.FromSeconds(options.AssignmentTtlSeconds));
                    });

                    services.AddSingleton(sp =>
                    {
                        var options = sp.GetRequiredService<IOptions<MatchmakingOptions>>().Value;
                        return new MatchmakingTicker(sp.GetRequiredService<IMatchQueue>(), sp.GetRequiredService<IClock>(), options.Rules, options.Regions);
                    });

                    services.AddSingleton<MatchmakerService>();
                    services.AddHostedService<GrpcServerHost>();
                    services.AddHostedService<MatchmakingWorker>();
                })
                .Build();

            await host.RunAsync();
        }
    }
}
