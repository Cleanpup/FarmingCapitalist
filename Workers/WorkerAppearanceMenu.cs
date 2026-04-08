using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace FarmingCapitalist.Workers;

internal sealed class WorkerAppearanceMenu : IClickableMenu
{
    private sealed class SelectionField
    {
        public SelectionField(
            string label,
            Func<string> getSubLabel,
            Action<int> change,
            Vector2 labelPosition,
            ClickableTextureComponent leftButton,
            ClickableTextureComponent rightButton)
        {
            this.Label = label;
            this.GetSubLabel = getSubLabel;
            this.Change = change;
            this.LabelPosition = labelPosition;
            this.LeftButton = leftButton;
            this.RightButton = rightButton;
        }

        public string Label { get; }

        public Func<string> GetSubLabel { get; }

        public Action<int> Change { get; }

        public Vector2 LabelPosition { get; }

        public ClickableTextureComponent LeftButton { get; }

        public ClickableTextureComponent RightButton { get; }
    }

    private readonly Action<WorkerAppearanceData> onSave;
    private readonly Farmer previewFarmer;
    private readonly List<int> previewDirections = new() { 2, 1, 0, 3 };
    private readonly List<int> hairStyleIds;
    private readonly List<string> shirtIds;
    private readonly List<string> pantsIds;
    private readonly List<SelectionField> selectionFields = new();
    private ClickableTextureComponent directionLeftButton = null!;
    private ClickableTextureComponent directionRightButton = null!;
    private ClickableTextureComponent maleButton = null!;
    private ClickableTextureComponent femaleButton = null!;
    private ClickableTextureComponent okButton = null!;
    private ColorPicker hairColorPicker = null!;
    private ColorPicker eyeColorPicker = null!;
    private ColorPicker pantsColorPicker = null!;
    private Rectangle portraitBox;
    private Vector2 eyeColorLabelPosition;
    private Vector2 hairColorLabelPosition;
    private Vector2 pantsColorLabelPosition;
    private ColorPicker? heldColorPicker;
    private Action<Color>? heldColorApply;
    private int previewDirectionIndex;

    public WorkerAppearanceMenu(WorkerAppearanceData initialAppearance, Action<WorkerAppearanceData> onSave)
        : base(
            Game1.uiViewport.Width / 2 - (632 + IClickableMenu.borderWidth * 2) / 2,
            Game1.uiViewport.Height / 2 - (648 + IClickableMenu.borderWidth * 2) / 2 - 64,
            632 + IClickableMenu.borderWidth * 2,
            648 + IClickableMenu.borderWidth * 2 + 64,
            showUpperRightCloseButton: false)
    {
        this.onSave = onSave;

        this.previewFarmer = new Farmer
        {
            Name = TestWorkerDefinition.DisplayName,
            displayName = TestWorkerDefinition.DisplayName,
            currentLocation = Game1.currentLocation,
        };

        this.hairStyleIds = Farmer.GetAllHairstyleIndices();
        this.shirtIds = GetValidClothingIds(initialAppearance.ShirtId, Game1.shirtData, data => data.CanChooseDuringCharacterCustomization);
        this.pantsIds = GetValidClothingIds(initialAppearance.PantsId, Game1.pantsData, data => data.CanChooseDuringCharacterCustomization);

        initialAppearance.ApplyTo(this.previewFarmer);
        this.previewDirectionIndex = 0;

        this.RebuildLayout();
        this.RefreshPreviewFarmer();
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        base.gameWindowSizeChanged(oldBounds, newBounds);
        this.RebuildLayout();
        this.RefreshPreviewFarmer();
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (this.okButton.containsPoint(x, y))
        {
            Game1.playSound("smallSelect");
            this.onSave(WorkerAppearanceData.FromFarmer(this.previewFarmer));
            this.exitThisMenu();
            return;
        }

        if (this.directionLeftButton.containsPoint(x, y))
        {
            this.RotatePreviewDirection(-1);
            return;
        }

        if (this.directionRightButton.containsPoint(x, y))
        {
            this.RotatePreviewDirection(1);
            return;
        }

        if (this.maleButton.containsPoint(x, y))
        {
            this.previewFarmer.changeGender(true);
            this.RefreshPreviewFarmer();
            Game1.playSound("coin");
            return;
        }

        if (this.femaleButton.containsPoint(x, y))
        {
            this.previewFarmer.changeGender(false);
            this.RefreshPreviewFarmer();
            Game1.playSound("coin");
            return;
        }

        foreach (SelectionField field in this.selectionFields)
        {
            if (field.LeftButton.containsPoint(x, y))
            {
                field.Change(-1);
                this.RefreshPreviewFarmer();
                Game1.playSound("shwip");
                return;
            }

            if (field.RightButton.containsPoint(x, y))
            {
                field.Change(1);
                this.RefreshPreviewFarmer();
                Game1.playSound("shwip");
                return;
            }
        }

        if (this.TryBeginColorInteraction(this.eyeColorPicker, x, y, color => this.previewFarmer.changeEyeColor(color)))
        {
            return;
        }

        if (this.TryBeginColorInteraction(this.hairColorPicker, x, y, color => this.previewFarmer.changeHairColor(color)))
        {
            return;
        }

        if (this.TryBeginColorInteraction(this.pantsColorPicker, x, y, color => this.previewFarmer.changePantsColor(color)))
        {
            return;
        }

        base.receiveLeftClick(x, y, playSound);
    }

