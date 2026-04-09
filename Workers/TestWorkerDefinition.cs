using Microsoft.Xna.Framework;

namespace FarmingCapitalist.Workers;

internal static class TestWorkerDefinition
{
    public const string WorkerId = "test-worker";
    public const string InternalName = "FC.TestWorker";
    public const string DisplayName = "Test Worker";
    public const string LocationName = "FarmHouse";
    public static readonly Point SpawnTile = new(5, 7);
    public const int FacingDirection = 2;
    public const string InitialTravelLocationName = "Farm";
    public static readonly Point InitialTravelTile = new(60, 20);
    public const int InitialTravelFacingDirection = 2;

    // These are only fallback assets for the worker shell. Once an appearance is configured, the mod
    // generates a proper runtime NPC sprite sheet from that farmer-style appearance instead.
    public const string ShellSpriteAssetName = "Characters\\Fizz";
    public const string ShellPortraitAssetName = "Portraits\\Fizz";
}
