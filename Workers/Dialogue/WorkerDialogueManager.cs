using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace FarmingCapitalist.Workers;

/// <summary>Provides normal portrait dialogue for managed workers without turning their custom shells into vanilla villagers.</summary>
internal sealed class WorkerDialogueManager
{
    public const string DialogueAssetName = "Mods/Cleanpup.FarmingCapitalist/WorkerDialogue";

    private const string DefaultDialoguePath = "assets/worker-dialogue.json";
    private static WorkerDialogueManager? instance;

    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly WorkerShellManager workerShellManager;
    private bool assetFailureLogged;

    public WorkerDialogueManager(IModHelper helper, WorkerShellManager workerShellManager, IMonitor monitor)
    {
        this.helper = helper;
        this.workerShellManager = workerShellManager;
        this.monitor = monitor;
    }

    public void Register(Harmony harmony)
    {
        instance = this;
        this.helper.Events.Content.AssetRequested += this.OnAssetRequested;
        harmony.Patch(
            AccessTools.Method(typeof(NPC), nameof(NPC.checkAction)),
            prefix: new HarmonyMethod(typeof(WorkerDialogueManager), nameof(BeforeNpcCheckAction)));
    }

    public void Reset()
    {
        this.assetFailureLogged = false;
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(DialogueAssetName))
        {
            e.LoadFromModFile<Dictionary<string, string>>(DefaultDialoguePath, AssetLoadPriority.Medium);
        }
    }

    private static bool BeforeNpcCheckAction(NPC __instance, Farmer who, GameLocation l, ref bool __result)
    {
        if (instance is null || !instance.TryHandleDialogue(__instance, who, l))
        {
            return true;
        }

        __result = true;
        return false;
    }

    private bool TryHandleDialogue(NPC worker, Farmer who, GameLocation location)
    {
        if (!Context.IsWorldReady
            || !who.IsLocalPlayer
            || !who.CanMove
            || Game1.dialogueUp
            || Game1.activeClickableMenu is not null
            || Game1.CurrentEvent is not null
            || worker.IsInvisible
            || worker.currentLocation != location
            || !this.workerShellManager.TryGetWorkerId(worker, out string workerId))
        {
            return false;
        }

        WorkerTaskKind task = this.workerShellManager.GetAssignedTask(workerId);
        (string key, string text) = this.GetDialogueLine(workerId, task);
        string resolvedText = text
            .Replace("{{workerName}}", worker.displayName, StringComparison.Ordinal)
            .Replace("{{task}}", WorkerTaskPolicy.GetTaskLabel(task), StringComparison.Ordinal);

        Dialogue dialogue = new(worker, $"{DialogueAssetName}:{key}", resolvedText);
        worker.CurrentDialogue.Clear();
        worker.CurrentDialogue.Push(dialogue);
        Game1.drawDialogue(worker);
        return true;
    }

    private (string Key, string Text) GetDialogueLine(string workerId, WorkerTaskKind task)
    {
        Dictionary<string, string>? dialogue = null;
        try
        {
            dialogue = this.helper.GameContent.Load<Dictionary<string, string>>(DialogueAssetName);
        }
        catch (Exception ex)
        {
            if (!this.assetFailureLogged)
            {
                this.monitor.Log($"Worker dialogue couldn't be loaded; using the built-in fallback. {ex.Message}", LogLevel.Warn);
                this.assetFailureLogged = true;
            }
        }

        string taskKey = task.ToString();
        string[] prefixes =
        {
            $"{workerId}.{taskKey}.",
            $"{workerId}.Default.",
            $"{taskKey}.",
            "Default.",
        };

        if (dialogue is not null)
        {
            foreach (string prefix in prefixes)
            {
                KeyValuePair<string, string>[] candidates = dialogue
                    .Where(pair => pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(pair.Value))
                    .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (candidates.Length == 0)
                {
                    continue;
                }

                int index = (int)((GetStableWorkerOffset(workerId) + (uint)Game1.Date.TotalDays) % (uint)candidates.Length);
                return (candidates[index].Key, candidates[index].Value);
            }
        }

        return ("Fallback", "I'm ready when you need me.");
    }

    private static uint GetStableWorkerOffset(string workerId)
    {
        const uint fnvOffset = 2166136261;
        const uint fnvPrime = 16777619;
        uint hash = fnvOffset;
        foreach (char character in workerId.ToUpperInvariant())
        {
            hash ^= character;
            hash *= fnvPrime;
        }

        return hash;
    }
}
