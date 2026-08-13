# Findings (append-only)

- **F-1** (2026-07-22) — Wait_Combat-as-universal-pin is the wrong medium
  (operator diagnosis, decompile-confirmed). The game's sanctioned seams: four
  `Humanlike_*` think-tree insertion tags, auto-registering DutyDefs, the
  Lord/LordToil/Transition group AI, `LordJob.GetReport` for honest status
  strings. See `GAME_ARCHITECTURE.md`.
- **F-2** (2026-07-22) — 54-finding consistency audit against the decompiled
  engine: redundancy (vanilla already covers), engine-fight (constant-tree
  hostility response vs. undrafted pins; job-churn guards), patch-collisions
  (spike traps destroy themselves before Spring postfixes; KnowsOfTrap runs on
  pathfinder worker threads), and state-leak bugs (scribed hediffs vs. session
  flags; thingIDNumber collisions across saves). Criticals/majors fixed in
  `audit-1`; residue ranked in `ROADMAP.md`.
- **F-3** (2026-07-22) — Player-hand semantics are native: drafting and direct
  orders shadow duties and never eject pawns from non-voluntary lords
  (`Pawn_DraftController`, `TryTakeOrderedJob`); `AllowsDrafting` defaults true.
  The framework leans on this instead of re-implementing it.
- **F-4** (2026-07-22) — The flee-response carve-out pattern
  (`JobGiver_ConfigurableHostilityResponse` prefix) is vanilla-precedented
  (psychic rituals do exactly this); ordered positions must suppress civilian
  flee or dissolve at contact.
- **F-5** (2026-07-22) — A2 review verdicts proven SOUND (do not re-litigate):
  RestraintFactor rewire is range-preserving at the median and reaches all caller
  thresholds; freeze rewire chance stays in (0, 0.08] and the old mood gate is
  subsumed (a high-mood Wimp CAN now rarely freeze - intended texture); EatSmart
  disposition read is cache-cheap and reentrancy-safe with median preserved at
  2500 ticks; all 12 trait defNames + degree schemes verified against Core
  (Nerves/NaturalMood/Industriousness carry degrees 2/1/-1/-2); KnowledgeMapComponent
  auto-instantiates on existing saves; obedience verdicts are deterministic across
  repeated clicks. Manhunters are NOT a knowledge blind spot (mental-state
  hostility routes through GenHostility).

- **F-6** (2026-07-23 23:26 UTC / 16:26 PST) — RimWorld 1.6 has no time-control
  extension seam and its `TimeSpeed` enum is closed; an invented enum value would
  return `-1f` from `TickRateMultiplier`. The native seam is
  `TimeControls.DoTimeControlsGUI` (fixed 32x24 buttons) plus the
  `TickManager.TickRateMultiplier` getter. Keeping the underlying speed at Normal
  while returning `0.5f` produces a true 30 TPS rate through the game's own
  scheduler and preserves native pause/resume. The fifth button fits without
  moving the strip's right edge by extending it one slot to the left. A static
  scan of the installed mod DLLs found no existing owner of the time-control GUI
  seam; Map Preview's temporary pause/restore path was identified and preserved.

- **F-7** (2026-07-23 23:40 UTC / 16:40 PST) — RimWorld audits every loaded type
  with a static Unity asset field and warns unless it carries
  `StaticConstructorOnStartup` (`StaticConstructorOnStartupUtility`). A texture
  built lazily by the UI is still visible to this type-level audit; the owning
  half-speed control class must declare the attribute.

- **F-8** (2026-07-24 02:12 UTC / 19:12 PST) — The live Raywolfen/attacking-raider
  scenario exposed an ownership failure, not a lack of contact: the cover planner
  could choose the pawn's current cell without assigning a durable posture, while
  the prior hold-duty tree still admitted needs and ordinary work, so `RopeToPen`
  continued beside a known attacker. Generic `LordToil_AssaultColony` likewise
  supplies an assault duty but no awareness, risk, or egress contract for its
  sabotage choices. A2.3 therefore gives automatic defenders a dedicated
  no-needs/work defensive posture and exact generic settlement assaulters an
  awareness duty with visible combat, remembered-contact reconnaissance, and
  meaningful sabotage only from a validated touch cell with an open-air egress.

- **F-9** (2026-07-24 02:12 UTC / 19:12 PST) — RimWorld already separates the
  required clocks and physical media. `HumanlikeConstant` supplies the native
  30-tick reaction lane; Lords and duties supply slower group planning.
  `Verb_LaunchProjectile.TryCastShot` is the successful-launch seam, and
  `GenClamor.DoClamor` applies hearing capacity plus region/open-door traversal.
  The resulting gunfire fact can honestly say that a pawn heard repeated weapon
  noise near an approximate area; it cannot honestly name the shooter, faction,
  target, exact cell, or a raid without corroborating contact evidence.

- **F-10** (2026-07-24 04:00 UTC / 21:00 PST) — Biotech defines the Airwire,
  Array, and Integrator headsets with native `MechBandwidth` offsets of +3, +6,
  and +9, and the mechlink itself supplies the mechanitor baseline. Native
  bandwidth is a point budget consumed by controlled mechs and active gestation;
  RimWorld defines no human-to-human radio or mental-line rule. CA therefore
  treats the three exact headset Defs as radio hardware and provisionally maps
  their offsets one-for-one to 3/6/9 human peer lines. It separately maps each
  actually-free bandwidth point to one mechlink-mental peer line. Both media
  require capability at both endpoints. These conversions are CA policy, not
  native engine facts.

- **F-11** (2026-07-24 04:00 UTC / 21:00 PST) — RimWorld models technology,
  society, and attack organization on separate native axes. `FactionDef` exposes
  `techLevel`, attack/siege/avoid-grid capabilities, cultures, memes, and
  strategy restrictions independently; Core's Tribal faction is Neolithic and
  can still stage attacks. Raid strategy selects an objective/state graph, and a
  Lord owns the participating group, but `LordToil_AssaultColony` assigns duties
  directly to each pawn and defines no pawn leader or shared mind. CA's exact
  generic `AssaultColony` interception is therefore attacker-role-specific, not
  faction-differentiated. Neither a low technology level nor Lord membership
  proves disorganization, and neither advanced technology nor communications
  gear proves hierarchy.

- **F-12** (2026-07-24 04:00 UTC / 21:00 PST) — A2.3's fixed-radius contact
  observation could identify a pawn before CA's concealment system had revealed
  them. The observation sweep now skips pawns still registered as hidden, leaving
  discovery to the existing sight, skill, light, and dark-vision gate. This closes
  that identity leak only. Sprung-trap memory remains map-global rather than
  pawn-private, while per-pawn discovered territory, collaborative charting, and
  the optional player-view fog layer remain future work.

- **F-13** (2026-07-24 04:00 UTC / 21:00 PST) — `SquadComponent.LeaderPawn` and
  `FireteamLeadPawn` predate A2.3.1 and silently substitute the available pawn
  with the best combined Shooting and Intellectual score when the persisted
  assignee is unavailable. Communications and Authority consequently observe an
  acting leader that was never separately assigned. The recovered operator plan
  defines behavior after leadership loss but no succession-selection algorithm;
  assigned role identity and any acting-leader policy must therefore be separated
  and ratified before this fallback is treated as doctrine.

- **F-14** (2026-07-24 05:55 UTC / 22:55 PST) — DR-2's declared Directed
  survival floor was contradicted by three explicit `LevelOf(pawn) < 1` gates:
  Eat Smart returned to vanilla inventory fallback, Criticality skipped its work
  giver, and Life Safety skipped its rescue work giver. Removing those three
  gates restores exactly the declared floor without broadening tactical
  self-initiation. Feature settings still disable their own behavior, and Life
  Safety's separate Proactive threshold for automatic outsider rescue remains.

- **F-15** (2026-07-24 05:55 UTC / 22:55 PST) — The Threat knowledge kill switch
  disabled fact acquisition but six consumers compensated by enumerating every
  live hostile on the map. That made the simpler setting more omniscient than the
  richer setting. Disabled mode now retains immediate perception only: one shared
  predicate requires an awake humanlike observer, a spawned hostile on the same
  map, a non-concealed target, the behavior's range, and current LOS. The same
  predicate closes a second leak in which an enabled consumer could resolve a
  remembered hostile ID into a live target after that pawn entered concealment.
  Immediate reaction requires a separate transient first-seen clock; regenerating
  a synthetic contact at a fixed age each pass either bypasses short reaction
  delays or prevents longer pain-adjusted delays from ever maturing.

- **F-16** (2026-07-24 07:13 UTC / 00:13 PST) — RimWorld's IMGUI click routing
  and target resolution use different pointer authorities. `Event.current` tells
  `Selector`, designators, and targeters that a button changed, while
  `UI.MousePositionOnUI` derives the target coordinate from Unity's single
  `Input.mousePosition`. A second MouseMux pointer therefore activates ordinary
  widgets and event-local colonist-bar behavior but misses pawn and map targets.
  RimWorld's own `MousePosUIInvertedUseEventIfCan` establishes
  `GUIToScreenPoint(Event.current.mousePosition)` as the native event-to-screen
  conversion. A button-event-only Dev Mode bridge can use that seam without
  taking over the operator's hardware cursor. Mouse movement, drag motion, and
  held-button state remain process-global, so independent dragging is not claimed.
  RimWorld's Linux/Steam Deck fixer intentionally copies the native input position
  back into IMGUI events, so those surfaces are excluded.

- **F-17** (2026-07-24 10:00 UTC / 03:00 PST) — The observed in-place rotation
  after a successful hide came from three coupled implementation facts. Hiding
  correctly cleared the live enemy target and RimWorld's threat gate rejected the
  concealed pawn, but the generic-assault branch replaced that target with a
  repeatedly renewed combat-posture job for the remembered cell. RimWorld's base
  `JobDriver.IsContinuation` returns true for same-Def jobs, while that posture
  wrote one override facing every tick and a separately quantized remembered-cell
  facing every 15 ticks. The concealment scan then resolved discovery with one
  deterministic distance/LOS pass. A finite multi-waypoint search JobDriver now
  owns the copied last-known area, explicitly distinguishes replacement evidence,
  and marks the exact searched fact complete; the fallback posture has one facing
  authority, and discovery builds stable per-observer progress across scans.

- **F-18** (2026-07-24 10:09 UTC / 03:09 PST) — RimWorld ticks the components of
  every loaded map, but CA's concealment and en-route ambush registries were static
  and keyed only by pawn ID. A second map therefore treated a hider or approaching
  ambusher on the first map as absent and could reveal or disarm that episode
  without observation. Each episode now records its owning `Map.uniqueID`; slow
  discovery and the fast arrival/spring watcher skip entries owned by another map,
  while reveal, completion, and load reset clear ownership with the episode.

- **F-19** (2026-07-24 10:18 UTC / 03:18 PST) — RimWorld's durable diplomacy is
  faction-wide: `FactionRelation` scribes the other faction, Hostile/Neutral/Ally
  kind, and goodwill. A `Settlement` has an owning faction, trader, and remembered
  inhabitants, but no independent relationship vector. On-map Lords can receive a
  memo and transition a group into an exit toil, while generic
  `LordJob_AssaultColony` retains only its faction and behavior flags. Its graph can
  leave on elapsed time, damage satisfaction, or faction non-hostility, and `Lord`
  injects native casualty-based auto-flee; none records an origin settlement,
  deliberation evidence, unresolved objective, refuge plan, or condition for a later
  return. Generic raid history retains only coarse facts such as the last raid
  faction. Settlement-specific diplomacy and generic raid-campaign continuity are
  therefore new CA world state built above native faction, WorldObject, and Lord
  seams, not capabilities RimWorld already supplies.

- **F-20** (2026-07-24 11:15 UTC / 04:15 PST) — The colony dog's silence and its
  selection as a target came from two independent native/CA gaps. `GenClamor`
  already reaches every hearing pawn through capacity-scaled region traversal, but
  CA's audible store rejected every non-humanlike. Vanilla projectile-impact panic
  is only a 40% roll for a masterless animal and still calls
  `ShouldAnimalFleeDanger`, which explicitly rejects a player animal on a player
  home map. On the attacker side, vanilla merely subtracts 50 from a visible
  noncombatant and can still return the best candidate when every score is low;
  CA's validator independently admitted a passive animal with probability
  `1 - restraint` and its score penalty was only 25 times restraint. A further
  trap is `Pawn.IsCombatant()`: `Pawn_MindState` refreshes the target pawn's combatant
  clock when another pawn selects it as `enemyTarget`, so that label cannot prove
  the animal acted. Its own fighting/aggro/attack-target/enemy-target state and
  `lastEngageTargetTick` are the non-circular conduct seams.

- **F-21** (2026-07-24 11:28 UTC / 04:28 PST) — Review corrected three native
  seam assumptions in F-20 and the first A2.3.5 implementation. First,
  `mindState.enemyTarget` is scribed and the trained-animal cancel action clears
  `training.attackTarget` but not that reference, so `enemyTarget` alone is not
  live conduct. Second, `RCellFinder.TryFindDirectFleeDestination` assumes the
  pawn begins near its danger root; when the hearer is already beyond the supplied
  radius, its fallback can advance toward a distant sound. An ambiguous-sound
  safety move must use a radius beyond the hearer's present distance and validate
  that the result is reachable and strictly farther from the cue. Third, vanilla's
  `PawnUtility.PlayerForcedJobNowOrSoon` is the canonical current/front-queued
  direct-order gate; CA retains an additional any-forced-queue guard. Core
  insects use `InsectConstant`, which exposes the same post-`JobGiver_AnimalFlee`
  insertion seam as `AnimalConstant`.

- **F-22** (2026-07-24 19:53 UTC / 12:53 PST) — The starting colony's red-X
  weapons, apparel, and medicine come from
  `ScenPart_PlayerPawnsArriveMethod.DoDropPods`, which passes `forbid: true` to
  `DropPodUtility.DropThingGroupsNear` even for Standing arrivals; `instaDrop`
  changes only delivery. `DropThingGroupsNear` calls `SetForbidden(true)` before
  placement. Vanilla then has no general colonist weapon-acquisition giver:
  `JobGiver_PickupDroppedWeapon` only recovers that pawn's remembered dropped
  weapon, while `JobGiver_OptimizeApparel` waits 6,000–9,000 ticks and requires
  allowed apparel in storage. `CompForbiddable` retains one boolean and no caller
  provenance, so a recurring global auto-unforbid or `IsForbidden` exemption
  cannot distinguish startup bookkeeping from later player intent. The bounded
  seam is the identifiable scenario-start call plus a pre-reaction constant-tree
  giver that returns ordinary Equip or Wear jobs and respects every subsequent
  native forbid, reservation, reachability, apparel-policy, and compatibility
  check. Native field tending must also receive the medicine target (and its
  holder when applicable); a `TendPatient` job built with only the patient records
  no selected medicine.

- **F-23** (2026-07-24 20:10 UTC / 13:10 PST) — The first operational-equipment
  implementation returned null whenever its own Equip or Wear job was current.
  On RimWorld's 30-tick constant lane that lets a lower giver return a different
  job and `Pawn_JobTracker.ShouldStartJobFromThinkTree` interrupt the equipment
  action. An owned valid equipment job must instead be returned as the same Job
  object; the tracker then recognizes it as current and leaves it running. Review
  also established that one apparel marker cleared only when an otherwise eligible
  pawn sampled a calm interval can leak across raids. A scribed retry clock avoids
  both chain dressing and cross-contact identity. For item policy, the native
  Designator_Forbid/Unforbid overrides cover cell, drag, reverse, and bulk actions,
  while the selected-item gizmo bypasses them inside
  `Command_Toggle.ProcessInput`; a scoped patch around that command plus the
  `CompForbiddable.Forbidden` setter captures clicks, hotkeys, and grouped toggles
  without misclassifying simulation calls. `CompForbiddable.PostSplitOff` and
  `ThingWithComps.TryAbsorbStack` are the required propagation seams for a durable
  player-denial registry.

- **F-24** (2026-07-24 20:15 UTC / 13:15 PST) — Two review assumptions required
  narrower native readings. `ThingWithComps.TryAbsorbStack` delegates to
  `Thing.TryAbsorbStack`, whose boolean reports complete donor consumption; a
  partial merge changes both stack counts but returns false. Denial provenance
  must therefore compare pre/post counts rather than trust the return value. Also,
  `StoreUtility.IsInAnyStorage` means any registered haul destination, not player
  ownership. Colony-storage scope requires a player-created stockpile zone or a
  storage building whose faction is the player; the same predicate must bound
  both Autonomous Allow maintenance and threat-equipment selection.

- **F-25** (2026-07-24 21:11 UTC / 14:11 PST) — Luke's stationary exposure and
  Diver/Fazix's stationary fire were separate native-shaped short circuits.
  Applied injury reaches `Pawn_JobTracker.Notify_DamageTaken` after the hediff is
  present but before `Pawn_MindState.lastHarmTick` and CA's damage-contact postfix;
  fully absorbed apparel hits can call the same job notification earlier. The
  damage override therefore reevaluated Luke, but CA returned the same never-ending
  `CA_CombatPosture`, which `ShouldStartJobFromThinkTree` correctly recognized as a
  continuation. Diver's player path directly returned `AttackStatic`. Generic
  assaulters inherited `JobGiver_AIFightEnemy`, which accepts the current cell when
  it can shoot with more than one percent cover or from within five cells.
  `CastPositionFinder` has no friendly-corridor or target-exposure term, and
  vanilla's friendly-fire cone is only a target-selection penalty; projectile
  interception is exactly zero within five cells of the shooter. The correction
  must therefore record the impact before the native override, compare current and
  reachable firing cells before the player/assaulter split, treat close visible
  allies as bad geometry even where vanilla cannot intercept them, and return a
  target-aware finite movement job rather than another posture or a repeated Goto.

- **F-26** (2026-07-24 21:42 UTC / 14:42 PST) — The observed Man in Black freeze
  was CA's post-contact defect, not vanilla combat persistence. Vanilla excludes a
  downed pawn from active threat selection, but `KnowledgeMapComponent` retained
  every contact as actionable for 7,500 ticks and `CA_CombatPosture.ShouldEnd`
  asked only whether one remained fresh. The never-ending posture could therefore
  monopolize the constant-think result for roughly three in-game hours while the
  pawn faced the downed subject. The correction must retain the person as memory,
  resolve actionability from a state the knower honestly observed, and make both
  job selection and the already-running posture read Active contacts only.

- **F-27** (2026-07-24 21:42 UTC / 14:42 PST) — RimWorld's Man in Black event
  supplies a rescue premise but no rescue AI. `StorytellerComp_Triggered` queues
  `StrangerInBlackJoin` only after no other undowned player humanlike remains;
  `IncidentWorker_WandererJoin` then edge-spawns an ordinary player pawn with no
  casualty target or Lord. The PawnKind guarantees Violent and Caring capability
  and five industrial medicine, but not active Doctor work. Vanilla ground tending
  can treat another pawn, vanilla Rescue requires a bed, and player self-tending
  hard-fails when the self-tend toggle is off. Native work priorities also do not
  compare casualty deadlines. The bounded seam is the successful `SpawnJoiner`
  call plus a saved arrival-time beneficiary set and a constant-tree triage giver
  using `HealthUtility.TicksUntilDeathDueToBloodLoss`.

- **F-28** (2026-07-24 21:42 UTC / 14:42 PST) — Vanilla Capture and Arrest cannot
  supply impromptu field custody because both require a valid prisoner bed, and an
  outdoor sleeping spot cannot become one. The reusable native outcome seams are
  `Pawn_GuestTracker.CapturedBy` for real prisoner identity and faction/Lord/UI
  consequences, `WaitInsteadOfEscapingFor` plus its exact wait cell for finite
  compliance, and `JobGiver_PrisonerEscape` when that compliance ends. A custom
  finite carry/place action and small scribed custody record are still required.
  Slave suppression is slave-only and cannot substitute for prisoner compliance;
  repeated timer renewal without current intimidation evidence would be a hidden
  permanent lock. Autonomous execution likewise requires explicit outcome policy,
  not ordinary post-combat cleanup.

- **F-29** (2026-07-24 22:07 UTC / 15:07 PST) — The first mission-triage draft
  bounded *which* pawns the Man in Black could inspect but still read every saved
  pawn's live bleed-out countdown, downed/tend state, and health from anywhere on
  the map. A bounded subject list is not bounded knowledge. The honest execution
  seam is a finite job to the event-saved arrival cell, followed by a close
  sight/touch assessment whose precision derives from Medical and Intellectual.
  `MoteMaker.ThrowText` and `Messages.Message` expose the acquired coarse result;
  exact live values belong only in explicitly labeled developer instrumentation.

- **F-30** (2026-07-24 22:18 UTC / 15:18 PST) — `GenSight.LineOfSight` tests map
  geometry, not whether the observer can see. Vanilla witness logic separately
  rejects `PawnUtility.IsBiologicallyOrArtificiallyBlind` before its distance and
  line-of-sight checks. CA's shared visual-contact predicate must do the same for
  both new hostile observation and firsthand contact-state resolution. A tactile
  casualty assessment may remain available to a blind rescuer only while directly
  adjacent with Manipulation and an unobstructed physical line to the subject.

- **F-31** (2026-07-25 00:21 UTC / 17:21 PST) — Vanilla casualty work begins from
  global truth: `WorkGiver_RescueDowned` enumerates spawned downed pawns and its
  skip path scans faction-wide rescue eligibility. CA's former field-medicine
  branch also enumerated every free colonist and ranked exact remote
  `BleedRateTotal`. The actor-honest seam is a separate welfare fact family whose
  gross state comes from sight or age-preserving report and whose clinical detail
  requires a finite local assessment. Automatic rescue can be knowledge-gated at
  the two native `ShouldSkip(Pawn,bool)` and `HasJobOnThing(Pawn,Thing,bool)` seams;
  their `forced` argument cleanly preserves the direct float-menu order.

- **F-32** (2026-07-25 00:21 UTC / 17:21 PST) — A copied destination does not by
  itself make an asynchronous search honest. RimWorld can keep a same-Def job
  running while newer information arrives, and a `LocalTargetInfo` containing a
  Thing reports that Thing's current held position rather than an immutable cell.
  The first welfare-check draft therefore could mark a newer relocated fact
  Missing, overwrite a later accountability check-in, and let gross sight renew an
  old clinical estimate. The corrected job targets only a copied cell and scribes
  origin, subject ID, source/state ticks, and a monotonic record revision. A global
  end condition and completion-time compare both reject stale work; welfare and
  accountability failures mutate only their own exact originating record. Gross
  sight preserves clinical age while state/cell changes downgrade to a new visual,
  unassessed fact.

- **F-33** (2026-07-25 01:56 UTC / 18:56 PST) — A current fact can become fresher
  without becoming different knowledge. The first `a2-14` in-game welfare pass
  exposed that every 30-tick visual refresh advanced the record revision, so the
  finite `CA_CheckWelfare` invalidated and restarted before its assessment could
  complete. The shared ingestion seam now treats an exact, non-stale,
  Unassessed/Visual heartbeat at the same cell with the same gross state, clinical
  fields, channel, reporter, and provenance as freshness only: it updates source
  tick while preserving acquired tick, state tick, and revision. Stale
  reacquisition or any material semantic/provenance change still advances revision.
  On `a2-15`, a full-restart direct regression completed the visible local
  assessment and transitioned into tending. The centralized `a2-16`/`a2-17`
  implementation is code/build-verified; the equivalent relayed-heartbeat case
  remains to be judged in game.

- **F-34** (2026-07-25 01:56 UTC / 18:56 PST) — Command authority and direct
  accountability are different graph projections. Reusing the direct-report edge
  for legitimacy removed an assigned squad leader's authority over ordinary members
  beneath a fire-team leader, while an unchecked fire-team edge could point upward
  at the assigned squad leader. The corrected command projection gives an assigned
  squad leader the whole assigned squad and an assigned fire-team leader that team,
  never treating the assigned squad leader as the fire-team leader's subordinate.
  Accountability remains direct-report-only and now revalidates both the stored
  edge and an active CA duty before launch and throughout a check. In the final
  full-restart matrix, assigned squad leader Fujita traversed to both Flebe and Xin;
  each complied at opinion-shaded legitimacy 1.00. The fire-team tier and
  accountability cadence remain outside that live proof.

- **F-35** (2026-07-25 12:40 UTC / 05:40 PST) — The accountability record lifecycle
  and the automatic-defense execution lifecycle were individually valid and jointly
  self-cancelling. A due direct-report check needs its owner to still hold the CA
  tactical duty, but calm/no-contact teardown released that duty first, so the
  constant-think lane never launched `CA_CheckWelfare`. Retaining the exact duty while
  a check is due or already Checking closes the composition defect. The first fixture
  attempt also failed for a separate reason: `New Arrivals1` contains no player-faction
  buildings, and its ruins are factionless, so automatic defense had no reachable firing
  cell adjacent to player-owned artificial cover with fill 0.3–0.8 inside the Home area.
  Code and build verified; the live cadence remains unproven.

