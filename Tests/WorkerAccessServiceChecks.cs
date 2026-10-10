using System.Text.Json;
using FarmingCapitalist.Workers;
using StardewModdingAPI;
using StardewValley;

internal static class WorkerAccessServiceChecks
{
    private const string MirrorKey = "Test.Workers/WorkerFeaturesUnlocked";
    private const string SaveKey = "Test.Workers.WorkerPermit";
    private static void Check(bool value, string scenario)
    {
        if (!value) throw new Exception(scenario);
    }
    public static void Run()
    {
        var helper = new AccessTestHelper();
        Context.IsMainPlayer = true;
        Context.IsWorldReady = false;
        var access = new WorkerAccessManager(helper, helper, helper, bypassEvent: false);
        Check(!access.IsUnlocked, "Title access is safe without a live farm");
        access.Synchronize(); access.Save(); access.GrantPermission(); access.SetBypass(true);
        Check(helper.Writes == 0 && !access.IsUnlocked, "Title callbacks cannot write saves or open features");
        access.SetBypass(false);
        Context.IsWorldReady = true;
        Game1.TestFarm = new(); Game1.MasterPlayer = new();
        access.Load();
        Check(!access.IsUnlocked && Game1.getFarm().modData[MirrorKey] == "false", "New farm starts locked and publishes locked state");
        access.GrantPermission();
        Check(!access.IsUnlocked && helper.Writes == 0, "Starting or interrupting the event cannot grant permission");
        Game1.MasterPlayer.eventsSeen.Add(WorkerUnlockPolicy.EventId);
        Game1.CurrentEvent = new object(); Game1.eventUp = true;
        access.Synchronize();
        Check(!access.IsUnlocked && helper.Writes == 0, "Native end marker cannot activate workers before scene cleanup");
        Game1.CurrentEvent = null; Game1.eventUp = false; Game1.eventOver = true;
        access.Synchronize();
        Check(!access.IsUnlocked, "Scene exit fade does not activate workers yet");
        Game1.eventOver = false;
        access.Synchronize();
        Check(access.IsUnlocked && Game1.getFarm().modData[MirrorKey] == "true" && helper.Writes == 1,
            "Completed native event grants, saves and mirrors permission");
        access.GrantPermission(); access.Synchronize();
        Check(helper.Writes == 1, "Repeated ticks and completion cannot rewrite the grant");
        access.Reset(); access.Load(); access.SetBypass(false);
        Check(access.IsUnlocked, "Reload retains actual permission independently of bypass");

        var secondHelper = new AccessTestHelper();
        var second = new WorkerAccessManager(secondHelper, secondHelper, secondHelper, bypassEvent: false);
        Game1.TestFarm = new(); Game1.MasterPlayer = new();
        second.Load();
        Check(!second.IsUnlocked, "Switching farms cannot inherit the previous permit");
        second.SetBypass(true); second.Save();
        Check(second.IsUnlocked && Game1.getFarm().modData[MirrorKey] == "true"
            && !JsonSerializer.Deserialize<WorkerPermitState>(secondHelper.Store[SaveKey])!.PermissionGranted
            && !Game1.MasterPlayer.eventsSeen.Contains(WorkerUnlockPolicy.EventId),
            "Bypass enables/mirrors access without inventing event completion");
        second.SetBypass(false);
        Check(!second.IsUnlocked && Game1.getFarm().modData[MirrorKey] == "false", "Turning bypass off relocks an unapproved farm");

        Context.IsMainPlayer = false;
        int writes = secondHelper.Writes;
        var client = new WorkerAccessManager(secondHelper, secondHelper, secondHelper, bypassEvent: true);
        client.Load(); client.SetBypass(true); client.GrantPermission(); client.Synchronize(); client.Save();
        Check(!client.IsUnlocked && secondHelper.Writes == writes && Game1.getFarm().modData[MirrorKey] == "false",
            "Farmhand config and callbacks cannot open or write a locked farm");
        Game1.getFarm().modData[MirrorKey] = "true";
        client.SetBypass(false);
        Check(client.IsUnlocked && secondHelper.Writes == writes, "Farmhand access follows host unlock even with local bypass disabled");
        Game1.getFarm().modData[MirrorKey] = "false";
        Check(!client.IsUnlocked, "Host relock immediately affects farmhands");
        Context.IsWorldReady = false;
        client.Reset();
        Check(!client.IsUnlocked, "Title reset clears access without touching a nonexistent farm");
        Console.WriteLine("Worker access service checks passed: live host mirror, actual saved permission, farm changes, bypass and client isolation.");
    }
}
