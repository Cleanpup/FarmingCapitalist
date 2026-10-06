namespace FarmingCapitalist.Workers;

/// <summary>Safety conditions shared by job approaches, obstacle searches, and warp landings.</summary>
internal static class WorkerWorkTilePolicy
{
    public static bool CanStand(bool onMap, bool movementBlocked, bool npcBarrier, bool occupied, bool warp)
        => onMap && !movementBlocked && !npcBarrier && !occupied && !warp;
}
