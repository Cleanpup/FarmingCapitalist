using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;

namespace HireSkilledHelpers.Workers;

/// <summary>Replays the host's swing signal locally; drawing never invokes tool or damage logic.</summary>
internal sealed class WorkerCombatAnimationManager
{
    private const string SwingDataKey = "Cleanpup.FarmingCapitalist/CombatSwing";
    private sealed class Swing
    {
        public string Signal = "";
        public int Facing;
        public GameLocation Location = null!;
        public double Elapsed;
    }

    private static WorkerCombatAnimationManager? registered;
    private readonly Dictionary<NPC, Swing> swings = new();
    private readonly WorkerShellManager shell;
    private Farmer? weaponRenderer;
    private long sequence;

    public WorkerCombatAnimationManager(WorkerShellManager shell) => this.shell = shell;

    public void Register(Harmony harmony)
    {
        registered = this;
        harmony.Patch(AccessTools.Method(typeof(NPC), nameof(NPC.draw), new[] { typeof(SpriteBatch), typeof(float) }),
            prefix: new HarmonyMethod(typeof(WorkerCombatAnimationManager), nameof(BeforeNpcDraw)));
    }

    public void Start(NPC worker, int facing)
    {
        // Character.faceDirection ignores SimpleNonVillagerNPC workers. Capture
        // the chosen direction explicitly, then ApplyPose sets it for every pose.
        string signal = $"{++this.sequence}:{facing}";
        this.swings[worker] = new Swing { Signal = signal, Facing = facing, Location = worker.currentLocation };
        worker.modData[SwingDataKey] = signal;
        this.ApplyPose(worker, this.swings[worker]);
        worker.currentLocation.localSound("swordswipe");
    }

    public void SetElapsed(NPC worker, double elapsed)
    {
        if (this.swings.TryGetValue(worker, out Swing? swing))
        {
            swing.Elapsed = elapsed;
            this.ApplyPose(worker, swing);
        }
    }

    public void Stop(NPC worker)
    {
        if (Context.IsMainPlayer)
            worker.modData.Remove(SwingDataKey);
        if (this.swings.Remove(worker, out Swing? swing) && worker.Sprite is not null)
        {
            worker.Sprite.StopAnimation();
            worker.Sprite.CurrentFrame = WorkerCombatSwingPolicy.DirectionRow(swing.Facing) * 4;
        }
    }

    public void Reset()
    {
        foreach (NPC worker in this.swings.Keys.ToArray())
            this.Stop(worker);
        this.weaponRenderer = null;
    }

    public void UpdateClients()
    {
        if (!Context.IsWorldReady || Context.IsMainPlayer)
            return;
        HashSet<NPC> present = this.shell.GetSpawnedWorkers(recoverStale: false).ToHashSet();
        foreach (NPC worker in this.swings.Keys.Where(worker => !present.Contains(worker)).ToArray())
            this.Stop(worker);
        foreach (NPC worker in present)
        {
            if (!this.shell.TryGetWorkerId(worker, out string id)
                || this.shell.GetWorkerProfession(id) != WorkerProfession.CombatWorker
                || this.shell.GetAssignedTask(id) != WorkerTaskKind.SlayMonsters
                || !worker.modData.TryGetValue(SwingDataKey, out string? signal))
            {
                this.Stop(worker);
                continue;
            }
            if (!this.swings.TryGetValue(worker, out Swing? swing) || swing.Signal != signal)
            {
                string[] parts = signal.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[1], out int facing) || facing is < 0 or > 3)
                    continue;
                swing = new Swing { Signal = signal, Facing = facing, Location = worker.currentLocation };
                this.swings[worker] = swing;
            }
            else if (Game1.shouldTimePass())
                swing.Elapsed += Game1.currentGameTime.ElapsedGameTime.TotalMilliseconds;
            // Retain a completed signal until the host changes/removes it, so a
            // delayed network update can't start the same swing a second time.
            if (swing.Location == worker.currentLocation && swing.Elapsed < WorkerCombatSwingPolicy.DurationMilliseconds)
                this.ApplyPose(worker, swing);
            else if (worker.Sprite?.CurrentFrame >= WorkerCombatSwingPolicy.FirstSheetFrame)
                worker.Sprite.CurrentFrame = WorkerCombatSwingPolicy.DirectionRow(swing.Facing) * 4;
        }
    }

    private void ApplyPose(NPC worker, Swing swing)
    {
        if (worker.Sprite is null || worker.Sprite.Texture.Height < 320)
            return;
        worker.FacingDirection = swing.Facing;
        worker.Sprite.StopAnimation();
        worker.Sprite.CurrentFrame = WorkerCombatSwingPolicy.SheetFrame(swing.Facing,
            WorkerCombatSwingPolicy.PoseAt(swing.Elapsed));
    }

    private static void BeforeNpcDraw(NPC __instance, SpriteBatch b, float alpha)
        => registered?.DrawSword(__instance, b, alpha);

    private void DrawSword(NPC worker, SpriteBatch batch, float alpha)
    {
        if (!Context.IsWorldReady || worker.IsInvisible || alpha <= 0
            || worker.currentLocation != Game1.currentLocation
            || !this.swings.TryGetValue(worker, out Swing? swing)
            || swing.Location != worker.currentLocation || swing.Elapsed >= WorkerCombatSwingPolicy.DurationMilliseconds)
            return;
        this.ApplyPose(worker, swing);
        this.weaponRenderer ??= new Farmer();
        Farmer renderer = this.weaponRenderer;
        renderer.currentLocation = worker.currentLocation;
        renderer.FacingDirection = swing.Facing;
        renderer.Position = worker.Position;
        // Match the NPC's world depth without changing its position/collision box.
        renderer.Position += new Vector2(0, worker.StandingPixel.Y - renderer.StandingPixel.Y);
        MeleeWeapon.drawDuringUse(WorkerCombatSwingPolicy.PoseAt(swing.Elapsed), swing.Facing, batch,
            worker.getLocalPosition(Game1.viewport) + new Vector2(0, 16), renderer, "(W)0", 3, false);
    }
}
