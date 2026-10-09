using System.Globalization;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;

namespace FarmingCapitalist.Workers;

/// <summary>Transient host action timing and mirrored work poses. Drawing never executes an action or vanilla callback.</summary>
internal sealed class WorkerWorkAnimationManager
{
    private const string DataKey = "Cleanpup.FarmingCapitalist/WorkAnimation";
    private sealed class Work
    {
        public string Signal = "";
        public WorkerWorkAnimationKind Kind;
        public int Facing;
        public GameLocation? Location;
        public string LocationName = "";
        public Vector2 Position;
        public double Elapsed;
        public bool Applied;
        public Func<bool>? IsValid;
        public Action? Impact;
        public Action? Completed;
    }
    private static WorkerWorkAnimationManager? registered;
    private readonly Dictionary<NPC, Work> active = new();
    private readonly WorkerShellManager shell;
    private readonly IMonitor monitor;
    private Farmer? renderer;
    private long sequence;

    public WorkerWorkAnimationManager(WorkerShellManager shell, IMonitor monitor) => (this.shell, this.monitor) = (shell, monitor);
    public void Register(Harmony harmony)
    {
        registered = this;
        harmony.Patch(AccessTools.Method(typeof(NPC), nameof(NPC.draw), new[] { typeof(SpriteBatch), typeof(float) }),
            prefix: new HarmonyMethod(typeof(WorkerWorkAnimationManager), nameof(BeforeDraw)));
    }
    public void Start(NPC worker, WorkerWorkAnimationKind kind, Point target, Func<bool> valid, Action impact)
    {
        if (!Context.IsMainPlayer || !Context.IsWorldReady) return;
        int facing = WorkerCombatSwingPolicy.FacingTowards(worker.StandingPixel.X, worker.StandingPixel.Y,
            target.X * 64 + 32, target.Y * 64 + 32, worker.FacingDirection);
        string signal = FormattableString.Invariant($"{++this.sequence}:{(int)kind}:{facing}:{worker.Position.X:R}:{worker.Position.Y:R}:{worker.currentLocation.NameOrUniqueName}");
        this.active[worker] = new() { Signal = signal, Kind = kind, Facing = facing, Location = worker.currentLocation, LocationName = worker.currentLocation.NameOrUniqueName,
            Position = worker.Position, IsValid = valid, Impact = impact };
        worker.modData[DataKey] = signal;
        this.ApplyPose(worker, this.active[worker]);
    }
    public void AfterCompletion(NPC worker, Action completed)
    {
        if (this.active.TryGetValue(worker, out Work? work)) work.Completed = completed;
    }
    public void Stop(string workerId)
    {
        foreach (NPC worker in this.active.Keys.Where(worker => this.shell.TryGetWorkerId(worker, out string id) && string.Equals(id, workerId, StringComparison.OrdinalIgnoreCase)).ToArray())
            this.Stop(worker);
    }
    private void Stop(NPC worker)
    {
        if (Context.IsMainPlayer) worker.modData.Remove(DataKey);
        if (this.active.Remove(worker, out Work? work) && worker.Sprite is not null)
        {
            worker.Sprite.StopAnimation();
            worker.Sprite.CurrentFrame = WorkerCombatSwingPolicy.DirectionRow(work.Facing) * 4;
        }
    }
    public void Reset()
    {
        foreach (NPC worker in this.active.Keys.ToArray()) this.Stop(worker);
        this.renderer = null;
    }
    public bool IsActive(NPC worker) => this.active.TryGetValue(worker, out Work? work)
        && work.Elapsed + .001 < WorkerWorkAnimationPolicy.DurationMilliseconds;

