using System.Text.Json;
using HireSkilledHelpers.Workers;

internal static class WorkerUnlockChecks
{
    private static void Check(bool value, string scenario)
    {
        if (!value) throw new Exception(scenario);
    }

    public static void Run()
    {
        bool[] values = { false, true };
        foreach (bool ready in values)
        foreach (bool host in values)
        foreach (bool permit in values)
        foreach (bool bypass in values)
        foreach (bool mirror in values)
        {
            bool actual = WorkerUnlockPolicy.IsAvailable(ready, host, permit, bypass, mirror);
            Check(actual == (ready && (host ? permit || bypass : mirror)),
                "Host permit/bypass controls access; farmhands use only the host mirror; title stays locked");
        }
        foreach (int day in new[] { -1, 0, 6, 7, 8, 27, 28, 111, 112, 500 })
        {
            Check(WorkerUnlockPolicy.MayOfferEvent(day, true, false, true, true, false) == (day >= 7),
                "Visit begins Spring 8 and catches up on missed days, seasons, years and existing saves");
            Check(!WorkerUnlockPolicy.MayOfferEvent(day, false, false, true, true, false), "Farmhands cannot trigger the event");
            Check(!WorkerUnlockPolicy.MayOfferEvent(day, true, true, true, true, false), "Permit/bypass prevents replay");
            Check(!WorkerUnlockPolicy.MayOfferEvent(day, true, false, false, true, false), "Visit waits for the farm");
            Check(!WorkerUnlockPolicy.MayOfferEvent(day, true, false, true, false, false), "Busy player cannot be interrupted");
            Check(!WorkerUnlockPolicy.MayOfferEvent(day, true, false, true, true, true), "Festival days defer the visit");
        }
        foreach (bool ready in values)
        foreach (bool host in values)
        foreach (bool seen in values)
        {
            var state = new WorkerPermitState();
            Check(WorkerUnlockPolicy.TryGrantPermission(state, ready, host, seen) == (ready && host && seen),
                "Only a completed native event on the host can grant a permit");
            Check(state.PermissionGranted == (ready && host && seen), "Rejected grants cannot mutate save state");
        }
        var granted = new WorkerPermitState();
        Check(WorkerUnlockPolicy.TryGrantPermission(granted, true, true, true), "Completed or skipped native event grants once");
        Check(!WorkerUnlockPolicy.TryGrantPermission(granted, true, true, true), "Repeated completion cannot grant twice");
        var reloaded = JsonSerializer.Deserialize<WorkerPermitState>(JsonSerializer.Serialize(granted))!;
        Check(reloaded.PermissionGranted && WorkerUnlockPolicy.IsAvailable(true, true, reloaded.PermissionGranted, false, false),
            "Per-farm permission survives reload and disabling bypass");
        var anotherFarm = new WorkerPermitState();
        Check(!anotherFarm.PermissionGranted, "A new farm does not inherit another farm's permit");
        Check(WorkerUnlockPolicy.IsAvailable(true, true, false, true, false)
            && !WorkerUnlockPolicy.IsAvailable(true, true, anotherFarm.PermissionGranted, false, false),
            "Bypass opens immediately but does not permanently grant a permit when turned off");
        Console.WriteLine("Worker unlock checks passed: Spring 8 catch-up, host/client authority, completion, persistence and reversible bypass.");
    }
}
