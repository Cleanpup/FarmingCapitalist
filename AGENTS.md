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


# User preferences

- If an action requires the user's sudo password, provide the exact commands for the user to run themselves. Do not switch to an alternative installation method to avoid the password requirement.

# Stardew Valley debug commands

Reference: [Stardew Valley Wiki — Debug commands](https://stardewvalleywiki.com/Modding:Console_commands#Debug_commands).

- Enter commands in the SMAPI console with the Console Commands mod installed.
- Use `debug <command> [arguments]` for game debug commands. Wiki table entries usually omit the required `debug` prefix.
- Wiki notation: `<...>` is required, `[...]` is optional; `S`, `I`, and `F` indicate string, integer, and float parameters. Replace placeholders with values; do not type brackets or type labels.
- Preserve parameter capitalization. Use partial names only when the command explicitly supports fuzzy matching. Quote multiword arguments where supported.
- Check the linked command entry for argument order, defaults, and effects before suggesting a command. Use `debug search <term>` to discover debug commands; use `help` or `help <command>` for SMAPI command documentation.
- A no-output response does not necessarily mean failure; verify the resulting game state.
- Use a test save for commands that alter state; debug commands can damage saves.

Examples:

```text
debug search backpack
debug where Robin
debug fin "galaxy sword"
```

## FarmingCapitalist commands

These are registered by this mod in `ModEntry.cs`, so enter them directly without `debug`:

```text
workerstatus
spawn
spawn d
delete
```

- `workerstatus` reports the primary worker and configured worker count.
- `spawn` opens appearance customization; saving adds a worker. `spawn d` adds one with the default appearance.
- `delete` removes all worker shells and clears the saved roster.
- Recheck `ModEntry.cs` and the worker managers when documenting these commands; their console help text may lag behind implementation.
