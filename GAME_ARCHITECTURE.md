# RimWorld behavior architecture — ground-truth study

Source: decompiled Assembly-CSharp (1.6.4871) at `Projects/rimworld-decompiled/` (9,217 files) + `Data/*/Defs/`.
Every claim below was read from that source, not recalled. Cited paths are decompiled-tree-relative.

## 1. The individual mind loop

Cadence (`Verse/AI/Pawn_JobTracker.cs`):
- **Every 30 ticks** the **constant think tree** runs (`DetermineNextConstantThinkTreeJob`). If it yields a job, it starts with `JobCondition.InterruptForced` — this is the game's interrupt channel (flee explosion, hostility response, lord constant duties). Nothing else polls faster.
- The **main think tree** runs only when the current job ends, expires, or something calls `CheckForJobOverride`. Jobs are not stomped; they expire (`expiryInterval`, optional `checkOverrideOnExpire` → re-evaluate instead of end).
- `TryTakeOrderedJob` = the player channel (`playerForced`). Drafted orders live in the tree itself (see §2) — above duties.
- Built-in runaway guard: 10 jobs started in 10 ticks → error-recover job. (Re-issue loops are detected by the engine; a stomping MapComponent fights this.)

Stack: **ThinkTreeDef (XML) → ThinkNodes → JobGiver → Job → JobDriver → Toils.**
- ThinkNodes are priority-ordered fall-through; first job wins.
- A `JobGiver` returns a Job; a `JobDriver` executes it as a sequence of Toils (each with tick logic, end conditions, fail conditions).
- `ThinkResult` carries the source node; `ShouldStartJobFromThinkTree` dedupes continuations.

## 2. The humanlike think tree (priority order that matters to us)

`Data/Core/Defs/ThinkTreeDefs/Humanlike.xml`, top to bottom:
1. Lying-down machinery, Downed, Burning, MentalStateCritical
2. `JobGiver_ReactToCloseMeleeThreat` (everyone, always)
3. MentalStateNonCritical, roped
4. **`Humanlike_PostMentalState` insertion hook** (modder seam, `ThinkNode_SubtreesByTag`)
5. Queued jobs
6. **Drafted: `JobGiver_MoveToStandable` + `JobGiver_Orders`** — the player's hand
7. **Lord duties, HighPriority hook** (`ThinkNode_JoinVoluntarilyJoinableLord` → `LordDuty` subtree)
8. **`Humanlike_PostDuty` insertion hook**
9. Prisoner branch
10. Colonist: allowed area, safety, **emergency work**, starving-food, **Lord duties MediumPriority hook**, pickup dropped weapon, apparel...
11. Trait behaviors, **`Humanlike_PreMain` hook**
12. **MainColonistBehaviorCore** (work + needs — normal colonist life)
13. **`Humanlike_PostMain` hook**, idle joy, wander

Constant tree (`HumanlikeConstant`): flee explosion → find oxygen → hostility response → **`LordDutyConstant`** (lord's constant duty node — the lord's interrupt channel).

Consequences:
- Drafting and queued orders ALWAYS beat duties. Player's hand wins natively — no code needed.
- A HighPriority duty beats normal work but not drafting. A MediumPriority duty beats normal work but not emergency work.
- A pawn in a Lord **with no duty assigned** falls through and lives a completely normal colonist life. Standing squads cost nothing until activated.
- The four `insertTag` hooks are the sanctioned way to add whole behavior branches without Harmony.

## 2a. The animal think and hearing lanes

`Data/Core/Defs/ThinkTreeDefs/Animal.xml` gives animals a separate main tree and
`AnimalConstant`. The main tree exposes `Animal_PreMain` after Lord duty and
`Animal_PreWander` before idle wandering. The constant tree runs on the same
30-tick interrupt cadence but exposes no insertion tag; its priority is despawned
handling → `JobGiver_AnimalFlee` → auto-caravan join → constant Lord duty. A
precise XML insertion beside `JobGiver_AnimalFlee` is therefore the native-shaped
seam for an immediate animal response when no dedicated Def hook exists.

Vanilla danger response is deliberately narrow (`RimWorld/FleeUtility.cs`,
`Verse/AI/Pawn_MindState.cs`): `ShouldAnimalFleeDanger` excludes a player-faction
animal on a player home map, a following animal, a Lord member, a fighter, and a
job marked never-flee. Projectile-impact panic separately rolls 40%, requires no
master, and then calls that same predicate. This explains why a colony dog can
remain inert during gunfire without any failed job.

`Verse/GenClamor.cs` already supplies the physical acoustic seam: it walks regions,
respects closed-door boundaries, scales reach by the hearer's Hearing capacity,
and invokes its callback for every pawn reached. A muzzle-report fact can therefore
be retained by an animal without importing human speech, target identity, or map
omniscience.

Targeting and combatant labels require a second distinction. Vanilla shooting
subtracts score for a noncombatant but may still select the best remaining target.
`Pawn_MindState` also refreshes another pawn's `lastCombatantTick` merely because
that pawn was selected as `enemyTarget`; `Pawn.IsCombatant()` is therefore circular
for deciding whether a passive animal deserved selection. Actual animal threat
relevance must instead read its own fighting job, aggro state, assigned attack
target, enemy target, or short `lastEngageTargetTick` window.

