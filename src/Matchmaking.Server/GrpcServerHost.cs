using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Matchmaking.Server.Protos;

namespace Matchmaking.Server
{
    public class GrpcServerHost : IHostedService
    {
        private readonly Grpc.Core.Server _server;
        private readonly ILogger<GrpcServerHost> _logger;
        private readonly MatchmakingOptions _options;

        public GrpcServerHost(MatchmakerService service, IOptions<MatchmakingOptions> options, ILogger<GrpcServerHost> logger)
        {
            _options = options.Value;
            _logger = logger;
            _server = new Grpc.Core.Server
            {
                Services = { Matchmaker.BindService(service) },
                Ports = { new ServerPort(_options.GrpcHost, _options.GrpcPort, ServerCredentials.Insecure) }
            };
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _server.Start();
            _logger.LogInformation("gRPC listening on {Host}:{Port}", _options.GrpcHost, _options.GrpcPort);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _server.ShutdownAsync();
        }
    }
}
