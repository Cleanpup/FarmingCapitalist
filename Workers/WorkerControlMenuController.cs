using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace FarmingCapitalist.Workers;

internal sealed class WorkerControlMenuController
{
    private readonly IInputHelper inputHelper;
    private readonly WorkerShellManager workerShellManager;

    public WorkerControlMenuController(IInputHelper inputHelper, WorkerShellManager workerShellManager)
    {
        this.inputHelper = inputHelper;
        this.workerShellManager = workerShellManager;
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
            workerControlMenu.exitThisMenu();
            this.inputHelper.Suppress(e.Button);
            return;
        }

        if (Game1.activeClickableMenu is not null || !Context.IsWorldReady || !Context.IsPlayerFree)
        {
            return;
        }

        Game1.activeClickableMenu = new WorkerControlMenu(this.workerShellManager);
        Game1.playSound("bigSelect");
        this.inputHelper.Suppress(e.Button);
    }

    public void Reset()
    {
        if (Game1.activeClickableMenu is WorkerControlMenu workerControlMenu)
        {
            workerControlMenu.exitThisMenuNoSound();
        }
    }
}
