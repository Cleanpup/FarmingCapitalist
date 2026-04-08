using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace FarmingCapitalist.Workers;

internal static class WorkerNpcDrawPatch
{
    private static WorkerShellManager? workerShellManager;

    public static void Apply(Harmony harmony, WorkerShellManager manager)
    {
        workerShellManager = manager;

        harmony.Patch(
            original: AccessTools.Method(typeof(NPC), nameof(NPC.draw), new[] { typeof(SpriteBatch), typeof(float) }),
            prefix: new HarmonyMethod(typeof(WorkerNpcDrawPatch), nameof(BeforeDraw)));
    }

    private static bool BeforeDraw(NPC __instance, SpriteBatch b, float alpha)
    {
        return workerShellManager?.TryDrawCustomizedWorker(__instance, b, alpha) != true;
    }
}
