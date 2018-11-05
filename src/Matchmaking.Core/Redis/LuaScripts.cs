using System.IO;

namespace Matchmaking.Redis
{
    internal static class LuaScripts
    {
        public static readonly string Enqueue = Load("enqueue.lua");
        public static readonly string Cancel = Load("cancel.lua");
        public static readonly string Match = Load("match.lua");

        private static string Load(string name)
        {
            var assembly = typeof(LuaScripts).Assembly;
            using (var stream = assembly.GetManifestResourceStream("Matchmaking.Redis.Scripts." + name))
            {
                if (stream == null)
                    throw new FileNotFoundException("Missing embedded script " + name);

                using (var reader = new StreamReader(stream))
                    return reader.ReadToEnd();
            }
        }
    }
}