    public void Update()
    {
        if (!Context.IsWorldReady) return;
        HashSet<NPC> present = this.shell.GetSpawnedWorkers(recoverStale: false).ToHashSet();
        foreach (NPC worker in this.active.Keys.Where(worker => !present.Contains(worker)).ToArray()) this.Stop(worker);
        bool passing = Game1.shouldTimePass() && Game1.activeClickableMenu is null && Game1.CurrentEvent is null;
        if (!Context.IsMainPlayer)
        {
            foreach (NPC worker in present)
            {
                if (!worker.modData.TryGetValue(DataKey, out string? signal)) { this.Stop(worker); continue; }
                if (!this.active.TryGetValue(worker, out Work? work) || work.Signal != signal)
                {
                    string[] parts = signal.Split(':');
                    if (parts.Length != 6 || !int.TryParse(parts[1], out int kind) || !Enum.IsDefined(typeof(WorkerWorkAnimationKind), kind)
                        || !int.TryParse(parts[2], out int facing) || facing is < 0 or > 3
                        || !float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                        || !float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)
                        || !float.IsFinite(x) || !float.IsFinite(y)) continue;
                    this.active[worker] = work = new() { Signal = signal, Kind = (WorkerWorkAnimationKind)kind,
                        Facing = facing, Location = worker.currentLocation, LocationName = parts[5], Position = new(x, y) };
                }
                else if (passing) work.Elapsed += Game1.currentGameTime.ElapsedGameTime.TotalMilliseconds;
                // Retain completed signals to avoid replaying a delayed host update.
                if (work.LocationName == worker.currentLocation?.NameOrUniqueName && Vector2.DistanceSquared(work.Position, worker.Position) < 4 && this.IsActive(worker)) this.ApplyPose(worker, work);
                else if (worker.Sprite?.CurrentFrame >= (work.Kind == WorkerWorkAnimationKind.Scythe ? WorkerCombatSwingPolicy.FirstSheetFrame : WorkerWorkAnimationPolicy.FirstSheetFrame))
                    worker.Sprite.CurrentFrame = WorkerCombatSwingPolicy.DirectionRow(work.Facing) * 4;
            }
            return;
        }
        foreach ((NPC worker, Work work) in this.active.ToArray())
        {
            if (worker.currentLocation != work.Location || worker.Position != work.Position || worker.controller is not null
                || work.IsValid?.Invoke() != true) { this.Stop(worker); continue; }
            if (!passing) continue;
            work.Elapsed += Game1.currentGameTime.ElapsedGameTime.TotalMilliseconds;
            if (WorkerWorkAnimationPolicy.CanApplyImpact(work.Applied, work.Kind, work.Elapsed, Context.IsMainPlayer, Context.IsWorldReady, true))
            {
                work.Applied = true; // latch before touching crops/tools/storage, even if an action throws
                try { work.Impact?.Invoke(); }
                catch (Exception ex) { this.monitor.Log($"{worker.displayName} work action failed: {ex}", LogLevel.Error); this.Stop(worker); continue; }
            }
            if (!this.active.TryGetValue(worker, out Work? current) || current != work) continue; // impact may recall the worker
            if (!this.IsActive(worker))
            {
                this.Stop(worker);
                work.Completed?.Invoke();
            }
            else this.ApplyPose(worker, work);
        }
    }
    private void ApplyPose(NPC worker, Work work)
    {
        if (worker.Sprite is null) return;
        worker.FacingDirection = work.Facing;
        worker.Sprite.StopAnimation();
        worker.Sprite.CurrentFrame = WorkerWorkAnimationPolicy.SheetFrame(work.Kind, work.Facing, WorkerWorkAnimationPolicy.PoseAt(work.Kind, work.Elapsed));
    }
    private static void BeforeDraw(NPC __instance, SpriteBatch b, float alpha) => registered?.DrawTool(__instance, b, alpha);
    private void DrawTool(NPC worker, SpriteBatch batch, float alpha)
    {
        if (!Context.IsWorldReady || worker.IsInvisible || alpha <= 0 || worker.currentLocation != Game1.currentLocation
            || !this.active.TryGetValue(worker, out Work? work) || work.LocationName != worker.currentLocation.NameOrUniqueName
            || Vector2.DistanceSquared(work.Position, worker.Position) >= 4 || !this.IsActive(worker)) return;
        this.ApplyPose(worker, work);
        if (work.Kind == WorkerWorkAnimationKind.Gather) return;
        // Native drawing operates only on this private proxy/tool. It never calls
        // beginUsing/endUsing/DoFunction or animation callbacks, nor touches a farmer.
        if (batch != Game1.spriteBatch) return;
        this.renderer ??= new Farmer();
        Farmer proxy = this.renderer;
        if (proxy.Items.Count == 0) proxy.Items.Add(null);
        proxy.currentLocation = worker.currentLocation;
        proxy.Position = worker.Position;
        proxy.Position += new Vector2(0, worker.StandingPixel.Y - proxy.StandingPixel.Y);
        proxy.FacingDirection = work.Facing;
        if (work.Kind == WorkerWorkAnimationKind.Scythe)
        {
            // Use the same native slash placement and poses as combat, with the scythe blade.
            MeleeWeapon.drawDuringUse(WorkerCombatSwingPolicy.PoseAt(work.Elapsed), work.Facing, batch,
                worker.getLocalPosition(Game1.viewport) + new Vector2(0, 16), proxy, "(W)47", 3, false);
            return;
        }
        bool mining = work.Kind == WorkerWorkAnimationKind.Pickaxe
            && this.shell.TryGetWorkerId(worker, out string workerId)
            && this.shell.GetWorkerProfession(workerId) == WorkerProfession.Miner
            && this.shell.GetAssignedTask(workerId) == WorkerTaskKind.MineRocks;
        string toolId = work.Kind switch { WorkerWorkAnimationKind.Water => "(T)WateringCan", WorkerWorkAnimationKind.Axe => "(T)Axe",
            _ => mining ? "(T)SteelPickaxe" : "(T)Pickaxe" };
        if (proxy.CurrentTool?.QualifiedItemId != toolId) proxy.Items[0] = ItemRegistry.Create(toolId);
        proxy.CurrentToolIndex = 0;
        int pose = WorkerWorkAnimationPolicy.PoseAt(work.Kind, work.Elapsed);
        proxy.FarmerSprite.currentSingleAnimation = WorkerWorkAnimationPolicy.NativeAnimation(work.Kind, work.Facing);
        proxy.FarmerSprite.currentAnimationIndex = pose;
        proxy.FarmerSprite.CurrentFrame = 0; // body is already in the generated NPC sheet
        proxy.CurrentTool?.Update(work.Facing, pose, proxy);
        Game1.drawTool(proxy);
    }
}
