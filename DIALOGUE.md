# Hire Skilled Helpers dialogue

The public asset path retains its original identity for compatibility with existing content packs.

Workers use the game-content asset `Mods/Cleanpup.FarmingCapitalist/WorkerDialogue`. Talking to the same worker repeatedly on one day gives the same line; the selected line advances predictably between days.

Dialogue keys are checked in this order:

1. `<worker ID>.<task>.<line>`
2. `<worker ID>.Default.<line>`
3. `<task>.<line>`
4. `Default.<line>`

Task names are `Idle`, `WaterCrops`, `HarvestCrops`, `TendCrops`, `CollectForage`, `ChopTrees`, and `ChopHardwood`. Line suffixes can be any unique text; matching keys are sorted before the daily line is selected. Dialogue text supports Stardew's normal dialogue markup plus `{{workerName}}` and `{{task}}` placeholders.

A Content Patcher pack can add or replace lines with `EditData`:

```json
{
  "Format": "2.0.0",
  "Changes": [
    {
      "Action": "EditData",
      "Target": "Mods/Cleanpup.FarmingCapitalist/WorkerDialogue",
      "Entries": {
        "Idle.1": "A replacement idle line.",
        "worker-2.HarvestCrops.1": "A line only Worker 2 says while assigned to harvesting."
      }
    }
  ]
}
```
