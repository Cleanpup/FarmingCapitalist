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
        Jobs,
        Skills,
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
    private const int JobsTabId = 90008;
    private const int StorageTabId = 90009;
    private const int SkillsTabId = 90024;
    private const int OrderIdBase = 90010;
    private const int DestinationDropdownId = 90020;
    private const int DestinationPreviousId = 90021;
    private const int DestinationNextId = 90022;
    private const int DestinationRowIdBase = 92000;
    private const int ForageAreaId = 90023;
    private const int ExplorationAreaId = 90025;
    private const int Padding = 20;
    private const int Gap = 12;
    private static readonly Color Ink = new(91, 48, 30);
    private static readonly Color MutedInk = new(132, 89, 54);
    private static readonly Color Paper = new(255, 232, 186);
    private static readonly Color PaperShade = new(244, 207, 148);
    private static readonly Color Wood = new(139, 77, 39);
    private static readonly Color Leaf = new(85, 128, 61);
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
    private ClickableComponent jobsTabButton = null!;
    private ClickableComponent storageTabButton = null!;
    private ClickableComponent skillsTabButton = null!;
    private ClickableComponent destinationDropdownButton = null!;
    private ClickableComponent destinationPreviousButton = null!;
    private ClickableComponent destinationNextButton = null!;
    private ClickableComponent forageAreaButton = null!;
    private ClickableComponent explorationAreaButton = null!;
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
        if (this.jobsTabButton.containsPoint(x, y))
        {
            this.currentTab = WorkerMenuTab.Jobs;
            this.destinationDropdownOpen = false;
            this.RefreshClickableComponents(JobsTabId);
            Game1.playSound("smallSelect");
            return;
        }
        if (this.skillsTabButton.containsPoint(x, y))
        {
            this.currentTab = WorkerMenuTab.Skills;
            this.destinationDropdownOpen = false;
            this.RefreshClickableComponents(SkillsTabId);
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
                    if (this.EnsureSelectedHostWorker())
                    {
                        int index = row.myID - DestinationRowIdBase;
                        WorkerHarvestDestination? destination = index == 0 ? null : this.chestOptions[index - 1].ToDestination();
                        bool success = this.workerShellManager.TrySetHarvestDestination(this.selectedWorkerId!, destination, out string message);
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

        if (this.currentTab == WorkerMenuTab.Jobs)
        {
            WorkerSummarySnapshot? selectedWorker = this.GetSelectedWorker();
            if (selectedWorker is { Profession: WorkerProfession.Forager } forager && this.forageAreaButton.containsPoint(x, y))
            {
                if (this.EnsureSelectedHostWorker())
                {
                    IReadOnlyList<WorkerForageArea> areas = WorkerForageAreaCatalog.GetAvailableAreas();
                    int current = -1;
                    for (int i = 0; i < areas.Count; i++)
                    {
                        if (areas[i].LocationName == forager.ForageLocationName)
                            current = i;
                    }
                    if (areas.Count > 0)
                    {
                        WorkerForageArea next = areas[(current + 1 + areas.Count) % areas.Count];
                        bool success = this.workerBehaviorManager.TrySetForageLocation(forager.WorkerId, next.LocationName, out string message);
                        this.SetFeedback(message, !success);
                        this.RefreshSnapshots();
                        this.RebuildLayout(ForageAreaId);
                    }
                }
                return;
            }
            if (selectedWorker is { Profession: WorkerProfession.Fisher } fisher && this.forageAreaButton.containsPoint(x, y))
            {
                if (this.EnsureSelectedHostWorker())
                {
                    int current = WorkerFishingAreaCatalog.Areas.ToList().IndexOf(fisher.FishingArea);
                    string next = WorkerFishingAreaCatalog.Areas[(current + 1) % WorkerFishingAreaCatalog.Areas.Count];
                    bool success = this.workerBehaviorManager.TrySetFishingArea(fisher.WorkerId, next, out string message);
                    this.SetFeedback(message, !success);
                    this.RefreshSnapshots();
                    this.RebuildLayout(ForageAreaId);
                }
                return;
            }
            if (selectedWorker is { Profession: WorkerProfession.Miner } miner && this.forageAreaButton.containsPoint(x, y))
            {
                if (this.EnsureSelectedHostWorker())
                {
                    int current = WorkerMiningPolicy.Areas.ToList().IndexOf(miner.MiningArea);
                    string next = WorkerMiningPolicy.Areas[(current + 1) % WorkerMiningPolicy.Areas.Count];
                    bool success = this.workerBehaviorManager.TrySetMiningArea(miner.WorkerId, next, out string message);
                    this.SetFeedback(message, !success);
                    this.RefreshSnapshots();
                    this.RebuildLayout(ForageAreaId);
                }
                return;
            }
            if (selectedWorker is { Profession: WorkerProfession.CombatWorker } combat && this.forageAreaButton.containsPoint(x, y))
            {
                if (this.EnsureSelectedHostWorker())
                {
                    IReadOnlyList<string> areas = WorkerCombatAreaCatalog.GetAreas();
                    int current = -1;
                    for (int i = 0; i < areas.Count; i++)
                        if (areas[i] == combat.CombatArea)
                            current = i;
                    string next = areas[(current + 1 + areas.Count) % areas.Count];
                    bool success = this.workerBehaviorManager.TrySetCombatArea(combat.WorkerId, next, out string message);
                    this.SetFeedback(message, !success);
                    this.RefreshSnapshots();
                    this.RebuildLayout(ForageAreaId);
                }
                return;
            }
            if (selectedWorker is { Profession: WorkerProfession.CombatWorker } explorer && this.explorationAreaButton.containsPoint(x, y))
            {
                if (this.EnsureSelectedHostWorker())
                {
                    IReadOnlyList<string> areas = WorkerExplorationAreaCatalog.GetAreas();
                    int current = 0;
                    for (int i = 0; i < areas.Count; i++)
                        if (areas[i] == explorer.ExplorationArea) current = i;
                    bool success = this.workerBehaviorManager.TrySetExplorationArea(explorer.WorkerId,
                        areas[(current + 1) % areas.Count], out string message);
                    this.SetFeedback(message, !success);
                    this.RefreshSnapshots();
                    this.RebuildLayout(ExplorationAreaId);
                }
                return;
            }

            IReadOnlyList<WorkerTaskKind> tasks = this.GetSelectedTasks();
            for (int index = 0; index < this.orderButtons.Count; index++)
            {
                if (!this.orderButtons[index].containsPoint(x, y))
                    continue;
                if (this.EnsureSelectedHostWorker())
                {
                    bool success = this.workerBehaviorManager.TryAssignTask(this.selectedWorkerId!, tasks[index], out string message);
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
            this.hoverText = "Choose where this worker sends gathered items. Other workers keep their own destinations.";
        if (this.currentTab != WorkerMenuTab.Jobs)
        {
            return;
        }
        for (int index = 0; index < this.orderButtons.Count; index++)
        {
            if (this.orderButtons[index].containsPoint(x, y))
            {
                this.hoverText = GetTaskDescription(this.GetSelectedTasks()[index]);
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
        int tabWidth = Math.Min(150, Math.Max(80, (this.headerBounds.Width - hireWidth - Gap * 5 - 8) / 4));
        this.rosterTabButton = Button(RosterTabId, new Rectangle(this.headerBounds.X + 4, this.headerBounds.Y + 4, tabWidth, 56));
        this.jobsTabButton = Button(JobsTabId, new Rectangle(this.rosterTabButton.bounds.Right + Gap, this.headerBounds.Y + 4, tabWidth, 56));
        this.skillsTabButton = Button(SkillsTabId, new Rectangle(this.jobsTabButton.bounds.Right + Gap, this.headerBounds.Y + 4, tabWidth, 56));
        this.storageTabButton = Button(StorageTabId, new Rectangle(this.skillsTabButton.bounds.Right + Gap, this.headerBounds.Y + 4, tabWidth, 56));
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
        bool showForageArea = this.GetSelectedWorker() is { Profession: WorkerProfession.Forager or WorkerProfession.CombatWorker or WorkerProfession.Miner or WorkerProfession.Fisher };
        bool showExplorationArea = this.GetSelectedWorker() is { Profession: WorkerProfession.CombatWorker };
        int areaHeight = this.ordersBounds.Height >= 220 ? 48 : 32;
        int areaRowHeight = areaHeight + 8;
        int ordersTop = this.ordersBounds.Y + (this.ordersBounds.Height >= 220 ? 56 : 8)
            + (showForageArea ? areaRowHeight : 0) + (showExplorationArea ? areaRowHeight : 0);
        int cardGap = 8;
        int cardColumns = this.ordersBounds.Height < 220 ? 3 : 2;
        int cardWidth = Math.Max(1, (this.ordersBounds.Width - 28 - cardGap * (cardColumns - 1)) / cardColumns);
        IReadOnlyList<WorkerTaskKind> tasks = this.GetSelectedTasks();
        int cardRows = Math.Max(1, (tasks.Count + cardColumns - 1) / cardColumns);
        int cardHeight = Math.Max(1, (this.ordersBounds.Bottom - 14 - ordersTop - cardGap * (cardRows - 1)) / cardRows);
        for (int i = 0; i < tasks.Count; i++)
        {
            this.orderButtons.Add(Button(OrderIdBase + i, new Rectangle(this.ordersBounds.X + 14 + (i % cardColumns) * (cardWidth + cardGap), ordersTop + (i / cardColumns) * (cardHeight + cardGap), cardWidth, cardHeight)));
        }
        this.forageAreaButton = Button(ForageAreaId, new Rectangle(this.ordersBounds.X + 18,
            this.ordersBounds.Y + (this.ordersBounds.Height >= 220 ? 60 : 8), this.ordersBounds.Width - 36, areaHeight));
        this.explorationAreaButton = Button(ExplorationAreaId, new Rectangle(this.forageAreaButton.bounds.X,
            this.forageAreaButton.bounds.Y + areaRowHeight, this.forageAreaButton.bounds.Width, areaHeight));
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
            this.allClickableComponents.Add(this.jobsTabButton);
            this.allClickableComponents.Add(this.skillsTabButton);
            this.allClickableComponents.Add(this.storageTabButton);
            this.allClickableComponents.AddRange(this.workerRows);
            this.allClickableComponents.Add(this.hireButton);
            if (this.currentTab == WorkerMenuTab.Jobs)
            {
                this.allClickableComponents.AddRange(this.orderButtons);
                if (this.GetSelectedWorker() is { Profession: WorkerProfession.Forager or WorkerProfession.CombatWorker or WorkerProfession.Miner or WorkerProfession.Fisher })
                    this.allClickableComponents.Add(this.forageAreaButton);
                if (this.GetSelectedWorker() is { Profession: WorkerProfession.CombatWorker })
                    this.allClickableComponents.Add(this.explorationAreaButton);
            }
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
        this.DrawButton(b, this.jobsTabButton, "Jobs", true, Color.White, WorkerMenuArt.Icon.Sprout, this.currentTab == WorkerMenuTab.Jobs);
        this.DrawButton(b, this.skillsTabButton, "Skills", true, Color.White, WorkerMenuArt.Icon.Ledger, this.currentTab == WorkerMenuTab.Skills);
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
        WorkerSkillExperience experience = this.workerShellManager.GetWorkerExperience(worker.WorkerId);
        WorkerStaminaState stamina = this.workerShellManager.GetWorkerStamina(worker.WorkerId);
        string energy = $"Stamina {stamina.Current:0.#}/{WorkerStaminaPolicy.Maximum:0}";
        string skillName = worker.Profession switch
        {
            WorkerProfession.Forager => "Foraging",
            WorkerProfession.CombatWorker => "Combat",
            WorkerProfession.Miner => "Mining",
            WorkerProfession.Fisher => "Fishing",
            _ => "Farming",
        };
        int skillExperience = worker.Profession switch
        {
            WorkerProfession.Forager => experience.Foraging,
            WorkerProfession.CombatWorker => experience.Combat,
            WorkerProfession.Miner => experience.Mining,
            WorkerProfession.Fisher => experience.Fishing,
            _ => experience.Farming,
        };
        int skillLevel = WorkerExperiencePolicy.GetLevel(skillExperience);
        string location = worker.IsSpawned ? $"{worker.CurrentLocationName ?? "Unknown"} ({FormatTile(worker.CurrentTile)})" : "Waiting to appear";
        if (this.detailsBounds.Height < 160)
        {
            this.DrawText(b, $"{worker.DisplayName} | {GetTaskLabel(runtime.AssignedTask)} | {energy}\n{runtime.Status}\n{skillName} Lv. {skillLevel} | {location} | Done: {runtime.CompletedToday}", content, Ink);
            return;
        }
        Rectangle portraitFrame = new(content.X, content.Y + 4, 74, 74);
        this.DrawCard(b, portraitFrame, Paper, false);
        this.DrawWorkerFace(b, worker.WorkerId, new Rectangle(portraitFrame.X + 11, portraitFrame.Y + 11, 52, 52));
        int infoX = portraitFrame.Right + 16;
        int infoWidth = Math.Max(1, content.Right - infoX);
        bool showFarmingBadge = (this.currentTab == WorkerMenuTab.Roster || worker.Profession == WorkerProfession.CombatWorker)
            && infoWidth >= 320;
        int badgeWidth = showFarmingBadge ? Math.Min(270, infoWidth / 2) : 0;
        int topLineWidth = showFarmingBadge ? infoWidth - badgeWidth - 16 : infoWidth;
        this.DrawText(b, $"{worker.DisplayName} — {WorkerTaskPolicy.GetProfessionLabel(worker.Profession)}", new Rectangle(infoX, content.Y, topLineWidth, 30), Ink);
        this.DrawText(b, $"{GetTaskLabel(runtime.AssignedTask)} • {energy}", new Rectangle(infoX, content.Y + 34, topLineWidth, 28), runtime.AssignedTask == WorkerTaskKind.Idle ? MutedInk : Leaf);
        string status = worker.Profession == WorkerProfession.Forager
            ? $"{runtime.Status} • {WorkerForageAreaCatalog.GetDisplayName(worker.ForageLocationName)}"
            : worker.Profession == WorkerProfession.CombatWorker
                ? $"{runtime.Status} • {(runtime.AssignedTask == WorkerTaskKind.ExploreArea
                    ? WorkerExplorationAreaCatalog.GetLabel(worker.ExplorationArea) : WorkerCombatAreaCatalog.GetLabel(worker.CombatArea))}"
            : runtime.Status;
        this.DrawText(b, status, new Rectangle(infoX, content.Y + 56, infoWidth, 24), MutedInk);
        if (showFarmingBadge)
        {
            Rectangle badge = new(content.Right - badgeWidth, content.Y + 4, badgeWidth, 42);
            this.DrawCard(b, badge, Paper, false);
            if (worker.Profession == WorkerProfession.CombatWorker
                && this.workerBehaviorManager.TryGetCombatHealth(worker.WorkerId, out int health, out int maxHealth))
            {
                this.DrawText(b, $"Combat Lv. {skillLevel}   HP {health}/{maxHealth}",
                    new Rectangle(badge.X + 8, badge.Y + 3, badge.Width - 16, 25), Leaf, centered: true);
                Rectangle hpBar = new(badge.X + 12, badge.Bottom - 11, badge.Width - 24, 5);
                DrawRect(b, hpBar, Color.DarkRed);
                DrawRect(b, new Rectangle(hpBar.X, hpBar.Y,
                    (int)(hpBar.Width * (long)Math.Clamp(health, 0, maxHealth) / maxHealth), hpBar.Height), Leaf);
            }
            else
                this.DrawText(b, $"{skillName} Lv. {skillLevel}", new Rectangle(badge.X + 12, badge.Y + 6, badge.Width - 24, badge.Height - 12), Leaf, centered: true);
        }
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
        if (this.currentTab == WorkerMenuTab.Skills)
        {
            this.DrawSkillsPanel(b);
            return;
        }
        if (this.currentTab != WorkerMenuTab.Jobs)
        {
            this.DrawSectionHeading(b, this.ordersBounds, "A DAY ON THE FARM", string.Empty);
            if (this.ordersBounds.Height < 240)
            {
                WorkerMenuArt.Draw(b, WorkerMenuArt.Icon.Sprout, this.ordersBounds.Center.X - 18, this.ordersBounds.Y + 55);
                this.DrawText(b, "Choose Jobs to set a daily order.", new Rectangle(this.ordersBounds.X + Padding, this.ordersBounds.Y + 105, this.ordersBounds.Width - Padding * 2, 45), Ink, centered: true);
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
            this.DrawText(b, "Choose Jobs to set a daily order.", new Rectangle(this.ordersBounds.X + Padding, iconY + 105, this.ordersBounds.Width - Padding * 2, 54), Ink, centered: true);
            if (this.ordersBounds.Height >= 330)
            {
                Rectangle orderSummary = new(this.ordersBounds.X + 38, this.ordersBounds.Bottom - 90, this.ordersBounds.Width - 76, 63);
                this.DrawCard(b, orderSummary, Paper, false);
                WorkerSummarySnapshot? currentWorker = this.GetSelectedWorker();
                WorkerTaskKind currentOrder = currentWorker is WorkerSummarySnapshot summaryWorker
                    ? this.runtimeSnapshots[summaryWorker.WorkerId].AssignedTask
                    : WorkerTaskKind.Idle;
                WorkerMenuArt.Draw(b, currentOrder == WorkerTaskKind.Idle ? WorkerMenuArt.Icon.Home : WorkerMenuArt.Icon.Ledger, orderSummary.X + 18, orderSummary.Y + 13, 3);
                this.DrawText(b, "CURRENT ORDER", new Rectangle(orderSummary.X + 69, orderSummary.Y + 8, orderSummary.Width - 85, 23), MutedInk);
                this.DrawText(b, currentWorker is null ? "Select a worker" : GetTaskLabel(currentOrder), new Rectangle(orderSummary.X + 69, orderSummary.Y + 29, orderSummary.Width - 85, 26), Ink);
            }
            return;
        }
        if (this.ordersBounds.Height >= 220)
        {
            this.DrawSectionHeading(b, this.ordersBounds, "DAILY ORDERS", string.Empty);
        }
        bool enabled = Context.IsMainPlayer && this.selectedWorkerId is not null;
        WorkerTaskKind? assigned = this.selectedWorkerId is not null && this.runtimeSnapshots.TryGetValue(this.selectedWorkerId, out WorkerRuntimeSnapshot runtime) ? runtime.AssignedTask : null;
        WorkerSummarySnapshot? selectedWorker = this.GetSelectedWorker();
        if (selectedWorker is { Profession: WorkerProfession.Forager } forager)
        {
            this.DrawButton(b, this.forageAreaButton,
                $"Forage area: {WorkerForageAreaCatalog.GetDisplayName(forager.ForageLocationName)}  >",
                enabled, Color.White, WorkerMenuArt.Icon.Sprout);
        }
        if (selectedWorker is { Profession: WorkerProfession.Fisher } fisher)
            this.DrawButton(b, this.forageAreaButton, $"Fishing area: {WorkerFishingAreaCatalog.Label(fisher.FishingArea)}  >",
                Context.IsMainPlayer, Color.White, WorkerMenuArt.Icon.Water);
        if (selectedWorker is { Profession: WorkerProfession.Miner } miner)
        {
            this.DrawButton(b, this.forageAreaButton, $"Mining area: {WorkerMiningPolicy.GetLabel(miner.MiningArea)}  >",
                enabled, Color.White, WorkerMenuArt.Icon.Tend);
        }
        if (selectedWorker is { Profession: WorkerProfession.CombatWorker } combat)
        {
            this.DrawButton(b, this.forageAreaButton,
                $"Combat area: {WorkerCombatAreaCatalog.GetLabel(combat.CombatArea)}  >",
                enabled, Color.White, WorkerMenuArt.Icon.Ledger);
            this.DrawButton(b, this.explorationAreaButton,
                $"Explore area: {WorkerExplorationAreaCatalog.GetLabel(combat.ExplorationArea)}  >",
                enabled, Color.White, WorkerMenuArt.Icon.Ledger);
        }
        IReadOnlyList<WorkerTaskKind> tasks = this.GetSelectedTasks();
        for (int i = 0; i < this.orderButtons.Count; i++)
        {
            ClickableComponent button = this.orderButtons[i];
            bool selected = assigned == tasks[i];
            this.DrawOrderCard(b, button, tasks[i], enabled, selected);
        }
    }

    private void DrawSkillsPanel(SpriteBatch b)
    {
        this.DrawSectionHeading(b, this.ordersBounds, "WORKER SKILLS", string.Empty);
        WorkerSummarySnapshot? selected = this.GetSelectedWorker();
        if (selected is null)
        {
            this.DrawText(b, "Select a worker to view their skills.",
                new Rectangle(this.ordersBounds.X + Padding, this.ordersBounds.Y + 75, this.ordersBounds.Width - Padding * 2, 35), MutedInk);
            return;
        }

        WorkerSkillExperience experience = this.workerShellManager.GetWorkerExperience(selected.Value.WorkerId);
        (string Name, int Experience)[] skills =
        {
            ("Farming", experience.Farming),
            ("Mining", experience.Mining),
            ("Fishing", experience.Fishing),
            ("Foraging", experience.Foraging),
            ("Combat", experience.Combat),
        };
        int top = this.ordersBounds.Y + (this.ordersBounds.Height < 220 ? 58 : 64);
        int gap = this.ordersBounds.Height < 220 ? 4 : 8;
        int available = Math.Max(1, this.ordersBounds.Bottom - top - 12);
        int rowHeight = Math.Max(1, Math.Min(58, (available - gap * (skills.Length - 1)) / skills.Length));
        for (int index = 0; index < skills.Length; index++)
        {
            Rectangle row = new(this.ordersBounds.X + 20, top + index * (rowHeight + gap),
                this.ordersBounds.Width - 40, rowHeight);
            this.DrawSkillRow(b, row, skills[index].Name, skills[index].Experience);
        }
    }

    private void DrawSkillRow(SpriteBatch b, Rectangle row, string name, int experience)
    {
        this.DrawCard(b, row, Paper, false);
        int level = WorkerExperiencePolicy.GetLevel(experience);
        bool stacked = row.Height >= 42;
        int labelWidth = stacked ? row.Width - 36 : Math.Min(180, row.Width / 2);
        this.DrawText(b, $"{name}  Lv. {level}",
            new Rectangle(row.X + 18, row.Y + (stacked ? 4 : 2), labelWidth, Math.Min(28, row.Height - 4)), Ink);
        int low = WorkerExperiencePolicy.GetThreshold(level);
        int high = level == WorkerExperiencePolicy.MaximumLevel ? low : WorkerExperiencePolicy.GetThreshold(level + 1);
        float fraction = high == low ? 1f : Math.Clamp((float)(experience - low) / (high - low), 0f, 1f);
        Rectangle bar = stacked
            ? new Rectangle(row.X + 18, row.Bottom - 15, Math.Max(1, row.Width - 36), 8)
            : new Rectangle(row.X + labelWidth + 30, row.Y + (row.Height - 8) / 2,
                Math.Max(1, row.Width - labelWidth - 48), 8);
        DrawRect(b, bar, PaperShade);
        DrawRect(b, new Rectangle(bar.X, bar.Y, (int)(bar.Width * fraction), bar.Height), Leaf);
    }

    private void DrawStoragePanel(SpriteBatch b)
    {
        if (this.ordersBounds.Height >= 220)
            this.DrawSectionHeading(b, this.ordersBounds, "HARVEST STORAGE", $"{this.chestOptions.Count} chests found");

        WorkerHarvestDestination? destination = this.selectedWorkerId is null
            ? null
            : this.workerShellManager.GetHarvestDestination(this.selectedWorkerId);
        WorkerChestOption? selectedChest = destination is null ? null : this.chestOptions.Find(option =>
            option.LocationName == destination.LocationName && option.Tile == destination.Tile);
        string selectedLabel = destination is null ? "Shipping bin" : selectedChest?.Label ?? "Missing chest — using shipping bin";
        string workerLabel = this.GetSelectedWorker()?.DisplayName ?? "Select a worker";
        this.DrawButton(b, this.destinationDropdownButton, $"{workerLabel}: {selectedLabel}  {(this.destinationDropdownOpen ? "^" : "v")}",
            Context.IsMainPlayer && this.selectedWorkerId is not null, Color.White,
            destination is null ? WorkerMenuArt.Icon.Coin : WorkerMenuArt.Icon.Chest);

        if (!this.destinationDropdownOpen)
        {
            string description = this.selectedWorkerId is null
                ? "Select a worker to choose their item destination. Workers without a chest use the shipping bin."
                : "This worker sends gathered items here. A full, locked, or unavailable chest falls back to the shipping bin.";
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
            WorkerTaskKind.CollectForage => WorkerMenuArt.Icon.Harvest,
            WorkerTaskKind.ChopTrees => WorkerMenuArt.Icon.Tend,
            WorkerTaskKind.ChopHardwood => WorkerMenuArt.Icon.Ledger,
            WorkerTaskKind.ClearDebris => WorkerMenuArt.Icon.Tend,
            WorkerTaskKind.MineRocks or WorkerTaskKind.MineOreGems or WorkerTaskKind.FindLadder => WorkerMenuArt.Icon.Tend,
            WorkerTaskKind.SlayMonsters => WorkerMenuArt.Icon.Ledger,
            WorkerTaskKind.ExploreArea => WorkerMenuArt.Icon.Ledger,
            WorkerTaskKind.Fish => WorkerMenuArt.Icon.Water,
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
                WorkerTaskKind.CollectForage => "Gather wild items",
                WorkerTaskKind.ChopTrees => "Fell ordinary trees",
                WorkerTaskKind.ChopHardwood => "Clear hardwood sources",
                WorkerTaskKind.ClearDebris => "Remove small litter",
                WorkerTaskKind.MineRocks => "Mine all rocks on this floor",
                WorkerTaskKind.MineOreGems => "Mine resources, then find a ladder",
                WorkerTaskKind.FindLadder => "Break stones until an exit appears",
                WorkerTaskKind.SlayMonsters => "Fight monsters in selected area",
                WorkerTaskKind.ExploreArea => "Explore for loot each hour",
                WorkerTaskKind.Fish => "Cast for fish until midnight",
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

    private IReadOnlyList<WorkerTaskKind> GetSelectedTasks()
    {
        return WorkerTaskPolicy.GetTasks(this.GetSelectedWorker()?.Profession ?? WorkerProfession.Farmer);
    }

    private void SelectWorker(string workerId)
    {
        if (this.selectedWorkerId != workerId)
        {
            this.selectedWorkerId = workerId;
            this.feedback = "Choose a daily order below. Close the menu to watch work continue.";
            this.feedbackIsError = false;
            int index = this.workerSnapshots.FindIndex(snapshot => snapshot.WorkerId == workerId);
            this.RebuildLayout(index >= 0 ? WorkerRowIdBase + index : null);
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

    private static string GetTaskLabel(WorkerTaskKind task)
        => WorkerTaskPolicy.GetTaskLabel(task, "Idle / return home");

    private static string GetTaskDescription(WorkerTaskKind task) => task switch
    {
        WorkerTaskKind.WaterCrops => "Walk to dry, growing crops on the farm and water them. Rain and already-watered crops are skipped.",
        WorkerTaskKind.HarvestCrops => "Walk to ripe crops on the farm and harvest them. The worker checks again when there is no work left.",
        WorkerTaskKind.TendCrops => "Harvest ripe crops and water growing crops on the farm. Workers share available jobs.",
        WorkerTaskKind.CollectForage => "Walk to the nearest reachable wild forage item in the selected outdoor area and collect it.",
        WorkerTaskKind.ChopTrees => "Walk to the nearest reachable ordinary tree in the selected area, cut it down, and store its drops.",
        WorkerTaskKind.ChopHardwood => "Walk to the nearest reachable hardwood source, including mahogany trees and large stumps or logs.",
        WorkerTaskKind.ClearDebris => "Clear loose stones, weeds, and small fallen wood. Foragers work in their selected outdoor area; Farmers work on the farm.",
        WorkerTaskKind.MineRocks => "Mine all reachable rocks and resource nodes on the current floor with a steel pickaxe. Dungeon Miners appear at the selected entrance and follow the host through active floors. Real drops use this worker's storage; completed rocks grant 1 Mining XP. Large boulders are excluded.",
        WorkerTaskKind.MineOreGems => "Mine ore, gem, coal, geode and cinder-shard nodes, and gather loose quartz, fire quartz, frozen tears and earth crystals. Once these resources are exhausted in Mines or Skull Cavern, break stones to find a ladder or shaft, then wait for the host to descend. Quarry has no deeper floor. Volcano uses fixed exits and player-operated gates and lava crossings; it has no hidden ladders.",
        WorkerTaskKind.FindLadder => "Break stones until a real ladder or shaft is available in Mines or Skull Cavern, then follow when the host descends. Infested floors require the player to defeat monsters; terminal floors, Quarry and Volcano have no hidden ladder to uncover. Workers never create stairs, open gates or advance alone.",
        WorkerTaskKind.SlayMonsters => "Fight real monsters in the combat area. Mines and Skull Cavern follow the host farmer onto active floors.",
        WorkerTaskKind.Fish => "Teleport to a clear shore in the selected area and repeatedly cast until midnight. Catches are simulated every 30 active in-game minutes using location, season, time and weather. Legendary fish are excluded. Fish go to this worker's chest or shipping fallback and grant worker Fishing XP.",
        WorkerTaskKind.ExploreArea => "Teleport to the selected dungeon entrance and explore independently for loot every in-game hour, from 6:00 to 22:00. Exploration is simulated; the worker stays at the entrance. Loot goes to this worker's selected chest or shipping-bin fallback. Completed runs grant this worker 5 Combat XP; only monster drops are collected, with stone excluded.",
        _ => "Stop the current order and return to the worker's home tile. Daily wages still apply while hired.",
    };
}