Containment, culture, and sound use different native seams.
`AnimalPenUtility.NeedsToBeManagedByRope` is true only for a spawned rope-managed
roamer on a player home map; it is the pen-management boundary, not a species-name
rule. `Ideo.IsVeneratedAnimal` reads the ideology's actual `VeneratedAnimals` set.
`FactionIdeosTracker.PrimaryIdeo` and each pawn's `Ideo` must also remain
distinct: a faction can contain primary and minority adherents with different
venerated species. The live `AnimalVenerated` def makes adherents unwilling to
hunt or slaughter the species and defines death, meat-consumption, and living-
presence thoughts; `AnimalPenUtility` contains no veneration branch. Veneration
therefore materially constrains harmful and food-use postures without forbidding
a native pen or requiring a sleeping location. A bond, assigned/effective master,
follow settings, training, assigned animal bed, pen need, and veneration cannot be
collapsed into one animal posture.

`Pawn_CallTracker.DoCall` invokes the life-stage call or angry-call sound and does
not call `GenClamor`. Native animal vocalization is audible presentation, not an
engine report that another pawn heard a semantic warning. A future sentinel animal
requires its own grounded perception and communication path before CA may create
threat knowledge or wake nearby residents.

## 2b. Starting cargo and operational equipment

`RimWorld/ScenPart_PlayerPawnsArriveMethod.cs` sends every starting pawn-and-cargo
group through `DropPodUtility.DropThingGroupsNear(... forbid: true ...)`, including
Standing starts; `instaDrop` changes delivery, not the forbid flag.
`RimWorld/DropPodUtility.cs` applies `SetForbidden(true)` before placement. The
starting red X is therefore scenario-arrival bookkeeping, not evidence of a
player policy decision.

The runtime keeps only one forbidden bit (`RimWorld/CompForbiddable.cs`) and no
provenance for who set it. A broad `IsForbidden` exemption would consequently
erase player policy, allowed-area restrictions, Lord restrictions, and other
native constraints. CA changes the identifiable starting-cargo `forbid` argument
before the player can have expressed an item preference, then adds its own scribed
provenance for the native item toggle and Forbid/Allow designators. Stack splits
and merges carry that explicit denial forward. An old save has no recoverable
history, so its current red-X items are seeded as protected policy. Only an
unprotected system forbid on a visible, spatially relevant ordinary item is
eligible for stance-driven Allow; `IsForbidden(pawn)` still governs areas, Lords,
and every downstream job.

Vanilla has no general colonist giver that finds a suitable ground weapon.
`Verse/AI/JobGiver_PickupDroppedWeapon.cs` recovers only the pawn's own remembered
dropped weapon. `RimWorld/JobGiver_OptimizeApparel.cs` checks only every 6,000–9,000
ticks and requires allowed apparel in storage. Manual Equip and Wear clear the
forbid and issue native jobs; `JobDriver_Equip` can optionally ignore forbids,
whereas `JobDriver_Wear` always honors them. CA therefore inserts one bounded
operational-equipment giver immediately before its combat-reaction giver on
`HumanlikeConstant`: known danger may yield one normal Equip or Wear job from
reachable, reservable, player-allowed gear. While that owned job remains valid,
the giver returns the same job so the next 30-tick pass cannot fall through and
preempt it. A fresh external-violence impact is the deliberate exception: the
equipment giver yields so the following reaction giver can answer the native
damage-driven override. Permission maintenance never starts a pawn job.

## 2c. Immediate individual combat

Damage and injury are ordered facts, not one event. An apparel comp may fully
absorb damage inside `Verse/Pawn_HealthTracker.PreApplyDamage`; RimWorld still
calls `Pawn_JobTracker.Notify_DamageTaken` when the `DamageInfo` requests a job
override. For applied injury, `Pawn_HealthTracker.PostApplyDamage` calls the same
job notification after the hediff exists. `Verse/Pawn.PostApplyDamage` updates
`Pawn_MindState.lastHarmTick` only afterward, and CA's damage-contact seam rides
that later mind-state call. A reaction that must see the violent impact during the
same native override therefore records only the transient impact fact at
`Pawn_JobTracker.Notify_DamageTaken`; live pain, bleeding, and health already
describe any injury that actually landed.

Vanilla firing behavior does not continuously compare positions.
`RimWorld/JobGiver_AIFightEnemy` accepts the current cell when it can shoot with
more than one percent directional cover, or when the target is within five cells;
`JobDriver_Wait` and `JobDriver_AttackStatic` then attack from that cell without
requesting a new cast position. `Verse/AI/CastPositionFinder` evaluates cover,
range, reachability, reservation, known dangerous edifices, and travel, but not a
friendly firing corridor or the target's cover from a candidate angle.
`AttackTargetFinder.FriendlyFireConeTargetScoreOffset` is only a target-selection
penalty. Ordinary projectile casts allow non-target pawn hits, and
`VerbUtility.InterceptChanceFactorFromDistance` assigns exactly zero physical
interception chance within five cells of the muzzle.

