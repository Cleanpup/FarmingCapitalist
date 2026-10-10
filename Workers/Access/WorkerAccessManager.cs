using StardewModdingAPI;
using StardewValley;

namespace HireSkilledHelpers.Workers;

/// <summary>Per-save permit, separate from the host's optional global bypass configuration.</summary>
internal sealed class WorkerAccessManager
{
    private readonly IModHelper helper;
    private readonly IMonitor monitor;
    private readonly string saveKey;
    private readonly string mirrorKey;
    private WorkerPermitState state = new();
    private bool bypassEvent;

    public WorkerAccessManager(IModHelper helper, IManifest manifest, IMonitor monitor, bool bypassEvent)
    {
        this.helper = helper;
        this.monitor = monitor;
        this.saveKey = manifest.UniqueID + ".WorkerPermit";
        this.mirrorKey = manifest.UniqueID + "/WorkerFeaturesUnlocked";
        this.bypassEvent = bypassEvent;
    }

    public bool IsUnlocked => WorkerUnlockPolicy.IsAvailable(Context.IsWorldReady, Context.IsMainPlayer,
        this.state.PermissionGranted, this.bypassEvent,
        Context.IsWorldReady && Game1.getFarm().modData.TryGetValue(this.mirrorKey, out string? value) && value == "true");

    public void Load()
    {
        this.state = Context.IsMainPlayer ? this.helper.Data.ReadSaveData<WorkerPermitState>(this.saveKey) ?? new() : new();
        this.Synchronize();
        this.Publish();
    }

    public void Synchronize()
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer) return;
        if (!this.state.PermissionGranted && Game1.MasterPlayer.eventsSeen.Contains(WorkerUnlockPolicy.EventId))
            this.GrantPermission();
        this.Publish();
    }

    public void GrantPermission()
    {
        // Native exitEvent records the marker before its fade/cleanup finishes. Wait
        // for eventFinished so reactivating the crew cannot spawn NPCs inside the scene.
        if (!Context.IsWorldReady || Game1.CurrentEvent is not null || Game1.eventUp || Game1.eventOver) return;
        if (!WorkerUnlockPolicy.TryGrantPermission(this.state, Context.IsWorldReady, Context.IsMainPlayer,
            Game1.MasterPlayer.eventsSeen.Contains(WorkerUnlockPolicy.EventId))) return;
        this.Save();
        this.monitor.Log("Mayor Lewis has approved hiring farmhands. Farm Crew is now unlocked for this farm.", LogLevel.Info);
    }

    public void SetBypass(bool bypassEvent)
    {
        this.bypassEvent = bypassEvent;
        this.Publish();
    }

    public void Save()
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer) return;
        this.helper.Data.WriteSaveData(this.saveKey, this.state);
        this.Publish();
    }

    public void Reset() => this.state = new();

    private void Publish()
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer) return;
        string value = this.state.PermissionGranted || this.bypassEvent ? "true" : "false";
        if (!Game1.getFarm().modData.TryGetValue(this.mirrorKey, out string? previous) || previous != value)
            Game1.getFarm().modData[this.mirrorKey] = value;
    }
}
