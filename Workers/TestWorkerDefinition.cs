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

    // These are only fallback assets for the worker shell. The real visual comes from farmer-style
    // layered rendering once the player customizes the worker through the command flow.
    public const string ShellSpriteAssetName = "Characters\\Fizz";
    public const string ShellPortraitAssetName = "Portraits\\Fizz";
}
