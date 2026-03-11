using StardewModdingAPI;

namespace FarmingCapitalist
{
    internal static class VerbosePriceTraceLogger
    {
        private static IMonitor? _monitor;

        public static bool Enabled { get; private set; }

        public static void Initialize(IMonitor monitor, bool enabled)
        {
            _monitor = monitor;
            Enabled = enabled;
        }

        public static void Log(string message, IMonitor? monitorOverride = null)
        {
            if (!Enabled)
                return;

            (monitorOverride ?? _monitor)?.Log(message, LogLevel.Trace);
        }
    }
}
