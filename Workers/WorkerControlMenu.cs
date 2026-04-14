using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.BellsAndWhistles;
using StardewValley.Menus;

namespace FarmingCapitalist.Workers;

internal sealed class WorkerControlMenu : IClickableMenu
{
    [System.Flags]
    private enum ResizeEdges
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 4,
        Bottom = 8,
    }

    private const int DefaultMenuWidth = 1920;
    private const int DefaultMenuHeight = 1280;
    private const int MinMenuWidth = 960;
    private const int MinMenuHeight = 640;
    private const int EdgeMargin = 32;
    private const int WorkerRowIdBase = 91000;
    private const int ResizeBorderThickness = 28;
    private const int PanelGap = 32;
    private const int SectionPadding = 28;
    private const int WorkerRowHeight = 120;

    private readonly WorkerShellManager workerShellManager;
    private readonly List<ClickableComponent> workerRowComponents = new();
    private readonly List<WorkerSummarySnapshot> workerSnapshots = new();
    private readonly List<Rectangle> orderCardBounds = new();
    private Rectangle headerBounds;
    private Rectangle workerListPanelBounds;
    private Rectangle workerDetailsPanelBounds;
    private Rectangle workerOrdersPanelBounds;
    private ResizeEdges activeResizeEdges;
    private bool isResizing;
    private Point resizeStartMouse;
    private Rectangle resizeStartBounds;
    private string? selectedWorkerId;

    public WorkerControlMenu(WorkerShellManager workerShellManager)
        : base(0, 0, GetDefaultMenuWidth(), GetDefaultMenuHeight(), showUpperRightCloseButton: true)
    {
        this.workerShellManager = workerShellManager;
        this.closeSound = "bigDeSelect";
        this.CenterOnScreen();
        this.RefreshWorkerSnapshots();
        this.RebuildLayout();
    }

    public override void update(GameTime time)
    {
        base.update(time);
        this.RefreshWorkerSnapshots(rebuildLayoutIfRosterChanged: true);

        if (this.currentlySnappedComponent is null)
        {
            return;
        }

        int index = this.currentlySnappedComponent.myID - WorkerRowIdBase;
        if (index >= 0 && index < this.workerSnapshots.Count)
        {
            this.selectedWorkerId = this.workerSnapshots[index].WorkerId;
        }
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        _ = oldBounds;
        _ = newBounds;

        int? preferredSnapId = this.currentlySnappedComponent?.myID;
        this.ClampSizeToViewport();
        this.CenterOnScreen();
        this.RebuildLayout(preferredSnapId);
    }

    public override void snapToDefaultClickableComponent()
    {
        int? selectedRowId = this.GetSelectedWorkerRowComponentId();
        this.currentlySnappedComponent = selectedRowId is not null
            ? this.getComponentWithID(selectedRowId.Value)
            : this.upperRightCloseButton;

        if (this.currentlySnappedComponent is not null)
        {
            this.snapCursorToCurrentSnappedComponent();
        }
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (this.upperRightCloseButton?.containsPoint(x, y) == true)
        {
            base.receiveLeftClick(x, y, playSound);
            return;
        }

        ResizeEdges clickedEdges = this.GetResizeEdgesAtPoint(x, y);
        if (clickedEdges != ResizeEdges.None)
        {
            this.isResizing = true;
            this.activeResizeEdges = clickedEdges;
            this.resizeStartMouse = new Point(x, y);
            this.resizeStartBounds = new Rectangle(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height);
            return;
        }

        for (int i = 0; i < this.workerRowComponents.Count; i++)
        {
            ClickableComponent row = this.workerRowComponents[i];
            if (!row.containsPoint(x, y))
            {
                continue;
            }

            string workerId = this.workerSnapshots[i].WorkerId;
            if (this.selectedWorkerId != workerId)
            {
                this.selectedWorkerId = workerId;
                Game1.playSound("smallSelect");
            }

            this.currentlySnappedComponent = row;
            return;
        }

        base.receiveLeftClick(x, y, playSound);
    }

    public override void leftClickHeld(int x, int y)
    {
        if (!this.isResizing)
        {
            base.leftClickHeld(x, y);
            return;
        }

        Rectangle resizedBounds = this.GetResizedBounds(x, y);
        if (resizedBounds.X != this.xPositionOnScreen
            || resizedBounds.Y != this.yPositionOnScreen
            || resizedBounds.Width != this.width
            || resizedBounds.Height != this.height)
        {
            int? preferredSnapId = this.currentlySnappedComponent?.myID;
            this.xPositionOnScreen = resizedBounds.X;
            this.yPositionOnScreen = resizedBounds.Y;
            this.width = resizedBounds.Width;
            this.height = resizedBounds.Height;
            this.RebuildLayout(preferredSnapId);
        }
    }

    public override void releaseLeftClick(int x, int y)
    {
        _ = x;
        _ = y;
        this.isResizing = false;
        this.activeResizeEdges = ResizeEdges.None;
        base.releaseLeftClick(x, y);
    }

    public override void draw(SpriteBatch b)
    {
        if (!Game1.options.showClearBackgrounds)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.75f);
        }

        Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, speaker: false, drawOnlyBox: true);
        SpriteText.drawStringWithScrollCenteredAt(b, "Workers", this.xPositionOnScreen + (this.width / 2), this.yPositionOnScreen + 24);

        IClickableMenu.drawTextureBox(b, this.headerBounds.X, this.headerBounds.Y, this.headerBounds.Width, this.headerBounds.Height, Color.White);
        Utility.drawTextWithShadow(
            b,
            "Workers",
            Game1.smallFont,
            new Vector2(this.headerBounds.X + SectionPadding, this.headerBounds.Y + 30),
            Game1.textColor);
        string headerHint = "Press B to close";
        Vector2 headerHintSize = Game1.smallFont.MeasureString(headerHint);
        Utility.drawTextWithShadow(
            b,
            headerHint,
            Game1.smallFont,
            new Vector2(this.headerBounds.Right - SectionPadding - headerHintSize.X, this.headerBounds.Y + 30),
            Game1.textColor * 0.75f);

        this.DrawWorkerListPanel(b);
        this.DrawWorkerDetailsPanel(b);
        this.DrawWorkerOrdersPanel(b);

        base.draw(b);
        this.drawMouse(b);
    }

    private static int GetDefaultMenuWidth()
    {
        int maxWidth = GetMaxMenuWidth();
        return Math.Clamp(Math.Min(DefaultMenuWidth, maxWidth), GetMinMenuWidth(maxWidth), maxWidth);
    }

    private static int GetDefaultMenuHeight()
    {
        int maxHeight = GetMaxMenuHeight();
        return Math.Clamp(Math.Min(DefaultMenuHeight, maxHeight), GetMinMenuHeight(maxHeight), maxHeight);
    }

    private static int GetMaxMenuWidth()
    {
        return Math.Max(256, Game1.uiViewport.Width - EdgeMargin);
    }

    private static int GetMaxMenuHeight()
    {
        return Math.Max(256, Game1.uiViewport.Height - EdgeMargin);
    }

    private static int GetMinMenuWidth(int maxWidth)
    {
        return Math.Min(MinMenuWidth, maxWidth);
    }

    private static int GetMinMenuHeight(int maxHeight)
    {
        return Math.Min(MinMenuHeight, maxHeight);
    }

    private void RefreshWorkerSnapshots(bool rebuildLayoutIfRosterChanged = false)
    {
        IReadOnlyList<WorkerSummarySnapshot> latestSnapshots = this.workerShellManager.GetWorkerSummaries();
        bool rosterChanged = latestSnapshots.Count != this.workerSnapshots.Count;

        if (!rosterChanged)
        {
            for (int i = 0; i < latestSnapshots.Count; i++)
            {
                if (latestSnapshots[i].WorkerId != this.workerSnapshots[i].WorkerId)
                {
                    rosterChanged = true;
                    break;
                }
            }
        }

        this.workerSnapshots.Clear();
        this.workerSnapshots.AddRange(latestSnapshots);
        this.EnsureValidSelection();

        if (rebuildLayoutIfRosterChanged && rosterChanged)
        {
            this.RebuildLayout(this.currentlySnappedComponent?.myID);
        }
    }

    private void EnsureValidSelection()
    {
        if (this.workerSnapshots.Count == 0)
        {
            this.selectedWorkerId = null;
            return;
        }

        if (this.selectedWorkerId is not null)
        {
            foreach (WorkerSummarySnapshot snapshot in this.workerSnapshots)
            {
                if (snapshot.WorkerId == this.selectedWorkerId)
                {
                    return;
                }
            }
        }

        this.selectedWorkerId = this.workerSnapshots[0].WorkerId;
    }

    private void RebuildLayout(int? preferredSnapId = null)
    {
        int contentLeft = this.xPositionOnScreen + IClickableMenu.borderWidth + IClickableMenu.spaceToClearSideBorder;
        int contentTop = this.yPositionOnScreen + IClickableMenu.borderWidth + 64;
        int contentRight = this.xPositionOnScreen + this.width - IClickableMenu.borderWidth - IClickableMenu.spaceToClearSideBorder;
        int contentBottom = this.yPositionOnScreen + this.height - IClickableMenu.borderWidth - 28;
        int contentWidth = Math.Max(1, contentRight - contentLeft);

        this.headerBounds = new Rectangle(contentLeft, contentTop, contentWidth, 88);

        int bodyTop = this.headerBounds.Bottom + PanelGap;
        int bodyHeight = Math.Max(1, contentBottom - bodyTop);
        int workerListWidth = Math.Clamp(contentWidth / 3, 380, 560);
        workerListWidth = Math.Min(workerListWidth, Math.Max(320, contentWidth - 560));

        this.workerListPanelBounds = new Rectangle(contentLeft, bodyTop, workerListWidth, bodyHeight);

        int detailLeft = this.workerListPanelBounds.Right + PanelGap;
        int detailWidth = Math.Max(1, contentRight - detailLeft);
        int detailHeight = Math.Clamp(bodyHeight / 3, 220, 320);
        if (detailHeight + PanelGap >= bodyHeight)
        {
            detailHeight = Math.Max(160, bodyHeight / 2);
        }

        this.workerDetailsPanelBounds = new Rectangle(detailLeft, bodyTop, detailWidth, detailHeight);
        this.workerOrdersPanelBounds = new Rectangle(
            detailLeft,
            this.workerDetailsPanelBounds.Bottom + PanelGap,
            detailWidth,
            Math.Max(1, contentBottom - (this.workerDetailsPanelBounds.Bottom + PanelGap)));

        this.InitializeWorkerRowComponents();
        this.InitializeOrderCardBounds();

        this.initializeUpperRightCloseButton();
        this.upperRightCloseButton.myID = IClickableMenu.upperRightCloseButton_ID;
        this.upperRightCloseButton.leftNeighborID = this.workerRowComponents.Count > 0
            ? this.workerRowComponents[0].myID
            : -99998;
        this.upperRightCloseButton.downNeighborID = -99998;
        this.RefreshClickableComponents(preferredSnapId);
    }

    private void InitializeWorkerRowComponents()
    {
        this.workerRowComponents.Clear();

        if (this.workerSnapshots.Count == 0)
        {
            return;
        }

        int listTop = this.workerListPanelBounds.Y + 72;
        int rowWidth = this.workerListPanelBounds.Width - (SectionPadding * 2);

        for (int i = 0; i < this.workerSnapshots.Count; i++)
        {
            ClickableComponent row = new(
                new Rectangle(this.workerListPanelBounds.X + SectionPadding, listTop + (i * (WorkerRowHeight + 12)), rowWidth, WorkerRowHeight),
                this.workerSnapshots[i].DisplayName)
            {
                myID = WorkerRowIdBase + i,
                upNeighborID = i > 0 ? WorkerRowIdBase + i - 1 : -99998,
                downNeighborID = i < this.workerSnapshots.Count - 1 ? WorkerRowIdBase + i + 1 : -99998,
                rightNeighborID = IClickableMenu.upperRightCloseButton_ID,
                leftNeighborID = -99998,
            };

            this.workerRowComponents.Add(row);
        }
    }

    private void InitializeOrderCardBounds()
    {
        this.orderCardBounds.Clear();

        int cardTop = this.workerOrdersPanelBounds.Y + 68;
        int cardAreaHeight = Math.Max(48, this.workerOrdersPanelBounds.Bottom - SectionPadding - cardTop);
        int cardHeight = Math.Max(88, (cardAreaHeight - PanelGap) / 2);
        int fullWidth = this.workerOrdersPanelBounds.Width - (SectionPadding * 2);
        int halfWidth = Math.Max(160, (fullWidth - PanelGap) / 2);
        int secondColumnX = this.workerOrdersPanelBounds.X + SectionPadding + halfWidth + PanelGap;

        this.orderCardBounds.Add(new Rectangle(
            this.workerOrdersPanelBounds.X + SectionPadding,
            cardTop,
            halfWidth,
            cardHeight));
        this.orderCardBounds.Add(new Rectangle(
            secondColumnX,
            cardTop,
            halfWidth,
            cardHeight));
        this.orderCardBounds.Add(new Rectangle(
            this.workerOrdersPanelBounds.X + SectionPadding,
            cardTop + cardHeight + PanelGap,
            fullWidth,
            cardHeight));
    }

    private void RefreshClickableComponents(int? preferredSnapId)
    {
        if (!Game1.options.SnappyMenus)
        {
            return;
        }

        this.populateClickableComponentList();

        int? targetSnapId = preferredSnapId;
        if (targetSnapId is null || this.getComponentWithID(targetSnapId.Value) is null)
        {
            targetSnapId = this.GetSelectedWorkerRowComponentId() ?? IClickableMenu.upperRightCloseButton_ID;
        }

        this.currentlySnappedComponent = this.getComponentWithID(targetSnapId.Value);
        if (this.currentlySnappedComponent is null)
        {
            this.snapToDefaultClickableComponent();
            return;
        }

        if (Game1.options.gamepadControls)
        {
            this.snapCursorToCurrentSnappedComponent();
        }
    }

    private int? GetSelectedWorkerRowComponentId()
    {
        if (this.selectedWorkerId is null)
        {
            return null;
        }

        for (int i = 0; i < this.workerSnapshots.Count; i++)
        {
            if (this.workerSnapshots[i].WorkerId == this.selectedWorkerId)
            {
                return WorkerRowIdBase + i;
            }
        }

        return null;
    }

    private void DrawWorkerListPanel(SpriteBatch b)
    {
        IClickableMenu.drawTextureBox(b, this.workerListPanelBounds.X, this.workerListPanelBounds.Y, this.workerListPanelBounds.Width, this.workerListPanelBounds.Height, Color.White);
        Utility.drawTextWithShadow(
            b,
            "Roster",
            Game1.smallFont,
            new Vector2(this.workerListPanelBounds.X + SectionPadding, this.workerListPanelBounds.Y + 22),
            Game1.textColor);

        if (this.workerSnapshots.Count == 0)
        {
            Rectangle emptyRowBounds = new(
                this.workerListPanelBounds.X + SectionPadding,
                this.workerListPanelBounds.Y + 72,
                this.workerListPanelBounds.Width - (SectionPadding * 2),
                WorkerRowHeight);
            IClickableMenu.drawTextureBox(b, emptyRowBounds.X, emptyRowBounds.Y, emptyRowBounds.Width, emptyRowBounds.Height, Color.White * 0.65f);

            this.DrawWrappedText(
                b,
                "No workers configured yet.",
                new Rectangle(emptyRowBounds.X + 16, emptyRowBounds.Y + 18, emptyRowBounds.Width - 32, emptyRowBounds.Height - 36),
                Game1.textColor * 0.7f,
                verticallyCentered: true);
            return;
        }

        for (int i = 0; i < this.workerRowComponents.Count; i++)
        {
            ClickableComponent row = this.workerRowComponents[i];
            WorkerSummarySnapshot snapshot = this.workerSnapshots[i];
            bool isSelected = snapshot.WorkerId == this.selectedWorkerId;
            bool isHovered = row.containsPoint(Game1.getMouseX(), Game1.getMouseY());
            Color rowColor = isSelected
                ? Color.LightGoldenrodYellow
                : isHovered
                    ? Color.White
                    : Color.White * 0.92f;

            IClickableMenu.drawTextureBox(b, row.bounds.X, row.bounds.Y, row.bounds.Width, row.bounds.Height, rowColor);
            Rectangle iconBounds = new(row.bounds.X + 16, row.bounds.Y + 14, 56, 56);
            this.DrawWorkerFace(b, snapshot.WorkerId, iconBounds);
            Utility.drawTextWithShadow(
                b,
                snapshot.DisplayName,
                Game1.smallFont,
                new Vector2(iconBounds.Right + 14, row.bounds.Y + 16),
                Game1.textColor);

            this.DrawWrappedText(
                b,
                this.GetWorkerRowSubtitle(snapshot),
                new Rectangle(iconBounds.Right + 14, row.bounds.Y + 48, row.bounds.Width - (iconBounds.Width + 46), row.bounds.Height - 56),
                Game1.textColor * 0.75f);
        }
    }

    private void DrawWorkerDetailsPanel(SpriteBatch b)
    {
        IClickableMenu.drawTextureBox(b, this.workerDetailsPanelBounds.X, this.workerDetailsPanelBounds.Y, this.workerDetailsPanelBounds.Width, this.workerDetailsPanelBounds.Height, Color.White);
        Utility.drawTextWithShadow(
            b,
            "Selected Worker",
            Game1.smallFont,
            new Vector2(this.workerDetailsPanelBounds.X + SectionPadding, this.workerDetailsPanelBounds.Y + 22),
            Game1.textColor);

        WorkerSummarySnapshot? selectedSnapshot = this.GetSelectedWorkerSnapshot();
        if (selectedSnapshot is null)
        {
            this.DrawWrappedText(
                b,
                "No worker is available yet. Configure and spawn one to populate this panel.",
                new Rectangle(
                    this.workerDetailsPanelBounds.X + SectionPadding,
                    this.workerDetailsPanelBounds.Y + 64,
                    this.workerDetailsPanelBounds.Width - (SectionPadding * 2),
                    this.workerDetailsPanelBounds.Height - 88),
                Game1.textColor * 0.75f);
            return;
        }

        WorkerSummarySnapshot snapshot = selectedSnapshot.Value;
        Rectangle faceBounds = new(
            this.workerDetailsPanelBounds.X + SectionPadding,
            this.workerDetailsPanelBounds.Y + 56,
            64,
            64);
        this.DrawWorkerFace(b, snapshot.WorkerId, faceBounds);
        Utility.drawTextWithShadow(
            b,
            snapshot.DisplayName,
            Game1.smallFont,
            new Vector2(faceBounds.Right + 18, faceBounds.Y + 12),
            Game1.textColor);

        string detailText =
            $"Configured: {(snapshot.IsConfigured ? "Yes" : "No")}\n" +
            $"Spawned: {(snapshot.IsSpawned ? "Yes" : "No")}\n" +
            $"Location: {snapshot.CurrentLocationName ?? "Unavailable"}\n" +
            $"Tile: {this.FormatTile(snapshot.CurrentTile)}";
        this.DrawWrappedText(
            b,
            detailText,
            new Rectangle(
                this.workerDetailsPanelBounds.X + SectionPadding,
                this.workerDetailsPanelBounds.Y + 130,
                this.workerDetailsPanelBounds.Width - (SectionPadding * 2),
                this.workerDetailsPanelBounds.Height - 150),
            Game1.textColor * 0.82f);
    }

    private void DrawWorkerOrdersPanel(SpriteBatch b)
    {
        IClickableMenu.drawTextureBox(b, this.workerOrdersPanelBounds.X, this.workerOrdersPanelBounds.Y, this.workerOrdersPanelBounds.Width, this.workerOrdersPanelBounds.Height, Color.White);
        Utility.drawTextWithShadow(
            b,
            "Orders",
            Game1.smallFont,
            new Vector2(this.workerOrdersPanelBounds.X + SectionPadding, this.workerOrdersPanelBounds.Y + 22),
            Game1.textColor);

        for (int i = 0; i < this.orderCardBounds.Count; i++)
        {
            Rectangle cardBounds = this.orderCardBounds[i];
            IClickableMenu.drawTextureBox(b, cardBounds.X, cardBounds.Y, cardBounds.Width, cardBounds.Height, Color.White * 0.78f);

            string slotLabel = $"Order Slot {i + 1}";
            Utility.drawTextWithShadow(
                b,
                slotLabel,
                Game1.smallFont,
                new Vector2(cardBounds.X + 16, cardBounds.Y + 16),
                Game1.textColor * 0.75f);

            this.DrawWrappedCenteredText(
                b,
                "Coming Soon",
                new Rectangle(cardBounds.X + 16, cardBounds.Y + 44, cardBounds.Width - 32, cardBounds.Height - 52),
                Game1.textColor * 0.55f);
        }
    }

    private WorkerSummarySnapshot? GetSelectedWorkerSnapshot()
    {
        if (this.selectedWorkerId is null)
        {
            return null;
        }

        foreach (WorkerSummarySnapshot snapshot in this.workerSnapshots)
        {
            if (snapshot.WorkerId == this.selectedWorkerId)
            {
                return snapshot;
            }
        }

        return null;
    }

    private string GetWorkerRowSubtitle(WorkerSummarySnapshot snapshot)
    {
        if (snapshot.IsSpawned && snapshot.CurrentLocationName is not null)
        {
            return $"{snapshot.CurrentLocationName}  {this.FormatTile(snapshot.CurrentTile)}";
        }

        if (snapshot.IsConfigured)
        {
            return "Configured and waiting for orders";
        }

        return "Unavailable";
    }

    private string FormatTile(Point? tile)
    {
        return tile is Point point ? $"{point.X}, {point.Y}" : "--";
    }

    private void ClampSizeToViewport()
    {
        this.width = this.ClampWidth(this.width);
        this.height = this.ClampHeight(this.height);
    }

    private int ClampWidth(int value)
    {
        int maxWidth = GetMaxMenuWidth();
        return Math.Clamp(value, GetMinMenuWidth(maxWidth), maxWidth);
    }

    private int ClampHeight(int value)
    {
        int maxHeight = GetMaxMenuHeight();
        return Math.Clamp(value, GetMinMenuHeight(maxHeight), maxHeight);
    }

    private void CenterOnScreen()
    {
        this.xPositionOnScreen = (Game1.uiViewport.Width - this.width) / 2;
        this.yPositionOnScreen = (Game1.uiViewport.Height - this.height) / 2;
    }

    private ResizeEdges GetResizeEdgesAtPoint(int x, int y)
    {
        if (this.upperRightCloseButton?.containsPoint(x, y) == true)
        {
            return ResizeEdges.None;
        }

        Rectangle bounds = new(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height);
        if (!bounds.Contains(x, y))
        {
            return ResizeEdges.None;
        }

        ResizeEdges edges = ResizeEdges.None;
        if (x - bounds.Left <= ResizeBorderThickness)
        {
            edges |= ResizeEdges.Left;
        }
        else if (bounds.Right - x <= ResizeBorderThickness)
        {
            edges |= ResizeEdges.Right;
        }

        if (y - bounds.Top <= ResizeBorderThickness)
        {
            edges |= ResizeEdges.Top;
        }
        else if (bounds.Bottom - y <= ResizeBorderThickness)
        {
            edges |= ResizeEdges.Bottom;
        }

        return edges;
    }

    private Rectangle GetResizedBounds(int mouseX, int mouseY)
    {
        int deltaX = mouseX - this.resizeStartMouse.X;
        int deltaY = mouseY - this.resizeStartMouse.Y;

        int nextX = this.resizeStartBounds.X;
        int nextY = this.resizeStartBounds.Y;
        int nextWidth = this.resizeStartBounds.Width;
        int nextHeight = this.resizeStartBounds.Height;

        int globalMaxWidth = GetMaxMenuWidth();
        int globalMinWidth = GetMinMenuWidth(globalMaxWidth);
        int globalMaxHeight = GetMaxMenuHeight();
        int globalMinHeight = GetMinMenuHeight(globalMaxHeight);

        if ((this.activeResizeEdges & ResizeEdges.Left) != 0)
        {
            int fixedRight = this.resizeStartBounds.Right;
            int minX = Math.Max(0, fixedRight - globalMaxWidth);
            int maxX = fixedRight - globalMinWidth;
            nextX = Math.Clamp(this.resizeStartBounds.X + deltaX, minX, maxX);
            nextWidth = fixedRight - nextX;
        }
        else if ((this.activeResizeEdges & ResizeEdges.Right) != 0)
        {
            int maxWidthForPosition = Math.Min(globalMaxWidth, Game1.uiViewport.Width - this.resizeStartBounds.X);
            nextWidth = Math.Clamp(this.resizeStartBounds.Width + deltaX, globalMinWidth, maxWidthForPosition);
        }

        if ((this.activeResizeEdges & ResizeEdges.Top) != 0)
        {
            int fixedBottom = this.resizeStartBounds.Bottom;
            int minY = Math.Max(0, fixedBottom - globalMaxHeight);
            int maxY = fixedBottom - globalMinHeight;
            nextY = Math.Clamp(this.resizeStartBounds.Y + deltaY, minY, maxY);
            nextHeight = fixedBottom - nextY;
        }
        else if ((this.activeResizeEdges & ResizeEdges.Bottom) != 0)
        {
            int maxHeightForPosition = Math.Min(globalMaxHeight, Game1.uiViewport.Height - this.resizeStartBounds.Y);
            nextHeight = Math.Clamp(this.resizeStartBounds.Height + deltaY, globalMinHeight, maxHeightForPosition);
        }

        return new Rectangle(nextX, nextY, nextWidth, nextHeight);
    }

    private void DrawWrappedText(SpriteBatch b, string text, Rectangle bounds, Color color, bool verticallyCentered = false)
    {
        string wrappedText = Game1.parseText(text, Game1.smallFont, Math.Max(1, bounds.Width));
        Vector2 textSize = Game1.smallFont.MeasureString(wrappedText);
        float y = verticallyCentered
            ? bounds.Y + ((bounds.Height - textSize.Y) / 2f)
            : bounds.Y;

        b.DrawString(Game1.smallFont, wrappedText, new Vector2(bounds.X, y), color);
    }

    private void DrawWrappedCenteredText(SpriteBatch b, string text, Rectangle bounds, Color color)
    {
        string wrappedText = Game1.parseText(text, Game1.smallFont, Math.Max(1, bounds.Width));
        Vector2 textSize = Game1.smallFont.MeasureString(wrappedText);
        Vector2 position = new(
            bounds.X + ((bounds.Width - textSize.X) / 2f),
            bounds.Y + ((bounds.Height - textSize.Y) / 2f));
        b.DrawString(Game1.smallFont, wrappedText, position, color);
    }

    private void DrawWorkerFace(SpriteBatch b, string workerId, Rectangle bounds)
    {
        IClickableMenu.drawTextureBox(b, bounds.X, bounds.Y, bounds.Width, bounds.Height, Color.White * 0.75f);

        if (!this.workerShellManager.TryGetWorkerMenuFace(workerId, out Texture2D? texture, out Rectangle sourceRect) || texture is null)
        {
            return;
        }

        int padding = Math.Max(8, bounds.Width / 7);
        int innerSize = Math.Max(16, Math.Min(bounds.Width, bounds.Height) - (padding * 2));
        Rectangle innerBounds = new(
            bounds.X + ((bounds.Width - innerSize) / 2),
            bounds.Y + ((bounds.Height - innerSize) / 2) - 4,
            innerSize,
            innerSize);

        b.Draw(texture, innerBounds, sourceRect, Color.White);
    }
}
