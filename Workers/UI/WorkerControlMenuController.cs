using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace FarmingCapitalist.Workers;

internal sealed class WorkerControlMenuController
{
    private readonly IInputHelper inputHelper;
    private readonly WorkerShellManager workerShellManager;
    private readonly WorkerCustomizationManager workerCustomizationManager;
    private readonly WorkerBehaviorManager workerBehaviorManager;

    public WorkerControlMenuController(
        IInputHelper inputHelper,
        WorkerShellManager workerShellManager,
        WorkerCustomizationManager workerCustomizationManager,
        WorkerBehaviorManager workerBehaviorManager)
    {
        this.inputHelper = inputHelper;
        this.workerShellManager = workerShellManager;
        this.workerCustomizationManager = workerCustomizationManager;
        this.workerBehaviorManager = workerBehaviorManager;
    }

    public void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        _ = sender;

        if (e.Button != SButton.B)
        {
            return;
        }

        if (Game1.activeClickableMenu is WorkerControlMenu workerControlMenu)
        {
            workerControlMenu.RequestClose();
            this.inputHelper.Suppress(e.Button);
            return;
        }

        if (Game1.activeClickableMenu is not null || !Context.IsWorldReady || !Context.IsPlayerFree)
        {
            return;
        }

        if (this.OpenMenu())
        {
            this.inputHelper.Suppress(e.Button);
        }
    }

    public bool OpenMenu()
    {
        if (!Context.IsWorldReady || !Context.IsPlayerFree || Game1.activeClickableMenu is not null)
        {
            return false;
        }

        if (!this.workerShellManager.IsFeatureUnlocked)
        {
            Game1.addHUDMessage(new HUDMessage(WorkerUnlockPolicy.LockedMessage, HUDMessage.error_type));
            return false;
        }
        Game1.activeClickableMenu = new WorkerControlMenu(this.workerShellManager, this.workerBehaviorManager, this.workerCustomizationManager);
        Game1.playSound("bigSelect");
        return true;
    }

    public void Reset()
    {
        if (Game1.activeClickableMenu is WorkerControlMenu workerControlMenu)
        {
            workerControlMenu.exitThisMenuNoSound();
        }
    }
}
