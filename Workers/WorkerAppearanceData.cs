using Microsoft.Xna.Framework;
using StardewValley;

namespace FarmingCapitalist.Workers;

internal sealed class WorkerAppearanceData
{
    private static readonly Color DefaultHairColor = new(193, 90, 50);
    private static readonly Color DefaultPantsColor = new(46, 85, 183);
    private static readonly Color DefaultEyeColor = new(122, 68, 52);

    public bool IsMale { get; set; } = true;

    public int HairStyle { get; set; }

    public int SkinTone { get; set; }

    public int Accessory { get; set; } = -1;

    public string ShirtId { get; set; } = "1000";

    public string PantsId { get; set; } = "14";

    public string ShoeColorId { get; set; } = "2";

    public int HairColorPacked { get; set; } = unchecked((int)DefaultHairColor.PackedValue);

    public int PantsColorPacked { get; set; } = unchecked((int)DefaultPantsColor.PackedValue);

    public int EyeColorPacked { get; set; } = unchecked((int)DefaultEyeColor.PackedValue);

    public static WorkerAppearanceData CreateDefault()
    {
        return new WorkerAppearanceData();
    }

    public WorkerAppearanceData Clone()
    {
        return new WorkerAppearanceData
        {
            IsMale = this.IsMale,
            HairStyle = this.HairStyle,
            SkinTone = this.SkinTone,
            Accessory = this.Accessory,
            ShirtId = this.ShirtId,
            PantsId = this.PantsId,
            ShoeColorId = this.ShoeColorId,
            HairColorPacked = this.HairColorPacked,
            PantsColorPacked = this.PantsColorPacked,
            EyeColorPacked = this.EyeColorPacked,
        };
    }

    public static WorkerAppearanceData FromFarmer(Farmer farmer)
    {
        return new WorkerAppearanceData
        {
            IsMale = farmer.IsMale,
            HairStyle = farmer.hair.Value,
            SkinTone = farmer.skin.Value,
            Accessory = farmer.accessory.Value,
            ShirtId = farmer.GetShirtId(),
            PantsId = farmer.GetPantsId(),
            ShoeColorId = farmer.shoes.Value ?? "2",
            HairColorPacked = unchecked((int)farmer.hairstyleColor.Value.PackedValue),
            PantsColorPacked = unchecked((int)farmer.pantsColor.Value.PackedValue),
            EyeColorPacked = unchecked((int)farmer.newEyeColor.Value.PackedValue),
        };
    }

    public void ApplyTo(Farmer farmer)
    {
        farmer.changeGender(this.IsMale);
        farmer.changeHairStyle(this.HairStyle);
        farmer.changeSkinColor(this.SkinTone, force: true);
        farmer.changeAccessory(this.Accessory);
        farmer.changeShirt(this.ShirtId);
        farmer.changePantStyle(this.PantsId);
        farmer.changeShoeColor(this.ShoeColorId);
        farmer.changeHairColor(this.GetHairColor());
        farmer.changePantsColor(this.GetPantsColor());
        farmer.changeEyeColor(this.GetEyeColor());
        farmer.UpdateClothing();
        farmer.FarmerRenderer.MarkSpriteDirty();
    }

    private Color GetHairColor()
    {
        return new Color(unchecked((uint)this.HairColorPacked));
    }

    private Color GetPantsColor()
    {
        return new Color(unchecked((uint)this.PantsColorPacked));
    }

    private Color GetEyeColor()
    {
        return new Color(unchecked((uint)this.EyeColorPacked));
    }
}