CA's immediate humanlike correction remains actor-local. Against a currently
visible contact, eligible colonists and exact generic settlement assaulters score
the current cell and locally visible reachable alternatives from the acting
pawn's verb, Shooting skill, Disposition, directional cover, target exposure,
useful range, travel, and visible non-hostiles in the prospective `ShootLine`.
A violent impact separately derives survival pressure from current injury,
bleeding, cover, ability to engage, skill, and Disposition before recognition
latency. One finite `CA_CombatMove`, native attack, or native flee job carries the
decision; its continuation contract preserves the same destination and permits a
newer impact to redirect it without a polling re-issue loop. This is individual
position judgment, not leader allocation, fire-team maneuver, or shared map truth.

## 2d. Contact resolution and incident-bounded casualty triage

Vanilla stops treating a downed pawn as an active threat, but a CA contact formerly
carried only identity, cell, and age. A fresh contact therefore survived for 7,500
ticks after its subject fell. `CA_CombatPosture` was a never-ending job whose exit
condition read that same age-only fact, so the Man in Black could stand for several
in-game hours “assessing” the raider he had just downed. Contact memory and contact
actionability must be separate. CA now retains an explicit per-knower observed state;
only `Active` enters combat queries, while direct LOS to the known pawn or corpse can
resolve the remembered fact as downed, dead, captured, nonhostile, nonthreatening,
or destroyed. Resolved state is not yet relayed.

The native Man in Black event supplies fiction without supplying behavior.
`RimWorld/StorytellerComp_Triggered.Notify_PawnEvent` queues
`StrangerInBlackJoin` only after no other undowned player humanlike remains.
`RimWorld/IncidentWorker_WandererJoin` then creates and edge-spawns a generic
player-faction pawn; it gives him no rescue Lord, casualty target, or mission AI.
The Core `StrangerInBlack` PawnKind requires Violent and Caring work capability
and gives him five industrial medicine, but Doctor work is not guaranteed active.

Native medical fallthrough cannot express the requested comparison.
`HealthUtility.TicksUntilDeathDueToBloodLoss` supplies the death countdown shown
by the health UI. `JobDriver_TendPatient` can tend another pawn on the ground, but
its player self-path fails while `playerSettings.selfTend` is off. Native Rescue
requires an available bed, and its fixed work priorities do not compare the
rescuer's countdown with the beneficiaries' countdowns.

CA patches only the successful `StrangerInBlackJoin` `SpawnJoiner` call and saves
the alive downed player colonists present at that arrival plus their arrival cells
as that rescuer's finite beneficiary set. The event grants identity, location, and
gross downed state; it does not grant a live health feed. On `HumanlikeConstant`,
operational equipment retains its existing priority, mission triage yields to
perceived active danger, and combat reaction owns that danger before vanilla
hostility response.

`CA_AssessCasualty` goes to the saved cell without following a moved pawn reference,
performs a short local examination, and only then reads the patient's current
bleed-out countdown. Medical skill supplies most diagnostic precision and
Intellectual contributes; low skill collapses distinct countdowns into broad
uncertainty, while higher skill stores progressively narrower deterministic
estimates. The assessment job and progress bar make the act visible, and
`MoteMaker.ThrowText` exposes every completed diagnosis over the patient; a neutral
message and its native sound mark the first or a changed diagnosis without replaying
the same notice after every treatment pass.
Later triage ranks only the saved estimate, observed downed/tend state, and the
rescuer's equivalently skill-bounded self estimate. Developer census ground truth
is labeled separately and never enters the decision.

Triage uses inventory medicine, stabilizes the perceived urgent patient through
native tending, and uses native Rescue when an assessed stable downed beneficiary
has a bed. A finite custom self-tend driver uses native tend speed and
`Toils_Tend.FinalizeTend` without changing global self-tend policy. A newly
perceived danger interrupts assessment or medical work on the rescuer or a saved
mission beneficiary;
direct player work, drafting, patient forbids, medical care, reservations,
reachability, capacity, and aggro state retain their native authority.

## 2e. Pawn-private welfare and conditional accountability

Vanilla rescue begins from map-global enumeration. `WorkGiver_RescueDowned`
returns `map.mapPawns.SpawnedDownedPawns` from `PotentialWorkThingsGlobal`, and its
`ShouldSkip` scans faction-wide rescue eligibility. The former CA field-medicine
branch likewise enumerated every free colonist and ranked the exact remote
`BleedRateTotal`. Both are valid engine state queries, but neither is an honest
actor knowledge seam.

CA now acquires gross welfare through awake humanlike sight, preserves the observed
cell and source tick through one communication edge per relay pass, and requires a
close local clinical observation before reading
`HealthUtility.TicksUntilDeathDueToBloodLoss`. Medicine and Intellectual quantize
that reading into a saved estimate. Automatic rescue prefixes accept only a
current firsthand welfare fact while Life safety or Field medicine is enabled;
the forced parameter preserves the player's direct float-menu order, and disabling
both features returns the native methods untouched.