- **F-36** (2026-07-25 12:40 UTC / 05:40 PST) — Two corrections about the
  independent-pointer bridge, one of them to an earlier claim in this file.
  First, `Verse/Mouse.cs` resolves `IsOver` from `Event.current.mousePosition`, not the
  process-global pointer, so an ordinary RimWorld widget is already reachable by a second
  MouseMux pointer without the CA bridge; the bridge is required for map and pawn
  targeting, which asks `UI` for the global pointer. The claim that a play-state-scoped
  bridge was why menu clicks failed was therefore wrong. The bridge was still widened to
  cover every program state on its own merits (F-16's seam is state-independent, and a
  second seat that cannot reach the load dialog cannot start a session), but that change
  fixed nothing observable.
  Second, and the actual blocker: on this machine a MouseMux SDK virtual user does not
  actuate into any application. The seat is server-registered and visible
  (`Maroon/12290,12291(V)`), the mapper is armed with virtual input enabled and its flow
  running, the server acknowledges every request (`pointer.motion.request.A2M` →
  `pointer.motion.notify.M2A`, likewise for `pointer.button.*`), and MouseMux draws the
  cursor at the commanded position — yet a DOM `mousedown` listener installed in the
  mapper's own Electron window recorded zero events from a seat click. That isolates the
  loss inside MouseMux, independent of RimWorld. Ruled out by test: server mode (switched,
  multiplex, multiplex+multi-keyboard all identical), window focus and z-order,
  `pointer.capture.request.A2M`, `cursor.move.lock.request.A2M`, fullscreen (the game is
  windowed), Dev Mode (enabled), and the app's own virtual-user creation path (identical
  SDK calls). Consequently the 2026-07-24 in-game hover and drag previously read as agent
  input are not trustworthy evidence: the operator was using their own mouse at the time.
  Live verification of any CA behavior through an agent-driven seat is blocked pending a
  MouseMux-side answer.

- **F-37** (2026-07-25 23:56 UTC / 16:56 PST) — A dynamic maneuver does not need a
  sequence of jobs. `Pawn_JobTracker.StartJob` synchronously cleans the current
  driver, while `Pawn_PathFollower.StartPath` changes physical destination and
  `Pawn.TryStartAttack` begins a verb cast without replacing the job. A single
  `Never` toil can therefore own a complete fighting withdrawal, provided it handles
  `Notify_PatherArrived`, does not confuse an asynchronous pending path request with
  failure, and returns immediately after any terminal transition.

  Three adjacent seams are part of the same correctness boundary. First,
  `HumanlikeConstant` asks `JobGiver_ConfigurableHostilityResponse` for work every
  30 ticks, so that giver must yield while an explicit Withdrawal plan owns the
  pawn or it will churn the persistent job. Earlier explosion and oxygen nodes keep
  their native priority. Second, `Pawn_JobTracker.Notify_DamageTaken` calls
  `CheckForJobOverride` according to `JobDef.checkOverrideOnDamage`; `Never` preserves
  the maneuver's own response to pursuit. Third, `TryTakeOrderedJob` pre-reserves an
  incoming player job before interrupting the current job. Withdrawal cleanup must
  release only its own job-scoped state, never broad destination reservations that
  could belong to that incoming order.

  Job initialization is synchronous as well. Legacy plan migration must install the
  replacement job without advancing its physical phase inside toil init, or arrival
  or failure can mutate the plan list reentrantly while load validation is iterating.
  Deferring the first transition to DriverTick closes that path. Exact saved job-ID
  matching makes migration and finish cleanup authority-safe; a newer job is not
  inferred to be abandoned work.

- **F-38** (2026-07-26 00:19 UTC / 17:19 PST) — A `WorkGiver_Scanner` target
  predicate promises that the same scanner can materialize a job for that target.
  Decompiled `JobGiver_Work` selects global Thing candidates through
  `HasJobOnThing`, retains the winning scanner, then calls its `JobOnThing`; a null
  return emits `provided target ... but yielded no actual job` and identifies the
  two methods as unsynchronized. `WorkGiver_HaulCritical.HasJobOnThing` instead
  treated automatic pickup eligibility as sufficient even when neither native
  storage nor the bounded roofed-cell search could supply a destination.

  The correction retains the forbidden-target rejection and derives every positive
  scan answer from `JobOnThing(...) != null`, so selection and materialization share
  the same criticality, storage, roof, reservation, and reachability gates. The
  original `a2-20` full-restart log records the exact `Autosave-2` allowed-rifle
  failure for Xin. The replacement full-restart log identifies the pre-commit
  `a2-21` DLL with SHA256
  `D7E5D4F9F6D71551FC1F7990EFDC791093365A7C775387069B1F3EFCAABF87B9`,
  reloads `Autosave-2`, and records zero subsequent `provided target`,
  `WorkGiver_HaulCritical`, or `Exception` matches after the rifle was allowed. The
  exact `616ce29` Release rebuild completed afterward with 0 warnings and 0 errors
  and produced SHA256
  `A297F7955AAB9AA769E1BAC41CB408A596D7912389DB83C17A707E19A0AA8D8A`;
  that byte-identifiable rebuild has not yet received a further full restart.

- **F-39** (2026-07-26 07:29 UTC / 00:29 PST) — The fire-response defect was an
  arbitration defect, not a need for a second firefighting system. Decompiled
  `Humanlike.xml` gives emergency work its own think-tree lane and
  `WorkGiver_FightFires` together with `JobGiver_Work` already own the Home-area,
  faction, handled-fire, forbiddance, reservation, and reachability policy. A joy
  or idle job can hide that
  lane until reevaluation; ending only that job is sufficient. Outside Home, the
  native scanner deliberately returns no job, so the smallest honest extension is
  a direct `BeatFire` only when a parentless fire occupies a player building and the
  remaining native safety gates pass.

  The distinction was live-observable through deterministic developer fixtures.
  The Home census recorded native acceptance and Luc approached, extinguished, and
  began repairing the fixture wall. The off-Home census recorded native rejection
  alongside CA-candidate acceptance and Luc approached before the fire disappeared.
  Both following censuses recorded no active fire. RimWorld's existing Dev Palette
  can pin CA debug actions, which made the repeated census and fixture cycle a native
  one-click workflow; no separate palette is needed. The settings window's new
  scroll view was also live-proven from its first entries through Fire response,
  Field medicine, Survival responses, and the final autonomy slider.

- **F-40** (2026-07-26 09:06 UTC / 02:06 PST) — An active pawn path does not by
  itself identify whether the path belongs to the current threat-aware phase. Before
  contact was known, `EnsureFinalPath` legitimately set the plan goal to the final
  withdrawal destination. After contact acquisition, the pair driver entered its
  `IsMoving` branch and returned while that path remained active, preserving a
  no-threat decision inside a threat-aware state. The first mover consequently ran
  about 19 cells before the role swap despite the seven-cell bound contract.

  Phase provenance already existed in the scribed `aFinalRun` and `bFinalRun` flags.
  Clearing an active final-run phase through `SetHolding` on the first threat-known
  driver tick is sufficient: the same persistent job immediately selects the native
  local bound and the partner holds. Live sensor traces distinguished the correction
  by exact path targets: `(150,119)` persisted before the repair, while the corrected
  run changed to `(135,118)`, then alternated through `(138,111)` and `(141,118)`.
  This is phase invalidation, not job replacement or periodic supervision.

- **F-41** (2026-07-27 04:41 UTC / 21:41 PST) — Native blueprint placement is a
  planning seam, not a complete autonomy system. Decompiled `GenConstruct` permits
  same-tag replacement through `CanPlaceBlueprintAt`; an autonomous furniture
  planner must explicitly reject `CanReplace` or it may overwrite a player's bed or
  chair. `Designator_Cancel.DesignateThing` directly destroys a Blueprint or Frame
  with `DestroyMode.Cancel`, while `Designator_Deconstruct.DesignateThing` is the
  distinct player seam for a completed building. `Frame.CompleteConstruction`
  destroys the frame and spawns the final building inside one call, so exact
  completed-furnishing provenance must be captured there before a player can
  deconstruct it. The final `ThingID`, including through `GetInnerIfMinified`, is the
  stable identity; def and cell alone can misclassify an identical replacement.

  Geometric `Pawn.CanReach` does not enforce the pawn's allowed area. Placement must
  separately require `InAllowedArea` for the planner and at least one enabled,
  qualified, reachable constructor or native work can forbid the only pending plan
  indefinitely. Large-room candidate sampling also needs an exhaustive fallback;
  sampling alone can falsely report that no legal footprint exists.

- **F-42** (2026-07-27 04:41 UTC / 21:41 PST) — The locally installed Sim
  Settlements 2 MCM declares several transferable settlement-planning patterns:
  assignment qualifications, Citizen and Dynamic Needs, resource-complexity modes,
  leader requirements and traits, department task/energy/resource budgets, SPECIAL-
  shaped output, staged auto-upgrades, hold-until-seen presentation, build-limit
  respect, and construction/upgrade notifications. This is design-comparison
  evidence from SS2's shipped configuration, not a claim about its internal Fallout
  4 execution and not RimWorld engine ground truth. For Colonist Awareness it points
  toward explicit needs, qualified roles, bounded budgets, staged visible growth,
  and notification policy before autonomous settlement development expands beyond
  existing-shelter furnishings.

- **F-43** (2026-07-27 06:04 UTC / 23:04 PST) — `ResourceCounter` is a stored-
  resource readout, not the construction system's complete availability test.
  Decompiled `ResourceCounter.UpdateResourceCounts` visits only haul destination
  groups. Native `GenConstruct.CanGetResources_NewTemp` and
  `WorkGiver_ConstructDeliverResources` instead begin with
  `ItemAvailability.ThingsAvailableAnywhere`, which counts non-forbidden loose
  things, then locate an actually reachable, automatically haulable resource for
  the worker. A planner that rejects loose allowed materials at blueprint-selection
  time can therefore block work that RimWorld itself can perform.

  `ItemAvailability` also caches by material and pawn faction without including the
  requested amount. That is adequate for one native constructible check, but it can
  produce a false result when a planning pass compares two buildings made from the
  same stuff at different costs. The autonomous-home chooser therefore mirrors the
  native non-forbidden predicate while counting the exact amount locally. In the
  full-restart fixture, 75 allowed wood correctly failed the 80-wood double-bed
  option, passed the 45-wood single-bed option, and was then hauled and constructed
  through ordinary RimWorld jobs.

- **F-44** (2026-07-27 06:12 UTC / 23:12 PST) — Preserving an originless engine
  boolean can preserve the engine's automatic decision instead of player intent.
  RimWorld's arrival and scenario paths can create red-X forbids before the player
  has expressed item policy. The former existing-save migration copied every such
  bit into CA's durable player-denial registry, so later Proactive or Autonomous
  access correctly refused to reverse what CA had incorrectly labeled as a player
  command. Spatially restricting ordinary Allow to Autonomous storage, Home, and
  local range compounded the visible failure.

  A versioned migration is the necessary boundary: clear the old originless set
  once, make ordinary visible supplies operational whenever Proactive+ exists, then
  record only native player Forbid input from that point forward. The live census
  made the distinction measurable: the unchanged baseline reached 910 allowed out
  of 910 ordinary visible items with zero system-forbidden items immediately after
  the correction.

- **F-45** (2026-07-27 06:51 UTC / 23:51 PST) — A buildable enclosed room is not
  necessarily a plausible sleeping room. Local bed interaction clearance admitted
  a one-cell corridor intersection, while room size and door count alone admitted a
  battery room. RimWorld's native `Room.Role` further reports some utility and mech
  spaces only as generic `Room`, so role filtering cannot express the whole
  distinction. The contained buildings carry the missing evidence: power batteries,
  generators, worktables, turrets, and mech chargers identify functions that are
  incompatible with automatic human-bed placement even when the room geometry is
  otherwise valid.

  Screen locality was also part of the verification contract. Coordinates and room
  counts proved why candidates passed, but the native camera jump to the pending
  blueprint exposed their actual relationship to corridors, machinery, dividing
  walls, and usable approaches. In the corrected replay, that view distinguished an
  empty 7-by-7 room from the mech bay across its wall and showed the completed first
  bed and pending second bed against opposite walls. Metadata established the
  engine state; the focused game view established whether the placement was visually
  coherent.

- **F-46** (2026-07-27 19:52 UTC / 12:52 PST) — The operator's current save
  corpus separates two spatial questions that a nearest-empty-room heuristic
  collapses. The mature river/mountain colony has a central fallback stronghold, a
  developed northern compound, and a secured but mostly wild southern frontier;
  the smaller river colony organizes sleeping, crops, power, storage, and
  progressively excavated cold space inside one compact enclave. Core/frontier/
  fallback describes strategic topology. Sleeping/dining/freezer/workshop describes
  functional use. Both can apply to the same cells and therefore require separate
  axes.

  RimWorld 1.6 already supplies the functional-use seam. Decompiled
  `DesignationCategoryDef.ResolveDesignators` instantiates public custom designator
  classes from `specialDesignatorClasses`; `Designator_Cells` supplies native drag
  capture and area draw styles; `CellBoolDrawer` supplies colored cell overlays;
  and `DataExposeUtility.LookByteArray` supplies compressed grid persistence. In
  the full-restart `a2-37` receipt, the custom designator appeared in the native Zone
  menu, selected Sleeping, rendered a 12-cell blue overlay, and reported
  `[CA] planned uses: Sleeping 12`. That receipt verifies the intent surface and
  runtime grid. Automatic furnishing placement within painted uses remains pending
  operator visual judgment.

- **F-47** (2026-07-27 20:02 UTC / 13:02 PST) — A declaration can be technically
  adjacent to the implementation and still be false. The first live selector said
  Kitchen and the other reserved uses rejected "unrelated furnishings," but the
  planner deliberately leaves its room-light pass unrestricted. The accurate scope
  is automatic beds, tables, seats, and recreation. Appending full descriptions to
  all twelve menu rows also expanded one choice into a nearly screen-height text
  wall. `a2-38` keeps the exact descriptions in code, states the four affected
  furnishing kinds, and presents only the functional-use names in the selector.
  The full-restart load stamp is verified; the compact selector itself remains
  pending operator-visible confirmation.

- **F-48** (2026-07-27 20:37 UTC / 13:37 PST) — Functional-use enforcement needs
  two receipts that neither a screenshot nor a solver trace can supply alone. A
  production-path fixture can establish an eligible room without hard-coding the
  playground, while the planner's own outcome establishes that its selected
  footprint actually matched the grid. In the `a2-40` replay those halves agreed:
  49 claimed room cells were Sleeping, and the bed selected at `(28, 0, 185)`
  reported `[planned use Sleeping]` together with the same 49-cell, 7-short-span,
  two-door room evidence.

  The visual half still requires the native game. The compact selector rendered
  all twelve choices as one-line rows. In `a2-41`, selecting the real Sleeping
  designator kept the room's blue overlay visible while `Focus current home plan`
  exposed the translucent bed blueprint against its north wall. The focused view
  makes the otherwise abstract cell match legible without turning code or metadata
  into a claim about how the placement looks. Presentation remains the operator's
  judgment.

- **F-49** (2026-07-27 20:57 UTC / 13:57 PST) — The `a2-41` receipt proved the
  footprint obeyed the painted grid and simultaneously exposed that the grid modeled
  the wrong thing. The solver selected and painted its own eligible 7-by-7 room, then
  placed the bed there. That is a tautological placement receipt, not evidence that
  the room matched player intent. The operator identified that room as the intended
  kitchen and identified two separate parallel rooms as intended Barracks.

  Renaming Sleeping to Barracks would preserve the defect. A barracks is a social
  and operational space that can contain beds or floor spots, bonded animals,
  storage, tables, and recreation; its composition varies with duration, hostility,
  materials, ideology, relationships, leadership, and desired comfort. The missing
  substrate is program identity plus constraints and provenance. It must distinguish
  two adjacent programs of the same purpose and preserve a player-authored program
  against autonomous rewriting. Geometry remains evidence for whether a fixture can
  fit; it cannot infer what the player meant the space to become.

- **F-50** (2026-07-27 21:22 UTC / 14:22 PST) — A spatial-program receipt must
  begin with an author-selected place. Build `a2-43` read the proper visible room
  under the game cursor, admitted only its claimed Home cells, and produced a
  60-cell player-authored Austere Barracks with a maximum occupancy of seven and a
  sleep-capacity requirement. The production planner then independently selected
  a native sleeping spot inside that program and reported its unmet capacity. This
  separates authored intent from geometric fulfillment: the fixture supplies the
  program, while the planner supplies one bounded response to its requirement.

  The focused overlay made the relationship visible, but it does not establish
  that the operator approves the room, program shape, color, or placement. The
  active painter described a new Adaptive Barracks while the existing blue program
  was Austere, so the cursor text is not evidence of the existing program's saved
  metadata. The census and planner trace carry that metadata; the game view remains
  the presentation receipt for operator judgment.

- **F-51** (2026-07-27 21:22 UTC / 14:22 PST) — The seven-person home-planning
  fixture is structurally missing one of its original eight starting pawns. Its
  `startingAndOptionalPawns` list still names `Thing_Human1128`, but the save has no
  serialized definition for that ID. The same unresolved reference survives in
  CrashedTogether memories, a mech's Overseer relation, and Luc's Spouse relation.
  The predeploy save, `New Arrivals2`, the immutable material fixture, and its
  disposable copy all share that condition; loading them cannot reconstruct the
  missing pawn.

  The exact source data is not lost. `CA Test Range Eight Final.pcp` lists Luc as
  its first pawn and Kira "Dolly" Cole as its second, matching the save's surviving
  first pawn and missing second starting ID; the standalone `Dolly.pcc` preserves
  the same pawn UUID, backstories, traits, skills, appearance, apparel, ideology,
  genes, and mechlink. Restoration should consume that exact source into a new
  derived test fixture. It should not invent a pawn, splice an unrelated live pawn,
  overwrite the immutable fixture, or save over an operator baseline.

- **F-52** (2026-07-27 21:32 UTC / 14:32 PST) — A schema replacement is incomplete
  while its production declaration still describes the superseded model. After
  authored programs replaced the atomic Sleeping grid, the home-planning settings
  copy still promised Sleeping-cell routing and relationship-favored double beds.
  Those claims were no longer true for programmed space. Build `a2-44` names the
  actual program fields and exact current fulfillment boundary, including what the
  first slice does not yet decide.

  Extending a scribed enum also requires preserving its numeric history. Bedroom is
  appended after Defense instead of inserted beside Barracks, and all prior purpose
  values are now explicit. The result distinguishes private sleeping space from a
  communal Barracks without silently reinterpreting any saved `a2-42` or `a2-43`
  program.

- **F-53** (2026-07-27 21:34 UTC / 14:34 PST) — RimWorld already separates bed
  demand, ownership, and use. Decompiled `Alert_NeedColonistBeds` groups eligible
  love partners when calculating single- and double-bed demand.
  `Pawn_Ownership.ClaimBedIfNonMedical` first releases the pawn's previous bed,
  adds the pawn through the bed's native assignable component, updates the owned
  bed reference, and checks ideology. `CompAssignableToPawn_Bed` supplies the
  player-facing candidate and ideology gates. `RestUtility` then prefers the
  pawn's owned bed, followed by the owned bed of the pawn's most-liked existing
  love partner, before searching other valid beds.

  A space-program resident list therefore should not replace bed ownership or
  pretend that cell membership controls sleep. It supplies planning intent: how
  much capacity a Barracks or Bedroom should provide and who that provision is for.
  Once a CA-planned single-slot provision exists, native bed claiming is the seam
  that makes the authored residence operational. Shared beds remain gated on the
  relationship, privacy, and ideology policy that determines whether two program
  residents should share them.

- **F-54** (2026-07-27 21:55 UTC / 14:55 PST) — An explicit resident roster must
  remain legible even when geometric fulfillment is blocked. In the full-restart
  `a2-46` replay, the cursor-grounded fixture created one player-authored Austere
  Barracks with 98 claimed room cells and a maximum occupancy of seven. Assigning
  Luc through the production resident API changed the program census to `residents
  Luc`. The ordinary planner then reported `0 of 1 currently required sleeping
  places`, proving that the explicit roster—not maximum occupancy or total colony
  count—set demand.

  No provision fit because those authored cells did not contain a valid cell in a
  claimed enclosed room. That is a valid blocked-placement receipt, not evidence
  that the completed-provision ownership seam has run in-game. The planner preserves
  the `0 of 1` demand in its blocked outcome so the cause and requested capacity are
  both visible. The successful single-bed/sleeping-spot completion and native
  ownership transfer remain pending a suitable player-authored program.

- **F-55** (2026-07-27 22:04 UTC / 15:04 PST) — The explicit resident roster is
  usable as a production game surface, not only as a serialized list or planner
  input. Build `a2-47` opened the production resident dialog for the same disposable
  Cursor Barracks through a bounded debug-palette bridge. The dialog rendered all
  seven eligible colonists, reported `1 of 7 maximum occupants`, showed Luc with an
  `Unassign` action, and showed every other colonist with `Assign`.

  This receipt proves presentation of current membership and available roster
  actions. It does not prove a click-driven transfer, persistence after saving, or
  native ownership after construction; those remain separate receipts. The bridge
  only selects the first sleep program and opens the production dialog, so it does
  not maintain a second resident UI or mutate the roster.

- **F-56** (2026-07-27 22:08 UTC / 15:08 PST) — RimWorld's native Bedroom concept
  already distinguishes a love cluster from unrelated co-occupants. Decompiled
  `RoomRoleWorker_Bedroom` accepts adult owners from one `GetLoveCluster()` and
  permits juveniles when a parent belongs to that adult cluster; an unrelated adult
  turns the room into a Barracks. `Pawn_RoyaltyTracker.HasPersonalBedroom` uses the
  same love-cluster boundary when deciding whether another bed owner invalidates a
  personal bedroom.

  Bed sharing is a narrower decision. `BedUtility.WillingToShareBed` checks both
  pawns' ideology against general shared-bed rules and the spouse or non-spouse
  variant. `CompAssignableToPawn_Bed.IdeoligionForbids` applies that willingness to
  every current owner of a multi-slot bed, while `LovePartnerRelationUtility`
  exposes both direct love-partner checks and the most-liked existing partner. A CA
  Bedroom may therefore group a native love cluster without promising that its
  members share one bed; shared ownership remains a separate provision decision.

  Barracks have no equivalent native resident-selection policy. Pairwise
  `Pawn_RelationsTracker.OpinionOf` is the native social signal available for a
  stable first allocation. Using mutual opinion for empty slots, while preserving
  already valid resident choices, lets social grouping emerge without turning
  transient opinion changes into periodic forced relocation.

- **F-57** (2026-07-27 22:19 UTC / 15:19 PST) — Resident-roster authorship and
  Autonomous negotiation both survived full-restart receipts in build `a2-48`.
  With Luc assigned through the player API, the negotiation action returned `all
  sleep-program resident rosters are player assigned` and the census remained Luc
  alone. After the roster was explicitly handed to residents, the same production
  negotiation added Mara, Nadia, Brant, Soren, Elia, and Cardenas. A repeat returned
  `resident rosters are stable`; nobody was evicted or reassigned.

  A second fresh-fixture replay did not invoke the negotiation action. The cursor
  fixture created an unassigned Barracks, the ordinary home-planner debug action set
  the colony Autonomous and called the production planning cycle, and that cycle
  populated all seven residents before reporting `0 of 7 currently required
  sleeping places`. The production roster dialog then rendered `Roster: Residents
  decide`, `7 of 7 maximum occupants`, and `Keep current roster`.

  This one-program corpus cannot demonstrate mutual-opinion distribution between
  Barracks. Its missing Dolly also prevents a legitimate love-cluster Bedroom
  receipt. Owned-bed anchoring, love-cluster seeding, and multi-program social fit
  compile against the real reference assembly and follow the decompiled seams, but
  remain pending live fixtures. No replacement relationship was manufactured.

- **F-58** (2026-07-27 22:50 UTC / 15:50 PST) — The missing Dolly can be
  reconstructed exactly in a loaded disposable game without synthesizing a pawn or
  entering Prepare Carefully's pregame state. Build `a2-54` initializes Prepare
  Carefully's own reflection cache, loads the standalone version-5 `Dolly.pcc`,
  rejects any loader problem, and passes its native customizations back through
  `PawnCustomizer.CreatePawnFromCustomizations`. Because Prepare Carefully's
  request wrapper assumes `Find.GameInitData` exists, the bridge supplies a fresh
  `GameInitData` only during that call and restores RimWorld's original null value
  in `finally`. The spawned result was Kira "Dolly" Cole, female age 21, with her
  preset role and appearance, and the fixture's unresolved Luc spouse relation was
  restored as a live reflexive direct relation. The action rejects an existing
  Dolly, an existing live spouse for Luc, an absent preset, and every partial load.

  The same full-restart replay authored the operator-marked parallel rooms as two
  distinct 60-cell Austere Barracks programs and a separate transient 49-cell
  Adaptive Bedroom. Negotiation placed Luc and Dolly alone in the two-person
  Bedroom as a native love cluster. Mara's initial Barracks choice tied at fit 0.0
  and selected program 1 by stable ID; Nadia, Brant, Soren, Elia, and Cardenas then
  selected the same Barracks at fit 24.0, 19.8, 19.0, 26.5, and 20.7 respectively.
  Program 2 remained empty because mutual opinion outweighed its occupancy
  advantage, not because the selector ignored it. A repeat negotiation reported
  stable rosters. The ordinary planner then targeted program 1 and planned its
  first sleeping spot for the six assigned residents.

  This proves exact-preset restoration, love-cluster Bedroom seeding,
  multi-Barracks comparison, social fit, stable allocation, and planner
  consumption. It does not prove a derived saved fixture, shared-bed willingness,
  owned-bed anchoring, or built-provision ownership. No save was written. The
  immutable fixture and disposable copy remained byte-identical, and the only
  runtime error was the known empty-audio FMOD failure.

