using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace FarmingCapitalist.Workers;

/// <summary>Vanilla menu and item sprites, fitted without changing their aspect ratio.</summary>
internal static class WorkerMenuArt
{
    internal enum Icon
    {
        Sprout, Water, Harvest, Tend, Home, Coin, Ledger, Chest,
        Roster, Skills, Mining, Fishing, Foraging, Combat, Hardwood, Explore, Perk,
    }

    internal static void Draw(SpriteBatch batch, Icon icon, int x, int y, int pixelSize = 3)
        => Draw(batch, icon, new Rectangle(x, y, 12 * pixelSize, 12 * pixelSize));

    internal static void Draw(SpriteBatch batch, Icon icon, Rectangle bounds, float opacity = 1f)
    {
        // SkillsPage and GameMenu are the source of the cursor-sheet rectangles.
        Rectangle source = icon switch
        {
            Icon.Sprout => new Rectangle(10, 428, 10, 10),
            Icon.Mining => new Rectangle(30, 428, 10, 10),
            Icon.Fishing => new Rectangle(20, 428, 10, 10),
            Icon.Foraging => new Rectangle(60, 428, 10, 10),
            Icon.Combat => new Rectangle(120, 428, 10, 10),
            Icon.Coin => new Rectangle(338, 400, 8, 8),
            Icon.Home => new Rectangle(653, 880, 10, 10),
            Icon.Roster => new Rectangle(32, 368, 16, 16),
            Icon.Skills => new Rectangle(16, 368, 16, 16),
            Icon.Explore => new Rectangle(48, 368, 16, 16),
            Icon.Perk => new Rectangle(50, 428, 10, 10),
            Icon.Ledger => new Rectangle(80, 368, 16, 16),
            _ => Rectangle.Empty,
        };
        Texture2D texture = Game1.mouseCursors;
        if (source == Rectangle.Empty)
        {
            string itemId = icon switch
            {
                Icon.Water => "(T)WateringCan",
                Icon.Harvest => "(O)24", // Parsnip.
                Icon.Tend => "(T)Hoe",
                Icon.Hardwood => "(O)709",
                _ => "(BC)130", // Chest.
            };
            var data = ItemRegistry.GetDataOrErrorItem(itemId);
            texture = data.GetTexture();
            source = data.GetSourceRect();
        }
        DrawSprite(batch, texture, source, bounds, opacity);
    }

    internal static void DrawSprite(SpriteBatch batch, Texture2D texture, Rectangle source, Rectangle bounds, float opacity = 1f)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        float scale = Math.Min((float)bounds.Width / source.Width, (float)bounds.Height / source.Height);
        // Whole-pixel sprite scaling when space permits keeps native art crisp.
        if (scale >= 1f) scale = MathF.Floor(scale);
        int width = Math.Max(1, (int)(source.Width * scale));
        int height = Math.Max(1, (int)(source.Height * scale));
        Rectangle destination = new(bounds.X + (bounds.Width - width) / 2,
            bounds.Y + (bounds.Height - height) / 2, width, height);
        batch.Draw(texture, destination, source, Color.White * opacity);
    }
}