`CA_CheckWelfare` travels to copied target A; it never targets or follows the
subject pawn. Its giver-to-driver handoff captures origin, subject ID, source/state
ticks, monotonic revision, and cell. For a welfare fact, source tick remains scribed
freshness/provenance while origin, state tick, revision, and copied cell form the
semantic match token. An exact non-stale Unassessed/Visual heartbeat therefore does
not interrupt the finite check; stale reacquisition or a material fact/provenance
change does. Accountability additionally matches its exact last-confirmed tick.
The driver rejects ordinary same-Def continuation and revalidates its token before
local observation. Welfare failure may mark only that exact fact Missing;
accountability failure may report only that exact missed check-in. A successful
local observation may create new clinical knowledge.

Accountability begins only from stored squad/fire-team responsibility plus an
initially shared `LordJob_CATactical` or `LordJob_CAStackBreach`. The responsible
pawn receives confirmation through direct sight or an existing strategic
communication channel. No timetable, allowed area, remote job, health, downed, or
death query becomes a fact. The first implementation advances these records inside
`KnowledgeMapComponent`; separating Authority-owned policy/storage from Knowledge
observation and Doctrine execution is recorded structural debt.

## 2f. Persistent dynamic maneuver jobs

`Pawn_JobTracker.StartJob` synchronously cleans up the current driver before it
installs the next one. A maneuver that expresses each movement and firing phase as
a new `Goto` or `AttackStatic` therefore makes job replacement itself the state
machine. The native finite alternative is one custom `JobDriver` with a `Never`
toil: `Pawn_PathFollower.StartPath(LocalTargetInfo, PathEndMode)` changes its
physical destination without changing the job, and `Pawn.TryStartAttack` begins a
verb cast without creating an attack job. `Notify_PatherArrived` is the explicit
arrival seam because a `Never` toil does not advance automatically on arrival.

Dynamic destination reservations belong to the same persistent job. Calling
`PawnDestinationReservationManager.Reserve` obsoletes that pawn's prior claim;
`Pawn_JobTracker.CleanupCurrentJob` later clears reservations by the ending job's
identity. A finish action must not broadly release the pawn's destinations because
`TryTakeOrderedJob` pre-reserves an incoming player job before interrupting the
current one. The persistent driver's finish action may stop its own pather and
clear its own external plan token, but it must not end another pawn's job or start
a successor from cleanup.

`CastPositionFinder` is the native firing-halt selector, but its 1.6 locus bound
cannot safely express a local withdrawal bound. When `maxRangeFromLocus` is set,
the finder clips its enumeration rectangle around `targetLoc` even though
`EvaluateCell` later measures the candidate against `req.locus`. A distant target
can therefore eliminate every cell around the intended locus. CA leaves that field
unset, bounds enumeration by `maxRangeFromCaster`, and uses the request validator
to require a cell near the nominal halt that strictly advances toward the final
destination. The finder then supplies pawn-specific walkability, allowed-area,
`Danger.Some` reachability, destination-reservation, known-danger, firing-line,
range, avoid-grid, and cover scoring. Its target is a currently visible pawn; a
remembered-only contact cannot become a live firing target. If no firing cell is
valid, the same spatial and safety constraints govern the geometry fallback; no
local cell means hold and retry rather than an unbounded run.

Two native override paths need separate treatment. `HumanlikeConstant` evaluates
`JobGiver_ConfigurableHostilityResponse` every 30 ticks and can replace a
non-player-forced maneuver; a narrow giver prefix may yield while an explicit
Withdrawal plan owns the pawn, leaving the earlier explosion and oxygen jobs
untouched. Separately, `Pawn_JobTracker.Notify_DamageTaken` calls
`CheckForJobOverride` according to `JobDef.checkOverrideOnDamage`; a maneuver that
already owns the response to pursuit uses `Never` so ordinary main-tree work does
not replace it after impact. Player interruptibility remains enabled, and a queued
forced job is an explicit driver end condition.

## 3. The group mind: Lord system (`Verse/AI/Group/`)

RimWorld's native group-AI. Raids, sieges, caravans, rituals, parties all run on it.

