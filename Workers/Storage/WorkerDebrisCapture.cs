using System.Collections.Generic;
using System.Linq;

namespace HireSkilledHelpers.Workers;

internal static class WorkerDebrisCapture
{
    public static HashSet<T> CaptureNew<T>(IReadOnlySet<T> before, IEnumerable<T> after) where T : class
        => after.Where(item => !before.Contains(item)).ToHashSet();

    public static bool BelongsToZone<T>(T item, IReadOnlySet<T> before, IReadOnlySet<T>? captured, bool near)
        where T : class
        => captured is not null ? captured.Contains(item) : !before.Contains(item) && near;
}
