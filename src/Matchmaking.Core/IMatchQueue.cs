using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Matchmaking
{
    public interface IMatchQueue
    {
        /// <summary>Returns false if the player already has a ticket.</summary>
        Task<bool> EnqueueAsync(string playerId, string region, int mmr, DateTimeOffset now);

        Task<bool> CancelAsync(string playerId);

        Task<TicketStatus> GetStatusAsync(string playerId);

        Task<IReadOnlyList<Match>> FindMatchesAsync(string region, DateTimeOffset now, MatchRules rules);
    }
}
