using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace HireSkilledHelpers.Workers;

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
    private const int SkillLevelsId = 90026;
    private const int WorkerPerksId = 90027;
    private const int ProfessionDropdownId = 90028;
    private const int ProfessionRowIdBase = 93000;
    private static readonly WorkerProfession[] ProfessionOptions =
        { WorkerProfession.Farmer, WorkerProfession.Forager, WorkerProfession.CombatWorker, WorkerProfession.Miner, WorkerProfession.Fisher };
    private readonly record struct ProfileLayout(Rectangle Content, Rectangle Portrait, Rectangle Name,
        Rectangle Profession, Rectangle Wage, Rectangle Location, Rectangle Vitals);
    // drawDialogueBox starts its visible top border 64 pixels below its supplied Y.
    private const int FrameTopOffset = 64;
    private const int ResizeHitPadding = 12;
    private static Point preferredMenuSize = new(1200, 840);
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
    private readonly List<ClickableComponent> professionRows = new();
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
    private ClickableComponent skillLevelsButton = null!;
    private ClickableComponent workerPerksButton = null!;
    private ClickableComponent professionDropdownButton = null!;
    private Rectangle professionPopupBounds;
    private bool professionDropdownOpen;
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
    private bool showWorkerPerks;
    private double refreshMilliseconds;
    private ResizeEdges resizeEdges;
    private Point resizeStartMouse;
    private Rectangle resizeStartBounds;

    public WorkerControlMenu(
        WorkerShellManager workerShellManager,
        WorkerBehaviorManager workerBehaviorManager,
        WorkerCustomizationManager workerCustomizationManager,
        string? selectedWorkerId = null)
        : base(0, 0, GetMenuWidth(preferredMenuSize.X), GetMenuHeight(preferredMenuSize.Y), showUpperRightCloseButton: true)
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

        if (this.pendingDismissalId is null && !this.professionDropdownOpen && this.currentlySnappedComponent is { } component)
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
        this.resizeEdges = ResizeEdges.None;
        this.width = GetMenuWidth(this.width);
        this.height = GetMenuHeight(this.height);
        this.CenterOnScreen();
        this.RebuildLayout(this.currentlySnappedComponent?.myID);
    }

    public override void snapToDefaultClickableComponent()
    {
        int id = this.professionDropdownOpen ? ProfessionDropdownId : this.pendingDismissalId is not null
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
        if (this.professionDropdownOpen)
        {
            this.professionDropdownOpen = false;
            this.RebuildLayout(ProfessionDropdownId);
            return;
        }
        if (this.pendingDismissalId is not null)
        {
            this.CancelDismissal();
            return;
        }

        this.exitThisMenu();
    }

    public override void receiveKeyPress(Keys key)
    {
        if (key == Keys.Escape || Game1.options.doesInputListContain(Game1.options.menuButton, key))
        {
            this.RequestClose();
            return;
        }

        if (this.pendingDismissalId is null && !this.professionDropdownOpen && (key == Keys.PageDown || key == Keys.PageUp))
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
        if (this.professionDropdownOpen) return;
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

        if (this.professionDropdownOpen)
        {
            foreach (ClickableComponent row in this.professionRows)
                if (row.containsPoint(x, y))
                {
                    WorkerProfession profession = ProfessionOptions[row.myID - ProfessionRowIdBase];
                    if (this.EnsureSelectedHostWorker())
                    {
                        bool success = this.workerBehaviorManager.TryChangeProfession(this.selectedWorkerId!, profession, out string message);
                        this.SetFeedback(message, !success);
                    }
                    this.professionDropdownOpen = false;
                    this.RefreshSnapshots();
                    this.RebuildLayout(ProfessionDropdownId);
                    return;
                }
            this.professionDropdownOpen = false;
            this.RebuildLayout(ProfessionDropdownId);
            Game1.playSound("smallSelect");
            return;
        }
        if (this.professionDropdownButton.containsPoint(x, y))
        {
            if (this.EnsureSelectedHostWorker())
            {
                this.professionDropdownOpen = true;
                this.destinationDropdownOpen = false;
                int selected = Array.IndexOf(ProfessionOptions, this.GetSelectedWorker()!.Value.Profession);
                this.RebuildLayout(ProfessionRowIdBase + Math.Max(0, selected));
                Game1.playSound("smallSelect");
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
        if (this.currentTab == WorkerMenuTab.Skills)
        {
            if (this.skillLevelsButton.containsPoint(x, y) || this.workerPerksButton.containsPoint(x, y))
            {
                this.showWorkerPerks = this.workerPerksButton.containsPoint(x, y);
                this.RefreshClickableComponents(this.showWorkerPerks ? WorkerPerksId : SkillLevelsId);
                Game1.playSound("smallSelect");
                return;
            }
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
        bool fromLeft = (this.resizeEdges & ResizeEdges.Left) != 0;
        bool fromTop = (this.resizeEdges & ResizeEdges.Top) != 0;
        // Limit growth against the dragged edge, keeping the opposite edge fixed.
        int maximumWidth = Math.Min(GetMenuWidth(int.MaxValue), fromLeft
            ? this.resizeStartBounds.Right - 12
            : Game1.uiViewport.Width - 12 - this.resizeStartBounds.Left);
        int maximumHeight = Math.Min(GetMenuHeight(int.MaxValue), fromTop
            ? this.resizeStartBounds.Bottom + FrameTopOffset - 12
            : Game1.uiViewport.Height - 12 - this.resizeStartBounds.Top);
        int nextWidth = (this.resizeEdges & (ResizeEdges.Left | ResizeEdges.Right)) != 0
            ? Math.Clamp(this.resizeStartBounds.Width + widthDelta, Math.Min(800, maximumWidth), maximumWidth)
            : this.resizeStartBounds.Width;
        int nextHeight = (this.resizeEdges & (ResizeEdges.Top | ResizeEdges.Bottom)) != 0
            ? Math.Clamp(this.resizeStartBounds.Height + heightDelta, Math.Min(560, maximumHeight), maximumHeight)
            : this.resizeStartBounds.Height;
        if (nextWidth == this.width && nextHeight == this.height)
            return;

        this.width = nextWidth;
        this.height = nextHeight;
        this.xPositionOnScreen = fromLeft ? this.resizeStartBounds.Right - this.width : this.resizeStartBounds.X;
        this.yPositionOnScreen = fromTop ? this.resizeStartBounds.Bottom - this.height : this.resizeStartBounds.Y;
        this.RebuildLayout(this.currentlySnappedComponent?.myID);
    }

    public override void releaseLeftClick(int x, int y)
    {
        if (this.resizeEdges != ResizeEdges.None)
            preferredMenuSize = new Point(this.width, this.height);
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
        if (this.professionDropdownOpen) return;
        if (this.professionDropdownButton.containsPoint(x, y) && this.GetSelectedWorker() is not null)
        {
            this.hoverText = Context.IsMainPlayer
                ? "Change profession. Skills and perks stay; choose a new order afterward."
                : "Only the host can change professions.";
            return;
        }
        if (this.resizeEdges != ResizeEdges.None || this.GetResizeEdges(x, y) != ResizeEdges.None)
        {
            this.hoverText = "Drag this border or corner to resize the menu.";
            return;
        }
        foreach (ClickableComponent row in this.workerRows)
        {
            if (row.containsPoint(x, y))
            {
                WorkerSummarySnapshot worker = this.workerSnapshots[row.myID - WorkerRowIdBase];
                this.hoverText = $"{worker.DisplayName}\n{this.runtimeSnapshots[worker.WorkerId].State}";
                return;
            }
        }
        if (this.GetProfileLayout().Name.Contains(x, y) && this.GetSelectedWorker() is WorkerSummarySnapshot selected)
        {
            this.hoverText = selected.DisplayName;
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
        this.DrawResizeGrips(b);
        base.draw(b);
        if (this.professionDropdownOpen) this.DrawProfessionPopup(b);
        if (this.pendingDismissalId is not null)
        {
            this.DrawDismissalConfirmation(b);
        }
        else if (!this.professionDropdownOpen && this.resizeEdges == ResizeEdges.None && this.hoverText.Length > 0)
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
                changed |= latest[i].WorkerId != this.workerSnapshots[i].WorkerId
                    || latest[i].Profession != this.workerSnapshots[i].Profession;
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
            this.professionDropdownOpen = false;
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
        this.professionDropdownButton = Button(ProfessionDropdownId, this.GetProfileLayout().Profession);
        int popupWidth = Math.Max(160, this.professionDropdownButton.bounds.Width);
        int popupHeight = ProfessionOptions.Length * 36 + 12;
        int popupX = Math.Clamp(this.professionDropdownButton.bounds.X, this.detailsBounds.X,
            Math.Max(this.detailsBounds.X, this.detailsBounds.Right - popupWidth));
        int popupY = Math.Clamp(this.professionDropdownButton.bounds.Bottom + 4,
            this.headerBounds.Bottom, Math.Max(this.headerBounds.Bottom, this.footerBounds.Bottom - popupHeight));
        this.professionPopupBounds = new Rectangle(popupX, popupY, popupWidth, popupHeight);
        this.professionRows.Clear();
        if (this.professionDropdownOpen)
            for (int i = 0; i < ProfessionOptions.Length; i++)
                this.professionRows.Add(Button(ProfessionRowIdBase + i,
                    new Rectangle(popupX + 6, popupY + 6 + i * 36, popupWidth - 12, 34)));
        int hireWidth = Math.Min(220, this.headerBounds.Width / 4);
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
        int skillTabHeight = this.ordersBounds.Height < 220 ? 28 : 36;
        int skillTabTop = this.ordersBounds.Y + (this.ordersBounds.Height < 220 ? 10 : 52);
        int skillTabWidth = Math.Min(160, Math.Max(1, (this.ordersBounds.Width - 48) / 2));
        this.skillLevelsButton = Button(SkillLevelsId, new Rectangle(this.ordersBounds.X + 20, skillTabTop, skillTabWidth, skillTabHeight));
        this.workerPerksButton = Button(WorkerPerksId, new Rectangle(this.skillLevelsButton.bounds.Right + 8, skillTabTop, skillTabWidth, skillTabHeight));
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
        this.upperRightCloseButton.bounds.Y += FrameTopOffset;
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
        else if (this.professionDropdownOpen)
        {
            this.allClickableComponents.Add(this.professionDropdownButton);
            this.allClickableComponents.AddRange(this.professionRows);
        }
        else
        {
            if (Context.IsMainPlayer && this.GetSelectedWorker() is not null)
                this.allClickableComponents.Add(this.professionDropdownButton);
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
            if (this.currentTab == WorkerMenuTab.Skills)
            {
                this.allClickableComponents.Add(this.skillLevelsButton);
                this.allClickableComponents.Add(this.workerPerksButton);
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
        this.currentlySnappedComponent ??= this.getComponentWithID(this.professionDropdownOpen ? ProfessionDropdownId : this.pendingDismissalId is not null ? CancelId : this.workerRows.Count > 0 ? this.workerRows[0].myID : HireId);
        if (this.resizeEdges == ResizeEdges.None && Game1.options.SnappyMenus && Game1.options.gamepadControls && this.currentlySnappedComponent is not null)
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
        this.DrawButton(b, this.rosterTabButton, "Roster", true, Color.White, WorkerMenuArt.Icon.Roster, this.currentTab == WorkerMenuTab.Roster);
        this.DrawButton(b, this.jobsTabButton, "Jobs", true, Color.White, WorkerMenuArt.Icon.Sprout, this.currentTab == WorkerMenuTab.Jobs);
        this.DrawButton(b, this.skillsTabButton, "Skills", true, Color.White, WorkerMenuArt.Icon.Skills, this.currentTab == WorkerMenuTab.Skills);
        this.DrawButton(b, this.storageTabButton, "Storage", true, Color.White, WorkerMenuArt.Icon.Chest, this.currentTab == WorkerMenuTab.Storage);
        this.DrawCard(b, this.hireButton.bounds, Context.IsMainPlayer && this.IsControlHighlighted(this.hireButton) ? PaperShade : Paper, false);
        int hireTextX = this.hireButton.bounds.X + 12;
        if (this.hireButton.bounds.Width >= 160)
        {
            WorkerMenuArt.Draw(b, WorkerMenuArt.Icon.Coin, hireTextX, this.hireButton.bounds.Y + 16, 2);
            hireTextX += 32;
        }
        int hireTextWidth = this.hireButton.bounds.Right - hireTextX - 12;
        this.DrawSingleLineText(b, "Hire worker", new Rectangle(hireTextX, this.hireButton.bounds.Y + 6, hireTextWidth, 25), Context.IsMainPlayer ? Ink : MutedInk, centered: true);
        this.DrawSingleLineText(b, $"{WorkerEmploymentTerms.HiringCost}g", new Rectangle(hireTextX, this.hireButton.bounds.Y + 30, hireTextWidth, 20), MutedInk, centered: true, maximumScale: 0.85f);
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
            this.DrawCard(b, row.bounds, selected || this.IsControlHighlighted(row) ? PaperShade : Paper, selected);
            int faceSize = Math.Min(52, row.bounds.Height - 20);
            Rectangle faceFrame = new(row.bounds.X + 10, row.bounds.Y + (row.bounds.Height - faceSize - 6) / 2, faceSize + 6, faceSize + 6);
            DrawRect(b, faceFrame, Wood);
            Rectangle face = new(faceFrame.X + 3, faceFrame.Y + 3, faceSize, faceSize);
            this.DrawWorkerFace(b, snapshot.WorkerId, face);
            int textX = faceFrame.Right + 10;
            this.DrawSingleLineText(b, snapshot.DisplayName, new Rectangle(textX, row.bounds.Y + 10, row.bounds.Right - textX - 10, 27), Ink, ellipsize: true);
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

    private ProfileLayout GetProfileLayout()
    {
        Rectangle content = new(this.detailsBounds.X + Padding, this.detailsBounds.Y + 58,
            Math.Max(1, this.detailsBounds.Width - Padding * 2), Math.Max(1, this.detailsBounds.Height - 72));
        bool compact = this.detailsBounds.Height < 160 || content.Width < 540;
        int metricsY = this.detailsBounds.Bottom - 44;
        int vitalsWidth = Math.Min(Math.Min(220, Math.Max(96, content.Width / 3)), content.Width / 2);
        Rectangle vitals = new(content.Right - vitalsWidth, content.Y, vitalsWidth,
            compact ? content.Height : Math.Max(1, metricsY - content.Y - 10));
        int portraitSize = compact ? 0 : Math.Min(74, Math.Max(32, metricsY - content.Y - 16));
        Rectangle portrait = new(content.X, content.Y + 4, portraitSize, portraitSize);
        int infoX = compact ? content.X : portrait.Right + 16;
        int infoWidth = Math.Max(1, vitals.Left - Gap - infoX);
        int lineHeight = compact ? Math.Min(28, Math.Max(1, content.Height / 3)) : 28;
        int professionWidth = Math.Min(140, Math.Max(1, infoWidth / 2));
        Rectangle profession = new(infoX + infoWidth - professionWidth, content.Y, professionWidth, lineHeight);
        Rectangle name = new(infoX, content.Y, Math.Max(1, infoWidth - professionWidth - 6), lineHeight);
        Rectangle wage = new(infoX, content.Y + lineHeight, infoWidth,
            compact ? lineHeight : Math.Min(24, Math.Max(1, metricsY - 8 - content.Y - lineHeight)));
        Rectangle location = compact ? new Rectangle(infoX, content.Y + 2 * lineHeight, infoWidth, lineHeight)
            : new Rectangle(this.detailsBounds.X + 22, metricsY, this.detailsBounds.Width - 44, 30);
        return new ProfileLayout(content, portrait, name, profession, wage, location, vitals);
    }

    private void DrawDetails(SpriteBatch b)
    {
        this.DrawPanel(b, this.detailsBounds);
        this.DrawSectionHeading(b, this.detailsBounds, "WORKER PROFILE", string.Empty);
        ProfileLayout layout = this.GetProfileLayout();
        if (this.GetSelectedWorker() is not WorkerSummarySnapshot worker)
        {
            this.DrawText(b, "Select a worker to see their profile.", layout.Content, MutedInk);
            return;
        }
        if (layout.Portrait.Width > 0)
        {
            this.DrawCard(b, layout.Portrait, Paper, false);
            Rectangle face = layout.Portrait;
            face.Inflate(-11, -11);
            this.DrawWorkerFace(b, worker.WorkerId, face);
            DrawRect(b, new Rectangle(this.detailsBounds.X + 20, layout.Location.Y - 8, this.detailsBounds.Width - 40, 2), PaperShade);
        }
        this.DrawSingleLineText(b, worker.DisplayName, layout.Name, Ink, ellipsize: true);
        bool enabled = Context.IsMainPlayer;
        this.DrawCard(b, layout.Profession, enabled && this.IsControlHighlighted(this.professionDropdownButton) ? PaperShade : Paper, this.professionDropdownOpen);
        Rectangle professionText = layout.Profession;
        professionText.Inflate(-7, -2);
        professionText.Width = Math.Max(1, professionText.Width - 20);
        WorkerMenuArt.DrawSprite(b, Game1.mouseCursors, OptionsDropDown.dropDownButtonSource,
            new Rectangle(layout.Profession.Right - 23, layout.Profession.Y + 3, 20, Math.Max(1, layout.Profession.Height - 6)), enabled ? 1f : 0.5f);
        this.DrawSingleLineText(b, WorkerTaskPolicy.GetProfessionLabel(worker.Profession),
            professionText, enabled ? Ink : MutedInk, centered: true);
        Rectangle wageText = layout.Wage;
        if (wageText.Width >= 230 && wageText.Height >= 20)
        {
            WorkerMenuArt.Draw(b, WorkerMenuArt.Icon.Coin, new Rectangle(wageText.X, wageText.Y, 16, wageText.Height));
            wageText.X += 22;
            wageText.Width -= 22;
        }
        this.DrawSingleLineText(b, $"Daily wage: {this.workerShellManager.GetWorkerDailyWage(worker.WorkerId)}g", wageText, MutedInk);
        string location = worker.IsSpawned ? $"{worker.CurrentLocationName ?? "Unknown"} ({FormatTile(worker.CurrentTile)})" : "Waiting to appear";
        this.DrawSingleLineText(b, location, layout.Location, MutedInk);
        this.workerBehaviorManager.GetWorkerHealth(worker.WorkerId, out int health, out int maxHealth);
        this.DrawWorkerVitals(b, layout.Vitals, health, maxHealth, this.workerShellManager.GetWorkerStamina(worker.WorkerId));
    }

    private void DrawProfessionPopup(SpriteBatch b)
    {
        this.DrawCard(b, this.professionPopupBounds, Paper, false);
        WorkerProfession? selected = this.GetSelectedWorker()?.Profession;
        foreach (ClickableComponent row in this.professionRows)
        {
            WorkerProfession profession = ProfessionOptions[row.myID - ProfessionRowIdBase];
            bool highlighted = profession == selected || row.containsPoint(Game1.getMouseX(), Game1.getMouseY())
                || this.currentlySnappedComponent?.myID == row.myID;
            this.DrawCard(b, row.bounds, highlighted ? PaperShade : Paper, highlighted);
            Rectangle text = row.bounds;
            text.Inflate(-10, -5);
            text.Width -= 24;
            this.DrawSingleLineText(b, WorkerTaskPolicy.GetProfessionLabel(profession), text, Ink);
            if (profession == selected)
                WorkerMenuArt.DrawSprite(b, Game1.mouseCursors, OptionsCheckbox.sourceRectChecked,
                    new Rectangle(row.bounds.Right - 28, row.bounds.Y + 8, 18, 18));
        }
    }

    private void DrawWorkerVitals(SpriteBatch b, Rectangle bounds, int health, int maxHealth, WorkerStaminaState stamina)
    {
        int rowHeight = Math.Max(1, (bounds.Height - 4) / 2);
        string widestLabel = $"Stamina  {stamina.Maximum:0}.9/{stamina.Maximum:0}";
        Vector2 labelSize = Game1.smallFont.MeasureString(widestLabel);
        float labelScale = Math.Min(1f, Math.Min(bounds.Width / Math.Max(1, labelSize.X),
            Math.Max(1, rowHeight - Math.Clamp(rowHeight / 4, 4, 10) - 3) / Math.Max(1, labelSize.Y)));
        this.DrawVitalBar(b, new Rectangle(bounds.X, bounds.Y, bounds.Width, rowHeight),
            "Health", health, maxHealth, healthBar: true, labelScale: labelScale);
        this.DrawVitalBar(b, new Rectangle(bounds.X, bounds.Y + rowHeight + 4, bounds.Width, rowHeight),
            "Stamina", stamina.Current, stamina.Maximum, healthBar: false, labelScale: labelScale);
    }

    private void DrawVitalBar(SpriteBatch b, Rectangle row, string label, float current, float maximum,
        bool healthBar, bool large = false, float labelScale = 1f)
    {
        int barHeight = large ? Math.Clamp(row.Height - 28, 8, 48) : Math.Clamp(row.Height / 4, 4, 10);
        this.DrawSingleLineText(b, $"{label}  {current:0.#}/{maximum:0}",
            new Rectangle(row.X, row.Y, row.Width, Math.Max(1, row.Height - barHeight - 3)), Ink, maximumScale: labelScale);
        Rectangle bar = new(row.X, row.Bottom - barHeight, row.Width, barHeight);
        this.DrawVanillaVitalBar(b, bar, current, maximum, healthBar);
    }

    private void DrawVanillaVitalBar(SpriteBatch b, Rectangle bar, float current, float maximum, bool healthBar)
    {
        // Game1.drawHUD's cap/middle/cap artwork, rotated 90 degrees. Rotating
        // clockwise makes the native bottom-to-top fill run left-to-right.
        int sourceX = healthBar ? 268 : 256;
        float thicknessScale = bar.Height / 12f;
        float capLength = Math.Min(16 * thicknessScale, bar.Width / 2f);
        Vector2 capScale = new(thicknessScale, capLength / 16f);
        b.Draw(Game1.mouseCursors, new Vector2(bar.Right, bar.Y), new Rectangle(sourceX, 408, 12, 16),
            Color.White, MathHelper.PiOver2, Vector2.Zero, capScale, SpriteEffects.None, 1f);
        float middleLength = bar.Width - capLength * 2;
        if (middleLength > 0)
            b.Draw(Game1.mouseCursors, new Vector2(bar.Right - capLength, bar.Y), new Rectangle(sourceX, 424, 12, 16),
                Color.White, MathHelper.PiOver2, Vector2.Zero, new Vector2(thicknessScale, middleLength / 16f), SpriteEffects.None, 1f);
        b.Draw(Game1.mouseCursors, new Vector2(bar.Left + capLength, bar.Y), new Rectangle(sourceX, 448, 12, 16),
            Color.White, MathHelper.PiOver2, Vector2.Zero, capScale, SpriteEffects.None, 1f);

        int leftInset = Math.Max(1, (int)Math.Round(2 * thicknessScale));
        int rightInset = Math.Max(1, (int)Math.Round(12 * thicknessScale));
        Rectangle inner = new(bar.Left + leftInset, bar.Y + (int)Math.Round(3 * thicknessScale),
            Math.Max(0, bar.Width - leftInset - rightInset), Math.Max(1, (int)Math.Round(6 * thicknessScale)));
        float fraction = maximum > 0 ? Math.Clamp(current / maximum, 0, 1) : 0;
        Color fill = Utility.getRedToGreenLerpColor(fraction);
        int filledWidth = (int)(inner.Width * fraction);
        DrawRect(b, new Rectangle(inner.X, inner.Y, filledWidth, inner.Height), fill);
        // Match the native darker leading edge of the energy/health fill.
        fill.R = (byte)Math.Max(0, fill.R - 50);
        fill.G = (byte)Math.Max(0, fill.G - 50);
        int edgeWidth = Math.Min(filledWidth, Math.Max(1, (int)Math.Round(thicknessScale)));
        DrawRect(b, new Rectangle(inner.X + filledWidth - edgeWidth, inner.Y, edgeWidth, inner.Height), fill);
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
            if (this.ordersBounds.Height >= 180)
                this.DrawSectionHeading(b, this.ordersBounds, "WORKER VITALS", string.Empty);
            WorkerSummarySnapshot? currentWorker = this.GetSelectedWorker();
            if (currentWorker is not WorkerSummarySnapshot selected)
            {
                this.DrawText(b, "Select a worker to view health and stamina.",
                    new Rectangle(this.ordersBounds.X + Padding, this.ordersBounds.Y + 60,
                        this.ordersBounds.Width - Padding * 2, 45), Ink, centered: true);
                return;
            }
            this.workerBehaviorManager.GetWorkerHealth(selected.WorkerId, out int health, out int maxHealth);
            WorkerStaminaState stamina = this.workerShellManager.GetWorkerStamina(selected.WorkerId);
            bool showOrder = this.ordersBounds.Height >= 330;
            int top = this.ordersBounds.Y + (this.ordersBounds.Height >= 180 ? 66 : 20);
            int bottom = this.ordersBounds.Bottom - (showOrder ? 110 : 20);
            int availableHeight = Math.Max(1, bottom - top);
            int gap = availableHeight >= 120 ? 16 : 4;
            int rowHeight = Math.Max(1, Math.Min(100, (availableHeight - gap) / 2));
            top += Math.Max(0, (availableHeight - rowHeight * 2 - gap) / 2);
            int inset = Math.Min(38, this.ordersBounds.Width / 10);
            Rectangle healthRow = new(this.ordersBounds.X + inset, top, this.ordersBounds.Width - inset * 2, rowHeight);
            Rectangle staminaRow = new(healthRow.X, top + rowHeight + gap, healthRow.Width, rowHeight);
            bool large = rowHeight >= 48;
            this.DrawVitalBar(b, healthRow, "Health", health, maxHealth, healthBar: true, large: large);
            this.DrawVitalBar(b, staminaRow, "Stamina", stamina.Current, stamina.Maximum, healthBar: false, large: large);
            if (this.ordersBounds.Height >= 330)
            {
                Rectangle orderSummary = new(this.ordersBounds.X + 38, this.ordersBounds.Bottom - 90, this.ordersBounds.Width - 76, 63);
                this.DrawCard(b, orderSummary, Paper, false);
                WorkerTaskKind currentOrder = this.runtimeSnapshots[selected.WorkerId].AssignedTask;
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
                $"Forage area: {WorkerForageAreaCatalog.GetDisplayName(forager.ForageLocationName)}",
                enabled, Color.White, WorkerMenuArt.Icon.Sprout);
        }
        if (selectedWorker is { Profession: WorkerProfession.Fisher } fisher)
            this.DrawButton(b, this.forageAreaButton, $"Fishing area: {WorkerFishingAreaCatalog.Label(fisher.FishingArea)}",
                Context.IsMainPlayer, Color.White, WorkerMenuArt.Icon.Fishing);
        if (selectedWorker is { Profession: WorkerProfession.Miner } miner)
        {
            this.DrawButton(b, this.forageAreaButton, $"Mining area: {WorkerMiningPolicy.GetLabel(miner.MiningArea)}",
                enabled, Color.White, WorkerMenuArt.Icon.Mining);
        }
        if (selectedWorker is { Profession: WorkerProfession.CombatWorker } combat)
        {
            this.DrawButton(b, this.forageAreaButton,
                $"Combat area: {WorkerCombatAreaCatalog.GetLabel(combat.CombatArea)}",
                enabled, Color.White, WorkerMenuArt.Icon.Combat);
            this.DrawButton(b, this.explorationAreaButton,
                $"Explore area: {WorkerExplorationAreaCatalog.GetLabel(combat.ExplorationArea)}",
                enabled, Color.White, WorkerMenuArt.Icon.Explore);
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
        if (this.ordersBounds.Height >= 220)
            this.DrawSectionHeading(b, this.ordersBounds, "WORKER SKILLS", string.Empty);
        this.DrawButton(b, this.skillLevelsButton, "Skills", true, Color.White, selected: !this.showWorkerPerks);
        this.DrawButton(b, this.workerPerksButton, "Perks", true, Color.White, selected: this.showWorkerPerks);
        int top = this.skillLevelsButton.bounds.Bottom + 10;
        Rectangle content = new(this.ordersBounds.X + Padding, top, this.ordersBounds.Width - Padding * 2,
            Math.Max(1, this.ordersBounds.Bottom - top - 12));
        WorkerSummarySnapshot? selected = this.GetSelectedWorker();
        if (selected is null)
        {
            this.DrawText(b, this.showWorkerPerks ? "Select a worker to view their perks." : "Select a worker to view their skills.",
                content, MutedInk, centered: true);
            return;
        }
        if (this.showWorkerPerks)
        {
            this.DrawWorkerPerks(b, content, this.workerShellManager.GetWorkerExperience(selected.Value.WorkerId));
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
        int gap = this.ordersBounds.Height < 220 ? 4 : 8;
        int columns = content.Height < 160 ? 2 : 1;
        int rows = (skills.Length + columns - 1) / columns;
        int rowHeight = Math.Max(1, Math.Min(58, (content.Height - gap * (rows - 1)) / rows));
        int rowWidth = Math.Max(1, (content.Width - gap * (columns - 1)) / columns);
        for (int index = 0; index < skills.Length; index++)
        {
            Rectangle row = new(content.X + index % columns * (rowWidth + gap), top + index / columns * (rowHeight + gap),
                rowWidth, rowHeight);
            this.DrawSkillRow(b, row, skills[index].Name, skills[index].Experience);
        }
    }

    private void DrawWorkerPerks(SpriteBatch b, Rectangle content, WorkerSkillExperience experience)
    {
        int gap = content.Height < 160 ? 8 : 12;
        bool sideBySide = content.Height < 160 && content.Width >= 380;
        int rowHeight = Math.Max(1, Math.Min(128, sideBySide ? content.Height : (content.Height - gap) / 2));
        int cardWidth = sideBySide ? (content.Width - gap) / 2 : content.Width;
        bool compact = rowHeight < 72 || sideBySide;
        Rectangle stamina = new(content.X, content.Y, cardWidth, rowHeight);
        Rectangle health = sideBySide ? new Rectangle(stamina.Right + gap, content.Y, cardWidth, rowHeight)
            : new Rectangle(content.X, stamina.Bottom + gap, cardWidth, rowHeight);
        this.DrawPerkCard(b, stamina, "Endurance", WorkerMenuArt.Icon.Perk, WorkerPerkPolicy.HasStaminaPerk(experience),
            compact ? "Any work skill Lv. 5: stamina 270 → 540"
                : "Farming, Mining, Fishing or Foraging Lv. 5\nMaximum stamina: 270 → 540. Applies once.");
        this.DrawPerkCard(b, health, "Vitality", WorkerMenuArt.Icon.Combat, WorkerPerkPolicy.HasHealthPerk(experience),
            "Combat Lv. 5: maximum health 100 → 200");
    }

    private void DrawPerkCard(SpriteBatch b, Rectangle row, string name, WorkerMenuArt.Icon icon, bool unlocked, string description)
    {
        this.DrawCard(b, row, unlocked ? PaperShade : Paper, unlocked);
        int inset = row.Height < 72 ? 9 : 14;
        int iconSize = row.Height >= 90 && row.Width >= 360 ? 40 : 0;
        int textX = row.X + inset + (iconSize > 0 ? iconSize + 12 : 0);
        if (iconSize > 0)
            WorkerMenuArt.Draw(b, icon, new Rectangle(row.X + inset, row.Y + inset, iconSize, iconSize), unlocked ? 1f : 0.5f);
        int titleHeight = Math.Min(28, Math.Max(1, (row.Height - inset * 2) / 2));
        int statusWidth = Math.Min(116, (row.Right - inset - textX) / 3);
        this.DrawSingleLineText(b, name,
            new Rectangle(textX, row.Y + inset, Math.Max(1, row.Right - inset - statusWidth - textX - 8), titleHeight), Ink);
        this.DrawSingleLineText(b, unlocked ? "Unlocked" : "Locked",
            new Rectangle(row.Right - inset - statusWidth, row.Y + inset, statusWidth, titleHeight), unlocked ? Leaf : MutedInk, centered: true, maximumScale: 0.85f);
        this.DrawText(b, description, new Rectangle(textX, row.Y + inset + titleHeight + 4,
            row.Right - inset - textX, Math.Max(1, row.Height - inset * 2 - titleHeight - 4)), MutedInk);
    }

    private void DrawSkillRow(SpriteBatch b, Rectangle row, string name, int experience)
    {
        this.DrawCard(b, row, Paper, false);
        int level = WorkerExperiencePolicy.GetLevel(experience);
        bool stacked = row.Height >= 42;
        int iconSize = stacked && row.Width >= 260 ? 30 : 0;
        WorkerMenuArt.Icon icon = name switch
        {
            "Mining" => WorkerMenuArt.Icon.Mining,
            "Fishing" => WorkerMenuArt.Icon.Fishing,
            "Foraging" => WorkerMenuArt.Icon.Foraging,
            "Combat" => WorkerMenuArt.Icon.Combat,
            _ => WorkerMenuArt.Icon.Sprout,
        };
        if (iconSize > 0)
            WorkerMenuArt.Draw(b, icon, new Rectangle(row.X + 12, row.Y + (row.Height - iconSize) / 2, iconSize, iconSize));
        int textX = row.X + 12 + (iconSize > 0 ? iconSize + 10 : 0);
        int textWidth = Math.Max(1, row.Right - 12 - textX);
        int levelWidth = Math.Min(76, textWidth / 3);
        int labelHeight = stacked ? Math.Min(26, row.Height - 22) : Math.Max(1, row.Height - 11);
        this.DrawSingleLineText(b, name, new Rectangle(textX, row.Y + (stacked ? 6 : 4), Math.Max(1, textWidth - levelWidth - 8), labelHeight), Ink);
        this.DrawSingleLineText(b, $"Lv. {level}", new Rectangle(row.Right - 12 - levelWidth, row.Y + (stacked ? 6 : 4), levelWidth, labelHeight), MutedInk, centered: true);
        int low = WorkerExperiencePolicy.GetThreshold(level);
        int high = level == WorkerExperiencePolicy.MaximumLevel ? low : WorkerExperiencePolicy.GetThreshold(level + 1);
        float fraction = high == low ? 1f : Math.Clamp((float)(experience - low) / (high - low), 0f, 1f);
        // A quiet, inset XP track keeps progress distinct from the native health/stamina HUD bars.
        Rectangle bar = new(textX, row.Bottom - (stacked ? 14 : 5), textWidth, stacked ? 7 : 2);
        DrawRect(b, bar, Wood * 0.45f);
        Rectangle inner = new(bar.X + 1, bar.Y + 1, Math.Max(1, bar.Width - 2), Math.Max(1, bar.Height - 2));
        DrawRect(b, inner, PaperShade);
        DrawRect(b, new Rectangle(inner.X, inner.Y, (int)(inner.Width * fraction), inner.Height), Leaf);
    }

    private void DrawStoragePanel(SpriteBatch b)
    {
        if (this.ordersBounds.Height >= 220)
            this.DrawSectionHeading(b, this.ordersBounds, "HARVEST STORAGE", string.Empty);

        WorkerHarvestDestination? destination = this.selectedWorkerId is null
            ? null
            : this.workerShellManager.GetHarvestDestination(this.selectedWorkerId);
        WorkerChestOption? selectedChest = destination is null ? null : this.chestOptions.Find(option =>
            option.LocationName == destination.LocationName && option.Tile == destination.Tile);
        string selectedLabel = destination is null ? "Shipping bin" : selectedChest?.Label ?? "Missing chest — using shipping bin";
        string workerLabel = this.GetSelectedWorker()?.DisplayName ?? "Select a worker";
        this.DrawButton(b, this.destinationDropdownButton, $"{workerLabel}: {selectedLabel}",
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
            this.DrawCard(b, row.bounds, isSelected || this.IsControlHighlighted(row) ? PaperShade : Paper, isSelected);
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
        if (bounds.Width < 2 || bounds.Height < 2) return;
        // The vanilla options/shop button nine-slice keeps the same pixel vocabulary
        // as the surrounding dialogue frame without adding another heavy wood border.
        Color tint = fill == Paper ? Color.White
            : fill == Color.LightPink ? Color.LightPink
            : fill == new Color(211, 190, 158) ? new Color(210, 205, 195)
            : new Color(255, 235, 190);
        float scale = Math.Min(2f, Math.Min(bounds.Width, bounds.Height) / 6f);
        IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
            bounds.X, bounds.Y, bounds.Width, bounds.Height, tint, scale, drawShadow: false);
        if (selected)
            DrawRect(b, new Rectangle(bounds.X + 7, bounds.Bottom - 6, bounds.Width - 14, 3), Leaf);
    }

    private bool IsControlHighlighted(ClickableComponent button)
    {
        if (this.pendingDismissalId is not null && button.myID != ConfirmId && button.myID != CancelId) return false;
        if (this.professionDropdownOpen && button.myID != ProfessionDropdownId
            && (button.myID < ProfessionRowIdBase || button.myID >= ProfessionRowIdBase + ProfessionOptions.Length)) return false;
        return button.containsPoint(Game1.getMouseX(), Game1.getMouseY()) || this.currentlySnappedComponent?.myID == button.myID;
    }

    private void DrawSectionHeading(SpriteBatch b, Rectangle panel, string heading, string note)
    {
        bool showNote = note.Length > 0 && panel.Width > 280;
        this.DrawSingleLineText(b, heading, new Rectangle(panel.X + Padding, panel.Y + 14, Math.Max(1, panel.Width - Padding * 2 - (showNote ? 115 : 0)), 30), Ink);
        if (showNote)
            this.DrawSingleLineText(b, note, new Rectangle(panel.Right - 135, panel.Y + 18, 110, 24), MutedInk, centered: true);
        DrawRect(b, new Rectangle(panel.X + 18, panel.Y + 50, panel.Width - 36, 2), PaperShade);
    }

    private void DrawButton(SpriteBatch b, ClickableComponent button, string label, bool enabled, Color color, WorkerMenuArt.Icon? icon = null, bool selected = false)
    {
        bool hovered = this.IsControlHighlighted(button);
        Color fill = !enabled ? new Color(211, 190, 158) : selected ? new Color(245, 216, 145) : hovered ? new Color(255, 236, 187) : color == Color.White ? Paper : color;
        this.DrawCard(b, button.bounds, fill, selected);
        int textX = button.bounds.X + 10;
        if (icon is WorkerMenuArt.Icon motif && button.bounds.Width >= 135)
        {
            WorkerMenuArt.Draw(b, motif, new Rectangle(button.bounds.X + 12, button.bounds.Y + (button.bounds.Height - 24) / 2, 24, 24), enabled ? 1f : 0.5f);
            textX += 30;
        }
        Rectangle labelBounds = new(textX, button.bounds.Y + 5, Math.Max(1, button.bounds.Right - textX - 10), Math.Max(1, button.bounds.Height - 10));
        if (button.myID is DestinationDropdownId or ForageAreaId or ExplorationAreaId)
        {
            Rectangle arrow = new(button.bounds.Right - 30, button.bounds.Y + (button.bounds.Height - 22) / 2, 20, 22);
            Rectangle source = button.myID == DestinationDropdownId ? OptionsDropDown.dropDownButtonSource : new Rectangle(365, 495, 12, 11);
            WorkerMenuArt.DrawSprite(b, Game1.mouseCursors, source, arrow, enabled ? 1f : 0.5f);
            labelBounds.Width = Math.Max(1, labelBounds.Width - 26);
        }
        if (label is "<" or ">")
        {
            WorkerMenuArt.DrawSprite(b, Game1.mouseCursors,
                new Rectangle(label == "<" ? 352 : 365, 495, 12, 11), labelBounds, enabled ? 1f : 0.4f);
            return;
        }
        if (button.myID is DestinationDropdownId or ForageAreaId or ExplorationAreaId)
            this.DrawText(b, label, labelBounds, enabled ? Ink : MutedInk, centered: true);
        else
            this.DrawSingleLineText(b, label, labelBounds, enabled ? Ink : MutedInk, centered: true);
    }

    private void DrawOrderCard(SpriteBatch b, ClickableComponent button, WorkerTaskKind task, bool enabled, bool selected)
    {
        bool hovered = this.IsControlHighlighted(button);
        Color fill = !enabled ? new Color(211, 190, 158) : selected ? new Color(244, 221, 165) : hovered ? new Color(255, 239, 198) : Paper;
        this.DrawCard(b, button.bounds, fill, selected);
        WorkerMenuArt.Icon icon = task switch
        {
            WorkerTaskKind.WaterCrops => WorkerMenuArt.Icon.Water,
            WorkerTaskKind.HarvestCrops => WorkerMenuArt.Icon.Harvest,
            WorkerTaskKind.TendCrops => WorkerMenuArt.Icon.Tend,
            WorkerTaskKind.CollectForage => WorkerMenuArt.Icon.Foraging,
            WorkerTaskKind.ChopTrees => WorkerMenuArt.Icon.Foraging,
            WorkerTaskKind.ChopHardwood => WorkerMenuArt.Icon.Hardwood,
            WorkerTaskKind.ClearDebris => WorkerMenuArt.Icon.Foraging,
            WorkerTaskKind.MineRocks or WorkerTaskKind.MineOreGems or WorkerTaskKind.FindLadder => WorkerMenuArt.Icon.Mining,
            WorkerTaskKind.SlayMonsters => WorkerMenuArt.Icon.Combat,
            WorkerTaskKind.ExploreArea => WorkerMenuArt.Icon.Explore,
            WorkerTaskKind.Fish => WorkerMenuArt.Icon.Fishing,
            _ => WorkerMenuArt.Icon.Home,
        };
        if (button.bounds.Height < 90)
        {
            bool showIcon = button.bounds.Width >= 180 && button.bounds.Height >= 36;
            if (showIcon)
                WorkerMenuArt.Draw(b, icon, new Rectangle(button.bounds.X + 10, button.bounds.Y + (button.bounds.Height - 24) / 2, 24, 24), enabled ? 1f : 0.5f);
            int inset = showIcon ? 44 : 10;
            this.DrawText(b, GetTaskLabel(task), new Rectangle(button.bounds.X + inset, button.bounds.Y + 7,
                button.bounds.Width - inset - 10, Math.Max(1, button.bounds.Height - 14)), enabled ? Ink : MutedInk, centered: !showIcon);
            return;
        }
        int iconSize = 3;
        WorkerMenuArt.Draw(b, icon, button.bounds.X + 17, button.bounds.Y + 15, iconSize);
        int titleX = button.bounds.X + 17 + 12 * iconSize + 13;
        int titleWidth = Math.Max(1, button.bounds.Right - titleX - (selected ? 40 : 15));
        this.DrawSingleLineText(b, GetTaskLabel(task), new Rectangle(titleX, button.bounds.Y + 18, titleWidth, 32), enabled ? Ink : MutedInk);
        if (selected && button.bounds.Width > 190)
        {
            WorkerMenuArt.DrawSprite(b, Game1.mouseCursors, OptionsCheckbox.sourceRectChecked,
                new Rectangle(button.bounds.Right - 32, button.bounds.Y + 20, 18, 18));
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
        string wrapped = text;
        Vector2 size = Vector2.Zero;
        float scale = 1f;
        for (int step = 0; step <= 10; step++)
        {
            scale = 1f - step * 0.05f;
            wrapped = Game1.parseText(text, Game1.smallFont, Math.Max(1, (int)(bounds.Width / scale)));
            size = Game1.smallFont.MeasureString(wrapped);
            if (size.X * scale <= bounds.Width && size.Y * scale <= bounds.Height) break;
        }
        scale = Math.Min(scale, Math.Min(bounds.Width / Math.Max(1f, size.X), bounds.Height / Math.Max(1f, size.Y)));
        Vector2 position = new(bounds.X, bounds.Y);
        if (centered)
        {
            position += new Vector2((bounds.Width - size.X * scale) / 2f, (bounds.Height - size.Y * scale) / 2f);
        }
        b.DrawString(Game1.smallFont, wrapped, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
    }

    private void DrawSingleLineText(SpriteBatch b, string text, Rectangle bounds, Color color,
        bool centered = false, float maximumScale = 1f, bool ellipsize = false)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        Vector2 size = Game1.smallFont.MeasureString(text);
        if (ellipsize)
        {
            float readableScale = Math.Min(maximumScale, Math.Min(0.8f, bounds.Height / Math.Max(1f, size.Y)));
            string fullText = text;
            if (size.X * readableScale > bounds.Width)
            {
                while (text.Length > 1 && Game1.smallFont.MeasureString(text + "...").X * readableScale > bounds.Width)
                    text = text[..^1];
                if (text != fullText) text += "...";
            }
            size = Game1.smallFont.MeasureString(text);
        }
        float scale = Math.Min(maximumScale, Math.Min(bounds.Width / Math.Max(1f, size.X), bounds.Height / Math.Max(1f, size.Y)));
        Vector2 position = new(bounds.X, bounds.Y + (bounds.Height - size.Y * scale) / 2f);
        if (centered) position.X += (bounds.Width - size.X * scale) / 2f;
        b.DrawString(Game1.smallFont, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 1f);
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
            this.professionDropdownOpen = false;
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

    private Rectangle GetMenuFrameBounds()
        => new(this.xPositionOnScreen, this.yPositionOnScreen + FrameTopOffset, this.width, this.height - FrameTopOffset);

    private ResizeEdges GetResizeEdges(int x, int y)
    {
        if (this.professionDropdownOpen || this.pendingDismissalId is not null || this.upperRightCloseButton?.containsPoint(x, y) == true)
            return ResizeEdges.None;

        Rectangle bounds = this.GetMenuFrameBounds();
        Rectangle hitBounds = bounds;
        hitBounds.Inflate(ResizeHitPadding, ResizeHitPadding);
        if (!hitBounds.Contains(x, y))
            return ResizeEdges.None;

        int left = Math.Abs(x - bounds.Left);
        int right = Math.Abs(x - bounds.Right);
        int top = Math.Abs(y - bounds.Top);
        int bottom = Math.Abs(y - bounds.Bottom);
        bool nearCorner = Math.Min(left, right) <= 28 && Math.Min(top, bottom) <= 28;
        int sideReach = nearCorner ? 28 : 20;
        int endReach = nearCorner ? 28 : ResizeHitPadding;
        ResizeEdges edges = ResizeEdges.None;
        if (left <= sideReach) edges |= ResizeEdges.Left;
        else if (right <= sideReach) edges |= ResizeEdges.Right;
        if (top <= endReach) edges |= ResizeEdges.Top;
        else if (bottom <= endReach) edges |= ResizeEdges.Bottom;
        return edges;
    }

    private void DrawResizeGrips(SpriteBatch b)
    {
        if (this.pendingDismissalId is not null)
            return;

        Rectangle frame = this.GetMenuFrameBounds();
        ResizeEdges active = this.resizeEdges != ResizeEdges.None
            ? this.resizeEdges : this.GetResizeEdges(Game1.getMouseX(), Game1.getMouseY());
        Color GripColor(ResizeEdges edges) => (active & edges) != 0 ? Paper : MutedInk;
        void Grip(Rectangle bounds, ResizeEdges edges) => b.Draw(Game1.staminaRect, bounds, GripColor(edges));
        int centerX = frame.Center.X;
        int centerY = frame.Center.Y;
        for (int offset = -4; offset <= 4; offset += 4)
        {
            Grip(new Rectangle(frame.Left + 5 + offset + 4, centerY - 12, 2, 24), ResizeEdges.Left);
            Grip(new Rectangle(frame.Right - 15 + offset + 4, centerY - 12, 2, 24), ResizeEdges.Right);
            Grip(new Rectangle(centerX - 12, frame.Top + 5 + offset + 4, 24, 2), ResizeEdges.Top);
            Grip(new Rectangle(centerX - 12, frame.Bottom - 15 + offset + 4, 24, 2), ResizeEdges.Bottom);
        }
        for (int column = 0; column < 2; column++)
        {
            bool left = column == 0;
            for (int row = 0; row < 2; row++)
            {
                bool top = row == 0;
                // Leave the close button its own upper-right corner.
                if (!left && top)
                    continue;
                ResizeEdges edges = (left ? ResizeEdges.Left : ResizeEdges.Right) | (top ? ResizeEdges.Top : ResizeEdges.Bottom);
                int x = left ? frame.Left + 7 : frame.Right - 7;
                int y = top ? frame.Top + 7 : frame.Bottom - 7;
                int length = 16;
                Grip(new Rectangle(left ? x : x - length, y, length, 3), edges);
                Grip(new Rectangle(x, top ? y : y - length, 3, length), edges);
            }
        }
    }

    private static string FormatTile(Point? tile) => tile is Point point ? $"{point.X}, {point.Y}" : "--";

    private static string GetTaskLabel(WorkerTaskKind task)
        => WorkerTaskPolicy.GetTaskLabel(task, "Idle / return home");

    private static string GetTaskDescription(WorkerTaskKind task) => task switch
    {
        WorkerTaskKind.WaterCrops => "Walk to dry, growing crops on the farm and water them. Rain and already-watered crops are skipped.",
        WorkerTaskKind.HarvestCrops => "Walk to ripe crops on the farm and harvest them. When finished, the worker wanders the Farm and repeats this order tomorrow.",
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
