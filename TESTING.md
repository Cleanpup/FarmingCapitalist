# Manual in-game checks

Build with `~/.dotnet/dotnet build FarmingCapitalist.csproj -p:EnableModDeploy=false` and copy the generated mod into the game only when you are ready to test.

1. Load a save with at least 500g. Press `B` and hire a worker with the custom appearance flow. Confirm the worker appears in the farmhouse and the hire cost is charged once.
2. Open the worker menu again. Confirm the worker card shows assignment, current state, location, wage, and completed work. Resize the game window and check the menu remains usable.
3. Assign `Water crops`, `Harvest crops`, and `Tend crops` one at a time. Confirm the worker visibly leaves the farmhouse, walks to the farm, performs the matching crop work, and returns or idles when no matching work remains.
4. Confirm harvested produce is placed in the farm shipping bin and regrowing crops retain their regrowth behavior.
5. Assign `Idle` while the worker is travelling. Confirm the route stops and the worker does not continue changing crops.
6. End the day and reload the save. Confirm identity, appearance, assignment, and worker count persist, with one wage per worker per day.
7. Try hiring with insufficient funds, hiring at the worker cap, dismissing one worker, and dismissing all workers. Confirm failed actions do not charge money and other workers remain intact.
8. In multiplayer, confirm the host can hire/assign/dismiss and clients can view mirrored worker state without changing it.
9. Use `workers help`, `workers status`, and `workers assign <id> <water|harvest|tend|idle>` to inspect and control workers from the SMAPI console.
