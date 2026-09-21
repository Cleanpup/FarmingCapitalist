# FarmingCapitalist — repository context

## Goal and scope
- Stardew Valley 1.6 / SMAPI mod in C#, targeting .NET 6; entry DLL is `FarmingCapitalistFull.dll`.
- Build hirable NPC workers who visibly travel to assigned jobs and perform real farm work.
- Read `Plan.txt` for milestone status and `TESTING.md` for the current manual checks.
- Keep this file concise. Update facts when architecture or commands change; avoid task transcripts.

## Read only what the task needs
- `ModEntry.cs`: event wiring, console commands, lifecycle.
- `Workers/WorkerShellManager.cs`: roster, spawning, identity, persistence and hiring.
- `Workers/WorkerBehaviorManager.cs`: task selection and execution; `WorkerNavigationManager.cs`: routes/recovery.
- `Workers/WorkerControlMenu*.cs`: management UI and input; `WorkerCustomizationManager.cs`: appearance workflow.
- `Workers/WorkerAppearance*.cs`, `WorkerSpriteSheetBuilder.cs`: farmer appearance and NPC sprite generation.
- Search `Workers/` first. `SMAPI/` and `StardewValleyDecompiled/` are ignored local reference sources, not mod code.
- Inspect only the specific reference API needed; do not recursively dump decompiled source or generated output.

## Invariants
- Host owns hiring, wages, tasks, movement, crop changes and save writes. Clients only observe mirrored state.
- Guard game access with `Context.IsWorldReady`; stop work during menus, events and inactive game time.
- Save worker identity, appearance and assignments in mod data; rebuild temporary runtime state from the live world.
- Never serialize generated textures or path controllers; clean up transient workers and textures on lifecycle boundaries.
- Never overwrite player objects, destroy obstacles, or teleport to a job to hide a failed route.
- Revalidate a crop at execution, reserve jobs across workers, and recover from blocked/invalid destinations.
- Preserve harvested items and vanilla crop/regrowth rules. Never silently discard produce.
- Use existing NPC/game APIs where practical and keep policy, movement, persistence and UI separate.
- Treat legacy roster saves as supported input. Avoid duplicate workers and repeated daily charges.

## Work and validation
- Preserve unrelated working-tree edits. Do not modify the user's `.gitignore` changes or reference source trees.
- Build with `dotnet build -p:EnableModDeploy=false`; use `$HOME/.dotnet/dotnet` if `dotnet` is absent from PATH.
- Game path is normally detected; override with `-p:GamePath="/path/to/Stardew Valley"` when needed.
- Disable mod deployment during validation. Keep generated binaries under ignored `bin/` and `obj/`.
- Compile against installed game assemblies; verify uncertain APIs against the local game/SMAPI sources.
- Use focused tests for gameplay/data-loss/authority rules. A successful build is not an in-game playtest.
- Finish with a brief change summary, actual verification results, and a concrete list of features to test in game.
