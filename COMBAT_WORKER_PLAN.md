# Combat worker implementation plan

Status: combat implementation is in progress on this branch. The latest build awaits in-game testing; the branch is not merged.

## Player-facing contract

- Add a **Combat Worker** profession at hire time. Its orders are **Slay monsters**, **Explore Area**, and **Idle**. It does not farm, forage, clear debris, physically break rocks, or attack non-monster characters.
- The Jobs screen selects one combat destination: **Farm**, **regular Mines**, **Skull Cavern (desert caves)**, **Ginger Island farm (slimes)**, or **Ginger Island near the Volcano entrance**. Store the selection per worker, independently of other workers. The Island destinations are outdoor areas; the Volcano Dungeon is not included by this decision.
- Regular Mines workers first appear in the **mine entrance cave**, inside the Mines and before descending to level zero, at a checked free tile. Never place them outside on Mountain.
- **Slay monsters** follows the host farmer onto already active dungeon floors. **Explore Area** has its own saved destination (Mines, Skull Cavern, or Volcano), teleports to a checked permanent entrance tile, and simulates exploration and rewards while the worker stays there. It never creates or enters generated floors; Volcano workers remain outside on IslandNorth. Host floor changes do not move exploring workers.
- When a worker's HP is depleted, stop combat and teleport it home immediately; it remains off duty for the rest of that day. Assigning **Idle** starts its return home immediately, without waiting for a fight or floor to finish. Monster loot goes to the same shared chest destination and overflow path as other worker loot; there is no separate blue chest.
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

## Daily return rules

The worker must be home by **midnight (12:00 AM)**. Begin the normal home route early enough to meet that deadline rather than starting it at midnight. HP depletion and an Idle order trigger the returns described above regardless of the clock. Clearing a floor alone does not determine the daily return time.

## Implementation sequence

### 1. Data, UI, and authority

1. Append `CombatWorker` to `WorkerProfession` and `SlayMonsters` to `WorkerTaskKind` without changing numeric values of existing entries.
2. Add a combat area catalog with stable save IDs and user-facing labels. Keep user-facing selection separate from concrete `GameLocation.NameOrUniqueName` and dynamic floor identity. Validate unlock/availability at assignment and again before departure. Unavailable areas should show a reason and leave the worker safely idle at home.
3. Add `CombatArea` to `WorkerRosterEntry`, clone/snapshot/network mirror, save normalization, and host-only setter. Unknown/missing saved areas fall back to a safe valid value; preserve legacy rosters and existing wages.
4. Extend hire dropdown, Jobs selector, task descriptions, roster status, dialogue, and `workers assign`/area commands. Changing area cancels any target/attack/path and recalls the worker safely. Only the host accepts state changes.
5. Add focused pure tests for enum stability, profession-task restrictions, area normalization, save round trips, and two workers with distinct selections.

### 2. Combat runtime on Farm

1. Introduce a `WorkerCombatManager` called only by the host from the existing update loop. State is keyed by worker ID: `AtHome`, `Travelling`, `Searching`, `Approaching`, `Attacking`, `Recovering`, and `Returning`. Clear it on reassignment, dismissal, save lifecycle, day reset, and title return.
2. Search only monsters in the worker's current authorized area. Exclude invulnerable, dead, or non-attackable entities, and reserve a target so two workers do not pile into the same pursuit unless the design permits shared fights. Retest existence, location, health, and attackability at execution time.
3. Walk to a safe adjacent tile with existing collision/path checks. Keep a bounded pursuit/repath interval and attack cadence. If the monster moves, dies, becomes unreachable, or changes maps, release the reservation and choose another. Never teleport onto or through a monster to hide route failure.
4. Give the worker an explicit health/damage/defeat model and visible attack feedback. The host must apply damage once per valid strike; clients render mirrored results. Attacks can affect only monsters and must not use the farmer as the attacker for XP, knockback, kill statistics, or quests.
5. Capture drops attributable to that kill, including cases where vanilla death creates debris, without collecting older player drops. Deposit those drops through `WorkerItemStorage` using the existing shared chest destination and its overflow behavior. Do not introduce a separate chest.
6. On a confirmed kill, increment this worker's work count and award Combat XP only to its own persistent XP record. Zero XP for merely hitting, travelling, or another worker's kill unless the player chooses a different rule. Ensure no player XP is awarded.
7. On Farm, when no eligible monster remains or the worker must depart to be home by midnight, visibly return through an exit to a reserved home tile. An Idle order interrupts combat and starts the normal return path immediately. If HP reaches zero in any area, cancel attack/path state and teleport to a checked safe home tile immediately, then latch off duty until the next day. Handle unavailable normal routes with the existing conservative return policy; never strand or duplicate a worker.

### 3. Mine and Island transitions

1. Build a separate dungeon entry adapter for each selected venue. Resolve the actual loaded location, entrance, ladder/elevator/door semantics, floor ownership, and safe NPC tile before a transition. Use existing authored warps where possible; record why any engine-assisted floor transfer is needed.
2. For the regular Mines, route into the mine entrance cave, validate occupancy and collision, then follow the host farmer onto an already active floor. Do not spawn at Mountain or assume floor 0 is an ordinary outdoor location.
3. For Skull Cavern, handle bus/desert access and cavern entry. For Ginger Island, route to either the Island farm where slimes appear or the outdoor area near the Volcano entrance, according to the worker's saved selection. Validate each area's unlock/access and live monsters. Do not enter the Volcano Dungeon under the current destination choices. Distinguish persistent outdoor maps from generated dungeon floors.
4. For generated floors, reacquire `GameLocation` and monsters after each floor change. Cancel old target references, release reservations, and select a safe arrival tile away from ladders, exits, NPC barriers, other workers, and farmers. Never serialize floor objects or internal generated IDs.
5. Watch the host farmer's warp/floor events and move the worker to a checked safe tile on that farmer's active floor. Remove the worker from the old floor character list. If the destination cannot safely host a worker, keep it at the entrance station or recall it; do not leave duplicates.
6. End dungeon work and start the home route early enough for the worker to be home by midnight (12:00 AM), accounting for the longer dungeon exit. Manual Idle starts the home route immediately; depleted HP triggers an immediate safe teleport home and an off-duty latch for the day. A floor clear or followed farmer's exit must release stale targets and choose a safe next state without silently treating either as the daily deadline. Save and disconnect cleanup must not delete the worker roster or leave a stale NPC in a dead generated location.

