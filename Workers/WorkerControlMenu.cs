using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace FarmingCapitalist.Workers;

/// <summary>Host-owned hiring and orders, with a read-only roster for farmhands.</summary>
internal sealed class WorkerControlMenu : IClickableMenu
{
    [Flags]
    private enum ResizeEdges { None = 0, Left = 1, Top = 2, Right = 4, Bottom = 8 }

    private enum WorkerMenuTab
    {
        Roster,
        FarmerJobs,
        Storage,
    }

    private const int WorkerRowIdBase = 91000;
    private const int HireId = 90000;
    private const int PreviousId = 90001;
    private const int NextId = 90002;
    private const int PayId = 90003;
    private const int DismissId = 90004;
    private const int ConfirmId = 90005;
    private const int CancelId = 90006;
    private const int RosterTabId = 90007;
    private const int FarmerJobsTabId = 90008;
    private const int StorageTabId = 90009;
    private const int OrderIdBase = 90010;
    private const int DestinationDropdownId = 90020;
    private const int DestinationPreviousId = 90021;
    private const int DestinationNextId = 90022;
    private const int DestinationRowIdBase = 92000;
    private const int Padding = 20;
    private const int Gap = 12;
    private static readonly Color Ink = new(91, 48, 30);
    private static readonly Color MutedInk = new(132, 89, 54);
    private static readonly Color Paper = new(255, 232, 186);
    private static readonly Color PaperShade = new(244, 207, 148);
    private static readonly Color Wood = new(139, 77, 39);
    private static readonly Color Leaf = new(85, 128, 61);
    private static readonly WorkerTaskKind[] TaskKinds =
    {
        WorkerTaskKind.WaterCrops, WorkerTaskKind.HarvestCrops, WorkerTaskKind.TendCrops, WorkerTaskKind.Idle,
    };

    private readonly WorkerShellManager workerShellManager;
    private readonly WorkerBehaviorManager workerBehaviorManager;
    private readonly WorkerCustomizationManager workerCustomizationManager;
    private readonly List<WorkerSummarySnapshot> workerSnapshots = new();
    private readonly Dictionary<string, WorkerRuntimeSnapshot> runtimeSnapshots = new(StringComparer.Ordinal);
    private readonly List<ClickableComponent> workerRows = new();
    private readonly List<ClickableComponent> orderButtons = new();
    private readonly List<WorkerChestOption> chestOptions = new();
    private readonly List<ClickableComponent> destinationRows = new();
    private ClickableComponent hireButton = null!;
    private ClickableComponent previousButton = null!;
    private ClickableComponent nextButton = null!;
    private ClickableComponent payButton = null!;
    private ClickableComponent dismissButton = null!;
    private ClickableComponent confirmButton = null!;
    private ClickableComponent cancelButton = null!;
    private ClickableComponent rosterTabButton = null!;
    private ClickableComponent farmerJobsTabButton = null!;
    private ClickableComponent storageTabButton = null!;
    private ClickableComponent destinationDropdownButton = null!;
    private ClickableComponent destinationPreviousButton = null!;
    private ClickableComponent destinationNextButton = null!;
    private Rectangle headerBounds;
    private Rectangle rosterBounds;
    private Rectangle detailsBounds;
    private Rectangle ordersBounds;
    private Rectangle footerBounds;
    private Rectangle confirmationBounds;
    private WorkerMenuTab currentTab = WorkerMenuTab.Roster;
    private string? selectedWorkerId;
    private string? pendingDismissalId;
    private string pendingDismissalName = string.Empty;
    private string feedback = "Choose a worker and give them an order. Orders repeat each day.";
    private string hoverText = string.Empty;
    private bool feedbackIsError;
    private int firstVisibleWorker;
    private int visibleWorkerCount = 1;
    private int firstVisibleDestination;
    private int visibleDestinationCount = 1;
    private bool destinationDropdownOpen;
    private double refreshMilliseconds;
    private ResizeEdges resizeEdges;
    private Point resizeStartMouse;
    private Rectangle resizeStartBounds;

    public WorkerControlMenu(
        WorkerShellManager workerShellManager,
        WorkerBehaviorManager workerBehaviorManager,
        WorkerCustomizationManager workerCustomizationManager,
        string? selectedWorkerId = null)
        : base(0, 0, GetMenuWidth(1200), GetMenuHeight(840), showUpperRightCloseButton: true)
    {
        this.workerShellManager = workerShellManager;
        this.workerBehaviorManager = workerBehaviorManager;
        this.workerCustomizationManager = workerCustomizationManager;
        this.selectedWorkerId = selectedWorkerId;
        this.closeSound = "bigDeSelect";
        this.CenterOnScreen();
        this.RefreshSnapshots();
        this.RefreshChestOptions();
        this.RebuildLayout();
        this.ShowSelectedWorkerPage();
    }

    public override void update(GameTime time)
    {
        base.update(time);
        this.refreshMilliseconds += time.ElapsedGameTime.TotalMilliseconds;
        if (this.refreshMilliseconds >= 250)
        {
            this.refreshMilliseconds = 0;
            this.RefreshSnapshots();
        }

        if (this.pendingDismissalId is null && this.currentlySnappedComponent is { } component)
        {
            int index = component.myID - WorkerRowIdBase;
            if (index >= 0 && index < this.workerSnapshots.Count)
            {
                this.SelectWorker(this.workerSnapshots[index].WorkerId);
            }
        }
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        this.width = GetMenuWidth(this.width);
        this.height = GetMenuHeight(this.height);
        this.CenterOnScreen();
        this.RebuildLayout(this.currentlySnappedComponent?.myID);
    }

    public override void snapToDefaultClickableComponent()
    {
        int id = this.pendingDismissalId is not null
            ? CancelId
            : this.workerRows.Count > 0 ? this.workerRows[0].myID : HireId;
        this.currentlySnappedComponent = this.getComponentWithID(id);
        if (this.currentlySnappedComponent is not null)
        {
            this.snapCursorToCurrentSnappedComponent();
        }
    }

    public void RequestClose()
    {
        if (this.pendingDismissalId is not null)
        {
            this.CancelDismissal();
            return;
        }

        this.exitThisMenu();
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape)
        {
            this.RequestClose();
            return;
        }

        if (this.pendingDismissalId is null && (key == Keys.PageDown || key == Keys.PageUp))
        {
            if (this.currentTab == WorkerMenuTab.Storage && this.destinationDropdownOpen)
                this.ScrollDestinations(key == Keys.PageDown ? 1 : -1);
            else
                this.ChangePage(key == Keys.PageDown ? 1 : -1);
            return;
        }