- **F-59** (2026-07-28 01:39 UTC / 18:39 PST) — Shared Bedroom provision completed
  through the intended native chain in build `a2-57`. The production planner chose
  a double bed only after Luc and Dolly formed the exact two-person native love
  cluster and `BedUtility.WillingToShareBed` approved them. Ordinary hauling and
  construction completed the blueprint; the completion hook then logged native bed
  ownership for both Luc and Dolly, and Luc visibly used the bed. The same planner
  proceeded to a different missing single-bed objective afterward. This proves the
  positive shared-selection, construction, and dual-ownership path. It does not yet
  prove cancellation after consent loss, claiming a pre-existing unowned bed,
  save/load persistence, or owned-bed anchoring.

- **F-60** (2026-07-28 02:58 UTC / 19:58 PST) — The first native producer policy
  completed its positive live chain in build `a2-59`. With every pre-existing loose
  stuff stack recorded as player-reserved, the retained double-bed objective
  measured an 85-wood deficit rather than selecting starting steel. It designated
  exactly two distant teak trees for an estimated 114 wood. Dolly and Luc selected
  the ordinary plant-cutting jobs, the engine produced and hauled the wood, and the
  double bed completed. Home planning then reported two sleeping slots, cleared the
  double-bed pending state, and advanced to an unrelated single-bed objective with
  36 of 45 wood already available. This proves objective retention, bounded source
  selection, native work pickup, material consumption, and resumption after
  satisfaction. It does not prove source cancellation, threat suspension, a player
  veto, or save/load persistence.

- **F-61** (2026-07-28 02:58 UTC / 19:58 PST) — RimWorld already exposes the
  correct native storage execution substrate, while the loaded definitions show
  why one generic stockpile policy would be false. Decompiled `Zone_Stockpile`
  implements `IHaulDestination` through a `SlotGroup`; `StorageSettings` supplies
  per-thing filters and `StoragePriority`; and `StoreUtility` selects better valid
  destinations for native hauling. A CA Storage program can therefore project
  intent into engine-owned hauling without issuing haul jobs.

  Environmental requirements are orthogonal. `CompRottable` advances from the
  item's ambient temperature through `GenTemperature.RotRateAtTemperature`, while
  `SteadyEnvironmentEffects.FinalDeteriorationRate` separately considers roof,
  outdoor-temperature rooms, terrain, rain, and the item's deterioration stat.
  Core Defs make the distinction concrete: herbal medicine has deterioration 6
  and a 150-day rottable comp; industrial and glitterworld medicine deteriorate but
  do not inherit that rottable comp; wood deteriorates at 0.5; steel carries no
  equivalent deterioration requirement. Biotech's toxic wastepack adds
  deterioration 4, gas-on-damage, eight-day dissolution, indoor and rain factors,
  and pollution on dissolution. Runtime properties, not labels alone, must drive
  roof, refrigeration, isolation, and priority.

  The RimWorld Wiki corroborates the operational consequences: higher-priority
  accepting stockpiles pull items, workflow-adjacent buffers reduce hauling, and
  temperature rot remains separate from outdoor deterioration. It is a design
  reference; the decompiled game and loaded Defs remain engine authority.

- **F-62** (2026-07-28 03:33 UTC / 20:33 PST) — RimWorld's world and map lifecycle
  directly supports a persistent-record/materialized-map regional architecture.
  `World.ExposeData` deep-scribes world objects, world pawns, factions, and
  `WorldComponent`s, and `WorldTick` advances each of those collections.
  `WorldObject` supplies a stable saved identity, one tile, faction, quest tags,
  and scribed/ticking comps. `MapParent` exposes one map, and `MapGenerator`
  explicitly rejects generating another for a parent that already has one.
  `GetOrGenerateMapUtility` lazily reuses or creates the map at a tile.

  `Settlement.ShouldRemoveMapNow` removes a non-home map after its pawn, building,
  and transporter blockers clear while retaining the settlement world object.
  `Game.DeinitAndRemoveMap` calls `Notify_MyMapAboutToBeRemoved` before native
  deinitialization, making that the correct final reconciliation seam. Sites,
  settlement assaults, caravan incidents, and quest-spawned world objects use the
  same lazy-materialization pattern. Loaded `WorldObjects.xml`,
  `BaseFactionMapGenerator.xml`, and `EncounterMapGenerator.xml` confirm the Def
  bindings. The map is therefore an authoritative physical execution surface while
  loaded, not the sole persistent owner of the settlement.

- **F-63** (2026-07-28 03:33 UTC / 20:33 PST) — Full regional simulation cannot
  scale by retaining every map or resident. `TickManager` pre-ticks and post-ticks
  every loaded map every game tick. Each map constructs pathing, regions, rooms,
  lighting, terrain, roofs, temperature, gases, zones, reservations, Lords,
  weather, power, and wildlife; many grids allocate by total cell count. Advanced
  game configuration lists ordinary sizes from 200 through 325, hides 350 and 400
  as test sizes, and warns above 280 that large maps affect performance, AI
  decisions, and balance. `WorldPawns` calls `Pawn.DoTick` on every alive,
  non-mothballed world pawn, and `WorldObjectsHolder` traverses every world object.
  Indexed settlement and person records, sparse edges, scheduled events, and
  bounded materialization are required architecture rather than an optional
  optimization.

- **F-64** (2026-07-28 03:33 UTC / 20:33 PST) — Native settlements and factions
  are encounter and compatibility shells, not a regional institutional model.
  `Settlement` persists its faction, trader tracker, name, and references to
  previously generated inhabitants but no durable population, culture,
  government, economy, territory, institutional knowledge, or settlement relation.
  `FactionRelation` stores only the other faction, scalar goodwill, and
  Hostile/Neutral/Ally kind; `Faction` mirrors those relations and lets hostility
  alter sites, prisoners, traders, passing ships, targeting, and Lords globally.
  A local feud therefore cannot be represented honestly by a native hostility
  flip. `SettlementDefeatUtility` also collapses defeat into physical destruction
  and possible faction defeat, leaving occupation, surrender, tribute, negotiated
  access, and political transfer to CA state.

- **F-65** (2026-07-28 03:33 UTC / 20:33 PST) — Native raids, diplomacy, roles,
  and trade are valid projections but lack institutional provenance.
  `IncidentParms` and raid workers carry tactical parameters and create pawns and
  Lords without source settlement, authorization, logistical cost, durable war
  aim, or political consequence. `LordJob_AssaultColony` uses tactical time,
  damage, kidnap, and steal transitions rather than persistent objectives.
  `PeaceTalks` resolves into goodwill, assault, or rewards without signatory
  authority, clauses, obligations, breach, or succession. Ideology roles, faction
  leaders, royal titles, and permits express status or eligibility but no
  jurisdiction, quorum, delegation, or decision procedure.

  `Settlement_TraderTracker` generates, destroys, and regenerates stock from trader
  Defs, and `TradeDeal` settles selected exchanges immediately. Physical trader
  caravans, travelling transporters, and arrival actions nevertheless supply the
  execution seams for CA-owned contracts and shipments. The consistent projection
  rule is institutional evidence and authorization -> CA operation or contract ->
  native incident, quest, caravan, map, Lord, duty, trader, or transporter ->
  reconciled result.

- **F-66** (2026-07-28 03:33 UTC / 20:33 PST) — Comparative implementations and
  behavioral research support visible actors, bounded abstraction, conditional
  territorial action, and procedure-derived compliance without becoming engine
  authority. RimWar documents world-map scouts, warbands, traders, settlers, and
  destination-driven events; Dynamic Diplomacy documents abstract off-map
  political and battle resolution; CAI 5000 documents objective-, observation-,
  and local-influence-driven tactical actors; Sim Settlements 2 documents local
  settlement roles, supplies, routes, known targets, and strategic/manual conflict
  resolution. These are design precedents only.

  Wilson et al. (2001) found chimpanzee responses to a simulated outsider depended
  strongly on available adult-male numbers; Samuni et al. (2021) found encounter
  participation also depended on numbers and differentiated social bonds, with
  most encounters vocal and lethal outcomes rare; Mitani, Watts, and Amsler
  (2010) linked repeated coalitionary violence to later territorial expansion.
  Baldassarri and Grossman (2011) found greater compliance under elected than
  randomly appointed monitors in Ugandan cooperative groups. These findings support
  patrol, signaling, coalition, attribution, use-based territorial change, and
  authorization mechanics. They do not place human societies on a linear
  primitive-to-advanced scale or equate technology, hierarchy, cognition, and
  institutional complexity.

  Sources:
  - https://steamcommunity.com/sharedfiles/filedetails/?id=2222935097
  - https://steamcommunity.com/workshop/filedetails/?id=1875168898
  - https://github.com/kbatbouta/CAI-5000
  - https://wiki.simsettlements2.com/gameplay/settlements
  - https://doi.org/10.1006/anbe.2000.1706
  - https://doi.org/10.1038/s41467-020-20709-9
  - https://doi.org/10.1016/j.cub.2010.04.021
  - https://pmc.ncbi.nlm.nih.gov/articles/PMC3131358/

- **F-67** (2026-07-28 03:58 UTC / 20:58 PST) — Build `a2-60` proved the first
  construction-demand Storage projection at live engine-state level. Decompiled
  `ZoneManager`, `Zone_Stockpile`, `Zone`, `StoreUtility`, and `GridsUtility`
  establish the native sequence: register the zone and haul destination, add cells
  to its slot group, filter accepted things, and measure whether each reachable,
  unblocked, unreserved cell has actual stack capacity. The projection therefore
  needs no custom haul job and must compare total usable capacity with the exact
  material deficit before treating player storage as sufficient.

  A fresh disposable restart loaded the exact build, forbade 3,182 units of
  pre-existing loose stuff through the deterministic fixture, and retained a Bed
  at `(28, 0, 185)` with a 45-wood deficit. The game created one Important native
  wood reserve at `(28, 0, 187)`, outside the provision footprint, and one native
  teak harvest designation. The storage census reported one automatic projection,
  one cell, demand and deficit 45, held 0, active provenance, and zero vetoes. This
  proves objective-to-zone projection, exact filtering and sizing for this case,
  footprint separation, saved provenance construction, and producer gating on an
  authorized destination. It does not yet prove visible quality, subsequent native
  hauling, edit/delete vetoes, authority reset, retirement, or persistence.

- **F-68** (2026-07-28 04:04 UTC / 21:04 PST) — Build `a2-61` proved the automatic
  reserve's negative authority path through production seams. Calling native
  `Zone.Delete(bool)` on the active projection invoked the shipped deletion patch,
  removed the projection, and retained one veto keyed to the exact Bed cell,
  rotation, program, provision, and wood requirement. Re-running the ordinary home
  planner could not recreate storage and reported that veto with zero zones and
  projections. Toggling Home planning off and on advanced the saved reset
  generation, cleared the veto, and let the same retained need create a new native
  reserve. The final census held one active projection and zero vetoes. This proves
  player deletion, exact suppression, and explicit renewal; label, priority,
  filter, or footprint edit transfer remains code-reviewed but not separately
  exercised live.

- **F-69** (2026-07-28 04:08 UTC / 21:08 PST) — Build `a2-62` proved that editing
  an automatic reserve transfers the native surface rather than destroying or
  competing with it. Renaming the active one-cell reserve made the saved expected
  label diverge from live `Zone_Stockpile` state. The production audit retained
  the native zone, dropped CA's projection, and recorded the exact-objective veto.
  A subsequent planner pass summed 75 currently usable units from that edited
  zone, treated it as player-owned existing storage, and created no replacement.
  This is direct evidence for label-edit transfer and the shared audit predicate;
  priority, filter, and footprint variants remain inferred from that predicate
  until separately exercised.

- **F-70** (2026-07-28 04:14 UTC / 21:14 PST) — Build `a2-63` proved native
  material flow and automatic reserve retirement across successive objectives.
  The first teak source satisfied a 45-wood Bed demand and the planner transferred
  ownership to a native blueprint at tick 32,621. The associated empty reserve was
  marked closed, survived its deliberate 2,500-tick grace, and then disappeared.
  When the completed Bed exposed the next missing sleeping place, a new objective
  measured 16 of 45 wood and a new one-cell reserve held those 16 units through
  ordinary native hauling. Only the new zone remained in the map's stockpile count.

  Storage is therefore a lawful destination rather than a mandatory detour. Native
  construction may consume just-produced material directly when the blueprint is
  ready, while surplus or waiting material can be hauled into the reserve. Both
  routes preserve objective provenance, and an empty completed buffer does not
  accumulate indefinitely.

- **F-71** (2026-07-28 04:19 UTC / 21:19 PST) — Build `a2-64` proved storage
  projection persistence through RimWorld's native save/load lifecycle. A fixture
  saved at tick 25,421 and reloaded at tick 25,422 retained the exact Bed objective,
  45-wood material vector, responsible forester, teak source, zone ID and cell,
  active projection count, and zero-veto state. This validates the new scribed
  projection fields and their references against a real reload, not only an
  `ExposeData` review. The uniquely named fixture and any backup were removed after
  the receipt; no baseline save changed.

- **F-72** (2026-07-28 04:25 UTC / 21:25 PST) — Operator visual review accepted
  the first construction-storage composition: the Bed placement read as sensible
  and the isolated 75-unit woodpile as appropriately bounded rather than an
  arbitrary warehouse. The fixture deliberately forbids loose building-stuff
  stacks, so it cannot adjudicate normal operational access; nor does that fixture
  alone explain the forbidden food and weapons visible to the operator. A fresh
  state is required to test automatic release and differentiated storage of food,
  weapons, medicine, and other starting goods.

- **F-73** (2026-07-28 04:33 UTC / 21:33 PST) — Fresh paused-first-frame testing
  separated a real access defect from the construction fixture. The initial
  categorized receipt found 36 ordinary visible system-forbidden stacks and no
  player vetoes even though three Proactive colonists were present. The exact
  starting groups were already released by CA's scoped `DropThingGroupsNear`
  prefix; the broader access sweep incorrectly depended on game ticks advancing.
  Calling that same policy-preserving sweep from `StartedNewGame` and `LoadedGame`
  produced a clean repeat: all 1,879 visible eligible stacks were allowed at the
  paused first frame, including every counted food, medicine, weapon, apparel, and
  raw-material stack. Explicit player-denied IDs remain excluded from the sweep.

- **F-74** (2026-07-28 05:03 UTC / 22:03 PST) — Build `a2-75` proved the first
  differentiated durable-inventory projection at live engine-state level. The
  loaded playground produced seven bounded native stockpiles whose exact filters
  followed runtime item evidence: Medical, Drugs, Food, Armory, Protected
  materials, Materials, and General. `ThingDef.IsWithinCategory` was required for
  child-category stone chunks; direct membership in the parent Chunks category was
  false. A loaded antibiotic inherited drug behavior with
  `DrugCategory.Medical`, so `IsDrug && !IsNonMedicalDrug` correctly classified it
  as Medical while yayo remained Drugs. Only two minified objects remained General.

  The 12-cell cap prevented 174 evidenced material stack slots from becoming a
  map-wide warehouse, while smaller needs received their measured footprints.
  Initial access was 910/910 allowed. After the visual command exposed overlays,
  selected Food stores, centered the footprint, and activated half-speed, native
  hauling placed one antibiotic stack containing two units in Medical stores; the
  follow-up access census remained 909/909 allowed. This proves creation,
  classification, bounded sizing, first native flow, and the dev visual handoff.
  Operator judgment of the visible composition remains authoritative and open.

- **F-75** (2026-07-28 06:02 UTC / 23:02 PST) — Four verified operator saves and
  the community comparison set reject both a single operator-style template and an
  optimization-first placement objective. The mature bird settlement has a dense
  social core, distinct work and storage bands, terrain-following frontier growth,
  and layered fallback. The four-person river settlement uses communal barracks,
  a walled crop compound, separated climate-controlled storage, and a formal
  research room. The controlled playground is infrastructure-first and preserves
  large unfinished reserves. The second mountain settlement accretes irregular
  corridors, small rooms, common halls, fields, utility clusters, and a distinct
  high-status room around existing rock. Their unfinished areas are evidence of
  temporal growth rather than missing data to fill automatically.

  These saves use different custom ideologies and some contain adherents of more
  than one ideology. That same-builder comparison makes belief and social
  composition causal variables instead of folding every layout into a personal
  aesthetic. Decompiled RimWorld 1.6 exposes each pawn's current `Ideo`, the player
  faction's `AllIdeos`, mutable primary/minor membership, ideology memes, precepts,
  roles, culture, and style. The current home implementation instead consults the
  primary ideology only when projecting a building style and uses individual
  ideology only for specific consent and work gates. It therefore has a native seam
  for ideological plurality but no settlement placement context yet.

  Community examples independently show central plazas with opportunistic houses,
  dispersed road-and-building villages, inner residential and outer utility rings,
  ideology-shaped temples and collective activity centers, historical room-by-room
  modernization, and role-specific compounds. Across both corpora, successful
  layouts remain legible and narratively grounded while differing in efficiency,
  symmetry, density, defensibility, privacy, and finish. The transferable artifact
  is a contextual scoring grammar with provenance, not copied coordinates or one
  adjacency table.

- **F-76** (2026-07-28 06:24 UTC / 23:24 PST) — Operator correction: Red Cube
  and Patriotic Academy are the two operator-authored ideologies in the placement
  corpus. Sophian Cooperative, Rogian Way, Deep Cave, and Archic Way are valid
  evidence of beliefs held by residents in the inspected saves, but their presence
  does not establish operator authorship. This supersedes any reading of F-75 that
  treats every ideology found in those saves as custom-authored. The architectural
  consequence is unchanged: the controlled same-builder comparison uses Red Cube
  and Patriotic Academy, while the runtime context separately preserves every
  actual adherent ideology without inventing provenance for it.

- **F-77** (2026-07-28 06:26 UTC / 23:26 PST) — Build `a2-77` corrected two
  visually implausible availability-first placement outcomes without replacing
  native work. RimWorld's `GenStuff.DefaultStuffFor` and
  `StuffProperties.canSuggestUseDefaultStuff` provide a native distinction between
  practical suggested material and manually valid currency or prestige material.
  Retaining a governed reachable wood source therefore keeps a Bed's exact wood
  demand instead of converting 2,000 loose silver into an unattended silver bed.
  A live receipt retained the Bed at `(28, 0, 185)` with `WoodLog 0/45`.

  Provisional Armory placement now begins from the colony's interior rather than
  the loose weapon stack and penalizes outdoors, exterior ingress, and the outward
  side of a nearby player turret. Placement-policy provenance migrates only an
  untouched CA projection; player-edited and completed surfaces remain protected.
  The repeated playground receipt placed Armory at `(43, 0, 170)`, indoors and on
  the defended side of the turret at `(42, 0, 154)`. These are engine-state
  corrections; operator visual acceptance of the resulting composition remains a
  separate judgment.

- **F-78** (2026-07-28 06:26 UTC / 23:26 PST) — Build `a2-78` proved the first
  saved settlement-planning context across four distinct operator corpus saves.
  The observation-only map component records revision and evidence rather than
  changing construction. It reads actual pawn ideologies, faction primary/minor
  status, memes, precepts, culture, structure meme, assigned roles, love and family
  pairs, space programs and residents, player buildings, blueprints, rooms, native
  growing and stockpile zones, built span, Home roof/water composition, and the
  nearby natural-roof and water field. An explicit census refreshes immediately;
  the passive scan is staggered every 7,500 ticks.

  Live receipts separated the corpus without floor-plan labels. The controlled
  playground reported seven Astropolitan adherents, three family pairs, and a
  125-by-81 infrastructure footprint. The four-person river settlement reported
  three Red Cube adherents and one Sophian Cooperative adherent, one love pair,
  five growing zones, five stockpiles, and a 77-by-55 footprint. The bird colony
  reported four Red Cube adherents plus one each of Rogian Way, Deep Cave, and
  Archic Way, with one love pair, two family pairs, ten growing zones, twelve
  stockpiles, and a 132-by-211 footprint. The second mountain settlement reported
  four Sophian Cooperative, one Patriotic Academy, and four Red Cube adherents,
  one assigned ideology role, thirty-four initially observed enclosed rooms, nine
  growing zones, five stockpiles, and a 147-by-123 footprint. Per F-76, Red Cube
  and Patriotic Academy are the two operator-authored ideologies; the other names
  establish resident belief, not operator authorship.

  A native save/load test on the multi-ideology river case preserved revision 1,
  first-observed tick 2,446,684, and the full evidence signature, including both
  ideology records. The temporary fixture was deleted and no baseline save was
  changed. After the final room-provenance filter, the exact rebuilt DLL restarted
  cleanly and reported nine player-anchored enclosed rooms on that same save. The
  final Release build completed with 0 warnings and 0 errors; no Colonist Awareness
  runtime exception or map-component construction failure appeared. This proves
  inspectable context and persistence, not yet ideology-directed construction.

- **F-79** (2026-07-28 06:50 UTC / 23:50 PST) — Operator correction: the two
  confirmed custom ideologies in the controlled placement corpus are **Red Cube**
  and **The Academy**. This supersedes the authorship claim in F-76 and the
  corresponding authorship restatements in F-78 and the a2-78 batch receipt.
  `Patriotic Academy` remains a correctly observed resident ideology in the second
  mountain census; its presence does not make it the operator-authored comparison
  ideology.

  Five disposable full-game inspections then established The Academy as a real,
  varied placement corpus rather than an XML label. `The SixSeventh Reich` used a
  mountain-embedded service core, ten growing zones, two stockpiles, and outward
  fencing across a 72-by-79 built span. `The Fifth Reich` separated a southern
  residential/work core, central crops, and northern animal infrastructure along a
  mountain edge, with ten enclosed rooms and a 49-by-124 span. `Koeron` paired a
  sparse operational core with 115 queued `Frame_Tile_Transhumanist` floor frames
  across a large mountain chamber, preserving an aspirational plan that exceeded
  current means. `The Fourth Reich` used a compact six-room wooden domestic block,
  a detached walled project area, adjacent crops, retained water and trees, and
  limited mountain excavation. `Coalition of Erewhon` arranged small modules in a
  radial ring around a growing area, with detached domestic and functional nodes,
  a southern perimeter line, and forest retained between them.

  The transferable Academy evidence is therefore not a fixed floor plan. Across
  different populations and stages it weights adaptive clustering, retained
  landscape, mixed natural and technological commitments, legible functional
  separation, and staged or aspirational expansion. These observations refine the
  future placement consumer while leaving the a2-78 context component
  observation-only. Every source save remained untouched; each inspected copy was
  closed without saving.

- **F-80** (2026-07-28 07:15 UTC / 00:15 PST) - Build `a2-79` proves the first
  bounded settlement-context consumer. Only a new Bed proposed by an Autonomous
  pawn inside a player-authored sleep program receives the additional ranking.
  Native blueprint validity, claimed-room, program, research, construction,
  material, and prerequisite gates remain authoritative. The saved context scores
  semantic legibility, actual resident social and ideology evidence, environmental
  fit, operational access, existing program-bed continuity, and settlement
  topology separately; it cannot relocate completed construction, alter a retained
  objective, or redraw the authored program footprint and parameters.

  The deterministic playground receipt reused the exact operator-marked 60-cell
  Barracks at `(40, 0, 171)` and refused changed geometry rather than selecting a
  convenient room. The pre-context winner was `(40, 0, 171)` facing 3 at `-7.49`;
  its context contribution was `+1.44`. The selected candidate was
  `(40, 0, 170)` facing 3, with pre-context `-7.53`, context `+1.59`, and combined
  `-5.94`, so the receipt reported `ranking changed yes`. The fixture had no love
  pair, program bed, relevant meme, or decisive natural-roof signal, so those axes
  honestly remained zero; legibility, operations, and topology caused the change.

  The objective then retained its exact `WoodLog 0/45` demand, authored one native
  harvest designation for about 57 wood, and established a one-cell construction
  reserve without debug-completing the Bed. The immutable source save and its
  disposable copy both retained SHA256
  `4C094DC9D5721FBFA68A8AAC3001355BCCDD4511F160EFF1C1108C017F73EA14`.
  The final 585216-byte DLL has SHA256
  `A2E5DF288106868866F026D8EAC09D98BE1C668D0E2DC66137014958305BA0FD`.
  A full restart logged `[Colonist Awareness] loaded - build a2-79` with no CA
  runtime errors; the process was closed without saving. This proves candidate
  consumption and provenance, not operator visual acceptance of a completed room.

- **F-81** (2026-07-28 07:23 UTC / 00:23 PST) - The a2-79 code-quality delta
  found that its deterministic receipt had observed the intended result but did
  not yet prove that result structurally. A same-sized existing program could have
  differed from the marked room, and the global planning call could have resumed
  another objective while the receipt labeled it as the target Barracks. This
  supersedes only the verifier-strength claim in F-80, not the recorded live
  candidate scores.

  Build `a2-80` requires exact set equality between the player-authored program
  and all 60 claimed room cells. After the normal global planner runs, a structured
  readback now accepts only an active or retained Bed with the exact program ID, a
  cell inside that footprint, and persisted context evidence containing
  `ranking changed yes`; every unrelated, satisfied, blocked, or stale objective
  produces a rejected receipt instead of a pass. The hardened full-restart receipt
  verified the exact retained Bed at `(40, 0, 170)` in program 1 and reproduced the
  a2-79 scores and `WoodLog 0/45` prerequisite.

  The real-reference Release build completed with 0 warnings and 0 errors. The
  final 587776-byte DLL has SHA256
  `87BAA3BE6738D1DF50EB6344D9A217DBF61BAC0B99D00E6CCD694AA0D408CBBD`.
  A full restart logged `[Colonist Awareness] loaded - build a2-80` with no CA
  runtime errors. Source and disposable save hashes remained byte-identical, and
  the process was closed without saving. Review also identified a Medium scaling
  debt: placement-invariant context queries should be precomputed before this
  consumer expands to very large programs or additional furnishing kinds.

