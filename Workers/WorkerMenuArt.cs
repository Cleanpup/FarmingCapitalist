using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace FarmingCapitalist.Workers;

/// <summary>Small pixel motifs for the worker ledger, drawn from a fixed palette at whole-pixel scale.</summary>
internal static class WorkerMenuArt
{
    internal enum Icon { Sprout, Water, Harvest, Tend, Home, Coin, Ledger, Chest }

    private static readonly string[] Sprout =
    {
        "............", "....gg......", "...gllg.gg..", "..gllllgllg.",
        "..gllllllg..", "...gllgg....", ".....s......", ".....s......",
        ".....s......", "....sss.....", "...ooooo....", "............",
    };

    private static readonly string[] Water =
    {
        "............", "......ooo...", ".....obbo...", "....obbbbo..",
        "...obbbhbo..", "..obbbbbbo..", ".obbbbbbbbbo", "obbbbbbbbbo.",
        "obbbbbbbbo..", ".oooooooo...", ".........b..", "........bbb.",
    };

    private static readonly string[] Harvest =
    {
        ".....o......", "....oyo.....", "...oyyyo....", ".....s......",
        "..o..s..o...", ".oyo.s.oyo..", "oyyyosoyyyo.", "..s..s..s...",
        "..s..s..s...", "..s..s..s...", ".oooooooooo.", "............",
    };

    private static readonly string[] Tend =
    {
        ".......bbb..", "......bbhb..", ".....bbb....", "....bbb.....",
        "...bbb..gg..", "..bbb..gllg.", ".bbb....gg..", "........s...",
        "........s...", "...oooooooo.", "...oyyyyyyo.", "...oooooooo.",
    };

    private static readonly string[] Home =
    {
        ".....oo.....", "....oyyo....", "...oyyyyo...", "..oyyyyyyo..",
        ".oyyyyyyyyo.", "oooooooooooo", ".owwwwwwwwo.", ".owwwwwwwwo.",
        ".owwwoowwwo.", ".owwwoowwwo.", ".oooooooooo.", "............",
    };

    private static readonly string[] Coin =
    {
        "....oooo....", "..ooyyyyo...", ".oyyyyyyyyo.", ".oyyhyyhyyo.",
        "oyyhyyyyhyyo", "oyyyhyyhyyyo", "oyyyhyyhyyyo", "oyyhyyyyhyyo",
        ".oyyhyyhyyo.", ".oyyyyyyyyo.", "..ooyyyyo...", "....oooo....",
    };

    private static readonly string[] Ledger =
    {
        "..oooooooo..", "..owwwwwwo..", "..owoooooo..", "..owwwwwwo..",
        "..owoooooo..", "..owwwwwwo..", "..owoooooo..", "..owwwwwwo..",
        "..owoooooo..", "..owwwwwwo..", "..oooooooo..", "............",
    };

    private static readonly string[] Chest =
    {
        "............", ".oooooooooo.", ".oyyyyyyyyo.", ".oyyyyyyyyo.",
        ".oooooooooo.", ".oyyyyyyyyo.", ".oyyyoooyyo.", ".oyyyoooyyo.",
        ".oyyyyyyyyo.", ".oyyyyyyyyo.", ".oooooooooo.", "............",
    };

    private static readonly Color Ink = new(91, 48, 30);
    private static readonly Color Leaf = new(70, 119, 54);
    private static readonly Color LeafLight = new(151, 182, 77);
    private static readonly Color Stem = new(131, 91, 44);
    private static readonly Color Blue = new(70, 132, 168);
    private static readonly Color Highlight = new(168, 219, 230);
    private static readonly Color Gold = new(221, 160, 53);
    private static readonly Color Paper = new(255, 237, 193);

    internal static void Draw(SpriteBatch batch, Icon icon, int x, int y, int pixelSize = 3)
    {
        string[] rows = icon switch
        {
            Icon.Water => Water,
            Icon.Harvest => Harvest,
            Icon.Tend => Tend,
            Icon.Home => Home,
            Icon.Coin => Coin,
            Icon.Ledger => Ledger,
            Icon.Chest => Chest,
            _ => Sprout,
        };

        for (int row = 0; row < rows.Length; row++)
        {
            for (int col = 0; col < rows[row].Length; col++)
            {
                Color? color = rows[row][col] switch
                {
                    'o' => Ink,
                    'g' => Leaf,
                    'l' => LeafLight,
                    's' => Stem,
                    'b' => Blue,
                    'h' => Highlight,
                    'y' => Gold,
                    'w' => Paper,
                    _ => null,
                };
                if (color is Color tint)
                    batch.Draw(Game1.staminaRect, new Rectangle(x + col * pixelSize, y + row * pixelSize, pixelSize, pixelSize), tint);
            }
        }
    }
}