### 4. Integration and verification

- Rebase onto the completed XP feature and integrate only through its per-worker API. Verify distinct Combat XP totals for two workers, and unchanged player XP.
- Compile with `dotnet build -p:EnableModDeploy=false` against the installed game assemblies. Verify every uncertain NPC/monster/mine API by a focused prototype and local source inspection before finalizing it.
- Add focused tests for target reservation and release, damage ownership, one kill/one XP grant, drop attribution without old-debris capture, save migration, host-only mutation, floor-transition cleanup, and safe return/defeat rules.
- In a test save, hire two Combat Workers with different destinations. Check Farm combat; the regular mine entrance cave; following the host through Mines and Skull Cavern; both outdoor Island destinations; inaccessible/unlocked areas; a moving monster; a kill and shared-destination loot; zero-HP immediate home teleport and off-duty latch; home arrival by midnight (12:00 AM); immediate Idle recall mid-attack; save/reload; day reset; and multiplayer host/client views. Check that the farmer and the second worker retain independent XP, and that no stale worker remains on a prior dungeon floor.

## Shipping gates

A successful build and focused tests are required before a test DLL is installed. The player's in-game test is required before merging the branch.

## Live playtest notes and deferred fixes

- On one tested mine level, the combat worker found and killed every monster without reproducing the earlier unsafe-map-tile route warning. This is a successful retest for that level only; other mine layouts and moving targets still need coverage.
- **Duggy emergence implemented in af65e8e; playtest pending:** a nearby worker on valid ground can trigger the vanilla emergence cycle. Workers still ignore invisible/emerging frames and revalidate exposure at attack impact. Player combat, quests, and XP remain separate.
- **Armored Bug targeting guarded; armor-removal design still open:** the installed game exposes `Bug.isArmoredBug`, and its normal damage path blocks attacks while that flag is set. Workers now refuse armored bugs as combat targets, including when one hits them. Such bugs can still damage workers. The game code inspected did not show a normal armor-removal transition; decide how a player can remove armor before allowing worker attacks, then verify in game.
- **1.2.7 ready for manual testing:** combat-only directional body/sword swings apply one checked hit at the impact pose; generated-floor following searches the actual map for a safe connected walking area beyond the farmer's immediate surroundings; defeat produces one host HUD notice matching the Info log, with separate trace details for location and attacker. TESTING.md checks 51–53 cover these changes.
- **1.2.8 sword-pose correction:** combat sheet generation now copies the game's sword animation metadata, including the sword arm overlay in all directions, and removes its player callbacks. Body and blade use the same native slash timing. Check 54 covers the reported tool-like body motion.
- **1.2.9 swing-direction correction:** `Character.faceDirection` skips `SimpleNonVillagerNPC` workers. Combat now captures the target-relative direction explicitly from standing pixel centers, including above/below enemies sharing a tile, and retains it for body/blade poses and client replay through the whole swing. Trace logs report the chosen direction once per swing. Check 55 covers the upward-swing regression. Build with deployment disabled and install only after SMAPI exits: live DLL overwrites were observed to corrupt the mapped assembly in the 1.2.8 session.
- **1.3.0 simulated exploration:** separate Combat Worker order and destination; one batch per 60 active in-game minutes between 6:00 and 22:00 (maximum 16/day); 5 Mining plus 5 Combat XP to that worker per batch. No simulated HP loss; defeated workers cannot explore that day. Mines require the day-five opening; Skull Cavern requires the repaired bus and Skull Key; Volcano requires the repaired boat and first island parrot/north path. Debug arrival grants neither rewards nor access. Player XP, real kills, quests, walnuts, and dungeon-floor progress remain separate.
- **Exploration rewards:** each batch gives 2–4 stone and a 30% chance for one coal. Mines give 1–2 copper/iron/gold ore from unlocked mine bands (iron at 40+, gold at 80+), 40% for one matching bug meat/slime, bat wing, or void essence, and 10% for the band's geode. Skull Cavern gives 1–2 gold ore, 10% for one iridium ore, 40% for one solar/void essence, and 10% for one omni geode. Volcano gives 1–2 cinder shards, 25% for one gold ore, 40% for one solar essence, and 10% for one magma geode. No special chest, equipment, unique, or progression rewards. Initial rates are tunable in WorkerExplorationPolicy.
- **Exploration lifecycle:** partial progress, daily run count, and pending loot are saved/mirrored. Loot/XP is committed before shared storage delivery; retries only deliver queued items. Idle/reassignment/area changes discard unfinished progress and begin normal return. New days reset timing/counts, preserving queued rewards. Unpaid time and menus/events do not advance simulation. Return starts at 22:00 with the existing midnight deadline. Dismissal delivers queued loot first or refuses to discard it. TESTING.md checks 56–61 cover exploration.
