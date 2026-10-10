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

    private const string ObstacleReportAckMessageType = "WorkerObstacleReportAck";

    public sealed class ObstacleReportAck
    {
        public string WorkerId { get; set; } = string.Empty;

        public string ReportId { get; set; } = string.Empty;
    }

    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly WorkerShellManager workerShellManager;
    private readonly string modId;
    private readonly HashSet<string> locallyAcknowledgedReports = new(StringComparer.Ordinal);
    private bool assetFailureLogged;

    public WorkerDialogueManager(IModHelper helper, WorkerShellManager workerShellManager, IMonitor monitor, string modId)
    {
        this.helper = helper;
        this.workerShellManager = workerShellManager;
        this.monitor = monitor;
        this.modId = modId;
    }

    public void Register(Harmony harmony)
    {
        instance = this;
        this.helper.Events.Content.AssetRequested += this.OnAssetRequested;
        this.helper.Events.Multiplayer.ModMessageReceived += this.OnModMessageReceived;
        harmony.Patch(
            AccessTools.Method(typeof(NPC), nameof(NPC.checkAction)),
            prefix: new HarmonyMethod(typeof(WorkerDialogueManager), nameof(BeforeNpcCheckAction)));
    }

    public void Reset()
    {
        this.assetFailureLogged = false;
        this.locallyAcknowledgedReports.Clear();
    }

    private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer || e.FromModID != this.modId
            || e.Type != ObstacleReportAckMessageType)
            return;

        ObstacleReportAck ack = e.ReadAs<ObstacleReportAck>();
        this.workerShellManager.TryAcknowledgeObstacleReport(ack.WorkerId, ack.ReportId);
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
        if (!Context.IsWorldReady || !this.workerShellManager.IsFeatureUnlocked
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
        bool blockedMonster = task == WorkerTaskKind.SlayMonsters
            && worker.modData.TryGetValue(WorkerCombatManager.BlockedMonsterDataKey, out string? blockedLocation)
            && string.Equals(blockedLocation, location.NameOrUniqueName, StringComparison.Ordinal);
        WorkerObstacleReport? report = this.workerShellManager.GetPendingObstacleReport(workerId);
        if (report is not null && this.locallyAcknowledgedReports.Contains(report.Id))
            report = null;
        (string key, string text) = blockedMonster
            ? ("BlockedMonster", "I see a monster but I can't get to it!")
            : report is not null
                ? ($"ObstacleReport:{report.Id}", report.Message)
                : this.GetDialogueLine(workerId, task);
        string resolvedText = text
            .Replace("{{workerName}}", worker.displayName, StringComparison.Ordinal)
            .Replace("{{task}}", WorkerTaskPolicy.GetTaskLabel(task), StringComparison.Ordinal);

        Dialogue dialogue = new(worker, $"{DialogueAssetName}:{key}", resolvedText);
        worker.CurrentDialogue.Clear();
        worker.CurrentDialogue.Push(dialogue);
        Game1.drawDialogue(worker);
        if (report is not null && !blockedMonster)
        {
            this.locallyAcknowledgedReports.Add(report.Id);
            if (Context.IsMainPlayer)
                this.workerShellManager.TryAcknowledgeObstacleReport(workerId, report.Id);
            else
                this.helper.Multiplayer.SendMessage(new ObstacleReportAck { WorkerId = workerId, ReportId = report.Id },
                    ObstacleReportAckMessageType, modIDs: new[] { this.modId });
        }
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
        if (this.workerShellManager.GetWorkerProfession(workerId) == WorkerProfession.Fisher)
        {
            string area = this.workerShellManager.GetFishingArea(workerId);
            string context = task == WorkerTaskKind.Fish ? $"Fisher.{area}." : "Fisher.Idle.";
            if (dialogue is not null)
            {
                KeyValuePair<string, string>[] lines = dialogue.Where(pair => pair.Key.StartsWith(context, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(pair.Value)).OrderBy(pair => pair.Key, StringComparer.Ordinal).ToArray();
                if (lines.Length > 0)
                {
                    int index = (int)((GetStableWorkerOffset(workerId) + (uint)Game1.Date.TotalDays) % (uint)lines.Length);
                    return (lines[index].Key, lines[index].Value);
                }
            }
            return ("FisherFallback", task == WorkerTaskKind.Fish ? "I'll keep casting while the fish are biting." : "My rod's ready whenever you need me.");
        }
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