- **F-82** (2026-07-28 08:40 UTC / 01:40 PST) - Build `a2-82` completed the
  ordinary native-work composition test for the exact operator-marked 60-cell
  Barracks. The finished room held seven single beds at `(36, 0, 170)`,
  `(36, 0, 172)`, `(36, 0, 174)`, `(40, 0, 168)`, `(40, 0, 170)`,
  `(40, 0, 172)`, and `(40, 0, 174)`. The two banks faced each other, every
  receipted bank interval was regular, and one unpaired position remained rather
  than forcing symmetry beyond the seven-person demand.

  The planner placed `Table2x2c` at `(44, 0, 171)` in a separate unprogrammed
  room with two adjacent seats. The marked Barracks composition receipt found no
  table or seat intrusion. Visual inspection showed the dining group separated
  from the upper mech-charger bank rather than clustered against it. This is live
  composition evidence; the operator remains the acceptance authority for the
  room's appearance and use over play time.

- **F-83** (2026-07-28 08:40 UTC / 01:40 PST) - Build `a2-83` hardened Bed Feng
  Shui selection so a valid candidate with no irregular bank interval
  lexicographically outranks unexplained irregularity before the softer pattern
  score is compared. The lower-pattern exception remains grounded in an existing
  assigned animal bed for the next resident's bonded animal; an unbuilt or
  imagined pet bed supplies no evidence.

  The real-reference Release build completed with 0 warnings and 0 errors. A full
  restart logged `[Colonist Awareness] loaded - build a2-83`, and the fixed
  contextual-Bed receipt verified the first selected Bed in the exact marked
  Barracks with Feng Shui evidence. The complete seven-Bed a2-82 composition was
  not rebuilt under a2-83, so F-82 remains the full-run visual evidence and this
  finding claims only startup, first-candidate, and compiled comparator behavior.

