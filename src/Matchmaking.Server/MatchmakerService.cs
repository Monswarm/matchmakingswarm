using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Options;
using Matchmaking.Server.Protos;

namespace Matchmaking.Server
{
    public class MatchmakerService : Matchmaker.MatchmakerBase
    {
        // ids end up inside the redis payloads, keep them simple
        private static readonly Regex PlayerIdPattern = new Regex("^[A-Za-z0-9_-]{1,64}$", RegexOptions.Compiled);

        private readonly IMatchQueue _queue;
        private readonly IClock _clock;
        private readonly MatchmakingOptions _options;

        public MatchmakerService(IMatchQueue queue, IClock clock, IOptions<MatchmakingOptions> options)
        {
            _queue = queue;
            _clock = clock;
            _options = options.Value;
        }

        public override async Task<EnqueueReply> Enqueue(EnqueueRequest request, ServerCallContext context)
        {
            ValidatePlayerId(request.PlayerId);
            if (!_options.Regions.Contains(request.Region))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Unknown region " + request.Region));
            if (request.Mmr < 0)
                throw new RpcException(new Status(StatusCode.InvalidArgument, "mmr must be positive"));

            var queued = await _queue.EnqueueAsync(request.PlayerId, request.Region, request.Mmr, _clock.UtcNow);
            return new EnqueueReply { Queued = queued };
        }

        public override async Task<CancelReply> Cancel(CancelRequest request, ServerCallContext context)
        {
            ValidatePlayerId(request.PlayerId);
            var cancelled = await _queue.CancelAsync(request.PlayerId);
            return new CancelReply { Cancelled = cancelled };
        }

        public override async Task<StatusReply> GetStatus(StatusRequest request, ServerCallContext context)
        {
            ValidatePlayerId(request.PlayerId);
            var status = await _queue.GetStatusAsync(request.PlayerId);

            var reply = new StatusReply();
            switch (status.State)
            {
                case Matchmaking.TicketState.Waiting:
                    var waited = _clock.UtcNow - status.EnqueuedAt.Value;
                    reply.State = Protos.TicketState.Waiting;
                    reply.Region = status.Region;
                    reply.Mmr = status.Mmr;
                    reply.WaitingMs = (long)waited.TotalMilliseconds;
                    reply.SearchRange = (int)_options.Rules.ToleranceAfter(waited);
                    break;

                case Matchmaking.TicketState.Matched:
                    reply.State = Protos.TicketState.Matched;
                    reply.Region = status.Region;
                    reply.Mmr = status.Mmr;
                    reply.WaitingMs = (long)(status.Match.CreatedAt - status.EnqueuedAt.Value).TotalMilliseconds;
                    reply.Match = new MatchInfo { Id = status.Match.Id, Region = status.Match.Region };
                    reply.Match.Players.AddRange(status.Match.Players.Select(p => new MatchedPlayer { PlayerId = p.PlayerId, Mmr = p.Mmr }));
                    break;

                default:
                    reply.State = Protos.TicketState.NotFound;
                    break;
            }

            return reply;
        }

        private static void ValidatePlayerId(string playerId)
        {
            if (string.IsNullOrEmpty(playerId) || !PlayerIdPattern.IsMatch(playerId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid player_id"));
        }
    }
}