- **Lord** — owns `ownedPawns`, a `StateGraph`, the current `LordToil`. Fully scribed (state machine survives save/load). Receives string **memos** (`ReceiveMemo`) and notifications (pawn lost, damaged, target acquired...).
- **LordJob** — builds the graph (`CreateGraph()`). Subgraphs attachable (`AttachSubgraph` — raid attaches kidnap/steal subgraphs). Key virtuals for player-faction use: `AllowsDrafting` (default **true**), `AllowsFloatMenu` (true), `AddFleeToil` (true — override to control auto-flee), `GetPawnGizmos(p)` (put buttons on member pawns), `GetReport(p)` (the pawn's inspect-pane status string — replaces "watching for targets" with real posture names), `Notify_PawnLost/Damaged/JobDone...`
- **LordToil** — one state. `UpdateAllDuties()` assigns each member a `PawnDuty`; `LordToilTick()` for group logic; notifications for event-driven awareness. `AssignsDuties`, `AllowSatisfyLongNeeds`, `AllowRestingInBed`, `CustomWakeThreshold` shape member life while in-state.
- **Transition** — sources → target, fires on **Triggers** (`Trigger_Memo`, `Trigger_TicksPassed`, `Trigger_PawnHarmed`, `Trigger_PawnsLost`, `Trigger_FractionPawnsLost`, `Trigger_TicksPassedWithoutHarm`, `Trigger_Custom(func)`, ~40 more) with **TransitionActions** (`TransitionAction_EndAllJobs`, `_WakeAll`, `_Message`, `_Custom`...).
- **Canonical duty assignment** (`RimWorld/LordToil_AssaultColony.cs`):
  ```csharp
  if (pawn.mindState.duty?.def != DutyDefOf.X) {
      pawn.mindState.duty = new PawnDuty(DutyDefOf.X, focus, radius);
      pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);  // interrupt ONCE
  }
  ```
  Assign duty, interrupt once; the duty's think subtree produces jobs from then on. No re-issue loop.

## 4. Duties: how the group reaches into the individual

- **DutyDef** (XML or code): `thinkNode` = a full think subtree (the behavior program), optional `constantThinkNode` (interrupt-class behavior), `hook` (HighPriority/MediumPriority...), `alwaysShowWeapon`.
- **PawnDuty** (per pawn): def + `focus`/`focusSecond`/`focusThird` + `radius` + `locomotion` + **`overrideFacing`** + `tag`. Facing is first-class.
- `ThinkNode_Duty` bakes EVERY DutyDef's subtree into the LordDuty tree at resolve time and dispatches on `duty.def.index` — **a mod's custom DutyDef auto-registers**. `lord.Notify_DutyResult` lets the LordJob veto/modify any duty-issued job.
- The vanilla `Defend` duty is the model of a living posture (`Data/Core/Defs/DutyDefs/Duties_Misc.xml`): combat drug if in danger → `JobGiver_AIDefendPoint` (acquire 65 / keep 72) → needs+work within flag radius 16 → wander near post (radius 8, sprint back if outside). Not a frozen wait — a leashed life.
- Vanilla duty vocabulary (~50): `Defend`, `Follow`, `Escort`, `AssaultColony`, `Sapper`, `Breaching`, `ManClosestTurret`, `DefendBase`, `ArriveToCell`, `TravelOrWait`, `HuntEnemiesIndividual`, `Steal`, `Kidnap`, caravan/ritual/gathering families...
- LordToil library (~90): `LordToil_Stage` (form up at point before assault), `LordToil_DefendPoint`, `LordToil_Siege` (positions + data), `LordToil_EscortPawn`, `LordToil_PanicFlee`, `LordToil_ExitMap`, ritual position generators...

## 5. Mod seams, ranked (least → most invasive)

1. **XML only**: new DutyDefs (auto-register), new JobDefs, think-subtree insertion via the four `Humanlike_*` insertTags.
2. **Code, no Harmony**: custom `LordJob`/`LordToil`/`Trigger`/`JobGiver`/`JobDriver` subclasses; spawn with `LordMaker.MakeNewLord(faction, lordJob, map, pawns)`. FloatMenuOptionProvider subclasses (auto-discovered). MapComponents/GameComponents.
3. **Harmony**: only where the game offers no seam (stat hooks, render details, method-boundary reroutes).

## 6. Honest map: what we built vs. the right substrate

| Ours today | Built on | Correct substrate |
|---|---|---|
| Holds / overwatch sectors | `Wait_Combat` pin + 45-tick re-issue MapComponent | Custom `CA_HoldPosition` DutyDef (JobGiver_AIDefendPoint-style + facing + leash) driven by a squad Lord — or standalone duty-shaped JobDriver for solo orders |
| Door stack + breach drills | Per-pawn Goto + Wait_Combat + phase MapComponent | `LordJob_BreachAssault`: toils Form→Set→Breach→Clear→Reconvene, transitions on memos/ticks/quiet; stack slots as per-pawn duty focus+facing |
| Painted lines / formations | Slot Goto + Wait_Combat holds | LordToil assigning positional duties (siege/ritual position-generation pattern) |
| Ambush commit | March-back + Wait_Combat pin loop | `CA_Ambush` DutyDef: hold-concealment subtree + constantThinkNode spring check; ordered = duty assigned by order (Lord optional) |
| Squad "communication" | Ad-hoc registries | Lord memos + notifications (native event bus) |
| Persistence bugs (session-only holds) | Scribe workarounds | Lords scribe natively; duties scribe on mindState |
| "Watching for targets" report string | Wait_Combat's label | `LordJob.GetJobReport`/`GetReport` per posture ("Holding sector NE", "Stacked #2, covering left") |

The operator's critique (2026-07-22, ratified direction): Wait_Combat is a vanilla idle-guard job abused as a universal pin; it can never carry intelligent behavior. The Lord/duty system is the game's own medium for exactly this. Port order: door stack first (cheapest test that the substrate delivers), then holds, then formations.

## 7. Layer map of the rest of the game (orientation)

- **Defs** = data atoms (`Data/*/Defs/*`), loaded into `DefDatabase<T>`; XML inheritance via `ParentName`; `MayRequire` DLC-gates.
- **Maps** own components (`MapComponent`), grids (glow, pathing, avoid), `lordManager`, `pawnDestinationReservationManager`.
- **Pawns**: trackers (`jobs`, `mindState`, `equipment`, `apparel`, `health`, `stances`, `pather`, `drafter`...). `mindState.duty` is the group-AI handle.
- **Rendering**: `PawnRenderTree` nodes (studied in arms work); world-space drawing via `MapComponentUpdate`.
- **Stats**: `StatDef` + `StatPart`/`StatWorker`; hediffs modify via `statOffsets`/`statFactors` (our CA_MovingFire/CA_FirstStrike pattern is correct).
- **Storyteller**: `IncidentDef` + `IncidentWorker` + `StorytellerComp` — the raid pipeline that ends in `LordMaker` calls.
- **Signals/quests**: string signal bus (`Find.SignalManager`, `lord.inSignalLeave`) — the global event channel; lords also take signals.

## 7a. Native stockpile authority

Verified against the decompiled RimWorld 1.6 source and the live storage tab on
2026-07-28:

- `Zone_Stockpile` owns its `StorageSettings`, `SlotGroup`, cells, inspect tab, and
  acceptance check. `ITab_Storage` exposes native `StoragePriority` plus the full
  `ThingFilterUI` tree. Priority orders valid haul destinations; the filter defines
  accepted things. They are distinct from any CA room-purpose or initiative value.
- `Zone_Stockpile.GetGizmos` yields its own commands, the base zone gizmos, and the
  native storage-settings clipboard commands. The per-zone CA initiative control
  composes at this selected-zone seam without replacing the storage tab or
  inventing another storage object.
- The CA value is an initiative ceiling for CA-originated work in that existing
  zone. It does not alter cells, filters, priority, labels, or held items. The UI
  control ships with its first operative shelf/capacity consumer, not as a
  button-to-nowhere.
- `StoreUtility.CurrentStoragePriorityOf` and
  `TryFindBestBetterStoreCellFor` expose the current native destination and an
  accepted higher-priority alternative. They do not compare medical criticality,
  treatment access, hostile approach, or authored defensive topology. Absence of
  a better-priority cell is therefore not proof that the present location is
  ideal. `RoofGrid.RoofAt` and `RoofDef.isNatural` independently distinguish no
  roof, constructed roof, and natural mountain protection.

## 8. World persistence and map materialization

Verified against the decompiled RimWorld 1.6 source and loaded Defs on
2026-07-28:

- `World.ExposeData` deep-scribes factions, ideologies, world pawns, world objects,
  game conditions, story state, world components, and pocket maps. `WorldTick`
  advances world pawns, factions, world objects, and world components. A saved,
  self-throttling `WorldComponent` is therefore the native-shaped owner for
  regional CA state.
- `WorldObject` scribes its definition, stable ID, tile, creation tick, destroyed
  state, faction, quest tags, and comps. `WorldObjectComp` instances can scribe and
  tick. These are persistent loci and visible actor projections, not a reason to
  create one ticking object for every resident, claim cell, or resource packet.
- `MapParent.Map` resolves one loaded map through the current game, and
  `MapGenerator.GenerateMap` rejects a parent that already has a map.
  `GetOrGenerateMapUtility` reuses the map at a tile or creates a map parent and
  generates lazily. A standard regional transition remains world state -> map
  projection -> reconciled world state.
- `Settlement.ShouldRemoveMapNow` removes an encounter map once no protected pawn,
  blocking building, player home, or incoming transporter requires it. The
  settlement world object remains. `Game.DeinitAndRemoveMap` calls
  `MapParent.Notify_MyMapAboutToBeRemoved` before deinitialization, which is the
  final native lifecycle seam for folding a still-intact map into its persistent
  settlement record.
- `Caravan` is a saved world object that owns actual pawns, inventory, pathing,
  needs, forage, carry, bed, trader, and story trackers. Its path follower scribes
  destination, next tile, movement cost, pause state, and arrival action. Native
  caravans suit player-visible or otherwise consequential parties; inactive
  patrols, traders, migrants, and armies may remain scheduled regional records
  until contact warrants materialization.
- `Site`, settlement attack, caravan incidents, and quest-spawned world objects all
  demonstrate lazy encounter-map generation. Loaded Core Defs bind `Settlement`
  to `Base_Faction`, sites to site components, and encounters to smaller generators.

## 9. Native settlement, diplomacy, authority, and trade limits

- `Settlement` persists faction ownership, trader state, a name, and references to
  previously generated inhabitants. It has no durable population, culture,
  government, economy, territory, institutional-knowledge, or settlement-relation
  model. Base generation projects faction and technology into a physical encounter;
  it cannot own regional continuity.
- `FactionRelation` stores only the other faction, scalar base goodwill, and
  Hostile/Neutral/Ally kind. `Faction` mirrors changes to both sides and propagates
  hostility into prisoners, sites, trade requests, passing ships, attack-target
  caches, and active Lords. A settlement feud cannot safely be represented by
  flipping native faction hostility; CA must project into that seam only after a
  faction-wide posture is justified.
- Harm, capture, death, and settlement assault normally collapse directly into
  faction goodwill. CA instead treats them as incident inputs with actor, target,
  witness, attribution, report, grievance, and authorization provenance before any
  larger relationship changes.
- `SettlementDefeatUtility` replaces a defeated base with a destroyed settlement
  and may defeat the entire faction. Occupation, surrender, tribute, political
  transfer, negotiated access, displacement, or limited defeat require CA-owned
  state; native destruction remains the physical-destruction projection.
- Ideology roles, `Faction.leader`, royal titles, and permit eligibility identify
  roles or status but provide no jurisdiction, quorum, consent, delegation,
  emergency authority, or succession procedure. They are evidence inputs to a CA
  government charter, office, or authorization receipt rather than government
  itself.
- Raid `IncidentParms` carry target, points, faction, strategy, arrival, and
  tactical flags, then raid workers generate pawns and Lords. They contain no
  source settlement, political authorization, logistical expenditure, durable aim,
  war status, or institutional consequence. CA operation records own those facts;
  incidents and LordJobs execute them.
- Generic `LordJob_AssaultColony` may leave after time or damage and may transition
  opportunistically to kidnap or steal. These are tactical triggers, not durable
  war aims. Their physical results must reconcile into the operation and conflict
  records that caused the map action.
- `PeaceTalks` resolves weighted outcomes into goodwill, assault, or rewards. It
  carries no treaty, signatory authority, term, territorial scope, obligation,
  breach, ratification, or successor binding. It may present a negotiation
  encounter; a persistent CA agreement owns peace.
- `Settlement_TraderTracker` lazily generates stock from trader Defs, transfers
  purchases immediately, and destroys and regenerates stock on its restock cycle.
  `TradeDeal` settles the selected exchange immediately. This is an encounter and
  UI seam, not production, demand, reserve, transport, or delivery. Physical trader
  caravans, travelling transporters, and arrival actions provide the native
  execution seams for shipments that CA has authorized and reserved.

## 10. Simulation budget and projection rule

Every loaded map receives `MapPreTick` and `MapPostTick` every game tick and
constructs pathing, temperature, region, room, lighting, terrain, roof, gas,
pollution, zone, reservation, Lord, weather, power, and wildlife systems. Many of
their grids allocate by `size.x * size.z`. Ordinary configured map sizes are
200-325; 350 and 400 are hidden test sizes; the advanced-config UI warns above
280 that large maps degrade performance, AI behavior, and balance. Retaining a map
does not hibernate it.

Every alive, non-mothballed world pawn receives `Pawn.DoTick`, and every world
object receives `DoTick`. A large region therefore cannot represent every inactive
resident as a live world pawn or every claim as its own world object. The regional
component uses indexed records, sparse relationship edges, scheduled event queues,
and staggered cadence buckets. It materializes only player maps, consequential
caravans, contacts, encounters, and explicitly pinned high-fidelity locations.

The canonical projection rule is:

`pawn evidence -> institutional record -> authorization -> regional operation or contract -> native world/incident/quest/map/Lord projection -> reconciled outcome`

The native projection remains authoritative for physical execution while it
exists. It never becomes the sole owner of the institutional fact that caused it.

## 7b. Native furnishing and content-pack seam

RimWorld already classifies lived and working space through `Room.Role`, related
`RoomStatDef` values, worktable role requirements and multipliers, and the
facility-link graph. Beds link native end tables and dressers; worktables link
their loaded facilities; Beauty and other room stats evaluate the actual room.
Colonist Awareness reads those systems as causal evidence and does not recreate a
Bedroom, Barracks, Laboratory, Workshop, or furniture-effect ontology.

Native Bedroom function is grounded in the loaded Bedroom role worker's real
humanlike-bed and owner composition. Love/family structure and
`BedUtility.WillingToShareBed` are related but separate from that classifier;
the player-authored program roster is separate again from native bed owners.
Room Beauty, Impressiveness, Cleanliness, Space, Wealth, and dependent factors
remain numeric telemetry, not a scalar definition of a good room or permission
to maximize furnishings.

The current Bedroom path is therefore a read-only comparison over an existing
player-authored footprint. It receipts present native function separately from
future optional facility capacity, research, technology, materials, spatial
opportunity, and construction stage. It neither selects nor paints a room nor
exposes construction authority. Only the Barracks bed-facility consumer may
originate new room furnishings at this stage; historical Bedroom associations
may be validated or retired without adopting, canceling, or deleting player
blueprints, frames, or completed property.

Native stockpiles and `Building_Storage` are complementary. Shelving changes
density and transfers only its exact footprint from zone-owned floor slots to
building-owned storage settings. Surrounding stockpile cells may continue holding
bulk output. Any operative furnishing decision therefore preserves native filter
and priority, current use, interaction cells, door and sleep approaches,
circulation, negative space, pattern, material and research capability, and the
exact policy transition.

God Mode item dragging is an operator editing instrument outside this furnishing
decision graph. It changes the exact cell of one existing whole item stack after
physical, non-wiping, and capacity validation, then refreshes the native
location-dependent caches. The move does not author a stockpile, alter a filter or
priority, infer a room purpose, request hauling, or grant a furnishing consumer.
Buildings retain their native Reinstall semantics rather than entering the item
path.

Loaded content packs extend the same definition graph. Functional furniture can
participate through native stats, room roles, facility links, storage capacity,
research, materials, placement, and source provenance without a hard-coded mod
list. Visual-only props remain appearance until a truthful semantic adapter exists.
Colonist Awareness supplies behavioral selection and receipts, not copied art or a
replacement asset pipeline.

The link graph is dynamic. `CompAffectedByFacilities.PostSpawnSetup` searches
potential facilities in distance order, and `Notify_NewLink` may supplant a farther
same-definition facility when `maxSimultaneous` is already reached. `CompFacility`
receives the reciprocal add/remove notifications. A later dresser can therefore
take one bed from the first dresser's original focus set without losing coverage.
Historical focus links and current bed-side coverage are different receipts; an
operative completion gate reads the live `LinkedFacilitiesListForReading` graph.
Blueprints and frames expose their intended building through
`entityDefToBuild`; an in-room player construction for a linkable facility is
therefore native intent and blocks CA from originating parallel facility work.
Pending and completed CA records must continue to resolve to an operative
player-authored sleep program, remain inside its footprint, and retain at least
one native bed relationship. `CanPotentiallyLinkTo` is appropriate only for a
pending facility because it tests adding a link against `maxSimultaneous`;
completed records validate the reciprocal `CompFacility.LinkedBuildings` graph.
Revoking authority cancels only the tracked pending blueprint; completed
buildings remain player property while stale CA associations retire.

## 15. Settlement Programs Over Loaded Functional Contracts

Established-settlement composition uses the loaded definition graph rather than
a CA facility enum. `Building_WorkTable` plus recipes establishes production;
`Building_Storage` and storage settings establish storage; research benches,
medical and prison beds, gathering and ritual targets, recreation buildings,
communication buildings, animal-pen contracts, room roles,
`CompProperties_Facility`, and reciprocal affected-by-facility links establish
their respective functions. Native placement acceptance remains the final
physical authority.

This evidence can come from Core, an expansion, or a ported content pack without
changing the settlement-program ontology. B9's loaded-definition audit admits
`Anon2CushionedChair` and `DankPyon_Bust` through the same functional evidence as
native candidates. A name, texture, or decorative plant spot alone is not a
functional contract. The saved program entry records the selected defs and the
placed identities so reload and generation consume one realized fact.

Faction era is a ceiling, never proof of a settlement facility. A research
program requires an active project or question, qualified worker, supporting
organization, relevant knowledge, and either a real bench or an explicit
non-bench research contract. A bench is a work node, not a research institution.
Likewise, a provision arrangement is valid only after its exact operator, stock,
access, labor, funding, material nodes, and distribution behavior exist. The
generator does not fabricate vendor, religious, dues, or abstract operator
institutions to make a supply record possible.

## 16. Causal Ownership of Social State

Native pawn relations and assigned beds are evidence available to domestic
formation. They do not imply that every nearby or similarly ordered pawn shares
a household. CA persists factual domestic units, their source-scoped members,
residential and provision bindings, and represented transition history. A pawn
without a valid multi-pawn unit retains individual self-provision.

Native work, skills, buildings, stocks, transactions, research projects,
policies, offices, routes, and histories provide domain evidence for practiced
capability. The capability resolver may summarize that evidence, but no score,
random perturbation, stable hash, ThingDef, room role, Culture value, Political
Belief, or world tendency creates the practice itself. Hashes and randomness
remain valid only after eligibility for equivalent names, layouts, visual forms,
material candidates, or bounded scheduling has been established.

World generation consumes one persisted realization. UI drawing and navigation
cannot form memberships, adopt policies, assign operators, or reroll semantic
facts. The governed fixture contains intentional authoring facts only and enters
the same production realization path as an operator-authored draft.

## 17. Durable serialization and compositional authoring seam

RimWorld's Scribe loader mutates objects while loading them, so CA campaign
compatibility is decided by a streaming read-only preflight before the normal
load path. The executable schema manifest then validates one semantic version and
owner per state family. Pending creation XML remains replaceable pre-campaign
state; it is not a mechanism for clearing realized world, map, organization,
Culture, political, knowledge, or behavior history.

Native factions, Ideoligions, relations, things, rooms, work, policies, and acts
remain the factual sources for CA authoring and interpretation. The social-subject
registry names what a population evaluates. Cultural meanings store that
evaluation. Concrete practices store repeated represented conduct and identify
the native or CA evidence adapter and runtime consumer. Political Beliefs store
normative mechanisms, while current order stores instituted mechanisms. Shared
editor layout does not merge those records or grant authority to presentation.

The authoring projection caches immutable registry slices and applies category
navigation only when multiple human-relevant groups contain enough entries to
improve discovery. Source-module taxonomies are diagnostic metadata, not the
player's ontology. Partial sets copy their exact mechanisms into the selected
owner and retain no shared runtime authority afterward.
