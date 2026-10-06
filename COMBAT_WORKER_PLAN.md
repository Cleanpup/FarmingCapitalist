# Combat worker implementation plan

Status: design plan only. No combat code, deployment, or save migration has been made on this branch.

## Player-facing contract

- Add a **Combat Worker** profession at hire time. Its only work order is **Slay monsters** (plus **Idle**). It does not farm, forage, clear debris, break rocks, or attack non-monster characters.
- The Jobs screen selects one combat destination: **Farm**, **regular Mines**, **Skull Cavern (desert caves)**, or **Ginger Island**. Store the selection per worker, independently of other workers.
- Regular Mines workers first appear *inside the mine entrance area before level zero*, at a checked free tile. The exact entrance map/floor interpretation needs the player's answer below.
- The selected job and location survive save/reload. Transient fight state, target references, generated floor references, paths, and animation timers do not.
- One host decides movement, attacks, drops, XP, and save writes. Multiplayer clients observe the mirrored worker state.

## Findings in this checkout

- `WorkerProfession` and `WorkerTaskKind` are stable numeric save enums. Add new values at the end; keep Farmer/Forager and all existing task numbers unchanged.
- `WorkerRosterEntry` holds profession, assignment, and Forager area. `WorkerShellManager.NormalizeRosterEntries` validates loaded values and supports legacy Farmer saves. A combat area needs its own field/catalog and normalization; do not reuse `ForageLocationName`.
- `WorkerTaskPolicy.GetTasks`, the hire dropdown, Jobs UI, console command mapping/help, dialogue, and snapshots all assume Farmer/Forager. The current two-way profession check often falls back to Farmer; replace it with explicit handling for the third profession.
- `WorkerBehaviorManager` is host gated in `Update` and pauses when time does not pass. It dispatches Farmer/Forager work and keeps transient per-worker state in dictionaries. Put combat selection/execution in a dedicated service rather than expanding crop/forage branches with dungeon rules.
- `WorkerNavigationManager` walks to an authored exit and then `Game1.warpCharacter`s directly to a safe target tile. This works for ordinary locations, but does not itself generate or enter dungeon floors or follow a player between floors. Never apply its direct-destination warp blindly to a mine level.
- Worker shells are `NPC`s, not farmers. Combat, health, damage, monster targeting, and drops therefore need a deliberate worker-owned adapter instead of assuming player combat methods apply. Confirm the exact Stardew 1.6 APIs against installed assemblies before implementing.
- The XP work being tested exists in another worktree. Merge or rebase it before implementing combat XP, then award only the attacking worker's Combat XP on a confirmed kill. Do not route worker actions through `Game1.player` or `Game1.MasterPlayer` XP calls.

## Decisions needed from the player

1. Does “inside just before level zero” mean the regular mine entrance lobby/cave reached from Mountain, or the generated `MineShaft` floor 0? Use that as the worker's initial Mines station. Never place it outside on Mountain.
2. In Mines, Skull Cavern, and Volcano, should the worker **follow a farmer between floors**, **independently descend**, or **stay on an assigned floor**? If following, which farmer in multiplayer and what if that farmer leaves?
3. Which Ginger Island zone is intended: Volcano Dungeon, outdoor Island areas with monsters, or both?
4. Should a dungeon worker leave at shift end, after a cleared floor, when its farmer leaves, or only on manual recall? This also determines whether a mine worker waits at the entrance before entering a floor.
5. Confirm the desired worker defeat rule and monster loot destination. Suggested first version: no permanent death; a defeated worker retreats home for the day, and worker-generated loot uses the existing shared chest/shipping destination. These are gameplay choices, not implementation defaults.

Do not hard-code an answer to these questions. The Farm location can be built first while dungeon behavior is decided.

## Implementation sequence

### 1. Data, UI, and authority

1. Append `CombatWorker` to `WorkerProfession` and `SlayMonsters` to `WorkerTaskKind` without changing numeric values of existing entries.
2. Add a combat area catalog with stable save IDs and user-facing labels. Keep user-facing selection separate from concrete `GameLocation.NameOrUniqueName` and dynamic floor identity. Validate unlock/availability at assignment and again before departure. Unavailable areas should show a reason and leave the worker safely idle at home.
3. Add `CombatLocation` to `WorkerRosterEntry`, clone/snapshot/network mirror, save normalization, and host-only setter. Unknown/missing saved areas fall back to a safe valid value; preserve legacy rosters and existing wages.
4. Extend hire dropdown, Jobs selector, task descriptions, roster status, dialogue, and `workers assign`/area commands. Changing area cancels any target/attack/path, recalls the worker safely, and requires a fresh order as Forager area changes do today. Only the host accepts state changes.
5. Add focused pure tests for enum stability, profession-task restrictions, area normalization, save round trips, and two workers with distinct selections.