    public override void leftClickHeld(int x, int y)
    {
        if (this.heldColorPicker is not null && this.heldColorApply is not null)
        {
            this.heldColorApply(this.heldColorPicker.clickHeld(x, y));
            this.previewFarmer.UpdateClothing();
            this.previewFarmer.FarmerRenderer.MarkSpriteDirty();
            return;
        }

        base.leftClickHeld(x, y);
    }

    public override void releaseLeftClick(int x, int y)
    {
        this.hairColorPicker.releaseClick();
        this.eyeColorPicker.releaseClick();
        this.pantsColorPicker.releaseClick();
        this.heldColorPicker = null;
        this.heldColorApply = null;
        base.releaseLeftClick(x, y);
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape)
        {
            Game1.playSound("bigDeSelect");
            this.exitThisMenu();
            return;
        }

        base.receiveKeyPress(key);
    }

    public override void draw(SpriteBatch b)
    {
        Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, speaker: false, drawOnlyBox: true);
        SpriteText.drawStringWithScrollCenteredAt(b, "Worker Appearance", this.xPositionOnScreen + this.width / 2, this.yPositionOnScreen + 24);

        b.Draw(Game1.daybg, new Vector2(this.portraitBox.X, this.portraitBox.Y), Color.White);

        this.directionLeftButton.draw(b);
        this.directionRightButton.draw(b);

        this.maleButton.draw(b);
        this.femaleButton.draw(b);

        ClickableTextureComponent selectedGenderButton = this.previewFarmer.IsMale ? this.maleButton : this.femaleButton;
        b.Draw(Game1.mouseCursors, selectedGenderButton.bounds, Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 34), Color.White);

        foreach (SelectionField field in this.selectionFields)
        {
            field.LeftButton.draw(b);
            field.RightButton.draw(b);
            this.DrawSelectionField(b, field);
        }

        this.DrawColorPickerSection(b, "Eye Color", this.eyeColorPicker, this.eyeColorLabelPosition);
        this.DrawColorPickerSection(b, "Hair Color", this.hairColorPicker, this.hairColorLabelPosition);
        this.DrawColorPickerSection(b, "Pants Color", this.pantsColorPicker, this.pantsColorLabelPosition);

        this.okButton.draw(b, Color.White, 0.75f);

        b.End();
        b.Begin(SpriteSortMode.FrontToBack, BlendState.AlphaBlend, SamplerState.PointClamp);
        FarmerRenderer.isDrawingForUI = true;
        this.previewFarmer.FarmerRenderer.draw(
            b,
            this.previewFarmer.FarmerSprite.CurrentAnimationFrame,
            this.previewFarmer.FarmerSprite.CurrentFrame,
            this.previewFarmer.FarmerSprite.SourceRect,
            new Vector2(this.portraitBox.Center.X - 32, this.portraitBox.Bottom - 160),
            Vector2.Zero,
            0.8f,
            Color.White,
            0f,
            1f,
            this.previewFarmer);
        FarmerRenderer.isDrawingForUI = false;
        b.End();
        b.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp);

        this.drawMouse(b);
    }

    private void RebuildLayout()
    {
        this.xPositionOnScreen = Game1.uiViewport.Width / 2 - (632 + IClickableMenu.borderWidth * 2) / 2;
        this.yPositionOnScreen = Game1.uiViewport.Height / 2 - (648 + IClickableMenu.borderWidth * 2) / 2 - 64;

        this.portraitBox = new Rectangle(
            this.xPositionOnScreen + 64 + 42 - 2,
            this.yPositionOnScreen + IClickableMenu.borderWidth + IClickableMenu.spaceToClearTopBorder - 16,
            128,
            192);

        this.directionLeftButton = new ClickableTextureComponent(
            "DirectionLeft",
            new Rectangle(this.portraitBox.X - 32, this.portraitBox.Y + 144, 64, 64),
            null,
            null,
            Game1.mouseCursors,
            Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44),
            1f);
        this.directionRightButton = new ClickableTextureComponent(
            "DirectionRight",
            new Rectangle(this.portraitBox.Right - 32, this.yPositionOnScreen + IClickableMenu.borderWidth + IClickableMenu.spaceToClearTopBorder + 128, 64, 64),
            null,
            null,
            Game1.mouseCursors,
            Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33),
            1f);

        this.maleButton = new ClickableTextureComponent(
            "Male",
            new Rectangle(this.xPositionOnScreen + IClickableMenu.spaceToClearSideBorder + IClickableMenu.borderWidth + 40, this.yPositionOnScreen + IClickableMenu.borderWidth + IClickableMenu.spaceToClearTopBorder + 192, 64, 64),
            null,
            "Male",
            Game1.mouseCursors,
            new Rectangle(128, 192, 16, 16),
            4f);
        this.femaleButton = new ClickableTextureComponent(
            "Female",
            new Rectangle(this.xPositionOnScreen + IClickableMenu.spaceToClearSideBorder + IClickableMenu.borderWidth + 128, this.yPositionOnScreen + IClickableMenu.borderWidth + IClickableMenu.spaceToClearTopBorder + 192, 64, 64),
            null,
            "Female",
            Game1.mouseCursors,
            new Rectangle(144, 192, 16, 16),
            4f);

        this.okButton = new ClickableTextureComponent(
            "OK",
            new Rectangle(
                this.xPositionOnScreen + this.width - IClickableMenu.borderWidth - IClickableMenu.spaceToClearSideBorder - 64,
                this.yPositionOnScreen + this.height - IClickableMenu.borderWidth - IClickableMenu.spaceToClearTopBorder + 16,
                64,
                64),
            null,
            "Save worker appearance",
            Game1.mouseCursors,
            Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 46),
            1f);

        this.selectionFields.Clear();
        int baseSelectionY = this.yPositionOnScreen + IClickableMenu.borderWidth + IClickableMenu.spaceToClearTopBorder + 256;
        this.AddSelectionField("Skin", baseSelectionY + 0, this.GetSkinSubLabel, this.ChangeSkinTone);
        this.AddSelectionField("Hair", baseSelectionY + 68, this.GetHairSubLabel, this.ChangeHairStyle);
        this.AddSelectionField("Shirt", baseSelectionY + 136, this.GetShirtSubLabel, this.ChangeShirt);
        this.AddSelectionField("Pants", baseSelectionY + 204, this.GetPantsSubLabel, this.ChangePants);
        this.AddSelectionField("Acc.", baseSelectionY + 272, this.GetAccessorySubLabel, this.ChangeAccessory);

        int pickerX = this.xPositionOnScreen + IClickableMenu.spaceToClearSideBorder + 320 + 48 + IClickableMenu.borderWidth;
        int pickerY = baseSelectionY;
        this.eyeColorPicker = new ColorPicker("Eyes", pickerX, pickerY);
        this.hairColorPicker = new ColorPicker("Hair", pickerX, pickerY + 68);
        this.pantsColorPicker = new ColorPicker("Pants", pickerX, pickerY + 136);
        int labelX = this.xPositionOnScreen + 16 + IClickableMenu.spaceToClearSideBorder + IClickableMenu.borderWidth + 192 + 8;
        this.eyeColorLabelPosition = new Vector2(labelX, pickerY + 16);
        this.hairColorLabelPosition = new Vector2(labelX, pickerY + 84);
        this.pantsColorLabelPosition = new Vector2(labelX, pickerY + 152);
        this.SyncColorPickersFromPreview();
    }

    private void AddSelectionField(string label, int rowY, Func<string> getSubLabel, Action<int> change)
    {
        int leftX = this.xPositionOnScreen + 16 + IClickableMenu.spaceToClearSideBorder + IClickableMenu.borderWidth;
        int rightX = this.xPositionOnScreen + 16 + IClickableMenu.spaceToClearSideBorder + 128 + IClickableMenu.borderWidth;
        int labelX = this.xPositionOnScreen + 16 + IClickableMenu.spaceToClearSideBorder + IClickableMenu.borderWidth + 64 + 8;

        ClickableTextureComponent leftButton = new(
            $"{label}_Left",
            new Rectangle(leftX, rowY, 64, 64),
            null,
            null,
            Game1.mouseCursors,
            Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 44),
            1f);
        ClickableTextureComponent rightButton = new(
            $"{label}_Right",
            new Rectangle(rightX, rowY, 64, 64),
            null,
            null,
            Game1.mouseCursors,
            Game1.getSourceRectForStandardTileSheet(Game1.mouseCursors, 33),
            1f);

        this.selectionFields.Add(new SelectionField(
            label,
            getSubLabel,
            change,
            new Vector2(labelX, rowY + 16),
            leftButton,
            rightButton));
    }

    private void DrawSelectionField(SpriteBatch b, SelectionField field)
    {
        float labelOffset = 21f - Game1.smallFont.MeasureString(field.Label).X / 2f;
        Utility.drawTextWithShadow(
            b,
            field.Label,
            Game1.smallFont,
            new Vector2(field.LabelPosition.X + labelOffset, field.LabelPosition.Y),
            Game1.textColor);

        string subLabel = field.GetSubLabel();
        if (subLabel.Length == 0)
        {
            return;
        }

        Utility.drawTextWithShadow(
            b,
            subLabel,
            Game1.smallFont,
            new Vector2(field.LabelPosition.X + 21f - Game1.smallFont.MeasureString(subLabel).X / 2f, field.LabelPosition.Y + 32f),
            Game1.textColor);
    }

    private void DrawColorPickerSection(SpriteBatch b, string label, ColorPicker picker, Vector2 labelPosition)
    {
        Utility.drawTextWithShadow(
            b,
            label,
            Game1.smallFont,
            labelPosition,
            Game1.textColor);
        picker.draw(b);
    }

    private void ChangeHairStyle(int delta)
    {
        int currentIndex = this.hairStyleIds.IndexOf(this.previewFarmer.hair.Value);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        currentIndex = Utility.WrapIndex(currentIndex + delta, this.hairStyleIds.Count);
        this.previewFarmer.changeHairStyle(this.hairStyleIds[currentIndex]);
    }

    private void ChangeSkinTone(int delta)
    {
        this.previewFarmer.changeSkinColor(this.previewFarmer.skin.Value + delta);
    }

    private void ChangeAccessory(int delta)
    {
        this.previewFarmer.changeAccessory(this.previewFarmer.accessory.Value + delta);
    }

    private void ChangeShirt(int delta)
    {
        this.previewFarmer.rotateShirt(delta, this.shirtIds);
    }

    private void ChangePants(int delta)
    {
        this.previewFarmer.rotatePantStyle(delta, this.pantsIds);
    }

    private void RotatePreviewDirection(int delta)
    {
        this.previewDirectionIndex = Utility.WrapIndex(this.previewDirectionIndex + delta, this.previewDirections.Count);
        this.RefreshPreviewFarmer();
        Game1.playSound("shwip");
    }

    private void RefreshPreviewFarmer()
    {
        this.previewFarmer.currentLocation = Game1.currentLocation;
        this.previewFarmer.faceDirection(this.previewDirections[this.previewDirectionIndex]);
        this.previewFarmer.FarmerSprite.StopAnimation();
        this.previewFarmer.UpdateClothing();
        this.previewFarmer.FarmerRenderer.MarkSpriteDirty();
        this.SyncColorPickersFromPreview();
    }

    private void SyncColorPickersFromPreview()
    {
        if (this.hairColorPicker is null || this.eyeColorPicker is null || this.pantsColorPicker is null)
        {
            return;
        }

        this.hairColorPicker.setColor(this.previewFarmer.hairstyleColor.Value);
        this.eyeColorPicker.setColor(this.previewFarmer.newEyeColor.Value);
        this.pantsColorPicker.setColor(this.previewFarmer.GetPantsColor());
    }

    private bool TryBeginColorInteraction(ColorPicker picker, int x, int y, Action<Color> apply)
    {
        if (!picker.containsPoint(x, y))
        {
            return false;
        }

        apply(picker.click(x, y));
        this.previewFarmer.UpdateClothing();
        this.previewFarmer.FarmerRenderer.MarkSpriteDirty();
        this.heldColorPicker = picker;
        this.heldColorApply = apply;
        Game1.playSound("coin");
        return true;
    }

    private string GetSkinSubLabel()
    {
        return (this.previewFarmer.skin.Value + 1).ToString();
    }

    private string GetHairSubLabel()
    {
        int hairIndex = this.hairStyleIds.IndexOf(this.previewFarmer.hair.Value);
        return (Math.Max(0, hairIndex) + 1).ToString();
    }

    private string GetShirtSubLabel()
    {
        return (GetCurrentIndex(this.previewFarmer.GetShirtId(), this.shirtIds) + 1).ToString();
    }

    private string GetPantsSubLabel()
    {
        return (GetCurrentIndex(this.previewFarmer.GetPantsId(), this.pantsIds) + 1).ToString();
    }

    private string GetAccessorySubLabel()
    {
        return (this.previewFarmer.accessory.Value + 2).ToString();
    }

    private static int GetCurrentIndex(string currentId, IList<string> ids)
    {
        int currentIndex = ids.IndexOf(currentId);
        return currentIndex < 0 ? 0 : currentIndex;
    }

    private static List<string> GetValidClothingIds<TData>(string currentId, IDictionary<string, TData> data, Func<TData, bool> canChooseDuringCharacterCustomization)
    {
        List<string> validIds = new();

        foreach ((string id, TData clothingData) in data)
        {
            if (id == currentId || canChooseDuringCharacterCustomization(clothingData))
            {
                validIds.Add(id);
            }
        }

        return validIds
            .Distinct()
            .OrderBy(id => int.TryParse(id, out int numericId) ? numericId : int.MaxValue)
            .ThenBy(id => id, StringComparer.Ordinal)
            .ToList();
    }
}