        base.receiveKeyPress(key);
    }

    public override void receiveScrollWheelAction(int direction)
    {
        if (this.pendingDismissalId is null && this.currentTab == WorkerMenuTab.Storage
            && this.destinationDropdownOpen && this.ordersBounds.Contains(Game1.getMouseX(), Game1.getMouseY()))
        {
            this.ScrollDestinations(direction < 0 ? 1 : -1);
            return;
        }
        if (this.pendingDismissalId is null && this.rosterBounds.Contains(Game1.getMouseX(), Game1.getMouseY()))
        {
            this.ChangePage(direction < 0 ? 1 : -1);
        }
    }

    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        if (this.upperRightCloseButton?.containsPoint(x, y) == true)
        {
            this.RequestClose();
            return;
        }

        if (this.pendingDismissalId is not null)
        {
            if (this.cancelButton.containsPoint(x, y))
            {
                this.CancelDismissal();
            }
            else if (this.confirmButton.containsPoint(x, y))
            {
                this.ConfirmDismissal();
            }
            return;
        }

        this.resizeEdges = this.GetResizeEdges(x, y);
        if (this.resizeEdges != ResizeEdges.None)
        {
            this.resizeStartMouse = new Point(x, y);
            this.resizeStartBounds = new Rectangle(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height);
            return;
        }

        if (this.previousButton.containsPoint(x, y))
        {
            this.ChangePage(-1);
            return;
        }
        if (this.nextButton.containsPoint(x, y))
        {
            this.ChangePage(1);
            return;
        }
        if (this.rosterTabButton.containsPoint(x, y))
        {
            this.currentTab = WorkerMenuTab.Roster;
            this.destinationDropdownOpen = false;
            this.RefreshClickableComponents(RosterTabId);
            Game1.playSound("smallSelect");
            return;
        }
        if (this.farmerJobsTabButton.containsPoint(x, y))
        {
            this.currentTab = WorkerMenuTab.FarmerJobs;
            this.destinationDropdownOpen = false;
            this.RefreshClickableComponents(FarmerJobsTabId);
            Game1.playSound("smallSelect");
            return;
        }
        if (this.storageTabButton.containsPoint(x, y))
        {
            this.currentTab = WorkerMenuTab.Storage;
            this.RefreshChestOptions();
            this.RebuildLayout(StorageTabId);
            Game1.playSound("smallSelect");
            return;
        }
        foreach (ClickableComponent row in this.workerRows)
        {
            if (row.containsPoint(x, y))
            {
                this.SelectWorker(this.workerSnapshots[row.myID - WorkerRowIdBase].WorkerId);
                this.currentlySnappedComponent = row;
                Game1.playSound("smallSelect");
                return;
            }
        }

        if (this.hireButton.containsPoint(x, y))
        {
            if (this.EnsureHost())
            {
                this.StartHiring();
            }
            return;
        }

        if (this.currentTab == WorkerMenuTab.Storage)
        {
            if (this.destinationDropdownButton.containsPoint(x, y))
            {
                this.RefreshChestOptions();
                this.destinationDropdownOpen = !this.destinationDropdownOpen;
                this.RebuildLayout(DestinationDropdownId);
                Game1.playSound("smallSelect");
                return;
            }
            if (this.destinationDropdownOpen)
            {
                if (this.destinationPreviousButton.containsPoint(x, y))
                {
                    this.ScrollDestinations(-1);
                    return;
                }
                if (this.destinationNextButton.containsPoint(x, y))
                {
                    this.ScrollDestinations(1);
                    return;
                }
                foreach (ClickableComponent row in this.destinationRows)
                {
                    if (!row.containsPoint(x, y))
                        continue;
                    if (this.EnsureHost())
                    {
                        int index = row.myID - DestinationRowIdBase;
                        WorkerHarvestDestination? destination = index == 0 ? null : this.chestOptions[index - 1].ToDestination();
                        bool success = this.workerShellManager.TrySetHarvestDestination(destination, out string message);
                        this.SetFeedback(message, !success);
                        if (success)
                            this.destinationDropdownOpen = false;
                        this.RefreshChestOptions();
                        this.RebuildLayout(DestinationDropdownId);
                    }
                    return;
                }
            }
        }

        if (this.currentTab == WorkerMenuTab.FarmerJobs)
        {
            for (int index = 0; index < this.orderButtons.Count; index++)
            {
                if (!this.orderButtons[index].containsPoint(x, y))
                    continue;
                if (this.EnsureSelectedHostWorker())
                {
                    bool success = this.workerBehaviorManager.TryAssignTask(this.selectedWorkerId!, TaskKinds[index], out string message);
                    this.SetFeedback(message, !success);
                    this.RefreshSnapshots();
                    this.currentlySnappedComponent = this.orderButtons[index];
                }
                return;
            }
        }

        if (this.payButton.containsPoint(x, y) && this.EnsureSelectedHostWorker())
        {
            if (this.workerShellManager.CanWorkerWorkToday(this.selectedWorkerId!))
            {
                this.SetFeedback("This worker's wages are already paid for today.");
                return;
            }
            bool success = this.workerShellManager.TryPayWorkerForToday(this.selectedWorkerId!, out string message);
            this.SetFeedback(message, !success);
            this.RefreshSnapshots();
            return;
        }

        if (this.dismissButton.containsPoint(x, y) && this.EnsureSelectedHostWorker())
        {
            this.pendingDismissalId = this.selectedWorkerId;
            this.pendingDismissalName = this.GetSelectedWorker()?.DisplayName ?? "this worker";
            this.RefreshClickableComponents(CancelId);
            Game1.playSound("smallSelect");
        }
    }

    public override void leftClickHeld(int x, int y)
    {
        if (this.resizeEdges == ResizeEdges.None)
        {
            return;
        }
        int dx = x - this.resizeStartMouse.X;
        int dy = y - this.resizeStartMouse.Y;
        int widthDelta = (this.resizeEdges & ResizeEdges.Left) != 0 ? -dx : dx;
        int heightDelta = (this.resizeEdges & ResizeEdges.Top) != 0 ? -dy : dy;
        this.width = GetMenuWidth(this.resizeStartBounds.Width + (((this.resizeEdges & (ResizeEdges.Left | ResizeEdges.Right)) != 0) ? widthDelta : 0));
        this.height = GetMenuHeight(this.resizeStartBounds.Height + (((this.resizeEdges & (ResizeEdges.Top | ResizeEdges.Bottom)) != 0) ? heightDelta : 0));
        this.xPositionOnScreen = (this.resizeEdges & ResizeEdges.Left) != 0 ? this.resizeStartBounds.Right - this.width : this.resizeStartBounds.X;
        this.yPositionOnScreen = (this.resizeEdges & ResizeEdges.Top) != 0 ? this.resizeStartBounds.Bottom - this.height : this.resizeStartBounds.Y;
        this.xPositionOnScreen = Math.Clamp(this.xPositionOnScreen, 0, Math.Max(0, Game1.uiViewport.Width - this.width));
        this.yPositionOnScreen = Math.Clamp(this.yPositionOnScreen, 0, Math.Max(0, Game1.uiViewport.Height - this.height));
        this.RebuildLayout(this.currentlySnappedComponent?.myID);
    }

    public override void releaseLeftClick(int x, int y)
    {
        this.resizeEdges = ResizeEdges.None;
        base.releaseLeftClick(x, y);
    }

    public override void performHoverAction(int x, int y)
    {
        this.hoverText = string.Empty;
        if (this.pendingDismissalId is not null)
        {
            return;
        }
        if (this.hireButton.containsPoint(x, y))
        {
            this.hoverText = Context.IsMainPlayer
                ? $"Choose an appearance, then hire for {WorkerEmploymentTerms.HiringCost}g. Includes today's pay. Each following day costs {WorkerEmploymentTerms.DailyWage}g."
                : "The host manages hiring, wages, and orders.";
        }
        if (this.currentTab == WorkerMenuTab.Storage && this.destinationDropdownButton.containsPoint(x, y))
            this.hoverText = "Choose the shared destination for every worker's harvested items.";
        if (this.currentTab != WorkerMenuTab.FarmerJobs)
        {
            return;
        }
        for (int index = 0; index < this.orderButtons.Count; index++)
        {
            if (this.orderButtons[index].containsPoint(x, y))
            {
                this.hoverText = GetTaskDescription(TaskKinds[index]);
            }
        }
        base.performHoverAction(x, y);
    }

    public override void draw(SpriteBatch b)
    {
        if (!Game1.options.showClearBackgrounds)
        {
            b.Draw(Game1.fadeToBlackRect, Game1.graphics.GraphicsDevice.Viewport.Bounds, Color.Black * 0.7f);
        }
        Game1.drawDialogueBox(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, speaker: false, drawOnlyBox: true);
        this.DrawHeader(b);
        this.DrawRoster(b);
        this.DrawDetails(b);
        this.DrawOrders(b);
        this.DrawFooter(b);
        base.draw(b);
        if (this.pendingDismissalId is not null)
        {
            this.DrawDismissalConfirmation(b);
        }
        else if (this.hoverText.Length > 0)
        {
            IClickableMenu.drawHoverText(b, this.hoverText, Game1.smallFont);
        }
        this.drawMouse(b);
    }

    private static int GetMenuWidth(int preferred)
    {
        int maximum = Math.Max(320, Game1.uiViewport.Width - 24);
        return Math.Clamp(preferred, Math.Min(800, maximum), maximum);
    }

    private static int GetMenuHeight(int preferred)
    {
        int maximum = Math.Max(320, Game1.uiViewport.Height - 24);
        return Math.Clamp(preferred, Math.Min(560, maximum), maximum);
    }

    private void CenterOnScreen()
    {
        this.xPositionOnScreen = (Game1.uiViewport.Width - this.width) / 2;
        this.yPositionOnScreen = (Game1.uiViewport.Height - this.height) / 2;
    }

    private void RefreshSnapshots()
    {
        IReadOnlyList<WorkerSummarySnapshot> latest = this.workerShellManager.GetWorkerSummaries();
        bool changed = latest.Count != this.workerSnapshots.Count;
        if (!changed)
        {
            for (int i = 0; i < latest.Count; i++)
            {
                changed |= latest[i].WorkerId != this.workerSnapshots[i].WorkerId;
            }
        }
        this.workerSnapshots.Clear();
        this.workerSnapshots.AddRange(latest);
        this.runtimeSnapshots.Clear();
        foreach (WorkerSummarySnapshot snapshot in this.workerSnapshots)
        {
            this.runtimeSnapshots[snapshot.WorkerId] = this.workerBehaviorManager.GetRuntimeSnapshot(snapshot.WorkerId);
        }
        if (this.GetSelectedWorker() is null)
        {
            this.selectedWorkerId = this.workerSnapshots.Count > 0 ? this.workerSnapshots[0].WorkerId : null;
        }
        if (changed && this.hireButton is not null)
        {
            this.RebuildLayout();
        }
    }

    private void RefreshChestOptions()
    {
        this.chestOptions.Clear();
        this.chestOptions.AddRange(WorkerChestCatalog.GetAvailableChests());
    }

    private void RebuildLayout(int? preferredSnapId = null)
    {
        int inset = this.width < 800 ? 28 : 40;
        int left = this.xPositionOnScreen + inset;
        int right = this.xPositionOnScreen + this.width - inset;
        this.headerBounds = new Rectangle(left, this.yPositionOnScreen + 76, right - left, 64);
        this.footerBounds = new Rectangle(left, this.yPositionOnScreen + this.height - 90, right - left, 58);
        int bodyTop = this.headerBounds.Bottom + Gap;
        int bodyHeight = Math.Max(100, this.footerBounds.Top - Gap - bodyTop);
        int rosterWidth = Math.Clamp(this.headerBounds.Width / 3, Math.Min(210, this.headerBounds.Width / 3), 350);
        this.rosterBounds = new Rectangle(left, bodyTop, rosterWidth, bodyHeight);
        int detailLeft = this.rosterBounds.Right + Gap;
        int detailWidth = Math.Max(1, right - detailLeft);
        int detailHeight = Math.Clamp((int)(bodyHeight * 0.46f), Math.Min(120, bodyHeight / 2), 190);
        this.detailsBounds = new Rectangle(detailLeft, bodyTop, detailWidth, detailHeight);
        this.ordersBounds = new Rectangle(detailLeft, this.detailsBounds.Bottom + Gap, detailWidth, Math.Max(1, bodyHeight - detailHeight - Gap));
        int hireWidth = Math.Min(240, this.headerBounds.Width / 3);
        this.hireButton = Button(HireId, new Rectangle(right - hireWidth, this.headerBounds.Y + 4, hireWidth, 56));
        int tabWidth = Math.Min(160, Math.Max(106, (this.headerBounds.Width - hireWidth - Gap * 4 - 8) / 3));
        this.rosterTabButton = Button(RosterTabId, new Rectangle(this.headerBounds.X + 4, this.headerBounds.Y + 4, tabWidth, 56));
        this.farmerJobsTabButton = Button(FarmerJobsTabId, new Rectangle(this.rosterTabButton.bounds.Right + Gap, this.headerBounds.Y + 4, tabWidth, 56));
        this.storageTabButton = Button(StorageTabId, new Rectangle(this.farmerJobsTabButton.bounds.Right + Gap, this.headerBounds.Y + 4, tabWidth, 56));
        int managementWidth = Math.Min(132, Math.Max(80, this.footerBounds.Width / 5));
        this.dismissButton = Button(DismissId, new Rectangle(right - managementWidth, this.footerBounds.Y + 4, managementWidth, 50));
        this.payButton = Button(PayId, new Rectangle(this.dismissButton.bounds.Left - managementWidth - Gap, this.footerBounds.Y + 4, managementWidth, 50));
        int paginationTop = this.rosterBounds.Bottom - 50;
        this.previousButton = Button(PreviousId, new Rectangle(this.rosterBounds.X + 14, paginationTop, 48, 36));
        this.nextButton = Button(NextId, new Rectangle(this.rosterBounds.Right - 62, paginationTop, 48, 36));
        int rowsTop = this.rosterBounds.Y + 58;
        int rowsHeight = Math.Max(1, paginationTop - Gap - rowsTop);
        this.visibleWorkerCount = Math.Max(1, rowsHeight / 82);
        this.firstVisibleWorker = Math.Clamp(this.firstVisibleWorker, 0, this.GetLastPageStart());
        this.firstVisibleWorker = (this.firstVisibleWorker / this.visibleWorkerCount) * this.visibleWorkerCount;
        int rowHeight = Math.Min(100, Math.Max(48, rowsHeight / this.visibleWorkerCount - 6));
        this.workerRows.Clear();
        for (int i = this.firstVisibleWorker; i < Math.Min(this.workerSnapshots.Count, this.firstVisibleWorker + this.visibleWorkerCount); i++)
        {
            this.workerRows.Add(Button(WorkerRowIdBase + i, new Rectangle(this.rosterBounds.X + 14, rowsTop + (i - this.firstVisibleWorker) * (rowHeight + 6), this.rosterBounds.Width - 28, rowHeight)));
        }
        this.orderButtons.Clear();
        int ordersTop = this.ordersBounds.Y + (this.ordersBounds.Height >= 220 ? 56 : 12);
        int cardGap = 8;
        int cardWidth = Math.Max(1, (this.ordersBounds.Width - 28 - cardGap) / 2);
        int cardHeight = Math.Max(1, (this.ordersBounds.Bottom - 14 - ordersTop - cardGap) / 2);
        for (int i = 0; i < TaskKinds.Length; i++)
        {
            this.orderButtons.Add(Button(OrderIdBase + i, new Rectangle(this.ordersBounds.X + 14 + (i % 2) * (cardWidth + cardGap), ordersTop + (i / 2) * (cardHeight + cardGap), cardWidth, cardHeight)));
        }
        bool tallStoragePanel = this.ordersBounds.Height >= 220;
        this.destinationDropdownButton = Button(DestinationDropdownId, new Rectangle(
            this.ordersBounds.X + 18, this.ordersBounds.Y + (tallStoragePanel ? 70 : 14),
            this.ordersBounds.Width - 36, tallStoragePanel ? 54 : 46));
        int destinationTop = this.destinationDropdownButton.bounds.Bottom + 9;
        int destinationPagerY = this.ordersBounds.Bottom - 38;
        this.visibleDestinationCount = Math.Max(1, (destinationPagerY - destinationTop) / 48);
        this.firstVisibleDestination = Math.Clamp(this.firstVisibleDestination, 0, Math.Max(0, this.chestOptions.Count + 1 - this.visibleDestinationCount));
        this.destinationRows.Clear();
        if (this.destinationDropdownOpen)
        {
            for (int i = this.firstVisibleDestination; i < Math.Min(this.chestOptions.Count + 1, this.firstVisibleDestination + this.visibleDestinationCount); i++)
                this.destinationRows.Add(Button(DestinationRowIdBase + i, new Rectangle(this.ordersBounds.X + 22, destinationTop + (i - this.firstVisibleDestination) * 48, this.ordersBounds.Width - 44, 44)));
        }
        this.destinationPreviousButton = Button(DestinationPreviousId, new Rectangle(this.ordersBounds.X + 22, destinationPagerY, 42, 30));
        this.destinationNextButton = Button(DestinationNextId, new Rectangle(this.ordersBounds.Right - 64, destinationPagerY, 42, 30));
        int confirmationWidth = Math.Min(560, this.width - 48);
        int confirmationHeight = Math.Min(270, this.height - 48);
        this.confirmationBounds = new Rectangle(this.xPositionOnScreen + (this.width - confirmationWidth) / 2, this.yPositionOnScreen + (this.height - confirmationHeight) / 2, confirmationWidth, confirmationHeight);
        int confirmWidth = Math.Max(80, (confirmationWidth - 68) / 2);
        this.cancelButton = Button(CancelId, new Rectangle(this.confirmationBounds.X + 24, this.confirmationBounds.Bottom - 78, confirmWidth, 54));
        this.confirmButton = Button(ConfirmId, new Rectangle(this.cancelButton.bounds.Right + 20, this.cancelButton.bounds.Y, confirmWidth, 54));
        this.initializeUpperRightCloseButton();
        this.upperRightCloseButton.myID = IClickableMenu.upperRightCloseButton_ID;
        this.RefreshClickableComponents(preferredSnapId);
    }

    private static ClickableComponent Button(int id, Rectangle bounds)
    {
        return new ClickableComponent(bounds, id.ToString()) { myID = id };
    }

    private void RefreshClickableComponents(int? preferredSnapId)
    {
        this.allClickableComponents = new List<ClickableComponent>();
        if (this.pendingDismissalId is not null)
        {
            this.allClickableComponents.Add(this.cancelButton);
            this.allClickableComponents.Add(this.confirmButton);
        }
        else
        {
            this.allClickableComponents.Add(this.rosterTabButton);
            this.allClickableComponents.Add(this.farmerJobsTabButton);
            this.allClickableComponents.Add(this.storageTabButton);
            this.allClickableComponents.AddRange(this.workerRows);
            this.allClickableComponents.Add(this.hireButton);
            if (this.currentTab == WorkerMenuTab.FarmerJobs)
                this.allClickableComponents.AddRange(this.orderButtons);
            if (this.currentTab == WorkerMenuTab.Storage)
            {
                this.allClickableComponents.Add(this.destinationDropdownButton);
                if (this.destinationDropdownOpen)
                {
                    this.allClickableComponents.AddRange(this.destinationRows);
                    if (this.firstVisibleDestination > 0)
                        this.allClickableComponents.Add(this.destinationPreviousButton);
                    if (this.firstVisibleDestination + this.visibleDestinationCount < this.chestOptions.Count + 1)
                        this.allClickableComponents.Add(this.destinationNextButton);
                }
            }
            this.allClickableComponents.Add(this.payButton);
            this.allClickableComponents.Add(this.dismissButton);
            if (this.firstVisibleWorker > 0)
            {
                this.allClickableComponents.Add(this.previousButton);
            }
            if (this.firstVisibleWorker + this.visibleWorkerCount < this.workerSnapshots.Count)
            {
                this.allClickableComponents.Add(this.nextButton);
            }
        }
        this.allClickableComponents.Add(this.upperRightCloseButton);
        foreach (ClickableComponent component in this.allClickableComponents)
        {
            component.leftNeighborID = this.FindNeighbor(component, -1, 0);
            component.rightNeighborID = this.FindNeighbor(component, 1, 0);
            component.upNeighborID = this.FindNeighbor(component, 0, -1);
            component.downNeighborID = this.FindNeighbor(component, 0, 1);
        }
        this.currentlySnappedComponent = preferredSnapId is int id ? this.getComponentWithID(id) : null;
        this.currentlySnappedComponent ??= this.getComponentWithID(this.pendingDismissalId is not null ? CancelId : this.workerRows.Count > 0 ? this.workerRows[0].myID : HireId);
        if (Game1.options.SnappyMenus && Game1.options.gamepadControls && this.currentlySnappedComponent is not null)
        {
            this.snapCursorToCurrentSnappedComponent();
        }
    }

    private int FindNeighbor(ClickableComponent source, int directionX, int directionY)
    {
        int nearestId = -99998;
        double nearestScore = double.MaxValue;
        foreach (ClickableComponent candidate in this.allClickableComponents)
        {
            int dx = candidate.bounds.Center.X - source.bounds.Center.X;
            int dy = candidate.bounds.Center.Y - source.bounds.Center.Y;
            int forward = directionX != 0 ? dx * directionX : dy * directionY;
            if (candidate == source || forward <= 0)
            {
                continue;
            }
            int lateral = directionX != 0 ? Math.Abs(dy) : Math.Abs(dx);
            double score = forward + lateral * 3;
            if (score < nearestScore)
            {
                nearestScore = score;
                nearestId = candidate.myID;
            }
        }
        return nearestId;
    }

    private void DrawHeader(SpriteBatch b)
    {
        this.DrawPanel(b, this.headerBounds);
        this.DrawButton(b, this.rosterTabButton, "Roster", true, Color.White, WorkerMenuArt.Icon.Ledger, this.currentTab == WorkerMenuTab.Roster);
        this.DrawButton(b, this.farmerJobsTabButton, "Farmer jobs", true, Color.White, WorkerMenuArt.Icon.Sprout, this.currentTab == WorkerMenuTab.FarmerJobs);
        this.DrawButton(b, this.storageTabButton, "Storage", true, Color.White, WorkerMenuArt.Icon.Chest, this.currentTab == WorkerMenuTab.Storage);
        this.DrawButton(b, this.hireButton, $"Hire worker  {WorkerEmploymentTerms.HiringCost}g", Context.IsMainPlayer, Color.White, WorkerMenuArt.Icon.Coin);
        Rectangle title = new(this.storageTabButton.bounds.Right + 12, this.headerBounds.Y + 14, this.hireButton.bounds.Left - this.storageTabButton.bounds.Right - 24, 34);
        if (title.Width > 90)
            this.DrawText(b, "FARM CREW", title, Ink, centered: true);
    }

    private void DrawRoster(SpriteBatch b)
    {
        this.DrawPanel(b, this.rosterBounds);
        this.DrawSectionHeading(b, this.rosterBounds, "YOUR CREW", $"{this.workerSnapshots.Count} hired");
        if (this.workerRows.Count == 0)
        {
            WorkerMenuArt.Draw(b, WorkerMenuArt.Icon.Sprout, this.rosterBounds.Center.X - 18, this.rosterBounds.Y + 106);
            this.DrawText(b, Context.IsMainPlayer ? "Hire your first worker to get the farm moving." : "No workers hired. The host can hire workers here.", new Rectangle(this.rosterBounds.X + Padding, this.rosterBounds.Y + 165, this.rosterBounds.Width - Padding * 2, 80), MutedInk, centered: true);
        }
        foreach (ClickableComponent row in this.workerRows)
        {
            WorkerSummarySnapshot snapshot = this.workerSnapshots[row.myID - WorkerRowIdBase];
            bool selected = snapshot.WorkerId == this.selectedWorkerId;
            this.DrawCard(b, row.bounds, selected ? new Color(255, 222, 150) : Paper, selected);
            int faceSize = Math.Min(52, row.bounds.Height - 20);
            Rectangle faceFrame = new(row.bounds.X + 10, row.bounds.Y + (row.bounds.Height - faceSize - 6) / 2, faceSize + 6, faceSize + 6);
            DrawRect(b, faceFrame, Wood);
            Rectangle face = new(faceFrame.X + 3, faceFrame.Y + 3, faceSize, faceSize);
            this.DrawWorkerFace(b, snapshot.WorkerId, face);
            int textX = faceFrame.Right + 10;
            this.DrawText(b, snapshot.DisplayName, new Rectangle(textX, row.bounds.Y + 10, row.bounds.Right - textX - 10, 27), Ink);
            WorkerRuntimeSnapshot runtime = this.runtimeSnapshots[snapshot.WorkerId];
            DrawRect(b, new Rectangle(textX, row.bounds.Y + 43, 7, 7), runtime.AssignedTask == WorkerTaskKind.Idle ? MutedInk : Leaf);
            this.DrawText(b, runtime.State, new Rectangle(textX + 12, row.bounds.Y + 36, row.bounds.Right - textX - 22, Math.Max(1, row.bounds.Height - 42)), MutedInk);
        }
        bool previous = this.firstVisibleWorker > 0;
        bool next = this.firstVisibleWorker + this.visibleWorkerCount < this.workerSnapshots.Count;
        this.DrawButton(b, this.previousButton, "<", previous, Color.White);
        this.DrawButton(b, this.nextButton, ">", next, Color.White);
        string pages = $"{this.firstVisibleWorker / this.visibleWorkerCount + 1} / {Math.Max(1, (this.workerSnapshots.Count + this.visibleWorkerCount - 1) / this.visibleWorkerCount)}";
        this.DrawText(b, pages, new Rectangle(this.previousButton.bounds.Right + 4, this.previousButton.bounds.Y + 6, this.nextButton.bounds.Left - this.previousButton.bounds.Right - 8, 26), Game1.textColor * 0.75f, centered: true);
    }

    private void DrawDetails(SpriteBatch b)
    {
        this.DrawPanel(b, this.detailsBounds);
        this.DrawSectionHeading(b, this.detailsBounds, "WORKER PROFILE", string.Empty);
        WorkerSummarySnapshot? selected = this.GetSelectedWorker();
        Rectangle content = new(this.detailsBounds.X + Padding, this.detailsBounds.Y + 58, this.detailsBounds.Width - Padding * 2, this.detailsBounds.Height - 72);
        if (selected is not WorkerSummarySnapshot worker)
        {
            this.DrawText(b, "Select a worker to see their assignment and today's progress.", content, MutedInk);
            return;
        }
        WorkerRuntimeSnapshot runtime = this.runtimeSnapshots[worker.WorkerId];
        string location = worker.IsSpawned ? $"{worker.CurrentLocationName ?? "Unknown"} ({FormatTile(worker.CurrentTile)})" : "Waiting to appear";
        if (this.detailsBounds.Height < 160)
        {
            this.DrawText(b, $"{worker.DisplayName} | {GetTaskLabel(runtime.AssignedTask)}\n{runtime.Status}\n{location} | Done: {runtime.CompletedToday}", content, Ink);
            return;
        }
        Rectangle portraitFrame = new(content.X, content.Y + 4, 74, 74);
        this.DrawCard(b, portraitFrame, Paper, false);
        this.DrawWorkerFace(b, worker.WorkerId, new Rectangle(portraitFrame.X + 11, portraitFrame.Y + 11, 52, 52));
        int infoX = portraitFrame.Right + 16;
        int infoWidth = Math.Max(1, content.Right - infoX);
        this.DrawText(b, worker.DisplayName, new Rectangle(infoX, content.Y, infoWidth, 30), Ink);
        this.DrawText(b, GetTaskLabel(runtime.AssignedTask), new Rectangle(infoX, content.Y + 34, infoWidth, 28), runtime.AssignedTask == WorkerTaskKind.Idle ? MutedInk : Leaf);
        this.DrawText(b, runtime.Status, new Rectangle(infoX, content.Y + 56, infoWidth, 24), MutedInk);
        int metricsY = this.detailsBounds.Bottom - 44;
        DrawRect(b, new Rectangle(this.detailsBounds.X + 20, metricsY - 8, this.detailsBounds.Width - 40, 2), PaperShade);
        this.DrawText(b, location, new Rectangle(this.detailsBounds.X + 22, metricsY, Math.Max(1, this.detailsBounds.Width - 215), 30), MutedInk);
        this.DrawText(b, $"Done today  {runtime.CompletedToday}", new Rectangle(this.detailsBounds.Right - 185, metricsY, 163, 30), Ink, centered: true);
    }

    private void DrawOrders(SpriteBatch b)
    {
        this.DrawPanel(b, this.ordersBounds);
        if (this.currentTab == WorkerMenuTab.Storage)
        {
            this.DrawStoragePanel(b);
            return;
        }
        if (this.currentTab != WorkerMenuTab.FarmerJobs)
        {
            this.DrawSectionHeading(b, this.ordersBounds, "A DAY ON THE FARM", string.Empty);
            if (this.ordersBounds.Height < 240)
            {
                WorkerMenuArt.Draw(b, WorkerMenuArt.Icon.Sprout, this.ordersBounds.Center.X - 18, this.ordersBounds.Y + 55);
                this.DrawText(b, "Choose Farmer jobs to set a daily order.", new Rectangle(this.ordersBounds.X + Padding, this.ordersBounds.Y + 105, this.ordersBounds.Width - Padding * 2, 45), Ink, centered: true);
                return;
            }
            int iconY = this.ordersBounds.Y + Math.Max(72, this.ordersBounds.Height / 3);
            int centerX = this.ordersBounds.Center.X;
            WorkerMenuArt.Draw(b, WorkerMenuArt.Icon.Harvest, centerX - 114, iconY);
            WorkerMenuArt.Draw(b, WorkerMenuArt.Icon.Sprout, centerX - 18, iconY - 10);
            WorkerMenuArt.Draw(b, WorkerMenuArt.Icon.Water, centerX + 78, iconY);
            this.DrawText(b, "HARVEST", new Rectangle(centerX - 143, iconY + 48, 96, 28), MutedInk, centered: true);
            this.DrawText(b, "GROW", new Rectangle(centerX - 48, iconY + 48, 96, 28), MutedInk, centered: true);
            this.DrawText(b, "WATER", new Rectangle(centerX + 48, iconY + 48, 96, 28), MutedInk, centered: true);
            this.DrawText(b, "Choose Farmer jobs to set a daily order.", new Rectangle(this.ordersBounds.X + Padding, iconY + 105, this.ordersBounds.Width - Padding * 2, 54), Ink, centered: true);
            if (this.ordersBounds.Height >= 330)
            {
                Rectangle orderSummary = new(this.ordersBounds.X + 38, this.ordersBounds.Bottom - 90, this.ordersBounds.Width - 76, 63);
                this.DrawCard(b, orderSummary, Paper, false);
                WorkerSummarySnapshot? currentWorker = this.GetSelectedWorker();
                WorkerTaskKind currentOrder = currentWorker is WorkerSummarySnapshot selectedWorker
                    ? this.runtimeSnapshots[selectedWorker.WorkerId].AssignedTask
                    : WorkerTaskKind.Idle;
                WorkerMenuArt.Draw(b, currentOrder == WorkerTaskKind.Idle ? WorkerMenuArt.Icon.Home : WorkerMenuArt.Icon.Ledger, orderSummary.X + 18, orderSummary.Y + 13, 3);
                this.DrawText(b, "CURRENT ORDER", new Rectangle(orderSummary.X + 69, orderSummary.Y + 8, orderSummary.Width - 85, 23), MutedInk);
                this.DrawText(b, currentWorker is null ? "Select a worker" : GetTaskLabel(currentOrder), new Rectangle(orderSummary.X + 69, orderSummary.Y + 29, orderSummary.Width - 85, 26), Ink);
            }
            return;
        }
        if (this.ordersBounds.Height >= 220)
        {
            this.DrawSectionHeading(b, this.ordersBounds, "DAILY ORDERS", "repeat each day");
        }
        bool enabled = Context.IsMainPlayer && this.selectedWorkerId is not null;
        WorkerTaskKind? assigned = this.selectedWorkerId is not null && this.runtimeSnapshots.TryGetValue(this.selectedWorkerId, out WorkerRuntimeSnapshot runtime) ? runtime.AssignedTask : null;
        for (int i = 0; i < this.orderButtons.Count; i++)
        {
            ClickableComponent button = this.orderButtons[i];
            bool selected = assigned == TaskKinds[i];
            this.DrawOrderCard(b, button, TaskKinds[i], enabled, selected);
        }
    }

    private void DrawStoragePanel(SpriteBatch b)
    {
        if (this.ordersBounds.Height >= 220)
            this.DrawSectionHeading(b, this.ordersBounds, "HARVEST STORAGE", $"{this.chestOptions.Count} chests found");

        WorkerHarvestDestination? destination = this.workerShellManager.GetHarvestDestination();
        WorkerChestOption? selectedChest = destination is null ? null : this.chestOptions.Find(option =>
            option.LocationName == destination.LocationName && option.Tile == destination.Tile);
        string selectedLabel = destination is null ? "Shipping bin" : selectedChest?.Label ?? "Missing chest — using shipping bin";
        this.DrawButton(b, this.destinationDropdownButton, $"{selectedLabel}  {(this.destinationDropdownOpen ? "^" : "v")}", true, Color.White,
            destination is null ? WorkerMenuArt.Icon.Coin : WorkerMenuArt.Icon.Chest);

        if (!this.destinationDropdownOpen)
        {
            string description = "All workers put harvested items here. If a chest is full or unavailable, remaining items go to the shipping bin.";
            this.DrawText(b, description, new Rectangle(this.ordersBounds.X + 24, this.destinationDropdownButton.bounds.Bottom + 20,
                this.ordersBounds.Width - 48, Math.Max(1, this.ordersBounds.Bottom - this.destinationDropdownButton.bounds.Bottom - 40)), MutedInk);
            return;
        }

        foreach (ClickableComponent row in this.destinationRows)
        {
            int index = row.myID - DestinationRowIdBase;
            WorkerChestOption? option = index == 0 ? null : this.chestOptions[index - 1];
            bool isSelected = index == 0 ? destination is null
                : destination is not null && option!.LocationName == destination.LocationName && option.Tile == destination.Tile;
            this.DrawCard(b, row.bounds, isSelected ? new Color(244, 221, 165) : Paper, isSelected);
            WorkerMenuArt.Draw(b, index == 0 ? WorkerMenuArt.Icon.Coin : WorkerMenuArt.Icon.Chest, row.bounds.X + 10, row.bounds.Y + 10, 2);
            this.DrawText(b, index == 0 ? "Shipping bin (default)" : option!.Label,
                new Rectangle(row.bounds.X + 43, row.bounds.Y + 6, row.bounds.Width - 53, row.bounds.Height - 12), Ink);
        }

        int total = this.chestOptions.Count + 1;
        this.DrawButton(b, this.destinationPreviousButton, "<", this.firstVisibleDestination > 0, Color.White);
        this.DrawButton(b, this.destinationNextButton, ">", this.firstVisibleDestination + this.visibleDestinationCount < total, Color.White);
        this.DrawText(b, $"{this.firstVisibleDestination + 1}-{Math.Min(total, this.firstVisibleDestination + this.visibleDestinationCount)} / {total}",
            new Rectangle(this.destinationPreviousButton.bounds.Right + 6, this.destinationPreviousButton.bounds.Y + 3,
                this.destinationNextButton.bounds.Left - this.destinationPreviousButton.bounds.Right - 12, 24), MutedInk, centered: true);
    }

    private void DrawFooter(SpriteBatch b)
    {
        string text = Context.IsMainPlayer ? this.feedback : "The host assigns work and pays wages. Press B to close.";
        DrawRect(b, new Rectangle(this.footerBounds.X + 5, this.footerBounds.Y + 8, 4, this.footerBounds.Height - 16), this.feedbackIsError ? Color.DarkRed : Leaf);
        this.DrawText(b, text, new Rectangle(this.footerBounds.X + 18, this.footerBounds.Y + 6, this.payButton.bounds.Left - this.footerBounds.X - Gap - 18, this.footerBounds.Height - 12), this.feedbackIsError ? Color.DarkRed : Ink);
        bool hasWorker = this.selectedWorkerId is not null;
        bool paid = hasWorker && this.workerShellManager.CanWorkerWorkToday(this.selectedWorkerId!);
        this.DrawButton(b, this.payButton, paid ? "Paid today" : $"Pay {WorkerEmploymentTerms.DailyWage}g", Context.IsMainPlayer && hasWorker && !paid, Color.White);
        this.DrawButton(b, this.dismissButton, "Dismiss", Context.IsMainPlayer && hasWorker, Color.White);
    }

    private void DrawDismissalConfirmation(SpriteBatch b)
    {
        b.Draw(Game1.fadeToBlackRect, new Rectangle(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height), Color.Black * 0.5f);
        this.DrawPanel(b, this.confirmationBounds);
        this.DrawText(b, $"Dismiss {this.pendingDismissalName}?\n\nThey will leave your roster. Hiring fees and wages are not refunded.", new Rectangle(this.confirmationBounds.X + 26, this.confirmationBounds.Y + 26, this.confirmationBounds.Width - 52, this.confirmationBounds.Height - 122), Game1.textColor);
        this.DrawButton(b, this.cancelButton, "Keep worker", true, Color.White);
        this.DrawButton(b, this.confirmButton, "Dismiss worker", true, Color.LightPink);
    }

    private void DrawPanel(SpriteBatch b, Rectangle bounds, Color? color = null)
    {
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            IClickableMenu.drawTextureBox(b, bounds.X, bounds.Y, bounds.Width, bounds.Height, color ?? Color.White);
        }
    }

    private static void DrawRect(SpriteBatch b, Rectangle bounds, Color color)
    {
        if (bounds.Width > 0 && bounds.Height > 0)
            b.Draw(Game1.staminaRect, bounds, color);
    }

    private void DrawCard(SpriteBatch b, Rectangle bounds, Color fill, bool selected)
    {
        DrawRect(b, bounds, new Color(106, 56, 31));
        DrawRect(b, new Rectangle(bounds.X + 3, bounds.Y + 3, bounds.Width - 6, bounds.Height - 6), fill);
        DrawRect(b, new Rectangle(bounds.X + 4, bounds.Y + 4, bounds.Width - 8, 3), Color.White * 0.45f);
        DrawRect(b, new Rectangle(bounds.X + 4, bounds.Bottom - 8, bounds.Width - 8, 4), selected ? Leaf : PaperShade);
    }

    private void DrawSectionHeading(SpriteBatch b, Rectangle panel, string heading, string note)
    {
        bool showNote = note.Length > 0 && panel.Width > 280;
        this.DrawText(b, heading, new Rectangle(panel.X + Padding, panel.Y + 14, Math.Max(1, panel.Width - Padding * 2 - (showNote ? 115 : 0)), 30), Ink);
        if (showNote)
            this.DrawText(b, note, new Rectangle(panel.Right - 135, panel.Y + 18, 110, 24), MutedInk, centered: true);
        DrawRect(b, new Rectangle(panel.X + 18, panel.Y + 50, panel.Width - 36, 2), PaperShade);
    }

    private void DrawButton(SpriteBatch b, ClickableComponent button, string label, bool enabled, Color color, WorkerMenuArt.Icon? icon = null, bool selected = false)
    {
        bool hovered = this.pendingDismissalId is null && button.containsPoint(Game1.getMouseX(), Game1.getMouseY());
        Color fill = !enabled ? new Color(211, 190, 158) : selected ? new Color(245, 216, 145) : hovered ? new Color(255, 236, 187) : color == Color.White ? Paper : color;
        this.DrawCard(b, button.bounds, fill, selected);
        int textX = button.bounds.X + 10;
        if (icon is WorkerMenuArt.Icon motif && button.bounds.Width >= 110)
        {
            WorkerMenuArt.Draw(b, motif, button.bounds.X + 12, button.bounds.Y + (button.bounds.Height - 24) / 2, 2);
            textX += 30;
        }
        this.DrawText(b, label, new Rectangle(textX, button.bounds.Y + 8, Math.Max(1, button.bounds.Right - textX - 10), Math.Max(1, button.bounds.Height - 16)), enabled ? Ink : MutedInk * 0.75f, centered: true);
    }

    private void DrawOrderCard(SpriteBatch b, ClickableComponent button, WorkerTaskKind task, bool enabled, bool selected)
    {
        bool hovered = this.pendingDismissalId is null && button.containsPoint(Game1.getMouseX(), Game1.getMouseY());
        Color fill = !enabled ? new Color(211, 190, 158) : selected ? new Color(244, 221, 165) : hovered ? new Color(255, 239, 198) : Paper;
        this.DrawCard(b, button.bounds, fill, selected);
        WorkerMenuArt.Icon icon = task switch
        {
            WorkerTaskKind.WaterCrops => WorkerMenuArt.Icon.Water,
            WorkerTaskKind.HarvestCrops => WorkerMenuArt.Icon.Harvest,
            WorkerTaskKind.TendCrops => WorkerMenuArt.Icon.Tend,
            _ => WorkerMenuArt.Icon.Home,
        };
        if (button.bounds.Height < 90)
        {
            WorkerMenuArt.Draw(b, icon, button.bounds.X + 10, button.bounds.Y + (button.bounds.Height - 24) / 2, 2);
            this.DrawText(b, GetTaskLabel(task), new Rectangle(button.bounds.X + 44, button.bounds.Y + 5, button.bounds.Width - 52, Math.Max(1, button.bounds.Height - 10)), enabled ? Ink : MutedInk);
            return;
        }
        int iconSize = 3;
        WorkerMenuArt.Draw(b, icon, button.bounds.X + 17, button.bounds.Y + 15, iconSize);
        int titleX = button.bounds.X + 17 + 12 * iconSize + 13;
        int titleWidth = Math.Max(1, button.bounds.Right - titleX - (selected ? 58 : 15));
        this.DrawText(b, GetTaskLabel(task), new Rectangle(titleX, button.bounds.Y + 18, titleWidth, 32), enabled ? Ink : MutedInk);
        if (selected && button.bounds.Width > 190)
        {
            DrawRect(b, new Rectangle(button.bounds.Right - 56, button.bounds.Y + 13, 42, 24), Leaf);
            this.DrawText(b, "SET", new Rectangle(button.bounds.Right - 54, button.bounds.Y + 14, 38, 21), Color.White, centered: true);
        }
        if (button.bounds.Height >= 100)
        {
            DrawRect(b, new Rectangle(button.bounds.X + 16, button.bounds.Y + 64, button.bounds.Width - 32, 2), PaperShade);
            string description = task switch
            {
                WorkerTaskKind.WaterCrops => "Water dry crops",
                WorkerTaskKind.HarvestCrops => "Gather ripe produce",
                WorkerTaskKind.TendCrops => "Harvest, then water",
                _ => "Return to the house",
            };
            this.DrawText(b, description, new Rectangle(button.bounds.X + 18, button.bounds.Y + 75, button.bounds.Width - 36, Math.Max(1, button.bounds.Height - 88)), enabled ? MutedInk : MutedInk * 0.7f);
        }
    }

    /// <summary>Fit text to its own panel so large UI scales and long statuses can't overlap controls.</summary>
    private void DrawText(SpriteBatch b, string text, Rectangle bounds, Color color, bool centered = false)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }
        string wrapped = Game1.parseText(text, Game1.smallFont, bounds.Width);
        Vector2 size = Game1.smallFont.MeasureString(wrapped);
        float scale = Math.Min(1f, Math.Min(bounds.Width / Math.Max(1f, size.X), bounds.Height / Math.Max(1f, size.Y)));
        Vector2 position = new(bounds.X, bounds.Y);
        if (centered)
        {
            position += new Vector2((bounds.Width - size.X * scale) / 2f, (bounds.Height - size.Y * scale) / 2f);
        }
        b.DrawString(Game1.smallFont, wrapped, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
    }

    private void DrawWorkerFace(SpriteBatch b, string workerId, Rectangle bounds)
    {
        if (this.workerShellManager.TryGetWorkerMenuFace(workerId, out Texture2D? texture, out Rectangle sourceRect) && texture is not null)
        {
            b.Draw(texture, bounds, sourceRect, Color.White);
        }
    }

    private WorkerSummarySnapshot? GetSelectedWorker()
    {
        foreach (WorkerSummarySnapshot snapshot in this.workerSnapshots)
        {
            if (snapshot.WorkerId == this.selectedWorkerId)
            {
                return snapshot;
            }
        }
        return null;
    }

    private void SelectWorker(string workerId)
    {
        if (this.selectedWorkerId != workerId)
        {
            this.selectedWorkerId = workerId;
            this.feedback = "Choose a daily order below. Close the menu to watch work continue.";
            this.feedbackIsError = false;
        }
    }

    private int GetLastPageStart()
    {
        return this.workerSnapshots.Count == 0 ? 0 : ((this.workerSnapshots.Count - 1) / this.visibleWorkerCount) * this.visibleWorkerCount;
    }

    private void ChangePage(int direction)
    {
        int next = Math.Clamp(this.firstVisibleWorker + direction * this.visibleWorkerCount, 0, this.GetLastPageStart());
        if (next == this.firstVisibleWorker)
        {
            return;
        }
        this.firstVisibleWorker = next;
        this.SelectWorker(this.workerSnapshots[next].WorkerId);
        this.RebuildLayout(WorkerRowIdBase + next);
        Game1.playSound("shwip");
    }

    private void ScrollDestinations(int direction)
    {
        int maximum = Math.Max(0, this.chestOptions.Count + 1 - this.visibleDestinationCount);
        int next = Math.Clamp(this.firstVisibleDestination + direction, 0, maximum);
        if (next == this.firstVisibleDestination)
            return;

        this.firstVisibleDestination = next;
        this.RebuildLayout(DestinationRowIdBase + next);
        Game1.playSound("shwip");
    }

    private void ShowSelectedWorkerPage()
    {
        for (int i = 0; i < this.workerSnapshots.Count; i++)
        {
            if (this.workerSnapshots[i].WorkerId == this.selectedWorkerId)
            {
                this.firstVisibleWorker = (i / this.visibleWorkerCount) * this.visibleWorkerCount;
                this.RebuildLayout(WorkerRowIdBase + i);
                return;
            }
        }
    }

    private bool EnsureHost()
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer)
        {
            this.SetFeedback("Only the host can hire workers, pay wages, and give orders.", true);
            return false;
        }
        return true;
    }

    private bool EnsureSelectedHostWorker()
    {
        if (!this.EnsureHost())
        {
            return false;
        }
        if (this.GetSelectedWorker() is null)
        {
            this.SetFeedback("Hire and select a worker first.", true);
            return false;
        }
        return true;
    }

    private void SetFeedback(string message, bool isError = false)
    {
        this.feedback = message;
        this.feedbackIsError = isError;
        Game1.playSound(isError ? "cancel" : "smallSelect");
    }

    private void StartHiring()
    {
        int previousCount = this.workerSnapshots.Count;
        string? previousSelection = this.selectedWorkerId;
        this.exitThisMenuNoSound();
        this.workerCustomizationManager.StartHiringSession(() =>
        {
            if (!Context.IsWorldReady || Game1.activeClickableMenu is not null)
            {
                return;
            }
            IReadOnlyList<WorkerSummarySnapshot> workers = this.workerShellManager.GetWorkerSummaries();
            string? selection = workers.Count > previousCount ? workers[workers.Count - 1].WorkerId : previousSelection;
            Game1.activeClickableMenu = new WorkerControlMenu(this.workerShellManager, this.workerBehaviorManager, this.workerCustomizationManager, selection);
        });
    }

    private void CancelDismissal()
    {
        this.pendingDismissalId = null;
        this.RefreshClickableComponents(DismissId);
        Game1.playSound("bigDeSelect");
    }

    private void ConfirmDismissal()
    {
        string? workerId = this.pendingDismissalId;
        this.pendingDismissalId = null;
        if (workerId is not null && this.EnsureHost())
        {
            this.workerBehaviorManager.StopWorker(workerId);
            bool success = this.workerShellManager.TryDismissWorker(workerId, out string message);
            this.SetFeedback(message, !success);
            this.RefreshSnapshots();
        }
        this.RebuildLayout();
    }

    private ResizeEdges GetResizeEdges(int x, int y)
    {
        Rectangle bounds = new(this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height);
        if (!bounds.Contains(x, y))
        {
            return ResizeEdges.None;
        }
        ResizeEdges edges = ResizeEdges.None;
        if (x - bounds.Left < 20) edges |= ResizeEdges.Left;
        else if (bounds.Right - x < 20) edges |= ResizeEdges.Right;
        if (y - bounds.Top < 20) edges |= ResizeEdges.Top;
        else if (bounds.Bottom - y < 20) edges |= ResizeEdges.Bottom;
        return edges;
    }

    private static string FormatTile(Point? tile) => tile is Point point ? $"{point.X}, {point.Y}" : "--";

    private static string GetTaskLabel(WorkerTaskKind task) => task switch
    {
        WorkerTaskKind.WaterCrops => "Water crops",
        WorkerTaskKind.HarvestCrops => "Harvest crops",
        WorkerTaskKind.TendCrops => "Tend crops",
        _ => "Idle / return home",
    };

    private static string GetTaskDescription(WorkerTaskKind task) => task switch
    {
        WorkerTaskKind.WaterCrops => "Walk to dry, growing crops on the farm and water them. Rain and already-watered crops are skipped.",
        WorkerTaskKind.HarvestCrops => "Walk to ripe crops on the farm and harvest them. The worker checks again when there is no work left.",
        WorkerTaskKind.TendCrops => "Harvest ripe crops and water growing crops on the farm. Workers share available jobs.",
        _ => "Stop the current order and return to the worker's home tile. Daily wages still apply while hired.",
    };
}