### 2. Combat runtime on Farm

1. Introduce a `WorkerCombatManager` called only by the host from the existing update loop. State is keyed by worker ID: `AtHome`, `Travelling`, `Searching`, `Approaching`, `Attacking`, `Recovering`, and `Returning`. Clear it on reassignment, dismissal, save lifecycle, day reset, and title return.
2. Search only monsters in the worker's current authorized area. Exclude invulnerable, dead, or non-attackable entities, and reserve a target so two workers do not pile into the same pursuit unless the design permits shared fights. Retest existence, location, health, and attackability at execution time.
3. Walk to a safe adjacent tile with existing collision/path checks. Keep a bounded pursuit/repath interval and attack cadence. If the monster moves, dies, becomes unreachable, or changes maps, release the reservation and choose another. Never teleport onto or through a monster to hide route failure.
4. Give the worker an explicit health/damage/defeat model and visible attack feedback. The host must apply damage once per valid strike; clients render mirrored results. Attacks can affect only monsters and must not use the farmer as the attacker for XP, knockback, kill statistics, or quests.
5. Capture drops attributable to that kill, including cases where vanilla death creates debris, without collecting older player drops. Deposit those drops through `WorkerItemStorage` using the shared destination and overflow behavior. Decide whether monster drops that are normally picked up by a player trigger special rewards before shipping.
6. On a confirmed kill, increment this worker's work count and award Combat XP only to its own persistent XP record. Zero XP for merely hitting, travelling, or another worker's kill unless the player chooses a different rule. Ensure no player XP is awarded.
7. When no eligible monster remains, or after work hours, visibly return through an exit to a reserved home tile. Handle unavailable routes with the existing conservative return policy; never strand or duplicate a worker.

### 3. Mine and Island transitions

1. Build a separate dungeon entry adapter for each selected venue. Resolve the actual loaded location, entrance, ladder/elevator/door semantics, floor ownership, and safe NPC tile before a transition. Use existing authored warps where possible; record why any engine-assisted floor transfer is needed.
2. For the regular Mines, route into the *interior entrance station* chosen by the player, validate occupancy and collision, then enter a floor only according to the answered floor-following rule. Do not spawn at Mountain or assume floor 0 is an ordinary outdoor location.
3. For Skull Cavern, handle bus/desert access and cavern entry; for Ginger Island, handle the chosen island zone and its unlock/access. A worker should not bypass locked content. Distinguish persistent outside maps from generated dungeon floors.
4. For generated floors, reacquire `GameLocation` and monsters after each floor change. Cancel old target references, release reservations, and select a safe arrival tile away from ladders, exits, NPC barriers, other workers, and farmers. Never serialize floor objects or internal generated IDs.
5. Watch farmer warp/floor events if the chosen design follows a farmer. Validate host/client player ownership and remove workers from old floor character lists before placement on the new floor. If the destination cannot safely host a worker, keep it at the station or recall it; do not leave duplicates.
6. Define explicit departure on farmer exit, shift end, manual Idle, defeat, save, and disconnect. Generated floor disposal must not delete the worker roster or leave a stale NPC in a dead location.

### 4. Integration and verification

- Rebase onto the completed XP feature and integrate only through its per-worker API. Verify distinct Combat XP totals for two workers, and unchanged player XP.
- Compile with `dotnet build -p:EnableModDeploy=false` against the installed game assemblies. Verify every uncertain NPC/monster/mine API by a focused prototype and local source inspection before finalizing it.
- Add focused tests for target reservation and release, damage ownership, one kill/one XP grant, drop attribution without old-debris capture, save migration, host-only mutation, floor-transition cleanup, and safe return/defeat rules.
- In a test save, hire two Combat Workers with different destinations. Check Farm combat, each dungeon entrance and floor transition, inaccessible/unlocked areas, a moving monster, a kill and its loot, worker defeat, shift end, manual recall mid-attack, save/reload, day reset, and multiplayer host/client views. Check that the farmer and the second worker retain independent XP, and that no stale worker remains on a prior dungeon floor.

## Shipping gates

The design questions above must be answered before dungeon behavior is coded. A successful build and focused tests are required, followed by the player's in-game test. No DLL should be installed by this planning branch.
