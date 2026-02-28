using StardewModdingAPI;
using System;

namespace FarmingCapitalist; 

public class ModEntry : Mod //base class for all SMAPI mods
{
    public override void Entry(IModHelper helper)  // creates entry function SMAPI can call to start the mod ( passing in an IMODHelper instance)
    {
        // Log a message to indicate that the mod has loaded successfully
        Monitor.Log("Farming Capitalist mod loaded successfully!", LogLevel.Info);
        /*example code helper.Events.Gameloop.UpdateTicked += GameLoop_UpdateTicked; // subscribe to the 
         UpdateTicked event, which is triggered every game tick (60 times per second) */
    }

    private void GameLoop_UpdateTicked(object sender, EventArgs e)
    {
        // This method will be called every game tick. You can add your mod's logic here.
        // For example, you could check for certain conditions and apply effects to the player or the game world.

        Monitor.Log("Game tick occurred!", LogLevel.Debug); // Log a message every tick (for demonstration purposes) inside SMAPI's debug log
    }
}
