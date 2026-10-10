using HarmonyLib;
using StardewValley;

namespace HireSkilledHelpers.Workers;

/// <summary>Prevents vanilla tool actions performed for workers from crediting a farmer.</summary>
internal static class WorkerExperienceSuppression
{
    [ThreadStatic]
    private static int scopeDepth;

    public static void Register(Harmony harmony)
    {
        harmony.Patch(
            AccessTools.Method(typeof(Farmer), nameof(Farmer.gainExperience), new[] { typeof(int), typeof(int) }),
            prefix: new HarmonyMethod(typeof(WorkerExperienceSuppression), nameof(BeforeGainExperience)));
    }

    public static IDisposable BeginScope()
    {
        scopeDepth++;
        return new Scope();
    }

    private static bool BeforeGainExperience() => scopeDepth == 0;

    private sealed class Scope : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (this.disposed)
                return;
            this.disposed = true;
            scopeDepth--;
        }
    }
}