- **F-84** (2026-07-28 08:40 UTC / 01:40 PST) - RimWorld 1.6's inspected toxic-
  waste seams provide physical and diplomatic endpoints, not a persistent
  settlement-owned NPC waste economy. `CompDissolution` suspends dissolution at
  or below 0 C and dissolves an abandoned world stack. `TravellingTransporters`
  passes unreceived contents through world-abandonment notification before
  destruction. `CompDissolutionEffect_Pollution` queues world-tile pollution;
  `CompDissolutionEffect_Goodwill` can penalize a nearby nonplayer settlement and
  choose pollution retaliation or a raid. `QuestNode_Root_PollutionDump` lets an
  eligible nonhostile humanlike faction ask the player to accept wastepacks, while
  the retaliation quest may send wastepacks back. These are quest, world-effect,
  and relationship consequences; the inspected code does not retain the sending
  settlement's waste inventory, storage capacity, chosen strategy, or shipment
  ledger.

  The live Biotech Def describes wastepacks as needing freezing, polluting on
  dissolution or destruction, releasing tox gas when burned or damaged, and being
  flammable. Community strategy references add adversarial uses of those same
  rules: freezing until late technology, remote dumping, using transport pods,
  selecting tribal targets that cannot return pods, accepting retaliation risk
  from industrial factions, and provoking raids. The comparative sources were the
  [RimWorld Wiki toxic wastepack page](https://mail.rimworldwiki.com/wiki/Toxic_wastepack),
  [drop-pod page](https://www.rimworldwiki.com/wiki/Drop_pod), and representative
  player discussion at [r/RimWorld](https://www.reddit.com/r/RimWorld/comments/1041zqr).
  These support a capability- and consequence-driven strategy portfolio; they do
  not justify copying a player metagame rule into every NPC society.

- **F-85** (2026-07-28 08:40 UTC / 01:40 PST) - Build `a2-84` adds a read-only
  facility-siting census and gives inventory Armory scoring and the census one
  shared exterior-ingress predicate. The receipt enumerates claimed or programmed
  proper rooms and keeps room footprint, Home coverage, roof and natural roof,
  open roof, mineable boundary, temperature, doors, exterior ingress, reachability,
  transmitted-power cells, temperature control, habitation, Kitchen programs,
  turrets, Defense programs, stockpiles, storage buildings, beds, wastepacks, and
  atomizers as separate facts. It explicitly refuses to infer a beachhead,
  facility purpose, relocation, or faction offloading strategy.

  In the unchanged controlled playground, the full-restart receipt found nine
  candidate rooms, eleven player turrets, eighteen exterior-ingress doors, no
  Defense program, no wastepacks, and no atomizer. It distinguished a 49-cell,
  fully constructed but non-natural-roof room from the 112-cell fully natural-
  roof interior and other mixed or unroofed rooms. With no existing beds or sleep
  programs in this baseline, habitation distance correctly reported `none
  observed` rather than manufacturing a domestic center. The Release build
  completed with 0 warnings and 0 errors. The exact 629248-byte DLL has SHA256
  `95C7A3AE0B80284099A16F2913DBA8CE0C16BCAF1719586B4FB622874CD34CB2`;
  the log confirmed `[Colonist Awareness] loaded - build a2-84` with no CA command
  failure. Both baseline and disposable save hashes remained
  `4C094DC9D5721FBFA68A8AAC3001355BCCDD4511F160EFF1C1108C017F73EA14`,
  and the observation-only process was closed without saving.

- **F-86** (2026-07-28 08:48 UTC / 01:48 PST) - Build `a2-85` adds advisory,
  non-mutating comparisons to the facility-siting census without collapsing its
  raw evidence into a universal facility score. Basic candidates must be enclosed,
  reachable, grid-near, large enough for the use, and nonresidential. Kitchen
  comparison then preserves player-authored purpose, lower natural-roof
  opportunity cost, lower current-use conflict, lower mineable-boundary
  consumption, scale fit, and grid distance in that declared order. Protected-
  store comparison preserves authored Freezer or Storage purpose, natural-roof
  share, observed ingress distance, grid reach, present-use burden, and scale fit.
  These are advisory comparisons; they do not paint a program or approve
  construction.

  On the immutable playground, the Kitchen comparison selected room #42 at
  `(28, 0, 182)` from seven viable rooms. It is a 49-cell fully roofed room with
  0% natural roof, zero current-use burden, zero mineable boundary, and power five
  cells away. This matches the operator's correction that the intended top-left
  Kitchen is exposed rather than mountain-embedded. The protected-store comparison
  selected room #22 at `(37, 0, 155)` for its 100% natural roof, but disclosed its
  25-building transition burden and refused to infer that it lies behind the
  defenses: the fixture has eleven turrets but no Defense program. No waste exists,
  so no toxic staging or offloading objective was selected.

  The real-reference Release build completed with 0 warnings and 0 errors. The
  exact 637952-byte DLL has SHA256
  `3A086FCE742224D40080BBA2FE222BFED9C1355F5914529FEC541E8B3ED7BBA9`;
  the log confirmed `[Colonist Awareness] loaded - build a2-85` without a CA
  command failure. Baseline and disposable save hashes remained
  `4C094DC9D5721FBFA68A8AAC3001355BCCDD4511F160EFF1C1108C017F73EA14`.
  The comparison changed no game state, so the process closed without saving.

- **F-87** (2026-07-28 10:29 UTC / 03:29 PST) - Build `a2-86` proves that an
  existing player-authored facility program can be evaluated without selecting or
  painting a room. The receipt retains separate axes for loss, loaded-item hazard,
  services, logistics, habitation, defensive topology, footprint, expansion,
  current use, culture, construction stage, present readiness, and future site
  potential. Its observational accessor does not invoke program normalization.
  Excavated natural-roof cells remain distinct from adjacent mineable seams, and
  turret distance cannot become a beachhead or defended-side claim.

  The exact controlled Kitchen verifier accepted only the fixed 49-cell room at
  `(28, 0, 182)` and an identical 49-declaration player program. It reported a
  fully enclosed and roofed envelope with the grid two cells from its edge, no
  food-preparation worktable, no natural roof, and no Defense program. The result
  preserves the operator's correction: an exposed enclosed Kitchen can be the
  lower-loss use of space while protected depth remains available for other
  burdens.

  The developed mountain receipt evaluated 32 authored Storage cells with 122
  loaded item units and nine adjacent mineable seam cells. The hardened service
  inspection found two coolers serving the footprint by their cold-side room,
  reported both powered, neither operating at high power at the sample, and
  preserved their -1 C to 1 C targets separately from the observed 2.6 C to
  33.4 C footprint range. The aboveground receipt evaluated 42 enclosed, roofed
  Storage cells with a native stockpile, habitation one cell from the edge, and a
  transmitted grid 49 cells away. Both receipts withheld freezing and defensive
  claims that the evidence did not support.

  The final Release build completed with 0 warnings and 0 errors. The 665600-byte
  DLL SHA256 is
  `28EA4BD852EA93553025039BC21A9BCB9E662F51A6E5903E772847E539AC48DB`.
  Controlled, mountain, and aboveground proof runs used the final DLL; the corpus
  copies closed without saving, all source/copy hashes remained byte-identical,
  and RimWorld was closed afterward. The evaluator enables no construction,
  toxic-waste, shipment, or NPC strategy consumer.

- **F-88** (2026-07-28 17:51 UTC / 10:51 PST) - Build `a2-87` proves the first
  bounded toxic-waste lifecycle objective without authorizing physical action.
  Recursive native inventory traversal found 60 Wastepack units in the developed
  mountain colony rather than only the 51 spawned units visible to the earlier
  room census: nine more units were held inside a native atomizer. The objective
  retained all 16 lot identities and holder relationships, counted all 60 as
  frozen, observed none dissolving, and treated atomizer containment separately
  from operating remediation. With no existing authored destination, it entered
  `StagingBlocked` and left reservation, relocation operation, transport,
  shipment, and consequences unstarted.

  A 15-cell player-authored Storage program over the colony's existing native
  waste store supplied the missing authority boundary. Every candidate cell
  accepted Wastepacks, was roofed, reachable, and actually -0.4 C. The bounded
  physical capacity was 75 units with 24 free, 51 units were already in those
  cells, and the nine atomizer-held units remained separate. The objective entered
  `FrozenStaged` with semantic evidence SHA256
  `FDD148D91237174A701418008B66C8B396A430C6A31297629FD24BA7CE5B91A9`.
  It still reserved and moved zero units. The exact receipt repeated unchanged and
  survived native save/reload with SHA256
  `C0AEADE43BE046B95759E08A2A12E79FCA0599DCC3AC480E1EB3AA55E27719CA`.

  Controlled and aboveground saves each contained zero real Wastepack inventory,
  and both emitted the same deterministic no-objective receipt rather than
  inventing a facility or strategy. All baseline hashes remained unchanged. The
  final real-reference build completed with 0 warnings and 0 errors; the exact
  698880-byte DLL SHA256 is
  `C890E7393A1EC0F7E426CA8683F9977AB0303CECFE7BF1AE2B459E0A66F2D7B1`.
  Before a relocation or construction consumer, capacity must be upgraded from
  physical slot evidence to native haul-valid capacity/reservation, duplicated
  facility observations should become a shared immutable snapshot, and future
  cross-map transport/consequence ownership must move to the regional world
  substrate.

- **F-89** (2026-07-28 20:31 UTC / 13:31 PST) - Build `a2-88` proves that an
  explicitly authorized local Wastepack relocation can survive an active native
  job save/reload and complete without converting advisory capacity into invented
  transport. The authorization retained exact source `Wastepack850298`, destination
  program #1, dev-fixture origin, and operation ID
  `player-map-0-toxic-waste-1-relocation-1-4668960-1`. Lifter
  `Mech_Lifter10463` obtained both native reservations, carried the one authorized
  unit, placed it at `(40, 0, 115)`, and finished with 1/1 succeeded jobs and zero
  failures. The active checkpoint recorded `Ongoing`; after exact native reload the
  same job recorded `Succeeded`. Final inventory was 61 units: 52 spawned units in
  the existing frozen staging program and nine still held by the atomizer.

  The source save also demonstrated why hostile-faction evidence cannot be treated
  as one category. Two spawned hostile-faction pawns were colony prisoners and were
  preserved; one non-prisoner pawn held an active `Defend` duty and was the combat
  interference. The isolation action refused until those states were separated,
  removed only the single active combatant in the hash-bound Temp copy, and made no
  claim about how he entered the original save. The real permadeath save remained
  byte-identical at SHA256
  `ED13B5EBEFE9B645CB67E67FCCF8169AB9267F628519EA82DE5ACC5940C0FB8B`.

  Save continuity is part of the proof rather than an afterthought. The clean
  mutation created a continuing working save; completed progress later overwrote
  that working save, while the exact active-reservation and resumed-completion
  states remained separately named checkpoints. The final DLL is 744448 bytes with
  SHA256 `A042607422AAB878078D47CFA9D7B6A11C1263943B6142076C193B8364786834`
  and loaded cleanly on the completed working save. No shipment, cross-map route,
  facility construction, remediation, contract, coercive dump, retaliation,
  hostile pollution, or NPC strategy is proven by this local receipt.

- **F-90** (2026-07-28 21:30 UTC / 14:30 PST) - Build `a2-89` proves why a
  transient observation cannot double as facility existence, action authority,
  and terminal success. One allowed fixture Wastepack triggered autonomous
  `CA_ToxicWasteResponse`; `Human590` obtained both real reservations, carried one
  unit through native `HaulToCell`, and placed it in existing program #1 at
  `(40, 0, 116)`. The response created no new program, storage setting, facility,
  source-permission change, or second authorization.

  The room itself crossed the previous binary threshold during the proof. The job
  finished at `-1.1 C`; a later receipt saw `+0.1 C`; native reload saw `+1.3 C`.
  At the warm samples the current frozen capacity was zero and all 53 program-held
  units could dissolve, yet the independently measured containment facts remained
  15 roofed accepted cells, 75 units of capacity, 53 units held, and 22 free. The
  response therefore remained completed while the thermal state correctly changed
  to `ContainedWithThermalRisk`. The saved response retains both facts: successful
  placement and the timestamped finish-temperature/dissolution snapshot.

  This is not merely a toxic-waste exception. Simulation receipts must separate
  observations, capabilities, intent, execution, and consequences so runtime
  variation can select or produce different valid branches. A low-tech actor's
  inability to freeze does not imply inability to contain or respond, and an
  industrial colony with an existing store and atomizer should not construct a
  redundant apparatus. This build supplies only the player-map containment branch;
  it does not claim or execute tribal or NPC strategy.

  The persisted working save is 20249262 bytes with SHA256
  `C6453DDB64BFE929C691F962484D65A3CD3FC6995F934B82BFBE262CC5E66CCC`.
  Baseline and real-save hashes remained unchanged. The final Release build passed
  with 0 warnings and 0 errors; the 777728-byte DLL SHA256 is
  `226352DB3DF899F926EC92A8D14CB7D969A8F5D29B9E1DBFF724A64D8AABE7EB`.

- **F-91** (2026-07-28 22:06 UTC / 15:06 PST) - Build `a2-90` proves that native
  stockpile policy can remain authoritative while CA retires its parallel durable
  inventory projection. Decompiled `Zone_Stockpile` owns `StorageSettings`, its
  `SlotGroup`, cells, inspect tab, acceptance check, and gizmos; `ITab_Storage`
  exposes native priority and the full `ThingFilterUI`. The live storage-tab crop
  records Priority, clear/allow controls, search, hit-point and quality ranges,
  freshness/rottenness, and the expandable definition tree. The 50,980-byte PNG
  SHA256 is
  `AA2CB9C05FE5B6D49B6DEA0C76735DED02286706D474FAC5D8F2C69F2153A6F4`.

  A legacy a2-85-era autosave supplied a real migration fixture: its XML contained
  seven differentiated CA provenance records and seven corresponding native zones.
  The a2-90 load receipt retired exactly seven records and retained the same seven
  zone IDs, labels, priorities, cell counts, allowed-definition counts, and held
  contents: Medical stores, Drug stores, Food stores, Armory, Protected material
  stores, Material stores, and General stores. No inventory veto remained. The
  source autosave and isolated autostart copy both remained byte-identical at
  SHA256 `EAC74C1E651CB759FC2820842BACE3793EA48A470627BA68BAD4AB69B34C2C98`.

  The same final DLL produced three complementary facility receipts. The
  controlled fixture evaluated only the authored 49-cell exposed Kitchen at
  `(28, 0, 182)`, reported zero natural-roof cells and grid access two cells from
  the footprint edge, and preserved the cheaper-loss rationale without treating
  protected depth as a Kitchen gate. The developed mountain working save retained
  twelve native stockpiles: its Important 44-cell mixed fridge held dye, kibble,
  survival meals, components, smokeleaf, medicine, rice, and mech corpses, while
  the separate Normal-priority Wastepack zone held 53 units. Its legacy Storage
  program reported native filter and priority as authority, a current `1.3 C`
  thermal-risk snapshot, and no fabricated defended-side relation. The aboveground
  corpus contained one Normal-priority native stockpile and no target program; the
  evaluator explicitly refused to invent a facility.

  `CASpacePurpose.Storage` remains deserializable and evaluable but is absent from
  the paintable purpose list; both the dialog and designator reject unchanged
  legacy-Storage expansion. Retained inventory receipt aliases are observational
  and no longer set colony autonomy. The category-bucket census is gone, while the
  distinct exact-objective construction reserve remains. Three independent review
  passes found no remaining correctness, structural, authority, or build blocker.

  The final real-reference Release build completed with 0 warnings and 0 errors.
  The 765952-byte DLL SHA256 is
  `559C347345B1169A6DE31401EF75C02598F9D2759E3199E540A0688C8D1C202B`.
  Controlled, migration, mountain, and aboveground loads reported no runtime
  exception. Baseline `autostart.rws`, the continuing mountain working save, and
  the operator's real permadeath save remained respectively
  `7ADB83329FC52F6E08AA1A05EBBBDD82A21725F74F4CC38F17034300F439A09E`,
  `C6453DDB64BFE929C691F962484D65A3CD3FC6995F934B82BFBE262CC5E66CCC`,
  and `ED13B5EBEFE9B645CB67E67FCCF8169AB9267F628519EA82DE5ACC5940C0FB8B`.
  No per-zone autonomy control, shelf consumer, facility construction, shipment,
  or NPC strategy executor is claimed by this proof.

- **F-92** (2026-07-28 22:39 UTC / 15:39 PST) - Build `a2-91` proves one
  definition-driven furnishing evaluator can cover native stockpiles, classified
  rooms, facility links, ambient Beauty, and mod content without painting a test
  room or inventing a universal score. The loaded profile exposed 21 room roles,
  13 room stats, 16 facility definitions, 48 affected definitions, six
  capacity-improving storage definitions, and 26 player-buildable positive-Beauty
  furnishing definitions. Every candidate retains its Core, expansion, or mod
  source in the receipt.

  The developed mountain receipt read twelve native stockpiles and the one legacy
  player-authored Storage program. Its Important 44-cell mixed fridge held 23
  stacks and 228 units across dye, kibble, survival meals, components, smokeleaf,
  medicine, rice, mech corpses, and other allowed contents. A Core shelf at the
  deterministic example `(51, 0, 106)` would replace two floor slots with six
  shelf slots, a density delta of four, while retaining the surrounding floor
  stockpile. The receipt separately reported current contents, exact cells removed
  from the zone, approaches, wall contacts, remaining negative space, material
  cost, capable builders, and reachability. It authorized none of them.

  The sparse aboveground corpus read one 42-cell native stockpile with five stacks
  and no authored room program. The same Core shelf had 122 feasible native and
  spatial placements out of 168 probes; twelve native rejections preserved the
  butcher table and fueled stove interaction spots. The encompassing native
  Barracks exposed active 0.80 out-of-role factors for its research bench and
  stove plus the loaded bed and workbench facility graph, but the evaluator
  produced no room-furnishing candidate because no `CASpaceProgram` authority
  existed. The controlled 49-cell exposed Kitchen likewise remained exactly the
  operator-authored footprint and inferred no Beauty requirement from a native
  role that declared none.

  Current Workshop research supports three compatibility tiers. [Vanilla
  Furniture Expanded](https://steamcommunity.com/sharedfiles/filedetails/?id=1718190143)
  and [More Furniture
  (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=2565302299)
  are functional-furniture candidates whose native comfort, recreation, bed-link,
  room, or stat contracts can feed the evaluator. [Neat
  Storage](https://steamcommunity.com/workshop/filedetails/?id=3416243474) is a
  storage-content candidate with 27 furniture forms and must be capacity- and
  exact-zone-transition tested. [Adaptive Storage
  Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=3033901359)
  extends rendering, capacity, temperature, grouping, and selection behavior and
  therefore belongs in a separate compatibility profile before adoption.
  [Vanilla Furniture Expanded - Props and
  Decor](https://steamcommunity.com/sharedfiles/filedetails/?id=2102143149) states
  that its roughly 350 props have no gameplay functionality; it is a visual
  vocabulary candidate, not native utility evidence. [Erin's
  Decorations](https://steamcommunity.com/sharedfiles/filedetails/?id=2463358089)
  mixes mostly decorative objects with a few explicit functions and must be read
  definition by definition. No Workshop subscription, install, load-order edit,
  or save mutation occurred in this batch.

  The final 92,731-byte mountain receipt has SHA256
  `2EB164989D9B1E0970B7093A87754658FAD8B6C6F6951217EE931BFAB3FAE7CA`.
  The real-reference Release build completed with 0 warnings and 0 errors; the
  806912-byte DLL SHA256 is
  `649FBE461800D4229497444F0B345B280221F3BD2287CC7CE113FAE5C7847E27`.

- **F-93** (2026-07-28 23:20 UTC / 16:20 PST) - Build `a2-92` proves a
  per-space initiative ceiling and a real native shelf consumer without creating
  a parallel storage ontology or a room checkbox with no consumer. The component
  persists the same four-level record for native stockpile IDs and authored room
  program IDs, defaults both to Standard, and computes effective initiative as
  the lower of pawn autonomy and space authority. Only stockpiles expose the
  control in this build.

  Decompiled construction evidence settled the policy transition. The ordinary
  build designator calls `GenSpawn.WipeExistingThings` and
  `GenConstruct.PlaceBlueprintForBuild`; `Blueprint_Storage` owns native
  `StorageSettings`, copies them into `Frame.storageSettings`, and the completed
  `Building_Storage` inherits them. The operative path uses those same calls,
  invokes loaded `PlaceWorker` hooks, and copies the originating zone settings
  into the storage blueprint. It does not directly spawn a shelf, fabricate a
  construction job, or continuously overwrite the finished building's policy.

  The final-DLL controlled receipt found the one exposed Kitchen program, zero
  stockpiles, a Standard shared ceiling, no room control, and no invented work.
  The aboveground corpus temporarily raised one qualified test author for the
  guarded evaluation, restored that pawn to Proactive immediately, and kept its
  5/42-slot stockpile unchanged because 11.9 percent was below the Autonomous
  50-percent evidence threshold. No aboveground save was written.

  In the developed mountain checkpoint, the Important mixed fridge began with 23
  stacks, 228 units, and 44 floor slots. A guarded fixture temporarily raised Luc
  from Proactive to Autonomous, selected a Core `Shelf` at `(52, 0, 109)` facing
  North in Wood, then restored Luc before native work began. The two-cell
  footprint already held two compatible stacks totaling ten units. The selection
  retained four open cardinal approaches, two wall contacts, 42 clear
  negative-space cells, the surrounding 42-cell floor stockpile, and a six-slot
  shelf for a positive four-slot density transition. The blueprint copied
  Important priority and 322 allowed loaded definitions. Ordinary RimWorld work
  completed it; the completion log matched expected and inherited policy, and the
  combined domain became 23/48 slots, 47.9 percent, with no second blueprint.

  The specific clean checkpoint is 20,255,123 bytes with SHA256
  `A65EDD76713B91BA7D3A2F81DA522F48A4BD31C4DCBDD9255C7252D347C9765A`.
  The cleaned continuing working save is 20,267,493 bytes with SHA256
  `10FA15C38D7D2D33D9C1F11B1E99215F68ADB3BCDCC409DC8337243B830FC422`.
  Native reload retained the Autonomous zone ceiling, Proactive Luc, the one
  completed association, two held shelf stacks, six shelf slots, 47.9-percent
  pressure, zero pending work, and the same file hash. The original autostart,
  aboveground corpus, and operator baseline remained respectively
  `7ADB83329FC52F6E08AA1A05EBBBDD82A21725F74F4CC38F17034300F439A09E`,
  `0DBB30070B57D9066E0C9AB5BB3F4B90BFE4410361821C8CEBEC0FC39583CA5F`,
  and `4C094DC9D5721FBFA68A8AAC3001355BCCDD4511F160EFF1C1108C017F73EA14`.

  The final real-reference Release build completed with 0 warnings and 0 errors.
  The 852,992-byte DLL SHA256 is
  `068C436894C22BB6BAC18A547B0B75E3027A02D2EFB1AC083DCEA78F0158631E`;
  a full restart logged build `a2-92` with no CA runtime exception. No Workshop
  subscription, content-pack dependency, room consumer, real-save write, window
  manipulation, shipment, or NPC execution occurred.

- **F-94** (2026-07-29 01:36 UTC / 18:36 PST) - Build `a2-93` proves the
  first player-authored room-furnishing consumer, restores the exact missing
  resident milestone, and adds read-only animal-infrastructure and critical-asset
  evidence without inventing a parallel room or storage ontology.

  The disposable Barracks fixture first restored the exact Prepare Carefully
  Dolly preset and reciprocal Dolly-Luc spouse relation. Native home planning
  completed an eighth Bed at `(36, 0, 168)`; the authored Barracks then contained
  eight residents, eight completed humanlike beds, and eight distinct resident
  owners. Settlement context revision 15 left the clean pre-context Feng Shui
  winner unchanged while independently adding 4.18 points. That is a valid
  completed comparison: context may confirm an already-correct placement. The
  older regression whose purpose is to prove a ranking change retains its
  separate `ranking changed yes` assertion.

  The shared room initiative consumer selected one native Dresser family. The
  first Dresser at `(39, 0, 166)` served the initial bed group with two
  wall-backed cells, two clear room-facing cells, zero protected-lane or bed-bank
  interval cells, all eight sleep approaches preserved, and 42 remaining
  circulation/negative-space cells. Native facility distance required a second
  same-family Dresser at `(40, 0, 177)` for the remaining coherent group. After
  full restart and reload, both completed records remained operative: the live
  reciprocal facility graph reported 4/5 and 3/3 expected-focus links, while the
  bed-side graph covered all eight beds. Native relinking may change historical
  focus without losing current coverage. Beauty was telemetry, never an
  initiative objective.

  The quality pass hardened player authority before release. Existing
  player-authored facility blueprints or frames block parallel CA origination.
  Home planning and spatial initiative share one active CA construction
  commitment, including retained home prerequisites. Revoking an authored sleep
  purpose, footprint, or potential bed relationship cancels only the tracked
  pending CA blueprint. Completed records instead validate the actual
  `CompFacility.LinkedBuildings` graph; completed buildings remain player-owned
  when a stale CA association retires. The final correctness recheck found no
  remaining blocker.

  Welfare memory now snapshots stable clinical meaning before updating the stored
  fact and separates evidence-source time from delivery freshness. The final
  deterministic receipt passed stable downed/no-bleed retention, finite-bleed
  refresh, dangerous-exposure refresh, expired-relay refresh, stable clinical
  heartbeat renewal without revision, and equal-source relayed heartbeat renewal
  without changing source time or revision.

  The Red Cube animal receipt observed three player animals, one rope-managed
  animal, no pen marker or hitch, no installed animal bed, and two existing
  minified installable animal-bed assets. Formosa the alpaca was rope-managed and
  unpenned; Didi the Labrador was bonded to Barracks resident Mara but had no
  installed assigned bed. The loaded Red Cube society showed no animal veneration.
  Academy may differ and remains a separate verification case; no universal
  veneration rule was inferred. Native call sounds created neither `GenClamor`
  nor semantic threat knowledge, and the absence of an authored Defense program
  left approach, beachhead, defended-side, and sentinel relations explicitly
  absent. No bed, pen, assignment, program, or construction changed.

  The critical-storage receipt observed four medical stacks, 2,000 Silver, 36
  Wood, and the two minified animal beds. Antibiotics remained in the dedicated
  Important one-cell Medical stores zone under natural roof at the dynamic 13.4 C
  sample. That is rational weather and category protection, not proof of an ideal
  strategic location; no authored Medical or Defense footprint and no
  better-priority accepted native destination existed. Historical legacy records
  now prove only native-zone origin, never who later moved or placed an item.
  Loose medicines, currency, wood, and animal beds were exposed without moving
  them or changing any native filter, priority, zone, designation, or program.

  The exact preplacement checkpoint is 10,941,875 bytes with SHA256
  `98706BF2A2063E7D43397BE5685F5DAA8BF637CA38EDBEFBCDB98D71BB08B58B`.
  The exact working checkpoint is 10,264,210 bytes with SHA256
  `D781B5884E1653C075DE93EB5FF81081EE1268B062CD0A21E127407B603F8696`;
  observation-only final reload left it byte-identical. The protected source
  baseline, `Autosave-5`, and derived autostart remained respectively
  `4C094DC9D5721FBFA68A8AAC3001355BCCDD4511F160EFF1C1108C017F73EA14`,
  `2B723B9D78B3CFA0958F9A55A021D31130BD1CE634749CE00BBA65D6B950B4E7`,
  and `7ADB83329FC52F6E08AA1A05EBBBDD82A21725F74F4CC38F17034300F439A09E`.

  The final real-reference Release build completed with 0 warnings and 0 errors.
  The 953,344-byte DLL SHA256 is
  `DF24C430490CCBD4AF4701337A1BC2FF48B6D905F19DFC1868837A60F6425D09`.
  A full restart produced the new welfare and zone-origin receipt wording and
  retained both completed room records, proving the final DLL was loaded. One
  isolated-profile RimWorld process remains open on the reloaded working
  checkpoint; no pointer, focus, position, size, or observation-save write was
  issued.

- **F-95** (2026-07-29 01:52 UTC / 18:52 PST) - Build `a2-94` closes the
  controlled room-authority regression gate under the final DLL. Three
  deterministic commands require the exact retained derived-save SHA before
  mutating the loaded test state; each pauses simulation, writes no save, and is
  followed by a native reload of the retained checkpoint.

  On the clean eight-bed preplacement, the fixture placed an ordinary
  player-faction Dresser blueprint at `(39, 0, 166)` through
  `GenConstruct.PlaceBlueprintForBuild`. Production room planning refused to
  originate CA work because one player-authored facility family was already
  under construction. `Blueprint.TryReplaceWithSolidThing` then performed the
  real native transition to a spawned `Frame`; the same production planner again
  refused parallel work. CA tracked neither object, placed nothing, and the
  temporary construction canceled cleanly.

  A separate reload placed exactly one tracked CA Dresser blueprint, then changed
  the real program from Barracks to Kitchen through
  `PlannedUseMapComponent.UpdateProgram`. The production census pruned that
  now-orphaned pending work: the blueprint was canceled, the program retained its
  new authored purpose with no sleep residents, all eight native beds survived,
  and no completed association appeared. Reload restored the exact 8/8 resident
  and bed-ownership checkpoint.

  On the retained working checkpoint, production `ClearCells` removed all 60
  cells of the authored program. Census retired both stale CA associations while
  both player-owned Dressers, all eight native beds, and all eight bed owners
  survived. Reload restored program #1 plus the two completed records, whose live
  expected-focus links remained 4/5 and 3/3. The preplacement and working save
  files remained byte-identical at 10,941,875 bytes / SHA256
  `98706BF2A2063E7D43397BE5685F5DAA8BF637CA38EDBEFBCDB98D71BB08B58B`
  and 10,264,210 bytes / SHA256
  `D781B5884E1653C075DE93EB5FF81081EE1268B062CD0A21E127407B603F8696`.

  The final real-reference Release build completed with 0 warnings and 0 errors.
  The 962,560-byte DLL SHA256 is
  `439FDC451E9BE1CBAE83ABA6AE167446491ABFBEF9D7298878773B2803D44ECE`.
  A full restart was required before the new a2-94-only commands produced all
  three receipts. One isolated-profile RimWorld process remains open on the
  reloaded working checkpoint; no baseline write, test-state save, pointer,
  focus, position, or size command occurred.

- **F-96** (2026-07-29 02:08 UTC / 19:08 PST) - Build `a2-95` closes the
  read-only multi-society animal-infrastructure gate and corrects the identity
  ambiguity left by the first animal receipt. The evaluator now reports player
  faction, primary and minority ideologies represented by spawned free colonists,
  each observed ideology's venerated species, present matching animals, assigned
  and effective masters, follow settings, bonds, sleeping, native containment,
  training, and defensive evidence as separate axes.

  Coalition of Erewhon's primary ideology was The Academy. All three map
  colonists venerated cats, huskies, labradors, and yorkshire terriers. Tess the
  Husky was the one present match: venerated by all three, bonded to and mastered
  by Denny, obedient, and assigned an installed sleeping spot. Her species did
  not require native rope management. The non-venerated Elk independently
  occupied one enclosed pen and had no assigned bed. There was no authored
  Defense program, mech charger, or semantic alarm/report channel.

  The developed contrast was The Sixth-Seventh Reich with Red Cube as its
  primary ideology for four of seven map colonists and three distinct minority
  ideologies. Red Cube venerated cats, horses, huskies, labradors, monkeys, and
  yorkshire terriers. Five matching horses were present, and every one remained
  rope-managed inside an enclosed native pen. The full settlement had 15 player
  animals, 14 rope-managed animals, seven enclosed pen markers, and 19 installed
  animal beds with 12 assigned. Non-venerated animals retained the same
  independent containment, sleeping, care, and topology evidence. This is the
  decisive counterexample: veneration materially constrains hunting, slaughter,
  food use, and loss without canceling pen eligibility.

  The earlier F-94 phrase "loaded Red Cube society showed no animal veneration"
  conflated fixtures. The exact Dolly working checkpoint is player faction New
  Arrivals with primary ideology Astropolitan; all eight residents follow it and
  it venerates no species. Its original no-veneration observation was real, but
  it was not evidence about Red Cube. Build `a2-95` makes faction, ideology, and
  observed free-colonist membership explicit so that attribution error cannot
  recur.

  The aboveground Academy, developed mountain, and restored Dolly working files
  remained byte-identical at respectively 9,665,660 bytes / SHA256
  `0DBB30070B57D9066E0C9AB5BB3F4B90BFE4410361821C8CEBEC0FC39583CA5F`,
  20,267,493 bytes / SHA256
  `10FA15C38D7D2D33D9C1F11B1E99215F68ADB3BCDCC409DC8337243B830FC422`,
  and 10,264,210 bytes / SHA256
  `D781B5884E1653C075DE93EB5FF81081EE1268B062CD0A21E127407B603F8696`.
  The final real-reference Release build passed with 0 warnings and 0 errors;
  the 988,160-byte DLL SHA256 is
  `DD8EBA38BA7E2E6DA26CE2B4272FAC0EE63C40467F1F0114853EC31B53B1DF90`.
  Final-DLL receipts followed a full restart and produced the new a2-95-only
  society and master evidence. One isolated-profile process remains open on the
  restored 8/8 Dolly working checkpoint; no observed save, pointer, focus,
  position, or size command occurred.

  The focused quality panel reported no Critical, High, or Medium finding. Its
  two Low findings were resolved before commit: documentation now names the
  actual spawned free-colonist group, and redundant animal-prefixed save-load
  aliases were removed in favor of the existing fixed fixture loaders plus the
  separate evaluator command.

- **F-97** (2026-07-29 03:32 UTC / 20:32 PST) - Build `a2-96` closes the
  read-only authored Bedroom requirement-comparison gate under the final DLL.
  The evaluator observed only exact Player-authored Bedroom programs and never
  selected, painted, normalized, furnished, or authorized a test room.

  The controlled 49-cell Bedroom had two Player-authored residents, reciprocal
  spouses Luc and Dolly, one native two-owner DoubleBed, a positive native
  Bedroom worker result, and current room role Bedroom. Present readiness passed.
  Three loaded optional facility definitions exposed unused native link capacity,
  research, technology, material-count, and spatial opportunity evidence, but
  none became a requirement or construction order. Beauty and dependent room
  stats remained telemetry. The functional room produced no pending work and its
  initiative consumer stayed hidden.

  The developed 174-cell mixed-room contrast retained seven native beds, eight
  slots, and seven native owners while the transient authored Bedroom roster
  remained empty. Native role stayed Barracks and Bedroom bed/owner composition
  failed. This proves that native ownership does not populate Player-authored
  residency and that future optional capacity cannot repair or conceal present
  functional conflict. The aboveground save exposed zero Player-authored Bedroom
  programs and remained explicitly unselected and unpainted.

  Safety review found that legacy Bedroom metadata could outlive current
  origination authority and that a failed fixture setup initially restored too
  little state. The final implementation makes Bedroom records inert on the
  construction path while preserving native player objects. Its transactional
  cleanup restores claimed cells, both exact prior beds, both prior program
  memberships, every distinct prior roster author, and removes the fixture bed.
  A two-phase post-membership ownership correction releases both unexpected
  claims before either exact snapshot is reclaimed, covering prior-null,
  outside-program, distinct, swapped, and shared-bed cases.

  The retained functional checkpoint is 10,271,501 bytes with SHA256
  `F9512EA693BFC8717FD66E557598F32C3D578D8F554D6D8187263654A7ECE8E7`.
  Developed, aboveground, Dolly working, autostart, and the protected operator
  baseline retained their established hashes. The final real-reference Release
  build passed with 0 warnings and 0 errors; the 1,029,632-byte DLL SHA256 is
  `796586F06F8ABDCAB9EF75288882E3AA7E76BE6467C50123AD1FBB917417F476`.
  Final-DLL receipts followed a full restart. One isolated-profile process
  remains open on the restored 8/8 Dolly working checkpoint; no pointer, focus,
  window geometry, baseline overwrite, or observation-save write occurred.

  Independent build, correctness, structural, and coherence review ended with no
  unresolved Critical, High, or Medium issue. The rollback High and follow-on
  Medium findings were both resolved before commit.

- **F-98** (2026-07-29 04:25 UTC / 21:25 PST) - RimWorld 1.6 exposes pawn-only
  debug teleportation and the native Reinstall designator for minifiable
  buildings, but no generic God Mode item mover. Build `a2-97` supplies the
  missing item-only seam through low-priority map input: Ctrl-left-drag preserves
  native short-click selection, resolves the top native hit, activates only after
  a five-pixel drag, and consumes only the owned drag and release events. Every
  drag event derives its map cell from the IMGUI event position; independent
  secondary-pointer dragging is not claimed.

  The initial review found that a lost release could leave a stale gesture, a
  same-map external item move could stale the recorded source, and a modded item
  definition could theoretically affect regions. The final implementation cancels
  on lost hold, pointer-window exit, application focus loss, map or source change,
  despawn, and authority loss; activates only on `MouseDrag`; and excludes
  `def.AffectsRegions`. Same-cell release is a silent no-op. Invalid destinations
  leave the item untouched. Successful relocation preserves the exact `Thing`
  identity and count while refreshing location-dependent storage, room, hauling,
  mergeable, region, thing-grid, cover, and destination steal-debug evidence.

  The coherence pass then found that native `GenSpawn.CanSpawnAt` defaults to
  allowing conflicts that a real spawn would wipe, while direct relocation owns
  no wipe lifecycle. The final destination gate additionally calls
  `WouldWipeAnythingWith` and rejects every conflicting object other than the
  source item. Compatible storage furniture remains valid. Same-cell release is
  tested before destination validation and therefore remains an absolute silent
  no-op.

  The final real-reference Release build passed with 0 warnings and 0 errors. The
  1,035,776-byte DLL has SHA256
  `15D1C779791C3E3C9115E4B520780701B32B1755A6336C66FAF4DE2EC5235674`.
  Independent correctness and build validation left no Critical, High, or Medium
  issue. One accepted Low is limited to possible stale source-side cells in the
  optional steal-AI debug overlay; gameplay and destination evidence are current.

  Runtime activation remains deliberately pending a safe full restart. The live
  Temp-profile process continues to hold the operator's in-memory debug-base
  expansion, while the separate `Debug Base Expansion.rws`, the Dolly working
  checkpoint, and the protected operator baseline were not overwritten. No
  pointer, focus, window-position, window-size, or process-control command was
  issued.

- **F-99** (2026-07-29 05:21 UTC / 22:21 PST) - The first operator interaction
  attempt falsified `a2-97`'s practical drag claim for a loose weapon. The weapon
  itself was valid: loaded base weapon definitions are selectable
  `ThingCategory.Item`, exactly matching the mover's eligibility gate. The
  failure was input starvation. RimWorld's `UIRoot_Play` sends map clicks to the
  selected designator and targeter, then to a persistent `DebugTool`, before
  `MapInterface.HandleLowPriorityInput`; each consumer may use left MouseDown.
  The mover ran only at that later seam and independently refused to arm while
  any of those tools was active. Spawn Thing remains active until canceled, so
  spawning a weapon and immediately dragging it is an engine-deterministic dead
  path in `a2-97`.

  Two narrower admission defects could compound it. `clickCount == 1` could
  reject an immediate post-spawn drag classified as a later click in the same
  sequence, and checking only `ThingsUnderMouse()[0]` allowed an overlapping
  pawn or building to mask an otherwise eligible weapon.

  Build `a2-98` moves gesture ownership to a first-priority prefix on
  `MapInterface.HandleMapClicks`, the grounded seam after window handling and
  before designator, targeter, debug-tool, and selector consumption. It claims
  only Ctrl-left on an actual eligible item, consumes the complete owned mouse
  gesture, preserves a competing tool across an activated drag, searches the
  ordered native hit list for the first eligible item, and removes click-count
  from admission. Short press selection and pre-activation cancellation remain
  explicit and separate from an activated drag.

  Final source and coherence review found no unresolved Critical or High issue.
  The real-reference Release build passed with 0 warnings and 0 errors; the
  1,035,776-byte DLL SHA256 is
  `898164B881234BFA7572BE437F3E9A8E6173345C84337C52081623B3DB0CC1AB`.
  No successful `a2-98` interaction is claimed until the restarted process
  produces an operator-visible move and the existing exact item receipt.

- **F-100** (2026-07-29 05:24 UTC / 22:24 PST) - The corrected `a2-98` DLL is
  runtime-active without recreating the prior unsafe auto-load condition. A full
  isolated-profile restart produced one `a2-98` build stamp, zero automatic-load
  events, and zero relevant startup exceptions; the process became responsive at
  the main menu. Because no save was loaded and no pointer interaction was used,
  this proves artifact activation and startup safety only. Loose-weapon movement
  remains explicitly unproven until the operator performs the Ctrl-drag in a
  deliberately loaded game and the exact move receipt appears.

- **F-101** (2026-07-29 23:12 UTC / 16:12 PST) - The apparent cursor-independent
  drag success was falsified by its required negative control. A marked sidecar
  sequence first moved exact `Gun_Autopistol84354`, stack 1, from `(42, 0, 170)`
  to `(42, 0, 171)`, but releasing the same synthetic gesture at logical UI
  `(240.829224, 20)` then moved it behind the colonist bar to `(42, 0, 184)`.
  `EventType.Used` is not a sufficient HUD-boundary signal for that path. The
  process was immediately closed with saving blocked, and the fixture remained
  byte-identical. The positive sequence was therefore not accepted as drag
  proof.

  Final build `a2-99` rejects all marked synthetic pointer events and keeps native
  physical Ctrl-left-drag as the sole gesture. Native source and release points
  now fail closed over the live WindowStack and conservative known-vanilla HUD
  exclusions. Cursor-independent testing instead invokes the exact stable-ID
  move transaction directly on RimWorld's main thread. In the deliberately
  loaded `Debug Base Expansion` fixture, a same-cell request for
  `Gun_Autopistol84354` failed and left it at `(42, 0, 170)`. The next request
  moved that same ThingID to `(42, 0, 171)` and receipted map 0, definition
  `Gun_Autopistol`, stack count 1 before and after, spawned state true,
  destination membership true, and source membership false. An independent
  scene snapshot found the same ThingID at the destination. A generic synthetic
  Ctrl-drag toward the top HUD was refused and changed nothing.

  Independent static review found no native-gesture or exact-transaction blocker.
  The canonical real-reference Release build passed with 0 warnings and 0 errors;
  its 1,044,992-byte DLL has SHA256
  `9EE258C5738D4C67034CCB678E0FCF7C1A9AE30B46531FBD72D930632C28BABD`.
  `Debug Base Expansion.rws` remains 10,303,006 bytes with SHA256
  `AC5F461593AA3676F72AF3DAABE3AC4EC070E4D49F6F09F5BFA205DF06FE968F`,
  active `autostart.rws` remains absent, and no RimWorld process remains. The
  shared move mechanics are runtime-proven; operator judgment of the native
  physical gesture's look and feel remains separate.

- **F-102** (2026-07-30 01:35 UTC / 18:35 PST) - The native MouseMux proof
  required transport and application evidence to agree; neither a sent packet
  nor a successful exact-item command was accepted as UI-gesture proof. The
  first hardened adapter run failed safely before any mode transition or input
  after revealing that MouseMux 3.0.7 completes a read-only mode query with
  `server.mode.notify.M2A`, not an echoed request ACK. The next transaction
  confirmed all 19 input requests and restored native mode, but the exact item
  remained at `(42, 0, 170)`: the game log proved a valid native drag had been
  canceled when Unity's global left-button state changed between Red-seat
  packets. A subsequent run further showed that classification could remain
  pending when the first valid drag saw global-left true and then expire between
  packets. Both failures closed unsaved with the fixture unchanged.

  Final build `a2-105` keeps Control authorization and hold continuity separate.
  In the clean isolated process, the native log recorded unconsumed LeftControl,
  exact `Gun_Autopistol84354` at `(42, 0, 170)`, `control=True`,
  `controlBridge=False`, `holdBridgeCandidate=True`, promotion on a valid native
  drag, and the exact successful transaction to `(42, 0, 171)`. The postcondition
  preserved ThingID and stack count 1, kept the item spawned, found it in the
  destination thing grid, and found it absent from the source. Independent scene
  snapshot `0:188600:30953` agreed, while `Gun_Autopistol84355` remained at
  `(43, 0, 171)`.

  The final adapter transaction confirmed 19 of 19 normal input requests, no
  cleanup request was required, neither left nor Control remained possibly down,
  exact native mode restoration succeeded, and logout was acknowledged. The
  server then closed without a WebSocket close frame; that post-logout transport
  detail is receipted separately and is not an input failure. The adapter is
  58,890 bytes with SHA256
  `665A35755062DA7453DBA2FAB64DE0D39517E34C487EEFB9B8FD23C1D74A66F9`.
  The final 1,048,064-byte DLL has SHA256
  `BC73B1C36E457DDF5E6F4E54AC89AB5007BC89ABB7AB5647F4ABAD4FCD7B642F`
  and passed the real-reference Release build with 0 warnings and 0 errors.
  The test process closed with saving blocked; `Debug Base Expansion.rws`
  remained 10,303,006 bytes with SHA256
  `AC5F461593AA3676F72AF3DAABE3AC4EC070E4D49F6F09F5BFA205DF06FE968F`,
  `autostart.rws` remained absent, and no RimWorld process remained.

- **F-103** (2026-07-30 08:11 UTC / 01:11 PST) - Spatial evidence must be
  event-time evidence. Reading a pawn's current position while reviewing an old
  combat line confounds where the event occurred with where the pawn survived,
  fell, fled, or was carried afterward. Build `a2-110` therefore records exact
  actor, target, contact, destination, anchor, BattleLog concern, and vanilla
  Bullet-impact cells at the producing seam. Missing or preexisting evidence
  stays missing; no later location is substituted.

  Static review of the same raid-driven combat changes found and closed four
  execution families before commit: a permanent player-forced Hold posture that
  blocked its own duty; constant-duty fallback that interrupted native weapon
  warmup and needs; new Hold orders that lost to force-complete or identical old
  jobs; and survival release that could re-enter job selection. Automatic defense
  retains constant post-taking because it has no needs subtree, while Hold uses
  constant thinking only for target reacquisition. Deterministic multi-contact
  selection prevents one stale freshness-ranked contact from masking a currently
  engageable melee threat.

  The real-reference build and independent reviews are clean. The final DLL is
  1,155,584 bytes with SHA256
  `7F933E41A885BDFB22C0D3805D4F99349F55193202FEA7B40DCF8066D95A5BB5`.
  Runtime behavior remains unproven until a natural full restart loads `a2-110`;
  the normal in-progress game was intentionally not manipulated to obtain it.

- **F-104** (2026-07-30 17:12 UTC / 10:12 PST) - The repeated assault separates
  positional initiative from Moving Fire. The defenders had Moving Fire off and
  were largely placed by the player in a limestone ruin whose walls, firing
  angles, and surviving cover made staying planted correct. Small movement would
  become useful only after evidence changed, such as destroyed cover, a lost
  firing angle, a blocked friendly lane, or local collapse. Static defense in a
  good position is not itself a failure to react.

  Generic `AssaultColony` pawns already enter the actor-neutral combat-reaction
  lane, so their uncommanded use of ruins, ancient warwalker feet, and APCs is the
  cleaner autonomy observation from this run. Drafted colonists cannot enter the
  same RimWorld constant-think lane; build `a2-111` supplies a separate 30-tick
  pump that honors direct and queued player work, withdrawal, concealment,
  emergency work, current movement, and committed weapon cycles before calling
  the shared position evaluator. The evaluator starts with the current cell and
  issues no job unless a concrete axis and the overall threshold both improve.

  Moving Fire remains a different question: whether a flagged pawn may complete
  fire during locomotion that another owner already justified. The solo
  suppressive and covered bounded withdrawal modes deliberately use it; ordinary
  defense does not require it. The final source and real-reference build are
  clean, but the running game log still identifies `a2-110`. No `a2-111` runtime
  behavior is claimed before a natural restart.

- **F-105** (2026-07-30 19:18 UTC / 12:18 PDT) - The terminal assault evidence
  shows real hostile movement initiative but cannot yet explain the whole
  battle. In the exact `a2-110` interval, Skag repeatedly improved ground and
  then executed fourteen harm-driven break contacts, while Gwugwuup combined
  four improvements with eleven break contacts under pressure reaching 1.00.
  Ditch broke contact twice. That is compatible with the operator's observation
  that the hostile side used movement more effectively and that one attacker
  escaped, but the log does not identify the escapee or prove each traversed
  route.

  The trace also exposes its present diagnostic limits. Protective-apparel skip
  receipts consume 428 of 536 actor-tagged lines, and twenty-six repeated weapon-
  report investigations dominate the last logged phase. Neither is evidence
  that medical work was causally blocked. There are zero explicit shot, hit,
  death, bleeding, or tending lines in the interval, so the observed many
  bleeding pawns and lack of patching cannot be quantified from this log. The
  next run needs topology, impacts, conditions, and recovery events in one
  time-aligned evidence surface before another combat behavior is authorized.

  Build `a2-112` supplies the missing epistemic map layer without pretending to
  reconstruct this completed fight. Cells become known only near involved pawns
  or through exact weapon-impact sensor bursts; structures require fully seen
  footprints; current hazards require current sight. This preserves the
  difference between accumulated combat knowledge and omniscient map state.

- **F-106** (2026-07-30 20:27 UTC / 13:27 PDT) - The first live `a2-112`
  engagement exposed an entry-order defect rather than a topology-capacity
  shortage. `Verse.Battle.Add` inserts the new log entry at index zero;
  `a2-112` inspected the tail, failed to recover `Battle_0`, and treated each
  qualifying event as a new incident. The five-incident cap then evicted 614
  singleton incidents, leaving only Luc's terminal volley instead of a
  continuous after-action record.

  Build `a2-113` associates the exact head entry through both native lookup
  paths and adds a deterministic newest-entry regression. This restores DR-74's
  native-incident continuity contract without changing gameplay or evidence
  authority. The completed `a2-112` battle remains incomplete evidence and is
  not reconstructed from its terminal state. A fresh multi-entry engagement is
  still required to prove one populated battle alias, one continuing map-bound
  incident, multiple retained LogIDs, and no per-event capacity eviction.

- **F-107** (2026-07-30 21:07 UTC / 14:07 PST) - The first interactive combat
  graph failed by conflating two different kinds of truth. Pawn knowledge is
  legitimately partial and time-local, but the battle's terrain, roofs, walls,
  doors, ruins, rock, water, and durable cover are a stable analyst reference.
  Rendering the latter only where a pawn happened to reveal the former erased
  the map's recognizable shape and made movement, cover, angles, and the
  operator's positioning decisions difficult to read.

  Build `a2-114` separates those layers. The full static reference persists
  across the timeline, genuine construction, destruction, terrain, roof, and
  gravship-grid changes apply by tick, and transient combat state remains pawn-
  proximal. Static changes neither add known cells nor authorize an AI decision.
  The change stream has its own cap and receipt, so a dense terrain event cannot
  silently consume dynamic combat evidence.

  The completed pre-v3 battle cannot supply this missing reference after the
  fact: using its terminal map would misdate destroyed or added geometry. The
  next fresh engagement must prove one continuous native incident and the v3
  reference together before its aggregate visualization is treated as a vivid
  account of what happened.

- **F-108** (2026-07-30 21:41 UTC / 14:41 PST) - The fresh `a2-114` engagement
  was a tactical loss, a material behavioral improvement, and still inadequate;
  those are three separate findings. The operator's outcome judgment remains
  authoritative. Native evidence establishes Soren's death, Cardenas's and
  Luc's collapses, Lenka's and Crush's deaths, and continuing Brant-Skag fire at
  the export frontier. It does not contain a terminal victory or defeat marker,
  so casualty counts neither override the operator nor convert the run into a
  victory by arithmetic.

  The apparent tick mismatch was a clock-domain distinction, not another
  session. Behavior trace prefixes use `TicksGame`; native `LogEntry.Tick` and
  topology reference changes use `TicksAbs`. This save persists a 17,500-tick
  absolute-game anchor, so the first native shot at absolute tick 378349 is game
  tick 360849, after hostile contact. The same conversion aligns the remainder
  of the native event stream with the observed behavioral interval.

  One operator-visible engagement legitimately arrived as two overlapping
  RimWorld native Battles. Unlike the `a2-112` defect, each event remained under
  a stable native alias and map-bound topology incident, all 472 events survived,
  and capacity eviction stayed zero. The truthful after-action surface therefore
  composes both event streams chronologically while preserving their provenance;
  it does not call them tactical phases or rewrite them into one engine record.

  The v3 reference is live-proven for a recognizable persistent map and ten
  thing spawn/removal changes. Both incident references converge on the same
  final artifact set, including changes that never leaked into pawn knowledge.
  No terrain or roof cell changed in this run, and no reload export was taken;
  those hooks remain structurally tested but not live-proven.

- **F-109** (2026-08-05 06:38 UTC / 23:38 PDT) - A person carried only THREE
  political-belief memories at a time, and the engine silently destroyed the words of
  every judgment past that. Verified against the decompile:
  `MemoryThoughtHandler.TryGainMemory` calls
  `Thought_Memory.TryMergeWithExistingMemory`, which - once `NumMemoriesInGroup`
  reaches `stackLimit` - renews the OLDEST memory and returns true, so the new
  `Thought_CAPoliticalBelief` (carrying its own detail string) was never added and
  was discarded with its text. `CA_PoliticalBeliefViolated` and
  `CA_PoliticalBeliefUpheld`
  both declare `stackLimit` 3, `durationDays` 3.

  This was an architectural constraint on every political-belief rule's pawn
  effects, not a fact about protection beliefs alone. The engine's `stackLimit`
  guarantees mood cannot run away and the memory list cannot bloat, but it also
  means the individual leg is a three-slot channel with a three-day window: a
  family that judges a frequent act jams it, and the jam is silent. Any new
  family must ask how often its row returns a non-Silent verdict before asking
  what the verdict should be. Probed: a 63-hit firefight judged by a
  combat-reading row discards 60 of 63 judgments and evicts an atrocity
  witnessed beforehand; the same firefight judged by a row silent on
  `mutual-combat` carries all three atrocities intact.

- **F-110** (2026-08-05 06:38 UTC / 23:38 PDT) - The shipped violence emitter
  described EVERY knockdown in a fair fight as striking the fallen. The
  circumstance branch read `victim.Downed` inside the `Notify_DamageTaken`
  postfix, but the engine has already applied this blow's downing by then:
  `DamageWorker` applies hediffs through `Pawn_HealthTracker.AddHediff` ->
  `CheckForStateChange` -> `MakeDowned`, and only afterwards does
  `Pawn.PostApplyDamage` call `mindState.Notify_DamageTaken`. A raider shot down
  in an ordinary exchange therefore emitted `struck-downed` - the atrocity word
  - and a people holding `the beaten are spared` would have been outraged by
  routine defensive combat.

  Corrected by stamping when a pawn actually went down (`CAViolenceSite`, fed by
  a postfix on the private `Pawn_HealthTracker.MakeDowned`) and asking whether
  they were already down BEFORE this tick. A stamp from the current tick means
  this blow is what put them there.

  The same reading exposed a second gap: the killing blow emitted nothing at
  all, because `Pawn.PostApplyDamage` returns at `if (Dead)` before notifying a
  mind that is already dead. Executing a downed captive - the canonical act a
  quarter norm exists to condemn - was invisible. Closed with a PREFIX on
  `Pawn.Kill`, which is exactly right rather than merely convenient:
  `CheckForStateChange` tests `ShouldBeDead()` BEFORE `ShouldBeDowned()`, so a
  pawn killed outright reaches `Kill` having never been downed, and `Downed`
  read in the prefix is precisely "was already beaten when someone ended them".
  Lethality rides as a separate neutral fact (`lethal`) because circumstance
  says what the person WAS and lethality says what the blow DID.

- **F-111** (2026-08-05 06:38 UTC / 23:38 PDT; superseded by DR-91 and DR-92) - The former political def
  surface was quarantined where it could never load. `Precepts_CAConventions.xml`
  and `Thoughts_Convictions.xml` sat in `Defs/BadHygiene/`, a folder deliberately
  absent from the live mod tree so the unfinished hygiene needs cannot boot.
  Neither file has any hygiene dependency - the precepts reference only vanilla
  issues and memes, the thoughts reference only `Thought_CAConviction`. Every
  custom-precept family was therefore unreachable in any deployed build, and would
  have stayed so for as long as the needs stayed unfinished. Moved to
  `Defs/PreceptDefs/` and `Defs/ThoughtDefs/`, which makes the separation
  structural rather than a thing someone has to remember at deploy time.

- **F-112** (2026-08-05 07:34 UTC / 00:34 PDT) - **Supersedes the constraint
  half of F-109.** The three-memory political-belief ceiling was not a fact to design
  around; it is now lifted, and F-109 should be read only as the record of what
  the ceiling destroyed and how it was discovered.

  `ThoughtDef.stackLimit` has exactly two consumers in the engine (every other
  `stackLimit` in the decompile is `ThingDef.stackLimit`, unrelated):
  `MemoryThoughtHandler.TryGainMemory`'s trim loop, which runs only when
  `stackLimit >= 0`, and `Thought_Memory.TryMergeWithExistingMemory`, which is
  **virtual**. So no Harmony patch was needed - the extension point is a virtual
  method on the thought class the mod already owns.

  THE TWO CHANGES ONLY WORK TOGETHER, and this is the trap. Setting
  `stackLimit -1` alone makes things strictly WORSE: the inherited merge tests
  `NumMemoriesInGroup(this) >= def.stackLimit`, and every count is `>= -1`, so
  it would renew the oldest and never add - collapsing every grievance a pawn
  ever forms into a single memory. Probed: five distinct atrocities become one.
  `Thought_CAPoliticalBelief.TryMergeWithExistingMemory` is what replaces
  the policy; the def value only switches off the trim so the override's
  decision is final.

  Mood is bounded WITHOUT a hand-written cap, by vanilla's own mechanism.
  `ThoughtHandler.MoodOffsetOfGroup` computes `mean(offset) x (1 + m + m^2 +
  ...)`, so with `stackedEffectMultiplier` 0.75 a group converges to exactly
  four times one memory no matter how many it holds. Measured: 40 grievances
  under one political belief reach -12.00 against a convergent bound of -12.00, where
  the old three-memory cap gave -6.94. Grouping by political belief rather than
  by def makes that the right bound: repeated offenses against one belief
  saturate, while another belief registers on its own. Four fully violated
  beliefs total about -39 mood. That is a colony
  in moral collapse and is reported rather than capped away.

  What remains true from F-109: a family must still ask how often its row
  returns a non-Silent verdict. The reason changed - no longer "the channel
  jams", now "memories are real objects that persist for their duration and
  a group's mood saturates" - but frequent-act families are still a design
  question, not a free lunch. The standing rows stay silent on `mutual-combat`
  on the separate grounds of DR-77, which never depended on the cap.

- **F-113** (2026-08-05 08:26 UTC / 01:26 PDT) - "One judgment per pawn per
  event" was collapsing several independent issues into one, on BOTH legs.
  A single act can break several unrelated commitments -
  beating an unresisting prisoner implicates protection of the helpless,
  prisoner treatment, legitimate punishment and authority over coercion at once
  - and vanilla's rule that a pawn cannot hold contradictory precepts WITHIN an
  issue says nothing about an act being judged ACROSS issues.

  Pawn leg: `JudgeForPawns` broke out of the custom loop after the first
  non-Silent verdict, so a pawn holding four relevant political beliefs felt
  exactly one. Fixed by removing the break. The loop is already bounded to one
  judgment per axis by construction: `CustomsHeldBy` derives at most one custom
  from each saved political-belief axis.

  Organization leg had the same defect in a different shape:
  `JudgeForOrganizations` added the organization to `orgsAnswered` inside the
  custom loop on the first non-Silent verdict, so the next custom hit the
  already-answered guard and never spoke. Fixed by marking the org answered
  once, before the pass, for any INFORMED org. That also closes a second-order
  cost: an organization whose customs were all silent was never marked at all and
  re-judged every retained event on every ledger tick for the whole fifteen-day
  retention window.

  Consequence to watch in play: an act that breaks two of an organization's
  customs now costs `onset` twice. That is the intended reading - two
  commitments broken is two - but it is a real multiplier on legitimacy loss
  that only shows up once several families coexist.

- **F-114** (2026-08-05 08:26 UTC / 01:26 PDT) - `MoodOffsetOfGroup` uses the
  MEAN of a group's offsets, so mixed offsets inside one group let a MILD
  grievance REDUCE the penalty of a severe one. confirmed
  by arithmetic against the decompiled formula: one memory at -10 gives -10.00,
  and adding a single -1 memory to that group gives -9.625. The colony gets
  happier because something else bad happened.

  CA is immune, but only because four conditions hold, and they are now checked
  structurally rather than assumed: each political-belief ThoughtDef has one
  stage (so `BaseMoodOffset` cannot vary by `CurStageIndex`); `lerpMoodToZero`
  is unset (or offsets would decay with age and a group would mix old with
  fresh); `effectMultiplyingStat` is unset; and no code path sets
  `moodPowerFactor`, `moodOffset` or `SetForcedStage` on a political-belief memory.
  `GroupsWith` additionally requires the same def, so offended (-3) and upheld
  (+2) can never share a group. `def_check.py` asserts all of it, so a future
  family that reaches for a per-memory multiplier fails the check instead of
  silently inverting the mood arithmetic.

- **F-115** (2026-08-05 08:26 UTC / 01:26 PDT) - Growth after the ceiling is
  bounded by EXPIRY and semantic merging, not by any cap, and the bound is
  arrival-rate x duration. Probed: three distinct grievances a day for thirty
  days converges at nine memories, never accumulating thirty days of history;
  one wrong struck fifty times a day for ten days stays at three memories with
  500 merges counted rather than dropped.

  The case the design does NOT protect against, stated plainly: a family whose
  row judges a frequent act would grow to rate x duration - sixty distinct
  grievances a day reaches 180 memories on one pawn in five days. That is the
  live reason DR-77 keeps the standing rows silent on ordinary combat. The
  reason has changed since F-109 (it was "the channel jams", it is now "the
  channel grows") but the design question a new family must answer is the same.

  NOT YET DEMONSTRATED IN GAME, and named as the open gap: many pawns, many
  political beliefs, repeated incidents, save/load, mood-tab rendering and a
  long-lived settlement. `Source/PoliticalBeliefStressModule.cs` ships the
  instruments for it - a read-only census that checks the scribe, merge, group
  and bound invariants per pawn, and two synthetic stress actions (40 distinct
  acts; 40 repeats of one act) that drive the REAL ledger and the REAL
  evaluator rather than an imitation. Dev mode only, ships inert. The run is
  the operator's.

- **F-116** (2026-08-05 08:58 UTC / 01:58 PDT; superseded implementation,
  current closure 2026-08-09) - The regional setup screen had no
  scope model, and the defect is not labelling - it is that **scope is not
  contiguous**. audited in full at `SETUP_SCOPE_MAP.md`.

  CURRENT CLOSURE. The former screen and its mixed-scope backing model were
  removed. Current setup is organized around the region, factions,
  settlements, and the landing area, while world tendencies and the player
  start have their own surfaces. The historical field and type names below
  identify the discarded prototype; they are not current contracts.
  Five scopes (world, region, faction group, settlement/site, player start)
  interleave down one scroll column, and two of them appear more than once:
  player start at `Founding` and again inside `Water`; world in the `Water`
  sliders, again in `World rules`, and again in `World policy - later regions`.

  Three conflations proven in code, not inferred. `Founding` writes
  `plan.founding`, read only by `FoundingCompactModule` to build the PLAYER's
  starting relations, but is drawn immediately after the faction-group loop so
  its nearest visible context is that group's name field. `Water` puts a
  player-start read (`CAGroundwater.SurveyTile(plan.startTileId, ...)`) above
  four world-scope sliders under one heading. `plan.worldPolicy` carries both
  everywhere-fields (`cabinShare`, `patrolsEngage`, `piersGenerate`,
  `caLiveliness`) and later-regions-only fields (`populatedRegionChance`
  through `maximumTechLevel`) on one object, split by section header alone.

  NO DEAD CONTROLS. Every visible control writes a field a consumer reads. An
  earlier pass suspected `patrolsEngage` and `piersGenerate` were orphaned;
  they are read through the `CAWorldRules` static facade, which the first grep
  excluded because the facade sits in the same file as the toggles. Recorded so
  the rebuild does not delete them as dead. The failure is controls without
  visible SUBJECTS, which is harder precisely because no code is wrong.

  Two structural facts the audit surfaced that were not visible from the
  screen. `plan.startTileId` - the subject of the water survey and the place
  the founding applies to - is not editable in this dialog at all; it lives on
  the outer panel, so player-start scope spans two surfaces with no
  cross-reference. And `IdeoConventionsModule` reads four founding fields:
  an unauthored founding is SHAPED BY the CA precepts, making this the adopted
  layer beneath the ideology layer. The four questions are the same four axes
  as the precepts. The UI says none of it.

- **F-117** (2026-08-05 08:58 UTC / 01:58 PDT; current closure 2026-08-09) - Naming and history gap at
  setup. Faction names: `group.customName` is a bare
  TextField applied as `faction.Name`; left empty, native generation names the
  faction and the player never sees the result. No generator, no reroll,
  despite `FactionDef.factionNameMaker` being exactly what vanilla uses.
  Settlement names ARE generated - `GenerateSettlementName`
  (`RegionalWorldModule:1027`) uses `faction.def.settlementNameMaker` with
  used-names passed for uniqueness - but only at world generation, after the
  dialog closes, so the player places settlements they cannot name, see, or
  reroll. History and backstory: nothing at any scope, for factions or places.
  That belongs to the roadmap's NPC settlement synthesis rather than bolted
  onto setup.

  CURRENT CLOSURE. Current faction and settlement names are generated, visible,
  editable, and rerollable before confirmation. Generated history remains a
  later settlement-synthesis concern. The old field names above are historical
  evidence only.

- **F-118** (2026-08-05 09:41 UTC / 02:41 PDT; superseded by DR-91 and DR-92) - **The political-belief-versus-founding-arrangement
  comparison shipped in build `scope-1` could never have worked where it was
  drawn, and the cause is page ordering in the vanilla start flow.**

  `Scenario.GetFirstConfigPage()` builds the chain in this order:
  `Page_SelectStoryteller` -> `Page_CreateWorldParams` ->
  `Page_SelectStartingSite` -> `Page_ChooseIdeoPreset` -> ScenPart pages
  (`Page_ConfigureStartingPawns`) -> `InitGameStart`. CA's entire setup surface
  hangs off five Harmony patches on `Page_SelectStartingSite` and nothing else,
  so the comparison was drawn one page BEFORE the ideoligion is chosen. Worse,
  `Page_ChooseIdeoPreset` calls `Faction.OfPlayer.ideos.RemoveAll()` then
  `SetPrimary(...)` - it replaces the ideoligion wholesale. Any precept the
  comparison read was both unchosen and about to be destroyed.

  The operator's "absurd text" about founders holding no settled political beliefs was
  therefore the correct output of a comparison placed one page too early - not a
  copy defect and not a missing-precept defect. `setup_probe.py` had proved the
  arithmetic right in all 24 states and consistent with `ShapeDefault` across 81
  ideoligions, and every one of those receipts was about logic running at the
  wrong moment. Internally correct, wrong system boundary - the same error as
  F-116's audit, one layer down.

  Fixed by moving the reading to `FoundingCompactModule.OnNewGame`, which runs
  at `GameComponentUtility.StartedNewGame` - after the ideoligion, the founders
  and the site all exist. Only departures are recorded; agreement between what a
  people holds proper and what they adopted is not an event.

- **F-119** (2026-08-05 09:41 UTC / 02:41 PDT) - **CA has exactly one door into
  the colony-creation flow, and every misplacement follows from it.** Five
  Harmony patches on `Page_SelectStartingSite` (`PostOpen`, `DoWindowContents`,
  `ExtraOnGUI`, `CanDoNext`, `DoNext`); nothing on scenario, storyteller, world
  params, ideology, or pawns. Every CA start decision was forced through the
  tile page because that is the only opening CA made. The modal's shape was
  never a design - it was an accumulation at the single available hook.

  The sanctioned alternatives, verified: `ScenPart.GetConfigPages()` with base
  `ScenPart_ConfigPage` is how vanilla itself injects a config page (that is how
  `Page_ConfigureStartingPawns` enters the chain), and per-page patches are how
  to add a control to a vanilla page that already owns the subject. Full
  stage-by-stage catalogue in `START_SURFACE_MAP.md`.

- **F-120** (2026-08-05 11:18 UTC / 04:18 PDT; superseded by DR-91 and DR-92) - **Five settlement axes were one
  integer plus noise, and ideology was a display string.** Operator-directed
  audit of the generation model rather than the interface. The interface could
  never have been made usable because there were barely any discrete choices
  underneath it.

  The chain, verified: `FactionDef.techLevel` decided technology;
  `CAInstitutionalBirth.TechTier` mapped it to three tiers;
  `CAMorphologyAdapter.FormFor` mapped those tiers 1:1 onto three settlement
  forms; `CapabilityBasis` mapped it to `clamp(techLevel-1, 0, 5)`; the eight
  capabilities were eight `basis + Rand(-1..1)` rolls against that one number;
  and `DerivedMask` built the institution mask from those rolls plus the tier.
  Faction identity was a god-variable. Nothing about this was designed - it was
  derivation written three times in three modules, each reaching for the nearest
  available number.

  Ideology was worse than derived, it was inert. `record.ideologyName` is
  captured as a string and read only for display and logging.
  `CAIdeoConventions.SeedConventions` took an `org` parameter and then read
  `PlayerIdeo()` unconditionally, and was called exactly once - for the player
  colony. An NPC settlement's ideoligion shaped nothing about it. Their
  customs came only from the former hostile-organization path (combat customs from
  hostility) and observed practice.

  THREE SIGNALS THAT SHOULD HAVE EXPOSED THIS EARLIER, each treated as
  something to describe or hide rather than as evidence of an incomplete model:
  a fully implemented `CAMorphForm.Medieval` that NO vanilla faction can
  generate, because no vanilla FactionDef ships `techLevel Medieval`; six of
  eight capabilities with no consumers anywhere; and an `ideologyName` used only
  for display. Each was a missing implementation reported as a curiosity.

  Vanilla faction content is also thin for this purpose: settlement-capable
  humanlike factions ship effectively at two tech levels - Neolithic (tribes)
  and Industrial (outlanders) - with pirates at Spacer and the Empire at Ultra,
  both special. The variants differ by xenotype and hostility, not by how they
  live.

- **F-121** (2026-08-05 13:48 UTC / 06:48 PDT; closed 2026-08-09) - **Parallel-ontology audit round
  1: one genuine parallel in eight candidates.** Full proofs in
  `PARALLEL_ONTOLOGY_AUDIT.md`. Standard applied per operator: declaration,
  every write site, the complete consumer set, runtime effect, and a
  classification of authoritative / historical / derived cache / isolated. No
  candidate was accepted on its name.

  CURRENT CLOSURE. The saved neighbour cache and its frozen `stance` were
  removed. Decisions read RimWorld's current faction relation, and regional
  neighbours are discovered from current geography. The evidence below records
  the removed implementation.

  THE FINDING: **neighbour-to-neighbour `stance` is a frozen snapshot read for
  decisions.** Seeded once in `OrganizationModule.cs:3633-3652` from
  `record.faction.RelationKindWith(other.faction)`, with the in-code note
  "frozen at worldgen (engine); movement awaits the relations manager", and
  never refreshed. It is then consumed by decisions rather than display -
  hostile checks at `:642` and `:1414`, an ally check at `:3464`, a suzerain's
  stance at `:5623`, and the mediation factor at `:571-574`. So an NPC-to-NPC
  relation frozen at world generation keeps driving CA behaviour after the
  engine's own relation has moved, and the two can disagree indefinitely.

  The COLONY side of the same structure is correct:
  `SyncColonyNeighbors` re-reads `PlayerRelationKind` on every sync and records
  transitions. One structure, two halves, opposite verdicts - which is why
  name-level nomination could not have found this.

  CLEARED, with proofs: `CAOffice` reads native `Precept_Role` via
  `p.Ideo.GetRole(p)` and extends it correctly; `populationCurrent` /
  `residentIds` are counted from real spawned Pawns and consumed only for
  display; colony-to-neighbour `stance` is a refreshed cache;
  `relationAtMaterialization` / `goodwillAtMaterialization` are a historical
  record the engine does not keep and are never read as current.

  Base rate matters here: one genuine parallel in eight audited candidates. The
  codebase is not riddled with them, and a sweeping remediation would not be
  justified by present evidence.

  FOLLOW-UP. The former custom-versus-`PreceptDef` candidate closed when DR-91
  and DR-92 removed the custom precepts and made saved political beliefs the
  direct source. Still unaudited: `CASecurityPractice`; `CAClaim` / `CAHolding` vs real Thing
  ownership; `seededAssets` / `buildingCount` / `infrastructureCount` vs
  `listerThings`; `CAPolicyRecord`; the knowledge structures vs
  `Thought_Memory`; the `legitimacy` and `treasury` scalars.

- **F-122** (2026-08-05 14:15 UTC / 07:15 PDT; revised 2026-08-09) - **Parallel-ontology audit round
  2, and a correction to F-121's accounting.** Proofs in
  `PARALLEL_ONTOLOGY_AUDIT.md`.

  CURRENT CLOSURE. `CAFacilityHolding` is no longer write-only. Current axis
  materialization records facility ownership and uses those holdings to assign
  stores and staffed posts. It remains a materialization record rather than an
  authority over the Thing, and destruction reconciliation remains a separate
  follow-up. The old writer and consumer count below describes the removed
  founding implementation.

  Count across rounds: one verified pre-existing parallel among established
  systems, plus two recent undeployed parallel abstractions
  (`techLevelResolved` / `authoredTechLevel`, and the authoritative
  capability vector). The base rate is not generalisable in either
  direction.

  ROUND 2, three candidates, frame = native fact -> CA interpretation or
  history -> current consumer.

  `CAFacilityHolding` is **isolated / write-only**. Declared with ownership,
  allocation, oversight, liability and beneficiaries, and with the stated
  intent that "a holding follows the BUILDING". Written at exactly two sites,
  both in `FoundingCompactModule`; read by exactly one log line counting them.
  No decision consults a holding, and there is NO destruction hook - the
  declaration's promise is unimplemented. So the hazard the operator named,
  an organization claiming a destroyed or transferred building, is REAL IN
  DESIGN BUT NOT YET ACTIVE. The reconciliation must ship before the first
  real consumer does. Separate legibility hazard: `CAFrontier.EnsureHoldings`
  means frontier settlements, an unrelated use of "holdings".

  `buildingCount` / `infrastructureCount` / `cultivatedPlantCount` are a
  **derived cache** recomputed from `map.listerThings.AllThings`, filtered by
  rect and `building.Faction == record.faction`, consumed only by log and
  display strings. The specific failure the operator flagged - generation or
  policy reading the stale count instead of the lister - does not occur.

  `treasury` is **legitimately CA-native and conserved**. RimWorld has no
  institutional accounting ledger. It gates a real decision
  (`org.treasury < price` blocks a purchase), and the conservation test passes
  on the audited path: the settlement's abstract money becomes REAL Silver -
  `ThingMaker.MakeThing(ThingDefOf.Silver)`, `TryPlaceThing` - at the instant
  it crosses to the player, while the goods are destroyed out of the player's
  inventory. Unaudited edges named rather than assumed: initial endowment, and
  whether the `FrontierModule` levy path conserves the same way.

  STILL UNAUDITED: `CAClaim`; `CASecurityPractice`; `CAPolicyRecord`;
  `seededAssets`; knowledge structures
  vs `Thought_Memory`. And `legitimacy`, which is a DIFFERENT RISK CLASS - not
  duplication but unsupported causality, whether its movements are grounded in
  actual practices, beliefs and institutional events. It needs its own audit
  question and should not be folded into a parallel-ontology sweep.

- **F-123** (2026-08-05 14:42 UTC / 07:42 PDT) - **Parallel-ontology audit round
  3.** Proofs in `PARALLEL_ONTOLOGY_AUDIT.md`.

  `treasury` DOWNGRADED to **partially conservation-verified**, as the operator
  anticipated. Both edges are now read. Initialization creates money from
  nothing at two sites: `FrontierModule:519` (`treasury = 60f`) and
  `OrganizationModule:2672` (`treasury = 200f + record.production * 150f`). The
  frontier levy at `FrontierModule:362-371` DESTROYS money rather than
  transferring it - the amount appears only in narrative text and no Silver
  Thing is spawned for anyone. The trade path remains coherent and materialises
  real Silver. So: a legitimate CA-native ledger, NOT a closed monetary system.
  Second-order: the endowment formula reads `record.production`, a capability
  value DR-85 rules must become derived from supports, so money currently
  derives from a summary scheduled to stop being authoritative.

  `CAClaim` is a **derived cache**, and says so - "Display data - re-derived
  every 4th pulse". Rebuilt from `map.areaManager.Home.TrueCount` and
  `map.zoneManager.AllZones` filtered to `Zone_Growing`; consumed by one UI
  section. Asserts no ownership the engine does not already hold.

  `seededAssets` is a **historical record with validated consumers** and is the
  best pattern found so far. It stores `"defName|x|z"` strings of what was
  placed at birth, but every consumer treats the string as a POINTER and checks
  reality first: `TryRebuild` asks
  `cell.GetThingList(map).Any(t => t.def == def || blueprint || frame)` and
  rebuilds only what is genuinely missing; the research path verifies
  `benchAlive` on the live map. A destroyed asset therefore produces a rebuild,
  never a false claim. This is exactly the lifecycle discipline
  `CAFacilityHolding` lacks - the pattern already exists in the codebase and
  can be copied rather than invented.

  NOT YET AUDITED, remaining items from the operator's order: knowledge vs
  native memory and knowledge-bearing objects;
  policy and security practice; and `legitimacy` under the separate
  unsupported-causality test.

- **F-124** (2026-08-05 15:10 UTC / 08:10 PDT) - **A record that commands
  reconstruction is not history.** The consumer tracing was right; the semantics were wrong. `TryRebuild` reads
  the birth record, finds an object absent, and RECREATES it - so the record
  holds causal authority over the settlement's future composition. Live-map
  validation prevents ghost duplication but does not make the record passive.
  Correct classification: **persistent reconstruction template, validated
  against native reality.**

  THE NEW AUDIT QUESTION, now required for every candidate alongside
  declaration / writes / consumers / runtime effect: **does this record only
  DESCRIBE the past, or does it PRESCRIBE what the world should become?**

  Unanswered questions this exposes for `seededAssets`: accidental destruction
  versus deliberate demolition; movement or replacement by something better;
  capture by another faction; whether rebuilding consumes real resources and
  labour; whether the settlement still holds the research, personnel, policy
  and need; whether a founding asset should remain obligatory fifty years on;
  and whether institutional change can amend or retire the template. Left as
  is, it preserves a settlement's birth state indefinitely and fights its own
  development - the same ontological problem one level subtler.

  Two smaller corrections. `treasury`: repointing `record.production * 150` at
  a future derived production score would NOT fix the endowment, because a
  summary would still generate wealth; a starting endowment must come from a
  persisted economic fact - stored silver, prior trade, taxation history,
  productive surplus - not a score. `CAClaim`: technically clean, but "claim"
  reads as territorial or legal ownership while the underlying facts are
  RimWorld's home area and growing zones; that gap must stay explicit before
  any political or ownership consumer attaches to it.

- **F-125** (2026-08-05 15:50 UTC / 08:50 PDT) - **Economic-function audit, first
  pass, with licences VERIFIED against the live repositories.** Full table in
  `ECONOMIC_FUNCTION_AUDIT.md`. Repositories inspected read-only over the web;
  nothing installed, no remotes added, no worktree created, CA unmodified.

  THE LICENCE FINDING, which corrects a working assumption: **three of six have
  no usable public licence.** `github.com/tomvd/Storefront` has NO LICENSE FILE
  (API `license: null`) - "publishes source" is not a licence grant, and the
  default is all rights reserved. `github.com/RadsuitRandy/Empire-Mod` (the
  origin; `BigBadE/Empire-Mod` is a fork of it) likewise has none.
  `github.com/OrionFive/Gastronomy` carries CC BY-NC-ND 4.0 in its README
  against a GPL-3.0 repo marker - the conflict the operator flagged is
  CONFIRMED, and the README appears operative.

  Cleanly licensed: `github.com/OrionFive/Hospitality` is dual - **GPLv3** for
  code, **CC BY-SA 4.0** for original artwork - with an irrevocable carve-out
  granted to Ludeon. `github.com/emipa606/RimBank` is **MIT** (original author
  user19990313, continued by emipa606 / Bar0th). Vanilla Trading Expanded
  remains CC BY-NC-ND: reference and compatibility target only.

  CONSEQUENCE: Hospitality's GPLv3 is COPYLEFT, so incorporating its code
  carries obligations for the combined work, while RimBank's MIT does not.
  That difference should decide what is copied versus what is depended upon.

  THE NATIVE FINDING: **RimWorld already has a contract system CA is ignoring.**
  `Quest`, `QuestPart`, `QuestManager`, `QuestGen`, `QuestScriptDef` model
  obligations, outcomes, expiry and rewards. CA's agreements, suzerainty and
  obligations are drifting toward a parallel one, which is precisely what DR-86
  forbids. Also verified: `TradeCurrency` is an enum of exactly two - Silver
  and Favor; `Tradeable.GetPriceFor` is VIRTUAL and therefore a real extension
  point; and there is NO native taxation beyond a Royalty tribute incident.

  RECOMMENDED POSTURE. CA should stop building point-of-sale, customer demand,
  currency media, price fluctuation, and shop/restaurant premises. CA should
  stop and study before building treasury and taxation flows - Empire is prior
  art for both. CA should adopt the native quest machinery rather than grow an
  obligation engine. Genuinely CA's, with no prior art: sub-faction ownership;
  the firm as an entity that owns, employs and bears liability; organization
  demand; regionally grounded prices; and the link from economic acts to the
  political-belief effects that already judge taxation against requisition.

- **F-126** (2026-08-05) — Two scopes now separate for regional mutator work,
  and `TileMutatorWorker_Mountain` is not tile-local.

  INSTALL SCOPE - **WITHDRAWN 2026-08-06.** This section recorded a
  five-`TileMutatorDef` ceiling on the premise that Odyssey was not installed.
  Odyssey is now installed (`Data/Odyssey/`, game 1.6.4871 rev590). The
  installed-content population is **87 concrete `TileMutatorDef`s** (Core 5,
  Odyssey 82) and **45 concrete `LandmarkDef`s**, abstract parents excluded. No
  workshop mod declares either, so Vanilla Landmarks Expanded remains a
  separate future scope and its absence must not be used to narrow Odyssey's
  native requirement. Whether those defs are LOADED at runtime is NOT
  established: `ModsConfig.xml` does not currently list
  `ludeon.rimworld.odyssey` as active and the engine branches on
  `ModsConfig.OdysseyActive`. Until an Odyssey-active def load is observed, the
  correct phrase is "installed-content population", never "loaded".

  Only the population bound is withdrawn; the Mountain conclusions below stand.
  Superseded in scope by the execution-contract audit now in progress.

  MOUNTAIN GEOMETRY. `TileMutatorWorker_Mountain.GeneratePostElevationFertility`
  takes its axis angle from `Find.World.CoastAngleAt(map.Tile, ...)` (the root
  tile), spans `map.Size.x * 0.15` with offset `map.Size.x * 0.2`, translates
  by half the map extents, and writes `MapGenerator.Elevation` over
  `map.AllCells`. It is anchored to map centre and scaled by map size, so on
  the observed 1774x1565 backing map it lays a band roughly 266 cells wide at
  roughly 355 cells offset across the whole region. Two consequences follow.
  A mountainous root paints a region-scale mountain flank over every member,
  attenuated but not removed by the later hill-factor ratio. A mountainous
  member under a flat root gets no band at all; what it does keep is the
  hilliness amplitude, because `memberHillFactors` is built per member from
  `HillFactor(tile.Tile.hilliness)` and reaches elevation through
  `hillFactorByCell` in `ApplyTerrainProjection`. Genstep order is coherent:
  MutatorPostElevationFertility 20, CA_RegionalProjection 190, RocksFromGrid
  200, so the rescale precedes rock placement.

  INVARIANT. Hook shape cannot establish tile-locality. A worker that
  overrides only the grid hooks is still free to read `map.Size`,
  `map.Center`, `map.Tile` or the map edges and write every cell; Mountain
  does. The provisional classes emitted by
  `CARegionalProjectedMutatorPass.ScopeClassOf` are hook-shape evidence and
  may not be promoted into a staging or adapter registry without that
  worker's body having been read.

  JUNCTION. Whether region-scale Mountain is acceptable geography for a
  stitched region or a defect to be replaced by a per-member mountain form is
  unresolved, and is the question the first live receipt run should inform.

- **F-127** (2026-08-05) — RS-008 final state, the largest completed regional
  specimen. Consolidated here from the 2026-08-02 orientation handoff before
  that document was removed; `REGIONAL_SPECIMENS.md` does not carry it.

  Build `a2-172`. Regional plan `CA-RG-E60B632E`. 500 cells per source, extent
  eight, orientation `1/6`. Landing/root tile `17506`; members
  `[17506,301861,301856,301860,170641,389338,170636,389345]`. Backing map
  `1774x1565` = 2,776,310 cells. Seven Tropical Swamp/Rainforest sources plus
  one Arid Shrubland source; 336,272 water cells and 109,011 beach cells.
  Authored CA regional bases: zero.

  Timing: full generation 1,950,387 ms; ordered gensteps 1,905,726 ms;
  finalization 40,702 ms. PlantMask audit-only: 13,254,339 predictions, all
  confirmed - zero mismatch, served rejects, anomalies or degradation.
  NoiseGrids: 10 grids / 105 MiB, zero unsafe, refusal, degradation or
  fallback. RockChunks noise grid: 323 ms parallel build with an exact
  sequential checksum match; the 607,550 ms ordered remainder was dominated by
  ReGrowth and region rebuilds. Renderer telemetry reached 60.7 fps initially,
  then roughly 43.8-53 fps during broad section materialization.

  Material caveat: private memory rose from roughly 11.2 GiB to 30.76 GiB and
  working set from roughly 5.7 GiB to 14.95 GiB during traversal, before an
  asset unload reduced them. Three `otherPawn` references emitted future-load
  serialization warnings; those are separate from generation correctness and
  must be investigated before any template round-trip safety is promised.

  Operator's visual verdict: the map is up and impressive.

  BOUNDS. RS-008 establishes playable generation and several exact audit gates.
  It does NOT establish that a fresh full regeneration is byte-identical, that
  the current save is a reusable template, or that fresh-game faction
  composition can be layered over a captured map. It also predates F-126, so it
  carries no evidence about Mountain geometry, cave continuity or member
  mutator loss.

- **F-128** (2026-08-05; historical seam record, terminology revised
  2026-08-09) — Verified engine seams for preserved-map redeployment.
  Consolidated from the 2026-08-02 orientation handoff before that document was
  removed; `GAME_ARCHITECTURE.md` does not carry these seams. Line references
  are against the decompiled 1.6 tree.

  A raw saved `Map` is NOT a portable cross-Game object. `Verse/Map.cs:620-706`
  serializes map identity/info, components and Things, and `Map.cs:858-898`
  covers terrain, roofs, snow, pollution, deep resources and map components -
  that is a capture substrate, not portability. `Verse/MapInfo.cs:33-39` retains
  the old `MapParent` by reference; `Map.cs:626-634` retains the old map ID,
  generation tick and generator; `Verse/Thing.cs:1219-1237` retains Thing
  identity and map state; faction links are load references. Never deserialize a
  raw old `Map` into a fresh Game.

  The fresh-run lifecycle that must be preserved: `Verse/GameInitData.cs:10-22`
  owns the starting tile, generator def, map size, player pawns and player
  faction; `Verse/Game.cs:492-553` is the authoritative new-game path;
  `Verse/MapGenerator.cs:79-186` allocates the fresh map ID, binds the fresh
  `MapParent`, constructs components, registers the map and then runs contents
  generation - which a CA template generator can enter through
  `GameInitData.mapGeneratorDef`. `RimWorld/PageUtility.cs:30-37` runs
  `GameInitData.PrepForMapGen` and `Scenario.PreMapGenerate`;
  `GenStep_ScenParts.cs:5-12` executes scenario parts; keep
  `Scenario.PostMapGenerate`, `Map.FinalizeInit`, the map/parent hooks and
  `Scenario.PostGameStart` from `Game.cs:529-553`.

  CA-side seams. The following paragraph records the pre-DR-91 implementation;
  its faction-group and base names are not current schema. The coast-model version is saved at
  `RegionalSetupModule.cs:2708-2717` and projection arrays rebuild from the
  world plan at `:3051-3065`; `CARegionalWorldComponent` owns the plan at
  `RegionalWorldModule.cs:668-678`, so a template needs a geography-only
  plan/fingerprint registered into the fresh run before any projection access.
  `CARegionalPlan` mixes geography with faction groups, bases and relations
  (`RegionalSetupModule.cs:344-395`) and its `regionalId` is a recipe hash
  rather than a content hash (`:866-903`) - immutable template identity must be
  split from the fresh run's composition plan, and old resolved faction load IDs
  must never be imported. `CARegionalPlanResolver.Resolve/CreateFaction`
  (`:2465-2568`) already supplies current-world faction resolution and fresh
  native faction creation; authored settlement generation
  (`RegionalWorldModule.cs:1234-1355`) and BaseGen materialization (`:1507-1550`)
  run after hydration. Settlement placement currently relies on ephemeral
  `MapGenerator.UsedRects` (`RegionalWorldModule.cs:1367-1465`) while native
  working data is cleared at `Verse/MapGenerator.cs:357-372`, so reserved
  rectangles must be captured, reconstructed, or replaced by collision
  validation derived from hydrated content. CA player start runs at order 840
  and native ScenParts at 875 (`Defs/RegionalWorld.xml:62-69`; native genstep
  sorting at `Verse/MapGenerator.cs:289-329`) - hydration fits that ordered
  lifecycle rather than replacing it with an ad-hoc colonist/cargo spawn.

  IMPLEMENTATION BOUNDARY. A versioned, filtered template DTO or canonical
  payload plus a fresh-map hydrator - never `Scribe_Deep<Map>` reuse. The fresh
  native Game/Map/MapParent lifecycle allocates legal runtime owners and IDs;
  hydration imports only explicitly classified physical state, remaps legal
  references, registers a fresh geography plan, and then lets the existing
  composition and scenario stages run. Licence-clean CA implementation only; no
  RimWorld or third-party code is copied.

- **F-129** (2026-08-06) — Cave continuity: the defect is dispatch, not geometry,
  and `TileMutatorWorker_UndergroundCave` is destructive to other cave defs.

  NO ANCHORING DEFECT. `MapGenCavesUtility.GenerateCaves` walks `map.AllCells`,
  flood-fills each connected rock group through the `isRock` predicate, trims it,
  drops small disconnected subgroups, then digs open and closed tunnels inside
  the group. `TileMutatorWorker_Caves.ShouldCarve` is `IsRock`, which is
  `elevation[c] > 0.7f`. Nothing reads `map.Center`, `map.Size` or the root tile,
  so caves already follow rock wherever rock is. This is unlike Mountain
  (F-126), and it means the fix is about dispatch alone.

  TWO NATIVE FAILURE MODES. `MapGenerator.cs:141-143` calls `Worker?.Init(map)`
  over `map.TileInfo.Mutators` — the ROOT tile's — and the genstep iterates the
  same list. So a cave-bearing MEMBER generates nothing, while a cave-bearing
  ROOT floods every rock group on the aggregate map, including members carrying
  no cave def. Loss and spread are the same root cause.

  INIT IS LOAD-BEARING. `Init` is what constructs `directionNoise` (Perlin
  0.00205, 4 octaves, `Rand.Int`). `TileMutatorDef.Worker` is a per-Def
  singleton, so a member-only cave def arrives at generation with that field
  null — or holding an earlier map's noise. Any regional cave path must call
  `Init(map)` itself.

  `UndergroundCave` IS DESTRUCTIVE. `TileMutatorWorker_UndergroundCave`
  .`GeneratePostElevationFertility` calls `base`, then loops
  `while (hashSet.Count < 2000)` calling **`MapGenerator.Caves.Clear()`** each
  pass and re-digging from a random cell of `map.AllCells`. Run alongside any
  other cave def it erases that def's tunnels wholesale, and it is whole-map by
  construction. It is landmark-placed (`chanceOnNonLandmarkTile` 0).
  **CORRECTED 2026-08-06:** recorded as deferred on the withdrawn non-Odyssey
  premise (F-126). Odyssey is installed, `UndergroundCave` is one of the 87
  installed concrete mutator defs, and CA's per-carried-def cave loop makes two
  cave defs on one map ordinary rather than rare, so the guard is a REQUIRED
  correction. It is deliberately not patched in isolation: it belongs inside
  the projection architecture being re-scoped, because 65 of the 87 mutators
  sit outside CA's ownership and the same defect class applies to all of them.

  ALSO ESTABLISHED. `TileMutatorWorker_IceCaves` overrides `ShouldCarve`, so the
  carve predicate is not uniform across the family. `MapGenCavesUtility` holds
  five static scratch collections (`tmpCells`, `tmpGroupSet`, `groupSet`,
  `groupVisited`, `subGroup`), which makes sequential calls safe and parallel
  calls corrupting.

  APPLIED. CA owns the cave family in the elevation phase and drives it once per
  CARRIED cave def, with the elevation grid temporarily masked so rock owned by
  members without that def reads as non-rock; the flood fill then stops at the
  boundary by itself. A rock ridge spanning a cave tile and a non-cave tile is
  deliberately split, because the def is a per-tile fact the player can read off
  the world map. The terrain phase uses a narrower ownership set: cave texturing
  self-masks on `MapGenerator.Caves` being positive, so it runs unmasked but
  once per carried def. Twin-built `1BACB46A2B6F67AF`; nothing runtime-verified.

- **F-130** (2026-08-07 08:36 UTC / 01:36 PDT; closed 2026-08-09) — Pending-plan persistence was
  globally single-slot.

  CURRENT CLOSURE. Pending plans are saved beneath
  `Config/CARegionalPendingPlans` using a SHA-256 key derived from the exact
  world identity. Restore reads only that keyed current-schema file. The fixed
  legacy slot is not scanned or migrated. The evidence below records the
  removed implementation.

  `CARegionalSetupSession.SavePending` (`Source/RegionalSetupModule.cs:1376`)
  writes one fixed path — `PendingFilePath()` at `:1370`,
  `Config/CARegionalPendingPlan.xml` — with no per-world slotting of any kind.
  The file records a `worldIdentity` (`:1362`, `seedString|planetCoverage|name|
  mapSize`) and `TryRestoreFromDisk` (`:1410`) refuses to load a plan whose
  identity does not match the world being set up.

  THE ASYMMETRY IS THE DEFECT. `worldIdentity` gates RESTORE only. It does not
  gate WRITE. Authoring a starting region in world B therefore destroys the
  stored pending plan for world A, silently and immediately — the first
  `SavePending` in the new world overwrites the file, and the operator's only
  signal is a later log line saying a saved setup exists "for another world",
  which by then describes the plan that just replaced theirs. Nothing in
  `Source/` ever deletes this file, so it is not a cleanup path: it is a single
  slot being reused.

  CONSEQUENCE. An authored regional setup is unrecoverable the moment the
  operator opens the starting-site page of any other world and touches a tile.
  Confirmed concretely: the file held candidate `29404bfb5a66` for
  `alysaliu|1|Algorab Markab|325` (root 395246, landing 33071, `confirmed`
  absent i.e. false) and was preserved out-of-band before the landing-swap
  harness runs, precisely because those runs would have destroyed it regardless
  of which seed they used. "Use a different seed" is NOT a mitigation; only an
  out-of-band copy is.

  This is an onboarding/persistence defect, not a regional-generation one. It
  belongs with the onboarding work in `ONBOARDING_IA.md`, which already treats
  the pre-landing surfaces as the thing being rebuilt. The obvious shape of a
  fix — key the file per `worldIdentity`, or hold a keyed collection rather
  than one plan — is deliberately NOT applied here: it changes the persistence
  contract of the setup session, and it was found while establishing a test
  procedure for a different invariant. Fixing it inside that work would have
  meant altering production candidate semantics to serve a harness.

- **F-131** (2026-08-07) — Mountain spatial magnitude: the carrier's saturating
  half-plane has no spatial support bound, so adjacent carriers merge into a
  region-scale rock slab. Fixture recorded BEFORE any code change.

  ACCEPTANCE FIXTURE, the `alysaliu` island. Preserved at
  `Config/CARegionalPendingPlan.PRESERVED-20260807-0836Z-0136PDT.xml` and in
  both preservation mirrors under `addenda/` (see F-130 for why an out-of-band
  copy was mandatory).

  | field | value |
  |---|---|
  | world identity | `alysaliu\|1\|Algorab Markab\|325` |
  | candidate id | `29404bfb5a66` |
  | regional id | `CA-RG-3CBFCD7A` |
  | bundle root / footprint anchor | 395246 |
  | landing tile | 33071 |
  | local source scale | 325; extent 10 tiles; orientation 1/6 |
  | backing map | 1433 x 1323 = 1,895,859 cells |
  | member tile ids | 395246, 395239, 176547, 395250, 33071, 307768, 307767, 176548, 18491, 307769 |

  Operator observation: 10 land constituents, 2 `Mountainous`, both on the
  western edge, remainder flat/hill; roughly HALF the island renders as solid
  mountain/rock. Carrier tile ids and the numeric Mountain receipt are runtime
  facts and are captured by the measurement receipt added with the fix, since
  no Mountain receipt existed in the current session log.

  NATIVE GROUND TRUTH. `TileMutatorWorker_Mountain.GeneratePostElevationFertility`
  builds `clamp01(0.5 + (xRot - 0.2W)/(0.3W))` about the MAP CENTRE with
  `W = map.Size.x`, warps it (0.003 freq, 20 strength, 2 octaves), and applies
  `elevation = Min(elevation + value, b)`. It ADDS, and the def's own
  description is "A mountain fills one side of this tile." CA's re-derivation of
  the ramp matches native exactly; the defect is not in the ramp.

  THE DEFECT. The ramp saturates at 1 for every `xRot >= 0.35W` and STAYS 1 with
  no far bound. On a one-tile map the map edge terminates it. On an aggregate map
  nothing does. `EnsureMountainField` then compounds it three ways:
  `influenceSquared = (mapSize * 1.5)^2` admits a carrier 487 cells from its own
  centroid - roughly twice its own equivalent radius (~245 at this fixture);
  `weighted` SUMS across carriers while `dominant` is the MAX single weight, so
  two adjacent carriers give `(w1+w2)/max(w1,w2)` in [1,2], clamped to 1 across
  everything jointly nearest to them; and `CharacteristicWidth` is the equal-area
  diameter (~491 here), an area surrogate that is not the carrier's span along
  its own rotated Mountain axis. Since `ApplyProjectedMountains` ADDS up to 1.0
  to elevation and rock is `elevation > 0.7`, a saturated cell is unconditionally
  solid rock. That is the only mechanism present that can produce SOLID rock at
  region scale, which matches the observation.

  SECOND, SEPARATE, BOUNDED LEAK - recorded, not fixed here. `hillFactorByCell`
  (`Source/RegionalSetupModule.cs:4536-4616`) blends `memberHillFactors` with the
  SAME `(mapSize * 1.5)^2` influence radius and `max(24, 0.30*mapSize)`
  smoothing, normalised by the SUM of weights, so a mountainous member pulls
  every constituent within 487 cells toward its own hilliness. It is real and it
  is the same defect class, but `MapGenTuning` elevation factors span only
  0.8 (Flat) to 1.1 (Mountains), so its worst-case lift is 1.375x - it can add
  rock at the margin, it cannot make solid rock. It is deliberately out of scope
  for this fix, which was scoped to the Mountain adapter; the measurement receipt
  reports hill-factor spread inside vs outside carrier ownership so the decision
  to treat it the same way can be made from data rather than from suspicion.

- **F-132** (2026-08-07; superseded implementation, current closure 2026-08-09) — Federation delegation drifted from the canonical
  domain vocabulary: the creation path wrote the literal `"defense"` while the
  constant is `CADomains.Defence == "defence"`. Latent, and fixed in the same
  pass that made the aggregate derived.

  CURRENT CLOSURE. The duplicate federation membership and delegation caches,
  their rebuild projections, the domain-spelling alias, and their pre-release
  migration path have been removed. Federation membership now has one saved
  representation: federation-member relations with explicit shared
  responsibilities.
  The receipt that existed only to test the abandoned migration was removed.
  The evidence below records why the duplicate model was unsafe; its described
  migration is no longer current code.

  ESTABLISHED FROM SOURCE, at `7f63b2f`. `OrganizationModule.cs:5865` executed
  `fed.delegatedDomains.Add("defense")`. `CADomains.Defence` is `"defence"`
  (`PoliticalPrimitivesModule.cs:124`). Every other domain write in the
  repository uses the constant — `FoundingCompactModule.cs:398, 579, 609`.

  WHY IT WAS LATENT RATHER THAN LIVE. `delegatedDomains` had exactly one read
  consumer, a display string at `OrganizationModule.cs:4610`, which prints the
  value rather than resolving it. No authorization path consulted the aggregate,
  so nothing ever asked whether `"defense"` was a domain. Had authorization
  consulted it, a federation founded through the defensive-compact path would
  have held **no delegated defence at all**, silently.

  WHAT IT IS EVIDENCE FOR. This is the concrete case for the ratified contract
  that political aggregates must be DERIVED rather than hand-maintained. The one
  hand-written delegation aggregate in the repository had already drifted from
  the constant catalog, and nothing caught it because nothing consumed it
  semantically. An intermediate validator asserted every projected domain was
  in the old catalog; it was removed with the duplicate cache.

  HISTORICAL NOTE. An intermediate build added a spelling alias, a projected
  cache, and a migration receipt for this defect. DR-91 and DR-92 removed all
  three. Current federation membership is saved only as federation-member
  relations with explicit shared responsibilities. No compatibility path or
  migration receipt remains for the abandoned schema.

- **F-133** (2026-08-08; terminology revised 2026-08-09) — Every CA-authored world faction was given an
  ideoligion generated as though no faction owned it, because
  `CreateFaction` passed `default(IdeoGenerationParms)`.

  CURRENT CLOSURE. Faction creation supplies `new IdeoGenerationParms(def)`.
  The separate former political-tier record and its receipt were removed;
  culture, Ideoligion, political beliefs, and faction structure now live on the
  faction model used by regional generation. Runtime verification remains for
  the operator's next test.

  ESTABLISHED FROM SOURCE. `Source/RegionalSetupModule.cs:2809-2810` built
  `new FactionGeneratorParms(def, default(IdeoGenerationParms), true)`.
  `IdeoGenerationParms`' default value leaves `forFaction` null and every
  authored field unset, so the FactionDef's `ideoName`, `forcedMemes`,
  `styles`, `deityPresets`, `hiddenIdeo`, `ideoDescription` and
  `requiredPreceptsOnly` are all discarded, and `fixedIdeo` stays false so
  `MakeFixedIdeo` is unreachable. Vanilla constructs these parms FROM the def
  (`FactionGenerator.cs:115` and `:120`). A def carrying a fixed ideoligion
  therefore lost it silently, and a def constraining allowed cultures or
  disallowed precepts had those constraints ignored.

  WHY IT MATTERED BEYOND ONE FIELD. `IdeoUtility.CanUseIdeo` reads
  `forcedMemes` and `styles` on the shared-ideo-REUSE branch, so the blast
  radius was every FactionDef carrying a `<styles>` block, not one meme on
  one def: the reuse filter had nothing to filter against and any existing
  ideoligion could be handed to the new faction.

  This is the same shape as F-120 - an axis that decided nothing because its
  input was never supplied - reappearing one level up, at the faction rather
  than the settlement.

  FIXED by constructing `new IdeoGenerationParms(def)`. Twin-built, **never
  run in game**: no authored faction has been materialized on the corrected
  path.

- **F-134** (2026-08-07; superseded by DR-91 on 2026-08-09) — The removed custom
  political-precept model assigned several generated answers without a cause.

  CURRENT CLOSURE. The custom precepts, separate belief state, Ideoligion
  write-back path, and receipt are gone. Ideoligion remains native RimWorld
  state. Political beliefs and faction structure are saved directly on the
  current faction model, while population groups may carry different
  Ideoligions or political beliefs. No current runtime path uses the removed
  derivation.

- **F-135** (2026-08-09; static convergence receipt) — The regional authoring
  implementation now has one current model and vocabulary before runtime test.

  CURRENT CLOSURE. Regions save factions and settlements. Factions save culture,
  Ideoligion, political beliefs, faction structure, settlement authority, and
  era. Settlements save form, role, starting conditions, population groups, and
  generated starting provisions. Organizations save their concrete offices,
  groups, customs, security, agreements, policies, claims, relations, and
  decision history. The superseded political tier, territorial model, custom
  political precepts, duplicate political ledgers and caches, generic place
  types, and their pre-release aliases and migration routines are absent from
  current source, Defs, filenames, and save tags.

  PROVISION CLOSURE. A provision's operator, access, funding, and cause are
  generated from current settlement and faction facts. The UI may override only
  its distribution. The abandoned custom-provider branch and generic authored
  flag are removed. Starting stock now saves its exact spawned thing identity
  and provider organization; allocation never guesses ownership from the
  nearest kitchen or scans unrelated food. Minority providers compare the full
  ownership choice rather than collapsing ownership into a binary category.

  POPULATION AND LAYOUT CLOSURE. `LocalResidents` names a distinct group whose
  pawns belong to the settlement faction. If hostile relations prevent external
  residency, faction affiliation changes explicitly while Ideoligion and
  political-belief sources remain recorded. `CABuiltMass` replaces the generic
  place abstraction, and settlement layout comments use direct settlement,
  colony, and built-area terms. Settlement and arrival labels anchor to the
  largest connected visible land component, snapped to a real land cell.

  FIXTURE. The keyed file and its mirror are byte-identical at SHA-256
  `588812BA0BB125FF0B196F5DBDF2B1A0F5D35CBF82B3666F8D136870228C88EE`.
  Schema 1 restores world `alysaliu|1|Algorab Markab`, region
  `CA-RG-EB596A12`, confirmed candidate `613b1fe44104`, arrival area `389638`,
  map size 350, 3 factions, 4 settlements, and 9 positive population groups
  whose shares total 100 in every settlement. Starting provisions remain empty
  in the fixture so current causes regenerate them.

  STATIC EVIDENCE. Both independent review lanes reported clean after their
  findings were repaired. All 120 Def XML files parse. Retired vocabulary,
  filenames, save tags, provision-authoring scaffolding, and obsolete migration
  functions scan clean. `git diff --check` reports no whitespace errors.
  Two clean Release builds both produced SHA-256
  `2C409E448CE5CB90C1D5E34607913AA188D17E583530BAEB3AF3622AEDFA0A01`
  with 0 errors and the same 12 existing compiler warnings.

  RUNTIME BOUNDARY. The DLL was not deployed and RimWorld was not launched.
  Geography, restoration, generation, settlement placement, population, and
  provisions remain for the operator's next runtime test.

- **F-151** (2026-08-10 05:17 UTC / 22:17 PST) - **The complete A-sequence corpus
  resolves to 102 coherent chronological batches and 30 thematic threads in 12
  families.** The frozen former ledger contains 93 recorded boundaries. Six of
  those boundaries split because they contained separate development units;
  adjacent implementation, correction, and verification records join where they
  form one continuous unit. All 93 former boundaries and all 285 first-parent
  commits through `03df661256da7b13ef20a5877d2f47c0af1c597b` are mapped.

  Five commits are intentionally referenced by more than one new batch because
  the original commit itself crossed a recataloged conceptual boundary. Source-
  only work recorded on 2026-08-05 points to `A70`, the snapshot where those
  then-uncommitted files first entered Git. The catalog therefore preserves
  provenance gaps and overlaps instead of inventing cleaner historical commits.

  A-sequence references in F-1 through F-135 retain their former labels as
  append-only historical text. Resolve those labels to the current chronology
  through `Batches/FORMER_LABELS.md`.

- **F-152** (2026-08-10 08:49 UTC / 01:49 PST) - **The thematic catalog was
  accidentally promoted with an A-series identity.** Its first canonical path
  was `History/A-Series/THREADS.md`, its title named the A-series, and its
  identifiers were `ATF-*` and `AT-*`. Moving the file to `Batches/THREADS.md`
  generalized its prose but left those identifiers in place. The classifications
  themselves remain useful; their series scope did not. Current regulatory
  references use `TF-*` and `T-*`, with a complete temporary-identifier
  crosswalk.

- **F-153** (2026-08-10 08:49 UTC / 01:49 PST) - **CAO's root version stopped
  recording development after the first four historical boundaries.** Git shows
  `0.1.0`, `0.2.0`, `0.2.1`, and `0.2.2`; the former ledger then repeats that
  VERSION remained `0.2.2` while A6-A102 added substantial runtime, authoring,
  world-generation, political, and convergence work.

  A source-owned semantic replay now uses those early declarations as boundary
  evidence and partitions all 102 batches by content, date, thread continuity,
  and actual development transitions. Twenty-seven contiguous units cover every
  batch exactly once and derive `0.12.3.0-alpha`. The validator also proves 12
  neutral families, 30 neutral threads, current projection freshness, and the
  absence of a `B1` record.

- **F-154** (2026-08-10 12:25 UTC / 05:25 PST) - **The World tendencies screen
  exposed a parallel causal model.** Outcome-shaped city and pattern controls,
  frontier count and size conflation, generated-relation rerolls, and source
  ownership language allowed UI policy, saved plans, and runtime generation to
  disagree about which fact each row owned.

  The converged model has eleven explicit UI-to-consumer contracts. A shared
  deterministic kernel realizes settlement placement, population shares,
  relations, economic and urban support, scale, frontier count and form, and
  off-map activity. Confirmed regional plans and standard-map frontier plans save
  those results before consumers run. A fixed-seed receipt runner varies every
  tendency independently, verifies unrelated-variable stability, checks all
  visible controls have consumers, round-trips saved state, proves later policy
  does not reroll it, checks override ownership, and recomputes the authored
  fixture's production realization hash and derived facts. The suite passes 179
  assertions; operator runtime judgment remains the next gate.

- **F-155** (2026-08-11) - **B5/B6 closed substantive Culture acceptance without
  implementing the required longitudinal state or consumer depth.** B5 persisted
  chiefly identity, background text, and an optional native visual source, then
  derived a cultural-expression read model from other settlement state. B6 wired
  parts of that derived expression into behavior and spatial infrastructure, but
  a status-to-score multiplier and prose summary did not establish
  `Culture(T) + lived history -> Culture(T+1)`. Source and runtime review found no
  persisted predecessor/evidence transition loop and no complete substantive
  spatial, social, institutional, political, and settlement-development consumer
  set. B7 treats this as failed prior acceptance, not new scope, and preserves
  the closed B5/B6 records as evidence of the discrepancy.

- **F-156** (2026-08-11) - **The B6 creation surfaces exposed derived conclusions
  and implementation classifications as though they were world facts.** Global
  Compact, Standard, and Expanded information detail; repeated local detail
  toggles; Minimal, Contextual, and Extensive settlement development; generic
  transport, services, civic, research, and facility controls; and Generated,
  Include, and Omit provenance made unrelated phenomena share one convenient UI
  grammar. The controls were not isolated copy defects. They encoded analytical
  and generated outcomes as constitutive variables. B7 removes those authoring
  objects, retains direct region, faction, settlement, location, population,
  ownership, relation, founding, provision, and sparse concrete exception facts,
  and presents the remaining material state as derived realization.

- **F-157** (2026-08-11) - **B7 closes its static acceptance boundary without
  converting receipts into runtime evidence.** The frozen source at
  `3043e6b737472ffb34cf47d856b880c4e6a07811` passes 50 creation-ontology
  assertions, 15 longitudinal-Culture assertions, 65 creation-flow assertions,
  48 player-founding assertions, 185 World-tendency assertions, and 98 of 107
  behavior cases; the remaining nine are named operator-runtime observations.
  Separate causality, ontology, UI, and playability reviews leave no unresolved
  Critical or High source finding. A full Release rebuild succeeds with zero
  errors and the same twelve inherited warnings. Its 3,255,808-byte assembly,
  SHA-256
  `2CF3982C3ECA6E83D335BD7BCEEDF0E4BE98DDF2B218A0DAF854F72EB630A4E1`,
  is byte-identical at RimWorld's active mod target.

  Both active fixture surfaces retain the same schema-5 world, region,
  candidate, arrival tile, scale, three factions, four settlements, nine
  population groups, and explicit established temporal bases. Their normalized
  XML content agrees; byte hashes differ only because the keyed file retains
  CRLF and the active mirror retains LF. The selected region's unsupported world
  mutators remain an explicitly stamped transient developer exercise. It may
  exercise cases 97-103 and 105 without inventing durable compatibility. Case
  104 requires a separate compatible-region save/reload run.

- **F-158** (2026-08-11–2026-08-12 UTC / 2026-08-11 PST) - **B8 makes the
  inherited cultural and political authoring boundary substantive before
  runtime.** The frozen source at
  `1560c2ed16f27eb3d258fd57962ea2c0fdb3e68a` introduces authoring epoch 8,
  twelve open registered social subjects, persistent meaning dimensions and
  practices, the full Culture composer, five complete thirteen-answer political
  profiles, question-first authoring, causal NPC derivation, pawn-local social
  interpretation, subject-specific group aggregation, and generic longitudinal
  and consumer paths. Invalid pre-B8 authoring objects are discarded rather
  than migrated.

  The exact B8 runner passes 57/57 receipts. Retained suites pass 185 World
  tendencies, 48 player-founding, and 58 creation-flow assertions; behavior
  convergence passes 98 static assertions with nine named operator-runtime cases
  pending. Ontology, causality, UI, structural, and playability reviews leave no
  unresolved Critical or High finding. A clean Release rebuild succeeds with
  zero errors and twelve inherited warnings. Its 3,321,344-byte assembly,
  SHA-256
  `995C8123DA81EB083C311C2C792223077B0A13B11FA7FB583531F704F563A6BF`,
  is byte-identical at the active project target after closed-process deployment.

  Both active fixture surfaces are byte-identical at SHA-256
  `4BEF806DADF9A40F83E4E8684B55BBC6335C8BC078119132D66BCA3769BC9093`.
  They retain the intentional world, region, confirmed candidate, arrival tile,
  map scale, three factions, four settlements, and nine population groups under
  regional plan schema 6, Culture and Political Beliefs schema 8, and player
  founding schema 3. The transient developer-exercise boundary and the separate
  compatible-region requirement for behavior case 104 remain explicit.

- **F-159** (2026-08-12 UTC / 2026-08-12 PDT) - **B9 closes Starting Region
  information architecture and settlement programs at the static boundary.**
  Source commit `20b181413a13a2b168c9c5d8dc23fb20241909c4` preserves Objects and
  Map, adds shared selection and compact settlement comparison, reconstructs
  region/faction/settlement Details around object-owned facts, removes
  one-option controls and backend exposition, and measures wrap-sensitive rows.
  Culture onboarding uses semantic meaning cards and guided editing while exact
  continuous values remain contextual and lossless.

  The seven persisted facility bits and exception masks are gone. A 22-program
  namespaced registry spans 16 functional domains and resolves native,
  expansion, and ported candidates from loaded contracts. Provision generation
  now admits only household, communal, and authority operators backed by actual
  social, material, funding, access, stock, and consumer evidence. Confirmed
  plans materialize saved programs and counts rather than rerolling tendencies.

  Exact B9 acceptance records 79 statically verified cases and five named
  operator-runtime observations. Retained suites pass 57/57 B8 receipts, 185
  World-tendency assertions, 48 player-founding assertions, 54 creation-flow
  assertions, and 98/107 behavior cases with nine operator observations. Five
  review lenses leave no unresolved Critical or High finding. The clean Release
  build succeeds with zero errors and twelve inherited warnings. Its
  3,404,288-byte assembly, SHA-256
  `9AD4EE62359CF1961FFCB55A7C45C946914F99E264FD7C819F74342E51D0EE19`,
  is byte-identical at the active project target after closed-process deployment.

  Both fixture surfaces are byte-identical at SHA-256
  `1DDAA4CD9ADC2CD557471B01D754BA37AE191A3588155645B1A872680D8ED4D2`.
  Regional schema 8 and settlement-program schema 1 retain three factions, four
  settlements, nine population groups, the current world/region/candidate/
  arrival/scale identity, and per-settlement program/provision counts. Visual
  layout, selection feel, Faction Relations, fine-tune reopening, and the
  generated game remain operator judgments.

- **F-160** (2026-08-12 UTC / 2026-08-12 PDT) - **The aggregate framework
  contained deterministic and random proxies in causal ownership positions.**
  The B10 ledger found hash-selected households, random capability jitter,
  tendency-created programs, category-created provision arrangements,
  Political Beliefs copied into current structure, a hash-selected independent
  Ideoligion, random local factions and relations, hash-created specialization,
  assets standing in for institutions, parallel fixture derivation, and a tax
  editor that rolled an institutional decision. All were Critical or High
  because they asserted semantic state without the process that makes it exist.

  B10 replaces those paths with persistent domestic-unit membership and
  represented transitions; eight domain-specific capability evidence records;
  complete settlement-program operational evidence; factual domestic,
  communal, and authority provision contracts; exact Ideoligion and relation
  identities; and separate normative belief, instituted order, Culture,
  authority, material, and behavior state. Unsupported programs and provision
  forms remain absent. ThingDefs and rooms are classified as material, work,
  storage, access, communication, symbolic, or spatial nodes rather than proof
  of an institution.

  The renewed source sweep classifies all 152 active C# RNG/hash occurrences
  and leaves no unresolved Critical or High synthetic-state finding. B10
  acceptance passes 75/75 against the production kernels. The final convergence
  adds an explicit established-program authoring owner, exact per-role program
  asset receipts, atomic rebuild rebinding, exclusive and reachable provision
  stock, exact provision-kind matching, live Defense labor assignments, and
  exact program continuity through later work.

  The active and keyed fixture surfaces are byte-identical at 65,717 bytes and
  SHA-256
  `F539239672875C668F750A09CCC370B4B6AFD8165B0F6FA7D37543AFE8391440`.
  They preserve three factions, four settlements, nine population groups, 19
  explicit established-program facts, and the current world, region, candidate,
  arrival, and map-scale identity under authoring epoch 10 and regional-plan
  schema 10. The clean Release assembly is 3,415,040 bytes at SHA-256
  `DD0EC6DB5C7D0C3B607C1D6FFBE813F6469CD4796CAB10E5C0D70D4223FFDF60`.
  RimWorld was closed for deployment, and the active Steam-mod target is
  byte-identical to that assembly. Aggregate look and play remain pending
  operator evidence.

- **F-161** (2026-08-12 UTC / 2026-08-12 PDT) - **The post-B10 tree supports a
  durable campaign boundary, but the inherited authoring vocabulary did not yet
  meet that boundary.** The structural audit found one coherent owner per live
  schema and cadence after narrow extraction of compatibility, profiling,
  pending-authoring, and settlement-wealth responsibilities. Streaming preflight,
  a complete executable manifest, idempotent controlled B10 upgrade, stable-ID
  receipts, truthful additive provenance, visible rejection, and rollback
  documentation establish the first retainable campaign baseline without a
  general destructive migration framework.

  The addendum audit distinguished four completion layers and classified 36
  mechanically observable social fact families. The former production surface
  had 12 social-subject examples and no distinct concrete-practice vocabulary.
  The corrected build has 45 sourced and consumed social referents and 33 sourced
  and consumed repeated practices. Subject, meaning, and practice records and
  candidate universes are distinct; invalid subject-shaped pending practices are
  accepted only when actual longitudinal evidence establishes their concrete
  conduct.

  Political Beliefs and current order now retain several independent mechanisms
  per subject. Synthetic `mixed` values, first-value views, complete Political
  Profiles, and authority bundles mislabeled as beliefs are absent. Twelve belief
  sets and ten current-order sets declare exact additive patches and preserve
  unrelated or compatible state. Culture and political authoring use
  content-driven sections; source-domain and one-item tab projection is gone.

  The governed fixture advances to pending epoch 11, regional-plan schema 11,
  and Culture/Political Beliefs schema 9 while retaining its existing identity,
  three factions, four settlements, nine population groups, and nineteen
  established operations. Exact receipt, review, build, commit, and deployment
  evidence is recorded in the B11 evidence files and append-only batch record.
  Look, interaction, campaign start, save, and later update remain operator
  runtime evidence rather than static claims.

- **F-162** (2026-08-13 UTC / 2026-08-12 to 2026-08-13 PDT) - **The B11 Culture
  surface had representational breadth but not the integrated causal cognition
  required by the supplied design package.** Its social meanings could preserve
  approval, normality, prestige, and salience, but they did not own a coherent
  population distribution, pawn-private/public attitude, sparse social network,
  emergent political cognition, institution appraisal, or proposition-knowledge
  lifecycle. Treating those absent systems as documentation-only work would have
  left the declared ontology without runtime owners.

  B12 replaces former meaning rows as current normative authority with thirteen
  explicit Culture questions and population distributions. Twenty-one former
  social-subject records map through exact adapters to eight questions. Only
  weighted approval and salience acquire new question semantics; all former
  dimensions and unmatched subjects remain `CACultureLegacyEvidence`.
  The governed fixture demonstrates 22 distributions and 26 evidence records
  across eight Culture instances without changing its world, region, candidate,
  arrival, scale, 3-faction, 4-settlement, 9-group, or 19-operation identity.

  `world.cultural-cognition` owns stable psychology evidence, dynamic condition,
  private/public attitudes, perceived norms, and sparse influence. A separate
  `world.political-cognition` owner forms issue positions, links, perceived
  majorities, and faction-bounded coalitions. `world.proposition-knowledge` owns
  claims, sources, access, confidence, transmission, research receipts, custody,
  and decay. Organization schema 2 owns legitimacy and sanction appraisals.
  Culture influences appraisal and discretionary CA action selection without
  changing native legality or direct operator authority.

  The expanded fixed-seed suite passes 113/113 assertions spanning distribution,
  semantics, psychology, networks, behavior, politics, institutions, knowledge,
  migration, history, fixture round trip, and performance bounds. That evidence
  establishes implementation contracts and causal isolation. Psychometric
  validity, empirical calibration, visual quality, and gameplay acceptance remain
  outside the static claim and await operator runtime evidence.

- **F-163** (2026-08-13 UTC / 2026-08-13 PDT) - **Independent report evidence
  requires a different reporter and holder.** Final causal review found that the
  first B12 report fan-out could route an organization member's already-known
  claim back to that same member. The route was delivered once, but proposition
  knowledge would have counted the holder as a second source after the holder had
  learned only from somebody else. Production ingress now rejects every reported
  route where reporter and holder are the same pawn, both at the act-record
  authority boundary and at organization fan-out. The executable knowledge
  receipt demonstrates two distinct reporters reaching one holder while the
  holder's self-route is rejected; all 113 B12 receipts and the retained B10/B11
  regression suites pass after the correction.
