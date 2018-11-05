using System;
using System.Collections.Generic;
using System.Globalization;

namespace Matchmaking.Redis
{
    // Formats shared with the lua scripts:
    //   ticket: region|mmr|enqueuedMs
    //   match:  id|region|createdMs|player:mmr:enqueuedMs,player:mmr:enqueuedMs,...
    internal static class MatchFormat
    {
        public static TicketStatus ParseTicket(string value)
        {
            var parts = value.Split('|');
            if (parts.Length != 3)
                throw new FormatException("Invalid ticket: " + value);

            return TicketStatus.Waiting(parts[0], ParseInt(parts[1]), FromUnixMs(parts[2]));
        }

        public static Match ParseMatch(string value)
        {
            var parts = value.Split('|');
            if (parts.Length != 4)
                throw new FormatException("Invalid match: " + value);

            var players = new List<MatchPlayer>();
            foreach (var p in parts[3].Split(','))
            {
                var fields = p.Split(':');
                if (fields.Length != 3)
                    throw new FormatException("Invalid match player: " + p);

                players.Add(new MatchPlayer(fields[0], ParseInt(fields[1]), FromUnixMs(fields[2])));
            }

            return new Match(long.Parse(parts[0], CultureInfo.InvariantCulture), parts[1], FromUnixMs(parts[2]), players);
        }

        // lua may hand back numbers as floats ("1500.0") depending on how they were built
        private static int ParseInt(string s)
        {
            return (int)double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static DateTimeOffset FromUnixMs(string s)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds((long)double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture));
        }
    }
}
