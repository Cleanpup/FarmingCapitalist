using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;

namespace HireSkilledHelpers.Workers;

/// <summary>Copies legacy SMAPI save entries into the standalone mod namespace.</summary>
internal static class WorkerSaveMigration
{
    internal static void Migrate()
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer) return;
        Copy(Game1.CustomData);
        if (SaveGame.loaded is not null) Copy(SaveGame.loaded.CustomData);
    }

    internal static void Copy(IDictionary<string, string> data)
    {
        const string oldPrefix = "smapi/mod-data/cleanpup.farmingcapitalist/";
        const string newPrefix = "smapi/mod-data/cleanpup.hireskilledhelpers/";
        foreach (var entry in data.Where(p => p.Key.StartsWith(oldPrefix, StringComparison.Ordinal)).ToArray())
        {
            string target = newPrefix + entry.Key.Substring(oldPrefix.Length);
            if (!data.ContainsKey(target)) data[target] = entry.Value;
        }
    }
}
