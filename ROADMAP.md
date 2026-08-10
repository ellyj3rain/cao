# Colonist Awareness - roadmap

The A sequence is closed at `A102`; `B1` is the next development batch.
Historical batch identifiers below use the current sequence. Thematic `T-*`
identifiers link continuing work across nonadjacent batches. The chronological
catalog is [`BATCH_LOG.md`](BATCH_LOG.md); thematic links are in
[`Batches/THREADS.md`](Batches/THREADS.md). Contiguous version units and their
tiers are in [`VERSION_MAP.md`](VERSION_MAP.md).

Shipped: eat smart; criticality hauling (+roof fallback); life safety (colonist/slave/prisoner/faction, outsiders toggle); area duty; corpse discipline (unforbid + auto-plan graves); autonomy levels (gizmo + portrait right-click); raid response v1 (Proactive shelters, Autonomous mans the cover line); stashing (targeter-picked spot, live cover/ground readout, deterioration protection = concealment quality x terrain/climate preservation, ~35kg gate, corpses exempt, aftermath corpse-drag); animal care (Handling-lane emergency feed for starving/urgent - patient-fed if downed, dropped at feet if mobile, kibble/hay first; rescue downed animals to animal beds) - field-confirmed; fire response (Proactive+ drop leisure/swim/idle to beat home-area fires, ~2s cadence) - field-confirmed.

## Live-verification controls

- Half speed: BUILT in `a2-4`, field-confirmed by the operator. A fifth native
  time-control button between Pause and Play runs the simulation at 0.5x/30 TPS;
  its Play glyph is divided by a transparent diagonal cut. Pause/resume and the
  Faster/Slower bindings traverse the five-state gradient naturally. The operator
  confirmed that the control works and its appearance is acceptable.
- Independent pointer click targeting: BUILT in `a2-8`, widened in `a2-19` (DR-27).
  On Windows in Dev Mode, pawn and map target queries made during MouseDown/MouseUp use
  the IMGUI event's pointer, allowing the operator and an agent to click through separate
  MouseMux pointers without forcing either one onto Windows' shared hardware cursor.
  `a2-19` removes the play-state restriction so the main menu and its dialogs are also
  reachable; a seat that cannot open the load dialog cannot start a session. Hover, edge
  scrolling, drag motion, and held-button state remain on RimWorld's native pointer path;
  independent dragging is not claimed, and Linux and Steam Deck retain their native path.
  Note per F-36 that ordinary RimWorld widgets never needed this bridge —
  `Verse.Mouse.IsOver` reads `Event.current.mousePosition` — so the bridge serves map and
  pawn targeting specifically.
  BLOCKED, and not by this code: a MouseMux SDK virtual seat is server-registered,
  acknowledged, and drawn on screen, but injects into no application, verified against the
  mapper's own window as well as RimWorld. Agent-driven live verification cannot proceed
  until that is resolved on the MouseMux side. The operator's own seats are unaffected.

## Shared awareness and reaction — A5 / T-004, T-005, T-008

Built as `a2-5` / historical VERSION 0.2.2; in-game behavior and presentation remain pending
operator verification after a full restart.

- Identified threat contacts are pawn-private evidence with explicit visual,
  damage, voice, radio, and future sensor provenance. Voice is local; radio
  requires enabled comms, equipment at both ends, and explicit player squad or
  fire-team command edges. A mayday relays a teller's actual remembered contact;
  it does not turn unidentified noise into a hostile. Tactical Lord membership is
  not a communications network.
- Successful firearm launches emit ambiguous gunfire evidence through RimWorld's
  hearing/room/open-door clamor traversal. It carries an approximate area, age,
  uncertainty, repetition, and report channel; it never identifies a shooter,
  faction, target, or weapon and never becomes a contact without corroboration.
- Immediate recognition and response run through the native 30-tick constant
  think lane. Deliberate cover, convene, reconnaissance, and objective selection
  remain slower duty/planning work. Each decision uses that pawn's facts; a group
  plan uses a real leader's facts only after information reaches that leader.
- Generic `AssaultColony` raiders now use the same awareness substrate with a
  faction-specific assault duty: visible combat, remembered-contact
  reconnaissance, visible meaningful sabotage with a validated approach and
  open-air egress, then geometry-only exploration. No global nearest-colonist or
  random-trash objective is introduced by CA.
- The declared equipment-transition debt is closed: EquipTransition and Withdrawal use remembered
  contact cells, resolving a live pawn only under current LOS. Withdrawal plans
  now preserve save state and yield immediately to later draft changes or forced
  player work.
- Future cameras and satellites are additional evidence producers for the same
  contact ingress; no camera or satellite gameplay ships in this unit.

## Communications medium and organization correction — A6 / T-004, T-005

Built as `a2-6` / historical VERSION 0.2.2; code/Defs verification only. In-game behavior and
presentation remain pending operator judgment after a full restart. This section
supersedes the A5 record only where its “faction-specific assault duty” wording or
references to a group leader could imply faction differentiation or automatic NPC
hierarchy.

- Communication medium and organization are independent. Voice is local,
  same-faction, and recipient-uncapped unless the teller is in CA's
  concealed/quiet posture. Remote radio requires an actual Airwire, Array, or
  Integrator headset at both endpoints. A separate mental edge requires a
  mechlink at both endpoints and free native bandwidth after mechs and gestation.
- The headsets' native +3/+6/+9 offsets currently provide 3/6/9 human radio peer
  lines, and each free bandwidth point provides one mental peer line. Those are
  provisional CA mappings, not native human-communications rules. Actual
  equipment controls capability; faction `techLevel` is not a hard veto.
- Player communication retains explicit squad-leader, fire-team-leader, and
  member routes. For nonplayers, same-Lord membership only bounds peer candidates
  for equipped remote reporting. It appoints no leader, grants no rank, and
  shares no knowledge.
- The existing generic `AssaultColony` awareness duty is attacker-role-specific,
  not yet differentiated by faction technology, culture, raid strategy,
  composition, roles, training, or capable individuals. Exact faction doctrine,
  emergent leadership, and succession topology remain future work.
- Pre-existing player `SquadComponent` lookup still substitutes the available
  member with the best Shooting+Intellectual score when an assigned squad or
  fire-team leader is unavailable. That behavior is disclosed, not ratified as
  succession: assigned identity and any acting-leader policy must be separated
  before the leadership-loss arc is claimed complete.
- Reliable command span is separate from hearing and reporting. The current
  Intellectual/Social/trait formula and hard capacity cutoff are provisional and
  disclosed; the command-and-control arc's promised degradation beyond capacity
  remains unbuilt.
- Contact observation now respects the concealment discovery gate. Spatial
  discovery, charting, and optional player-view fog remain in the territory arc.
  The existing map-global sprung-trap memory remains debt pending migration onto
  pawn-private evidence.

## Autonomy and knowledge correction — A7 / T-003, T-004, T-010

Built as `a2-7` / historical VERSION 0.2.2; code verification only. In-game behavior remains
pending operator judgment after a full restart.

- Directed now runs its declared survival floor: Eat Smart, Life Safety, and
  Criticality. Their feature settings remain authoritative; outsider rescue still
  requires its setting or Proactive+ autonomy.
- Turning Threat knowledge off disables new contact and audible-cue acquisition
  and relay. It does not expose live map truth. Raid Response, generic-assault
  awareness, hold deviation, immediate combat reaction, weapon transitions, and
  withdrawals may use only a hostile the pawn currently sees within that
  behavior's existing range.
- Current sight uses one shared predicate: the observer is awake and humanlike,
  the hostile is spawned on the same map and not concealed, and range plus LOS
  both pass. Remembered contacts still retain their remembered cells but resolve
  to a live pawn only through that same predicate.
- Disabled-mode combat reaction keeps a transient first-seen tick across the
  native 30-tick think passes so disposition and pain still determine recognition
  latency. That transient state clears through the existing cross-save reset.

## Lost-contact tracking and concealment correction — A9 / T-004, T-008

Built as `a2-9` / historical VERSION 0.2.2; code/Defs verification only. In-game behavior and
presentation remain pending operator judgment after a full restart.

- Losing sight of an identified contact now starts a finite search of 3–7 reachable
  cells around that pawn's copied last-known position. Age, confidence, and
  uncertainty change the sweep; the hidden pawn's live position is never used as a
  search target.
- New sight or changed contact evidence interrupts and replans the search. Completing
  or failing to reach an exact source-time-and-cell fact marks that attempt exhausted,
  and another fresh contact can then drive the decision.
- Combat posture now has one facing authority. Concealment discovery accumulates
  repeated clear observation against a stable per-observer episode threshold instead
  of resolving on one deterministic scan.
- Concealment remains owning-map-global: one observer's successful discovery reveals
  the hider to all target-selection consumers on that map. Episodes are ownership-
  scoped so another loaded map cannot reveal or disarm them by absence.
  Observer-relative reveal is future work.

## Individual immediate-combat correction — A12 / T-003, T-008

Built as `a2-12` / historical VERSION 0.2.2; code/Defs verification only. In-game behavior and
presentation remain pending operator judgment after a full restart.

- A fresh external-violence impact now reaches the existing humanlike 30-tick
  reaction lane before the later mind-state/contact notification. The pawn derives
  immediate survival pressure from live injury, bleeding, cover, ability to
  engage, combat skill, and Disposition. Undrafted Proactive+ colonists and exact
  generic settlement assaulters may hold, improve position, or break contact;
  Directed/Standard control and explicit player/emergency/tactical work retain
  priority.
- Against a currently visible contact, an eligible ranged pawn without an existing
  CA automatic-defense post compares nearby locally visible, reachable cells by
  shot quality, directional cover, target exposure, useful range, travel, and the
  prospective firing corridor through visible non-hostiles. Shooting skill weighs
  geometry more strongly, expands the bounded search, shortens reconsideration,
  and lowers the threshold for worthwhile movement. The same assessment serves
  player colonists and exact generic settlement assaulters.
- One finite `CA_CombatMove` owns each selected destination. Its continuation
  contract preserves an unchanged move, while a genuinely newer impact may replace
  the destination; the constant thinker does not manufacture a fresh move every
  pulse. Operational Equip/Wear continuation now yields only during a fresh violent
  impact so it cannot mask the survival override.
- The Immediate combat census exposes impact age, pressure, composure, ability to
  engage, decision, current/best cell scores, shot quality, self-cover, friendly-
  lane risk, range fit, and travel. It is evidence for replay, not a substitute for
  operator judgment of the screen.
- This slice does not claim squad allocation, leader deliberation, fire-team
  geometry, bounding, cross-team deconfliction, chokepoint/material analysis,
  remembered-contact firing cells, or guaranteed friendly-fire prevention. Those
  remain the broader Doctrine work.

## Contact resolution and mission casualty triage — A13 / T-004, T-006

Built as `a2-13` / historical VERSION 0.2.2; code/Defs verification only. In-game behavior and
presentation remain pending operator judgment after a full restart.

- Contact identity now survives separately from its last observed state. Active,
  downed, dead, captured, nonhostile, nonthreatening, and destroyed are explicit
  per-knower states; only Active facts drive combat selection and the transient
  combat posture. The Knowledge census shows both remembered state and whether it
  is actionable.
- State resolution requires awake firsthand LOS and respects concealment. Resolved
  state is not relayed in this slice: remote pawns retain their own last active
  report until they see the outcome or the contact ages out. State-fact relay is
  continuing Knowledge work.
- A successful Man in Black incident saves only the downed player colonists present
  when its joiner spawns as that pawn's mission beneficiaries, with their arrival
  cells and gross downed state. It grants no clinical severity, hidden enemies,
  supplies, or general map truth. The beneficiary set survives save/load and never
  expands to later casualties.
- A Proactive+ mission rescuer yields to perceived active danger, then goes to a
  beneficiary's saved cell and visibly assesses them. Medical skill drives
  assessment time and precision while Intellectual contributes. Low skill retains
  broad bleed uncertainty; higher skill saves progressively narrower estimates.
  A world-space diagnosis makes each acquired result visible. A neutral message and
  native sound mark the first or a changed diagnosis without repeating unchanged
  notices after every treatment pass.
- Triage compares only those saved clinical estimates with a similarly skill-bounded
  self estimate. An assessed urgent casualty is ground-tended, using permitted
  carried medicine when available; after stabilization, a downed beneficiary is
  rescued when a native bed exists. Observable ties favor the beneficiary; hidden
  health percentage never breaks one.
- `CA_EmergencySelfTend` is one finite mission-owned exception to vanilla's disabled
  self-tend gate. It leaves the player's global self-tend policy untouched and keeps
  native medicine, tend-quality, capacity, reservation, and medical-care semantics.
  Assessment or medical work on the mission rescuer or a saved beneficiary releases
  on newly perceived danger, drafting, or direct player control.
- The Mission triage census exposes saved cells, acquired state, diagnostic tier,
  assessment age, perceived bleed estimate, current job, and clinical priority.
  Developer-only actual health and bleed-out ground truth are labeled separately;
  neither enters pawn behavior. It does not substitute for operator judgment of the
  screen.

## Welfare knowledge and conditional accountability — A14 / T-004, T-005, T-006

Built as `a2-18` / historical VERSION 0.2.2. The direct visual welfare handoff through local
assessment into tending was verified in game on `a2-15`; the shared `a2-16`/`a2-17`
coalescing implementation is code/build-verified. The assigned squad-leader command
path was verified in game on `a2-17`. The welfare relay heartbeat was live-proven on a
disposable wall-separated fixture: a remembered fact reached the responsible pawn by
Voice, the immediate reporter and revision held, and repeated censuses renewed freshness
without changing semantic identity.

`a2-18` closes the accountability composition defect: the exact automatic raid-defense
duty is retained while a direct-report check is due or already Checking, including
through calm and no-contact teardown, so the finite check can launch and finish.
The `Overdue → Checking → ConcernReported` cadence is code and build verified and
REMAINS UNPROVEN IN GAME. The first fixture established no CA tactical Lord because
`New Arrivals1` has no player-faction buildings and its ruins are factionless; the retry
was blocked entirely by F-36, in which a MouseMux SDK virtual seat is acknowledged by the
server and drawn on screen but actuates into no application. Live verification of this
cadence is blocked pending a MouseMux-side answer, not pending the behavior.

- Gross humanlike welfare state is now pawn-private. Awake firsthand sight records
  downed state and a last-known cell; dangerous temperature is only inferred close
  enough to observe it. Same-faction voice, headset-radio, and mechlink-mental
  relays preserve the teller's source cell and age. A report is a location to check,
  not tracking or remote health telemetry.
- Clinical severity remains local. A finite `CA_CheckWelfare` goes to the copied
  cell, performs the visible assessment, and only then acquires a coarse estimate
  shaped by Medicine and Intellectual. Capable Proactive+ medics may tend an
  assessed bleed; automatic rescue requires a current firsthand welfare fact.
  Forced rescue and vanilla rescue with both owning features disabled pass through.
- Mission-event evidence is owner-scoped and nonrelayable. Its original event tick
  survives save/load; legacy manifests without a recoverable event tick retain their
  mission authorization but do not manufacture fresh general knowledge. A later
  independent visual or clinical observation may enter ordinary relay.
- Each general `CA_CheckWelfare` carries origin, subject ID, source/state ticks,
  monotonic revision, and copied cell. An equivalent non-stale visual heartbeat
  refreshes source freshness without changing the job token; stale reacquisition
  or a material semantic/provenance change interrupts the old job. Completion
  revalidates the same token and mutates only the welfare fact or accountability
  record that launched it. A failed mission assessment marks its
  exact owner-scoped event fact Missing so it cannot launch a redundant second
  search after the mission manifest closes.
- Conditional accountability begins only across explicit stored squad/fire-team
  responsibility while the pair initially shares a CA tactical or stack-breach
  Lord. Sight or a valid strategic check-in confirms presence. Disposition shapes
  the missed-check cadence; Proactive checks are nearby and Autonomous checks may
  range farther. No fallback leader, timetable, allowed area, remote job, health,
  death, downed state, or live position establishes the answer.
- The Welfare and accountability census exposes only the selected pawn's facts,
  provenance, age, assessment, and responsibility states. Exact clinical ground
  truth remains limited to existing labeled developer instrumentation.
- Structural follow-on requiring operator ratification: move accountability record
  policy out of `KnowledgeMapComponent` into an Authority-owned component; reduce
  the mission record to authorization over one welfare truth; split sensing/relay,
  clinical rules, accountability, and Doctrine executors; centralize their priority
  arbitration. The current slice is testable but does not claim that refactor.

## Native fighting-withdrawal executor — A16, A18, A20 / T-007

Persistent executor built as `a2-20`; cover-aware halt selection built as `a2-22`;
late-contact bound conversion corrected and sensor-verified as `a2-26`; historical
VERSION remained 0.2.2. Persistent job identity, local seven-cell bounds, mover/coverer role
swaps, and visible ranged-fire attempts are verified after full restarts.
Presentation remains pending operator in-game judgment.

- A selected solo or covered withdrawal starts one persistent
  `CA_FightingWithdrawal` job per participant. The scribed map plan retains the
  destination, mover/coverer relationship, fire cadence, member job identities,
  and physical phase across save/load; the component's 30-tick pass now performs
  integrity repair only.
- Each driver changes bound destinations through `Pawn_PathFollower.StartPath`
  and fires from a halt through `Pawn.TryStartAttack`. It creates no phase-level
  `Goto` or `AttackStatic` job, so there is no repeated `StartJob` supervision.
- The exact mover alone advances a covered pair. A departing or incapacitated
  member clears its moving-fire state and the survivor degrades to the same saved
  plan as a solo withdrawal. Both jobs finish when both participants reach the
  destination bound.
- The player's immediate or queued forced work, a draft-state change, explicit
  tactical/hide/stack ownership, setting disable, incapacity, path failure, and
  native higher-priority emergencies end the claim. Civilian hostility response
  and damage-triggered ordinary job reevaluation do not churn the persistent job.
- An old supervised plan migrates only when its saved member job ID still exactly
  owns a non-forced `Goto` or `AttackStatic`. A newer job wins; migration never
  resurrects a plan over later engine, mod, or player control.
- A non-final bound with a currently visible pursuer and a usable ranged verb asks
  `CastPositionFinder` for a covered firing cell no farther than seven cells from
  the mover and within four cells of the nominal halt. Every candidate must make
  strict destination progress and retain native pawn walkability, allowed-area,
  reachability, reservation, known-danger, firing-line, and range gates. A
  remembered-only contact or unavailable firing cell uses the equivalently bounded
  geometry fallback. No valid local cell holds and retries after 30 ticks; it never
  turns a failed bound into an unbounded sprint to the final destination.
- If a withdrawal begins before contact becomes actionable, the inherited direct
  run is stopped as soon as shared threat knowledge arrives. The same persistent
  job then selects an ordinary local bound; it does not carry the initial mover to
  the final destination before beginning pair alternation.

### Fire-response arbitration closure (`a2-25`)

Home-area response now releases only unforced leisure or idle work after the
pawn's native emergency `FightFires` scanner proves it can answer the fire. Native
emergency work owns the resulting job. The direct CA path remains only for a
parentless fire on a player building outside Home and retains the corresponding
native safety gates. Deterministic Home/off-Home fixtures, the selected-pawn census,
and the settings scroll view were live-proven after a full restart; the disposable
session was closed without saving.

## Post-contact outcomes and field custody (operator spec, 2026-07-24)

- Once perceived active danger ends, the pawn transitions through casualty triage
  before deciding what to do with a downed hostile. Stabilize, capture, abandon, and
  execute are contextual Doctrine outcomes shaped by observed state, Disposition,
  ideology, mission, capability, and Authority; none is universal cleanup.
- Impromptu field custody must work without a prisoner bed. Accepted custody uses
  native `CapturedBy` prisoner identity, a finite native wait-before-escape window,
  and a small scribed record containing detainee, custody cell, credible captor or
  guard, reassessment time, compliance reason, and transition status.
- Compliance derives from observable intimidation facts: captor Social and
  Manipulation, visible weapon and demonstrated capability, nearby credible guards,
  detainee courage/aggression/discipline, pain and mobility, nearby allies or weapons,
  and viable escape access. Use one stable event-seeded decision with hysteresis,
  not a fresh roll every think pass. When those facts no longer sustain compliance,
  stop renewing the wait and let native prisoner escape behavior resume.
- Formal prisoner beds transfer field custody into ordinary prisoner handling.
  Autonomous execution requires explicit Doctrine, Authority, ideology, and
  Disposition permission. Field custody, surrender demands, and hostile outcome
  selection are not built in A13.

## Sneaking and developmental training (operator spec, 2026-07-24)

- Sneaking is learned competence and therefore belongs as a first-class `SkillDef`,
  not a Disposition axis. Disposition governs whether and how a pawn risks hiding;
  Sneaking governs execution quality, movement discipline, detection risk, and
  resistance to reacquisition.
- Hide-and-seek is the safe childhood learning activity for Sneaking. It should use
  RimWorld's native child-learning seam while explicitly awarding Sneaking XP to the
  hider and seeker. The same developmental framework can let relevant phases of
  supervised hunting and child-eligible ceremonial fighting accrue the skills they
  actually exercise rather than granting one generic training bundle.
- Native anchors are child life stage at 3, Hunting/Handling work at 7, growth moments
  at 7/10/13, preteen at 9, teenager at 13, and adult life stage at 18. Vanilla also
  defines an exceptional all-child raid at ages 9–12, so RimWorld has no universal
  “official raid age.” Eligibility belongs to faction-specific CA doctrine, capable
  participants, training, equipment, and context.
- Vanilla gladiator duels disallow children even though their outcome worker awards
  Melee XP. Ceremonial childhood fighting therefore needs a bespoke safe,
  child-eligible activity or ritual; it must not silently reuse that duel contract.
- The exact Sneaking definition, XP rates, hide-and-seek jobs, ceremony safety rules,
  and faction raid-eligibility policies are not built or ratified yet.

## Affect and differentiated persistence (operator spec, 2026-07-24)

- “Give up” is a cross-cutting persistence decision, not one fixed timeout or binary
  failure. A behavior's progress, setbacks, danger, knowledge, authority, and purpose
  combine with the pawn's resolved Disposition—which already incorporates current
  mood and pain—to determine how long that pawn remains committed and when it
  reassesses.
- Mild affective states such as disappointment and elation use short-lived,
  save-persistent `Thought_Memory` records. That native surface already supplies a
  visible label, description, duration, stacking policy, code-selected stages or an
  expiry successor, and a bounded nonzero mood effect. `MentalState` remains the
  substrate for a full behavioral takeover or break, not ordinary disappointment,
  relief, frustration, encouragement, or pride.
- The loop is causal and bounded: an objective produces outcome evidence; that
  outcome changes affect; affect and overall mood influence the next commitment;
  the next result can reinforce or correct it. One outcome is recorded once per
  scribed goal-or-fact-keyed commitment, with bounded stack and renewal rules, so the
  system cannot manufacture an infinite self-reward or self-punishment loop.
- Uncertainty uses a stable per-commitment variation rather than a fresh random roll
  every think pass, and that commitment state survives saves. Traits, beliefs,
  skills, resolved Disposition, local conditions, and actual events change the odds;
  RNG keeps similarly situated pawns from becoming clones.
- Group outcomes emerge from member decisions. A nonviolent or pacifist colony may
  independently choose and successfully execute concealment while raiders exhaust
  their evidence-limited searches and lose the will to continue; the native Lord can
  channel an Authority decision into group withdrawal. There is no scripted
  “pacifist colony defeats raid” event.
- Native generic humanlike assault currently gives up after one random 26,000–38,000
  tick timer or leaves satisfied after 25–35% colony damage. Those Lord transitions
  are the group seam, but the fixed thresholds do not yet express pawn disposition,
  affect, search evidence, leadership, faction doctrine, or collective resolve. They
  must be explicitly superseded before CA claims that raid withdrawal is emergent.
- Exact affect names, mood offsets, durations, persistence formulae, and the first
  behavior integrations remain to be ratified and built.

## Operational deliberation and settlement relationships (operator spec, 2026-07-24)

- A withdrawal decision carries its justification. The operational record identifies
  the initiating faction and, when one exists, source settlement; objective; actual
  command roles; member reports; known losses and needs; observed progress; spent
  resources measured from explicit CA baselines; viable egress and refuge; decision
  authority; chosen action; and unresolved intent. It is scribed so save/reload
  cannot erase why a group left or whether it may return.
- There is no omniscient collective mood. Individual pain, rest, food, mood, injury,
  mobility, equipment, and willingness affect each pawn's resolved Disposition when
  that pawn actually has those trackers. Leaders receive only condition summaries
  that were directly observed or delivered through valid voice, radio, or mental
  edges. Fire-team leaders can aggregate their members' reports before a squad leader
  deliberates; those CA roles and that topology exist only when the group actually has
  them. A native Lord alone is only a flat owned-pawn group.
- The decision is the four-pillar composition: Disposition supplies member resolve;
  Knowledge supplies aged, provenance-bearing operational facts; Authority establishes
  who can call the deliberation and whether others comply; Doctrine assigns objective
  value, acceptable loss, regroup rules, and conditions for return.
- Faction relations and settlement relations are distinct strategic scales. Native
  goodwill and Hostile/Neutral/Ally state remain the faction baseline. Expanded
  villages, towns, and cities gain scribed settlement-level relationships and
  obligations so refuge, resupply, reinforcement, safe passage, retaliation, and
  willingness to support a renewed attempt depend on the places and people involved,
  not only a faction label.
- Technology remains separate from organization. Vanilla supplies one `FactionDef`
  tech level, not a per-settlement level. Each expanded settlement therefore needs a
  CA capability/technology profile, using its faction as a prior while allowing local
  communications, transport, medicine, equipment, infrastructure, and sustainment to
  differ. Leadership, discipline, alliances, and willingness to spend those resources
  come from social structure, roles, relationships, Doctrine, and capable pawns.
- CA delivers an Authority result through the native Lord memo/transition seam to
  move the on-map group into withdrawal. Strategic continuation is CA world state:
  map exit alone supplies no refuge or journey. CA would carry the operation's outcome
  and affect, select an eligible settlement when one exists, represent recovery or a
  support request, and reconsider the unresolved objective only when faction and
  settlement relationships, capability, intelligence, and Doctrine justify another
  attempt.
- The pacifist-hide outcome is emergent. Defenders independently choose and execute
  concealment; searchers exhaust their own evidence; reports reach whatever leadership
  exists; that leadership may conclude that continued expenditure is unjustified. No
  raid outcome is selected because the colony was assigned a pacifist label.
- The operational record, settlement relation graph, deliberation behavior, and
  persistent return campaign are not built yet.

## Combat doctrine arc (operator spec, 2026-07-21)

Humanlike tactical competence derives from skills and traits - no arbitrarily strong actors. Intelligence weighs heaviest; Shooting/Melee/Medicine gate their domains. Animal behavior instead resolves from species, individual temperament, training, relationships, and current condition.

- Positioning awareness: PARTIAL in A12 for actor-local, currently visible
  ranged contact cells scored by shot quality, directional cover, target exposure,
  useful range, travel, and visible friendly-lane risk. REMAINING: chokepoints,
  perimeter apex points, strength of surrounding materials, broader flanks,
  ambush points, terrain navigation speed, group allocation, and leader Doctrine.
- Movement tactics: running-and-shooting, bounding (move while covered by a holder), skill-gated.
- Suppressive fire: emergent focus-fire coordination for high-skill shooters.
- Trap awareness: SHIPPED (TrapAwarenessModule) - every trap SPRING is recorded (map memory, fades after ~1 day); smart hostile humanlikes (Intellectual 5+) then "know" every trap within ~12 of a sprung point via a KnowsOfTrap postfix, and the game's own avoidance pathing routes them around the lane. Dull raiders keep walking in; friendlies already know their own traps in vanilla.
- Field medicine: in-fight stabilization by capable medics, weighted by Medicine skill.
- Civilian/friendly awareness: PARTIAL in A12. Visible non-hostiles in the
  prospective firing corridor now strongly reduce an individual position's score,
  including the engine's artificial zero-interception zone near the muzzle.
  Guaranteed fire discipline, coordinated lane clearance, and protection of
  noncombatants remain unbuilt.
- Enemy restraint: enemies do not execute capturable/valuable targets (children, animals especially) except defensively, and rarely. Implement via attack-target scoring patch on the shared targeting seam; restraint scales with raider intelligence/traits.
- Animal awareness substrate: SHIPPED in A10. Animals now resolve a distinct
  Disposition vector (vigilance, nerve, attachment, defensive drive) from species
  properties, stable individual variation, learned training, master/bond, and
  condition. They retain only firsthand ambiguous gunfire and, on the native
  30-tick animal constant-think lane, may reconsider an initial non-response as
  later pulses accumulate but start at most one bounded response job per acoustic
  event: remain visibly alert, seek a reachable trusted pawn without materially
  closing on the sound, or move strictly farther from it to seek safety. Gunfire
  never identifies a shooter or creates an attack target. Player-forced work,
  master-follow, ropes, Lords, active combat, fire,
  mental states, and starvation keep priority. Passive colony animals are normally
  irrelevant to hostile sight/contact acquisition and attack targeting; actual
  fighting, aggro state, an assigned target, or a short self-engagement window
  removes that protection, with one stable rare low-restraint exception.
- Animal-system continuation (not built): persist recent acoustic-event memory
  across save/load; reuse the same profile for direct-damage choices, herd/pack
  coordination, protective behavior around bonded pawns, concealment and search,
  play/training, and low-amplitude affect states. Each consumer must preserve
  species senses, actual relationships and orders, and the distinction between
  ambiguous evidence and identified threats.
- Explosives arc (later): mines, breaching charges, grenades-with-judgment.
- Raider extraction doctrine (operator spec, 2026-07-21): no kidnapping the dying - stabilize first (v1 shipped: carrier field-dresses a bleeding captive). Drag/pull mechanic: SHIPPED (DragModule) - right-click a downed pawn, "Drag X to...", targeter capped at ~16 cells (a pull, not a transport): sprint grab, 25% move bonus while dragging (CA_Dragging), buddy-carry assists stack; rough handling - dragger below Medicine 4 has a 25% chance to bruise the casualty on the drop. Works on allies (out of the lane, to the medic) and downed raiders (toward extraction). Value gauging of captures; notify superior; retreat with the prize. Squad-leader-level decision to fall back or continue based on communication with allied attacker groups; phased (bounding) withdrawal like reality. Vanilla lord AI is the substrate - extend, don't replace.

## Autonomy doctrine (operator spec, 2026-07-21 - WIP, governs future modules)

The levels stay BEHAVIORAL MODES. What follows is the intended EFFECT when the modes work properly, not a redefinition:
- The player sets intent - positions, holds ("start in the line and wait there", via a shortcutable order/setting to build). The modes govern how pawns respond when holding becomes strategically injudicious (flanked, position collapsing): per what they know, their skills, their leadership quality.
- Proactive working properly = pragmatic collaborative decisions, buffers before dangerous/risky actions.
- Autonomous working properly = sound isolated decisions when leadership is gone and deliberation is internal.
- End-state: the player controls a favorite pawn directly and commands others; the rest work autonomously and their assigned leadership reports to the player.
- Item and action permissions (operator spec, 2026-07-24; PARTIAL): a new colony
  whose default is Proactive or Autonomous now receives its shared starting cargo
  allowed rather than red-X forbidden. Whenever any Proactive or Autonomous
  colonist is present, ordinary visible non-quest items across the colony map are
  maintained as allowed. On a pawn-specific known threat, Proactive+ colonists may
  acquire a suitable reachable weapon and, outside the immediate danger buffer,
  compatible protection. Field medics request allowed medicine through RimWorld's
  native finder. Native item toggles and Forbid/Allow designators made after this
  boundary create scribed player-denial exceptions; stack changes preserve them.
  Existing saves clear the former blanket protection of originless red Xs once.
  REMAINING: category-level interactable-item controls and permitted action classes.
  Higher autonomy is still bounded by visibility, allowed areas, reservations,
  compatibility, and direct orders.
- Domestic planning (built as `a2-28`, live verification pending): Home planning is
  opt-in and treats the player-drawn Home area as bounded colony policy. Proactive
  covers sleeping and eating essentials inside existing claimed shelter; Autonomous
  may add household seating, a light fixture to a furnished room, and one accessible
  horseshoes or chess option. It places one native blueprint and leaves construction
  to native work. Cancel and exact completed-furnishing deconstruct actions create
  scribed player vetoes. REMAINING before broader settlement growth: split the
  controller, need policy, placement solver, and executor; add explicit resource
  budgets and qualified domestic roles; then ratify how individualized Knowledge,
  Authority, social structure, and staged visible growth participate. The locally
  installed Sim Settlements 2 configuration is design evidence for needs, role
  qualifications, budgets, leader effects, staged upgrades, limits, and notices—not
  an engine model to import wholesale.
- Order/mode interaction (shipped): maneuver orders (ambush) at Directed/Standard require DRAFT (piloted play); Proactive is the hybrid (orders drafted or not); Autonomous minimizes manual low-level deliberation. Main-character pattern = the existing dial: your main (often the colony leader role) runs low-mode piloted, everyone else Autonomous.
- Hold-position + deviation + report-up: SHIPPED (HoldModule + standing CATactical Lord) - right-click "Hold this position": walk there, fight from the duty post without melee pursuit, re-anchor if displaced, and persist the standing order through saves. Proactive+ deviation reads only the holder's remembered contacts; live hostile positions enter immediate overrun/flank checks only under current LOS. Deviation = suppressive movement away from remembered pressure + report-up when the leader has command contact. Piloted holds never self-release.
- Mode dynamics - convene-plan-disperse: SHIPPED (RaidResponse) - Autonomous squadded fighters under a live commanding leader rally, plan, and disperse through persistent defensive duties. The leader plan uses that leader's relayed facts; individual positioning uses each pawn's own facts. Cover candidates require actual directional block chance, active defense posts retain their claims, and direct player/emergency/withdrawal control tears automatic defense down. PROACTIVE drafted HOI4 painted lines remain shipped through LineOrdersModule; later polish: multi-segment painting, saved plans, arrows.
- Tactics taxonomy: guerrilla isolated-pawn tactics / organized guerrilla / conventional - a doctrine spectrum units operate along, per skills, structure, and comms.
- Weapon transitions: SHIPPED both halves - kiting fall-back (ambush module) + proactive CQB switch (EquipTransitionModule): dead zone scaled to the weapon, sidearm at close range, melee when adjacent or melee outskills shooting, auto-return after the quiet horizon; Simple Sidearms bridge by reflection with vanilla inventory fallback. In A5, entry distance comes from the pawn's remembered contact cell and a live target is supplied to the sidearm bridge only under current LOS.
- Fighting withdrawals: SHIPPED as solo suppressive or two-element covered bounded movement. Each pawn reads its own remembered pursuer cell and fires only at a currently visible pawn inside weapon range. Plans, roles, phase, destination, draft-at-order state, and owned job IDs persist through saves; later draft changes or forced work cancel/degrade the plan before it can issue another job. The current 30-tick MapComponent state machine remains explicit architectural debt for a later native-duty/JobDriver port.
- Better close-quarters tactics in general (operator, 2026-07-22): the umbrella over corner/door takedowns, weapon transitions (shipped), first strike (shipped), non-lethal incapacitation, and room clearing.
- Hands (operator: "we should give them arms then... this enables dual wielding too" -> "let's stick to the persistent hands"): SHIPPED - skin-tinted hands on the grip of every drawn weapon (two on long guns, one on pistols/melee/each dual-wield hand), drawn from MapComponentUpdate (the DrawEquipmentAiming postfix proved UI-context-only in 1.6). ARM LIMBS PARKED: render-tree node machinery (ArmsNodeModule.cs - SetupDynamicNodes postfix, custom worker, skin colorType, life-stage scaling, baby skip) is built and working but the generated capsule art reads wrong; operator will author arm textures manually - drop them at Textures/CA/Arm.png and restore the one commented install line in ModEntry.
- Dual wielding: SHIPPED BESPOKE (operator: "we have to do bespoke but you can pull the assets and fork because it seems the author is not maintaining"). Tacticowl/DualWield source cloned and read as MECHANICS REFERENCE ONLY (DualWield=GPL3, Tacticowl=no license -> no code/assets copied; ours is original, license-clean). Architecture: offhand = second item in the vanilla equipment container (order-kept, MakeRoomFor/AddEquipment lift-and-re-add keeps Primary = main hand); second Pawn_StanceTracker per pawn ticked from Pawn.Tick; SetStance PREFIX reroutes offhand-verb stances (replaces their 3 fragile IL transpilers with method-boundary patches); TryStartCastOn postfix joins the offhand on the main hand's target; skill-scaled cooldown penalty both hands (offhand worse); Projectile.Launch origin offset; CurrentEffectiveVerb prefers the longer reach; offhand drawn beside the main through the arms seam (one hand per weapon when dual wielding). UX: right-click one-handed weapon (pistol/SMG/light melee heuristic) -> "Equip as offhand" job; Stow offhand gizmo. Persisted via GameComponent + natural container serialization.
- Corner/door takedowns + non-lethal: SHIPPED - doorframes and solid-wall corners now count as concealment (cardinal-adjacent door or impassable full-fill edifice), so "Wait in ambush" works indoors: prison corridors, breach points, room mouths. New order "Wait in ambush (subdue)": the spring is a skill-scaled STUN instead of a kill strike, then the ambusher pins and chokes over the next cycles (faster with melee skill) ending in Anesthetic sleep - downed, capturable, alive, zero wounds. Breaks if they separate or either goes down. Lethal ambush unchanged.
- Ambush aim/crit multipliers: SHIPPED - no verb patch needed; CA_FirstStrike hediff (8s, skill-scaled severity from best combat skill) via the game's own stats: MeleeDamageFactor x1.25/1.4/1.6, MeleeHitChance +2/3/4, ShootingAccuracyPawn +2/4/6. Granted on the ambush spring AND on any ordered attack launched from concealment. Stacks refresh, never double.

## Command and control arc (operator spec, 2026-07-21)

- Command capacity: how many pawns a leader reliably commands = f(Intellectual, Social, and the education/knowledge system already in game). Beyond capacity, orders degrade.
- Sector splitting: smart leaders divide force by geography and total defensive power - hold the river barricade with three, send two behind cover at the rear entrance. Requires threat-direction + defensive-value analysis of the map.
- Communications: headsets extend command range map-wide; without them, command works near the leader and degrades with distance; screaming extends it a bit. Comms quality gates squad cohesion during fights.
- Drafted-but-unassisted squads apply ranger-handbook basics on their own: casualty collection point, security halt, bounding withdrawal.
- Battle drills: stack/breach/clear SHIPPED (DrillsModule) - multi-select team + right-click door: "Stack on door" (wall-hug holds on the team's side, point man = best melee tightest to the frame, alternating sides by lane) then "Breach and clear" (appears once stacked: entry hooks left/right along the inside wall scored first, depth after, min 2-cell spacing, every clearer granted first-strike; works on rooms and open breaches). Locked enemy doors = the explosives/breaching-charge arc later. REMAINING drills: river crossing (covered two-element crossing - generalize the covered-bounded machinery to advance), casualty collection point + security halt (ranger-handbook auto-basics for drafted-unassisted squads).

## Shipped buildables

- Makeshift bridge (CA_MakeshiftBridge): 2 wood, fast, light-structure only, decays to collapse over ~1.5 seasons. Structure menu.
- (REMOVED 2026-07-22) CA_SackBarricade "sacks and rucks" security building - DELETED as incoherent. Error: operator said "cheaper sacks/rucks with plasteel and textiles" about FIELD GEAR; I read "sacks" as sandbags and fused it with rucks into a cover structure. It was a redundant clone of vanilla Sandbags (same texture, Security menu) with a name conflating carrying equipment with fortification. Rucks are carrying gear - that is Apparel_Rucks, which stands. Nothing was built in the save; def deleted clean. LESSON: do not invent a buildable from an offhand phrase; rucks belong to the carry/pack system only.

## Body and sustenance arc (operator spec, 2026-07-21)

- Eating works like RDR2 / San Andreas: caloric intake vs expenditure tracked per pawn; sustained surplus/deficit shifts body weight; weight drives hediff stages (underweight/fit/overweight/obese) affecting move speed, carrying, rest; body type graphic follows weight. Weight feeds ruck utilization.
- Rucks shipped: small 30 textile+10 plasteel = 45 cap; medium 60+20 = 75; large 80+40 = 100; XXL 120+80 = 150 (two full stacks). Utilization = body size, melee, traits (and weight, once the sustenance module lands).

## Offensive operations + night doctrine (operator spec, 2026-07-22)

"A lot of this behavior is either gonna be defensive on our end when we're attacked or we're gonna go out eventually and we're gonna raid other people, probably raiders. And if we have a sneak mechanic working, we want the squad to be able to go at night if possible... that's how the majority of military actions are taken by the United States in terms of infantry. They work at night."
- SHIPPED: darkness shields hiders - spotting range scales with the light on the hidden pawn's cell (pitch dark ~ halves it) unless the spotter sees in the dark (DarkVision gene, or the new CA_NightGoggles - craftable at machining, 40 steel + 3 components, Machining research). Night ambushes and night approaches are now materially safer.
- SHIPPED: leader-relayed drill pinning - select the SQUAD LEADER, right-click a door: "Fire team A/B: stack on this door" (members resolved via command contact); stacks HOLD until told to go; with 2+ stacks anywhere, "Breach all stacks (n doors)" sends every team through SIMULTANEOUSLY - the double-fed building scenario. After a quiet clear (~15s no contact) the team auto-reconvenes outside the entry, ready for the next door.
- The low-mode manual-pinning flow is the named hard part; leader relay is the answer: pin THROUGH the leader, comms gates the relay.
- Future: offensive raid framework rides the territory arc (fog of war = the vision substrate; sneak movement en route; night raid loadouts).

## Field-confirmed 2026-07-22 (operator, in game)

- Order provider surface live (ambush/subdue/hold/covered movement/go).
- Defensive line: "it IS a line, actually. That's pretty cool." Advance on objective: works. "That's a good mechanic. That works."
- Hold stuck-colony bug: FIXED in historical `holdfix-2`; its session-only implementation was superseded by the save-persistent standing CATactical Lord in `e2e-1`.
- RENAMED per operator: "retreat" dropped from movement orders - "Suppressive movement here (solo)" / "Covered bounded movement here (with X)"; the mechanic is direction-agnostic (offense and defense). Settings label now "Suppressive and bounded movement".
- GEOMETRY LAYER: SHIPPED (ShapeKit + PaintManager v2, operator: "solve the geometry problem now... spherical, triangular, free form shapes... an overlay on top of the grid that can post hoc shape formations like walls and colonists") - shapes drawn OVER the grid, rasterized post hoc: Freeform polyline (click points, click last again or Escape to commit), Circle (center + radius, angle-ordered ring), Triangle (3 corners). Two consumers: "Draw formation..." (holds spaced along the shape, with-who fork from single selection, box team from multiselect, commander unlock applies) and "Draw wall..." (wall BLUEPRINTS along the shape, material picker: wood/steel/any stone blocks, placement-validity checked per cell). Click-vertex capture replaced the drag-ride (reliable at any speed); live preview incl. radius ring. Extensible: new shapes = ShapeKit fns, new consumers = commit branches.
- (superseded) FREEHAND PAINTING v1 press-drag (PaintManager) - "Paint a line... (n)" beside the two-click line (both coexist). Press-and-drag: the targeter confirm starts the stroke, the held button rides frame-by-frame sampling every cell crossed (gaps bridged, 200-cell cap, live-drawn), release commits - team distributes into holds along exactly the painted path; Escape/right-click cancels. UNLOCK per operator: "Painting needs a commander" setting (default on) - requires a colonist with Intellectual 6 + combat 6 (Shooting or Melee); gated option shows the requirement; toggle off = always available. Two-click line unaffected by the gate.

## Formations and movement doctrine (VERIFIED against ATP 3-21.8 / Ranger Handbook lineage, 2026-07-22)

Operator law: their ideas are the CENTER, gaps get filled from the AUTHORITATIVE SOURCE (the handbook is online), never invented. Verified taxonomy:
- Fire team formations (only two): WEDGE (basic, ~10m interval - the dispersion-vs-explosives purpose; scale ~4 cells in game, matches blast radii) and FILE (restrictive terrain/buildings/dense vegetation/limited visibility).
- Squad formations: SQUAD COLUMN (default movement, teams in wedges, dispersion+control), SQUAD LINE (max firepower front, assault/pre-assault), SQUAD FILE (narrow terrain).
- Movement techniques (SEPARATE axis, by contact likelihood): TRAVELING (not likely - speed), TRAVELING OVERWATCH (possible - lead element ahead), BOUNDING OVERWATCH (expected - slowest/most secure, overwatch element covers the bounding element).
- TWO SEPARATE SYSTEMS in the mod: (1) DRAWN INTENT - geometry names (line/triangle/circle): perimeters, objectives, watch sectors, flank/choke annotations - the drawn shape is what pawns SCAN/WATCH, the fail-safe against pure AI inference; (2) FORMATIONS+TECHNIQUES - doctrine names: how squads MOVE; technique auto-selected by threat proximity at Proactive+, manual when piloted; react-to-contact branching (in line: down+return fire; or disperse-regroup-reform per threat).
- Foundation-first order (operator): OVERWATCH is the base behavior and currently dumb ("watching for targets") - rebuild it as sector-watching before stacking the advanced flow on it (bound-across-river -> reconvene -> comms next goal -> overwatch for bounding ally -> take ground -> assess cover relative to line -> advance -> objective end -> squad leaders deliberate -> PROVISIONAL SQUAD detached to cover perimeter vulnerabilities).

## Vacuum test protocol (operator, 2026-07-22)

When this set of expansions is finished: back up the save, then spawn a DOUBLE OPPOSING tribal raid - one from the top-right vector, one from the bottom-right of the map - and observe the full stack (squads, comms, raid response, transitions, ambush, survival responses) under real pressure.

## Territory arc: surveying, fog of war, parties (operator spec, 2026-07-22 - next arc, after vacuum test)

- Memory refinement (operator, 2026-07-22): discovery memorization is SKILL-SCALED - higher Intellect remembers discovered areas more quickly, and needs FEWER TRAVERSALS of a path before it is remembered permanently. Traversal-count is the permanence currency.
- Comms refinement (operator, 2026-07-22): beyond inter-squad communication, a DEDICATED RADIO COMMS ROLE (RTO) assignable within the squad.
- (Operator has first-hand infantry training - OSUT - and much more to dictate on this arc.)
- Party mechanic (new): pawns JOIN someone's party / squad / unit. While the party holds, members move as if they had discovered the area already - the head carries the holistic knowledge.
- Lose the head -> the party loses the holistic knowledge; members retain full memory of the ventured path only PARTIALLY and DISPARATELY among them. That elicits varying responses among the pawns.
- Survive and make it back -> all participants have discovered the territory permanently, and can COLLABORATE TO CHART A MAP - an artifact that conveys permanent awareness of that territory to the colony.
- Fog of war + surveying gate what is known vs unknown; discovery is per-pawn, charting promotes it to colony-permanent.
- Operator note (2026-07-22): "pinned behaviors fire off watchers based on pawn vision... Fog of war will come in very handy here" - the ordered behaviors' trigger conditions are VISION-gated (spring/flank/discovery all use LineOfSight as of the fast-watcher restructure); fog of war becomes the unified vision substrate those watchers read from when this arc lands.
- Payoff: QUADRUPLE the land area as the standard option, with SEXTUPLE and OCTUPLE landmass options for beefy computers.
- On the expanded landmass: spawn settlements from tribal to EARLY INDUSTRIAL TOWNS with varying starting opinion modifiers, military strength, ideologies, and cultures.
- Build order (proposed): (1) knowledge substrate - per-pawn discovered territory, scribed; (2) party join/leave + head-carries-knowledge; (3) head-loss fragmentation + responses; (4) return-home permanence + collaborative charting artifact; (5) fog-of-war rendering gated on colony awareness (Real Fog of War proves the seam is moddable); (6) oversized map options; (7) on-map town generation. Heavy pieces are 5-7; 1-4 ride existing substrate.

## Awareness substrate arc (operator spec, 2026-07-22 - the keystone)

Root diagnosis (from playtest): pawns have NO model of the near future and NO deferred value. RimWorld AI is stimulus-response - every tick asks "best action RIGHT NOW given what I perceive NOW." So the ambusher won't patiently wait for an enemy that isn't visible yet, and the hungry pawn eats a corpse while a meal is cooking. Same missing primitive: ANTICIPATION + PATIENCE, which is a LAYER behaviors read from, not a behavior. This is the through-line under everything already built (autonomy dial, drawn intent, overwatch, formations) - they all assume a competence floor the pawns don't have. It is the Factorio move: raise pawn competence so the PLAYER operates at INTENT (combat, politics), not per-pawn micromanagement. A pawn that cannot anticipate cannot be trusted with intent, which forces micromanagement - the fun ceiling.

CONFIGURABLE DEPTH SPECTRUM (per engine-mediates-explicit-controls; simple is an OPTION, richer layers are heavier OPTIONS):

- LAYER 0 - Contextual world-model (the simple option, colony-global). A cheap cached blackboard read each tick from queryable game state: bills-in-progress + ETAs, threat geometry (approach vectors / chokepoints / known+sprung trap lanes), ally intent (other pawns' CurJob + reservations), need trajectories (mine vs the colony's). Behaviors consult it to gain anticipation + deferred value: defer eating the corpse when a meal ETA beats it and hunger isn't critical; autonomously set an ambush on an anticipated approach. HONEST LIMIT: this biases a reactive scheduler, it is NOT a planner - augmented reflex, invisible for these cases, visible at real multi-step planning / deception / long-horizon politics.
  - FIRST SLICE SHIPPED (2026-07-22): ColonyContextComponent (ContextModule.cs) - the cached blackboard, refreshed every ~120 ticks, first fact = meals-in-progress with a REAL ETA (read from each cook's JobDriver_DoBill.workLeft / recipe workSpeedStat). First consumer: eat-smart refactored to consult it - a merely-hungry pawn defers eating a corpse/kibble ONLY when a proper meal is expected within ~1 in-game hour (MealExpectedWithin), instead of the old inline "any meal cooking -> wait forever" scan. This is the anticipation+deferred-value primitive proven end to end. REMAINING Layer-0 facts/consumers: threat geometry (autonomous ambush anticipation), ally intent, need trajectories.

- LAYER 1 - Social fog of war (the complex option; knowledge becomes PER-PAWN and PARTIAL). A pawn knows only: what they personally witnessed (in sight/present) + what propagated through their social ties (their milieu) + what came via official channels (leadership/comms). Events emit knowledge tokens that spread through the SOCIAL GRAPH over time, trust-gated (reuse the gossip Lands formula: Social x trust, resisted by Intellectual). Behaviors read the pawn's OWN known-facts, never global truth (already the stated gossip-substrate rule).
  - FIRST COMBAT FACTS SHIPPED through A3–A5: identified contacts and ambiguous audible cues are pawn-private, age-preserving, one-edge-per-pass facts. Wider social trust, misinformation, and non-combat fact families remain future work.

- LAYER 2 - Larger colonies. Information moves quickly through close social ties and more slowly between separated groups. Pawns trusted by both groups, leaders, and communications roles carry reports across those gaps. A small connected colony shares most news; a divided colony develops several partial views.

Shared model with regional discovery: pawn awareness combines what the pawn has physically discovered with what the pawn has learned from others.

PLAYER-VIEW FOG (resolved 2026-07-22): it's a TOGGLE, not a fork. Default OFF = player keeps god-view, only pawns act on partial info (RimWorld-normal, safe). Opt-in ON = the player's own view is gated to what the colony collectively knows - applies to BOTH axes (spatial: only charted/currently-seen territory renders; social: the relations graph / event feed shows only what has propagated to someone). The most Factorio-like mode, available to those who want it, never forced. Per engine-mediates-explicit-controls: a disclosed optional control, off by default.

Payoff: rumors, misinformation, subfactions acting on stale/partial info, leaders as information hubs, comms roles as bridges - emergent politics as a playable layer instead of a stat readout.

## Other queued

- Subfaction relations graph (cluster opinion + fact-flow data; new main tab).
- Dynamic organizational identity (operator spec, 2026-07-21): classify what the colony IS - cult, military, paramilitary, mercenary/for-hire, supremacist group, commune - computed from average age, gender composition, traits of those holding leadership (military = squad leaders, workforce = work-priority structure, political/ideological = Ideology leader/moralist roles, usually founders). Label recomputes as composition drifts; displayed atop the subfaction graph; later drives outward consequences (trade, raids, joiners, faction reputation).
- Gossip/knowledge substrate: typed facts propagated through social interactions, trust-gated (opinion). Behaviors read known facts, not global truth.
- Social engineering (operator spec, 2026-07-21): colonists influence how others perceive THIRD parties. New interaction family - praise/poison: A tells B about C; B gains a social memory toward C weighted by A's Social skill x B's trust of A (opinion), resisted by B's Intellectual/skepticism and B's own firsthand history with C. Repeated exposure compounds (propaganda). Risk: overheard by C or C's allies = backfire memory toward A. Manipulation becomes a playable vector and a visible edge type in the subfaction graph. Expand on the Psychology-mod lineage (therapy, personality) where present.
- Hospitality/logistics by area (warden, guest, patient care under area duty).
- Duty-area assignment decoupled from allowed areas (movement never restricted).
- Psych parameterization: per-pawn parameter vector driving all thresholds; optional event-driven LLM hooks (raid planning, social crises).


## Consistency-audit backlog (2026-07-22, decompile-grounded, 54 findings; criticals/majors fixed in build audit-1)

Fixed in audit-1: survival pump ungated from raidResponse; ordered-hide arrival routes
through SetHidden + concealment pin holds ranged fire; choke-out actually sedates
(0.9 severity, 270-tick stun); flee-response suppressed for stacked/held/hidden pawns
(vanilla psychic-ritual precedent); EquipFromInventory can no longer void a weapon;
moving-fire hediff self-expires + kill-switch sweep + respects FireAtWill/stuns/native
targeting; offhand: ability casts no longer trigger it, stale-registry wielders freed,
stun pauses it, stow gizmo always reachable, melee-main ranged offhand actually fires
(TryGetAttackVerb twin); spike-trap springs recorded via prefix snapshot; trap memory
thread-safe + scribed; fire response ignores burning enemies + honors work priority 0;
animal feed no longer churns instant-fail jobs + rescue gets vanilla safety gates +
no double-feed pile-up; meal ETA honest during ingredient hauling; registries cleared
on load; hold hygiene (watchdog reset/cleanup, checkOverrideOnExpire); portrait menu
moved to right-release so right-drag reorder works again; paint targeter chains
natively (needsStopTargetingCall) + map-switch cancels; per-block settings gates;
child-target restraint is a weighted roll + combatant exemption (no raid stall);
honest settings copy (holds session-only; eat-smart real scope).

Closed in `a2-20`: the Withdrawal Duty/Driver port. Its scribed plan is retained,
but one persistent native job now owns each participant and the 30-tick component
performs integrity repair rather than phase-level `StartJob` supervision.

Closed in `a2-22`: Withdrawal firing halts now use `CastPositionFinder` against an
honestly visible pursuer inside the strict seven-cell bound. The 1.6
`maxRangeFromLocus` clipping defect is avoided by an explicit nominal-halt
validator, and geometry fallback retains the same progress and safety gates.

Deferred (ranked):
- Closed in `a2-25`: FireResponse ends only unforced leisure/idle work when the
  native emergency scanner can answer a Home fire; direct assignment remains only
  for parentless fires on player buildings outside Home, with the handled-fire and
  native safety gates retained.
- AnimalCare downed-no-bed feeding needs a custom driver (vanilla FeedPatient
  hard-requires bed+medical-rest; current fallback drops food at the animal).
- Rucks: move flat capacity to equippedStatOffsets (info-card visibility), StatPart
  keeps only utilization scaling, per-def values via DefModExtension.
- Arms drawing: use PawnRenderUtility.CarryWeaponOpenly + vanilla EqLoc constants +
  recoil/life-stage scaling; arm-node revival needs NO Harmony
  (DynamicPawnRenderNodeSetup subclasses auto-register).
- Painting: optional Designator port (native drag capture) for freeform.
- BeginLineTargeting: move world-line preview out of the OnGUI-phase delegate.
- Offhand pendingReAdd: add a safety sweep for broken MakeRoomFor pairings.
- EnemyRestraint: consider the psychological-invisibility hediff channel for hides
  (native ThreatDisabled consumers incl. turrets); slim the downed double-penalty.
- EatSmart: exempt pawns whose genes make them indifferent to raw food.

## Spatial intent after the first live surface receipt — T-013, T-014, T-015

`a2-37` through `a2-41` proved the native designator, colored overlay, census, and
footprint enforcement, then exposed the wrong model during operator review. The
solver-painted 49-cell Sleeping room was the operator's intended kitchen; the two
parallel rooms were the intended barracks. F-49 and DR-34 supersede the atomic
functional-use binding and retire the solver-selected painting fixture.

Authored space programs now carry their own saved identity, name, purpose, planning
style, maximum occupancy, capability requirements, authorship, and optional explicit
resident roster while remaining independent of vanilla stockpile/growing zones,
Home, and pawn allowed areas. Bedroom is a distinct private sleep-capable purpose;
Barracks remains communal and also admits compatible seating and recreation. A
resident belongs to at most one sleep program, assignment transfers between
programs, and an authored roster sets that program's required sleep capacity without
restricting movement. Unassigned colonists still distribute across unrostered sleep
programs after usable outside capacity is counted. Austere programs resolve to
sleeping spots; other initial styles prefer single beds with a spot fallback. A
completed CA-planned single-slot provision uses native bed ownership for its next
unprovided resident. Builds `a2-43` through `a2-54` carry the editor, Bedroom,
resident UI, saved roster authorship, negotiation, exact Dolly fixture bridge, and
full-restart multi-program receipt.

Resident rosters now carry separate authorship. Player edits freeze the roster;
unassigned or resident-authored rosters may be filled by Autonomous residents while
valid choices remain stable. Owned beds anchor occupants, native love clusters seed
Bedrooms, and mutual opinion guides remaining Barracks choices. The two-Barracks
fixture now proves authority handoff, exact Dolly restoration, love-cluster Bedroom
seeding, scored multi-program choice, stable rosters, and ordinary Autonomous
planner consumption. The exact restoration remains transient; no derived fixture
has been saved.

Shared Bedroom provision now uses native relationship and ideology willingness.
Build `a2-57` live-proved selection, ordinary hauling/construction, and native
two-owner assignment for Luc and Dolly; negative consent loss, existing unowned-bed
claiming, save/load, and owned-bed anchoring remain separate receipts.

The exact saved material demand and first native wood producer shipped in `a2-58`
and received a full-restart live receipt in `a2-59`. Forestry retains player
provenance, skill and ideology gates, cultivated or important vegetation, authored
programs, generic Home-perimeter cover, Defense programs, and only threat facts
known by the responsible pawn. The current home controller still requires the
planned split between need policy, placement solving, prerequisite state, and
blueprint execution.

Build `a2-60` completes the first storage-program projection for temporary
construction-demand staging. It classifies loaded items from runtime stats and
comps, reuses only native storage with sufficient currently usable capacity, or
creates a bounded Important native stockpile filtered and sized to the exact
deficit. Saved provenance distinguishes it from player storage; player edits take
authority, exact-objective deletion creates a veto, and closed empty projections
retire. The deterministic live receipt proved a one-cell 45-wood reserve beside,
but not on, its Bed objective and gated one native forestry designation. It did not
approve visual placement or yet prove hauling, cleanup, or save/load. Build
`a2-61` subsequently proved native deletion, exact-objective veto, and recreation
only after Home-planning authority was explicitly renewed. Player edits to label,
priority, filter, or footprint share one ownership predicate; build `a2-62` proved
label-edit transfer, preservation and reuse of the player-owned zone, and no
automatic replacement. The other edit variants were not each repeated live.

Build `a2-63` then proved native material flow and cleanup: wood moved directly
into the first authorized Bed blueprint, its closed empty reserve retired after the
grace period, and 16 surplus units entered the next objective's new native reserve
through ordinary hauling. Build `a2-64` then preserved the exact demand, source,
zone ID and cell, projection, and veto state through a native save/load and removed
its disposable fixture afterward. The operator subsequently approved the central
Bed placement and bounded 75-unit woodpile. Build `a2-66` then proved normal
operational access on a fresh paused first frame without the stock-reserve fixture:
all 1,879 ordinary visible items were allowed, including every counted food,
medicine, weapon, apparel, and raw-material stack. Build `a2-75` now projects the
first durable inventory Storage programs from actual nearby needs, loaded item
properties, and player-authored base space, with safe Autonomous provisional space
as the unprogrammed fallback. A live playground receipt created bounded Medical,
Drug, Food, Armory, Protected-material, Material, and General native stockpiles,
retained 910/910 initial operational access, and observed the first native haul into
Medical stores. Operator visual judgment remains open. Resource budgets,
source-area controls, category/action policy, qualified work roles, and freezer,
workshop, medical, animal, utility, and defense construction remain later settlement
work. The ratified regional program below
owns the next architectural dependency after this operator-prioritized local gate;
it does not retroactively make the construction buffer a general warehouse.

Strategic topology follows as a separate axis: central core, northern or southern
frontier, reserve expansion, and fallback positions describe how colony spaces
relate, not what a room contains. The verified visual corpus now includes the
mature bird colony, the four-person river settlement, the controlled playground,
and the second developed mountain settlement, plus community examples selected for
different viable planning grammars. At least two custom operator ideologies and
multi-ideology colonies make actual pawn belief, authority, and social composition
required placement inputs rather than a post-hoc style pass.

Build `a2-78` adds the saved, inspectable settlement planning context without yet
changing construction. Its census exposes actual ideology and resident
participation, existing programs, built form, native zones, terrain evidence,
strategic span, and historical revision. Four corpus saves and a native reload now
prove that the receipt distinguishes plural belief and settlement form. The next
bounded slice may let one Autonomous proposal consume this context, but its receipt
must show which legibility, social, ideological, environmental, operational,
historical, and strategic evidence changed the candidate ranking. Corpus examples
remain evidence for varied rules; no example is copied as a universal floor plan.

The immediate visual corpus is the controlled playground with its two intended
parallel barracks and intended kitchen. Verification must use player-authored
programs; no solver may choose and paint its own test room.

Build `a2-79` closes that immediate bounded slice for new Beds. An Autonomous
proposal inside a player-authored sleep program now consumes the saved context only
to re-rank cells that already pass the existing placement gates. Its receipt shows
the pre-context winner, selected candidate, and separate legibility, social,
ideology, environment, operations, history, and topology contributions. The fixed
playground test reported a real ranking change without painting a solver-selected
room. Build `a2-80` hardens that proof: the program must exactly equal all 60 marked
room cells, and the normal planner's active or retained result must match the Bed
kind, program ID, selected footprint, and context-caused ranking change. The
retained wood demand and native producer path remained intact. Expansion to other
furnishings, room creation, or corpus-shaped layout generation remains future
work. Before use on very large programs or additional furnishing kinds, invariant
context queries should be precomputed once per ranking pass. The next visual
judgment is the eventual built composition after native work supplies the Bed.

## Regional living-world program — T-019–T-026

The current regional implementation uses one direct model: regions contain
factions and settlements. Factions own culture, Ideoligion, political beliefs,
faction structure, settlement authority, and faction era. Settlements own
population groups, form, role, starting facilities, access, services, civic
development, economic capacity, trade connectivity, historical development,
starting provisions, and derived capability. Settlement pattern and urban scale
are realized results, not independent authoring categories.

Built and statically verified:

1. **Regional geography.** One projection carries the selected visual land shape,
   coast, water depth, roads, rivers, caves, and supported geographic features.
   Settlement positions resolve on valid visual land.
2. **Current pending-plan persistence.** One keyed file per world identity stores
   the confirmed candidate directly as `factions`, `settlements`, relations,
   population groups, facilities, infrastructure, and provisions. The current
   authored composition is converted to this schema.
3. **Faction and settlement authoring.** Culture, Ideoligion, political beliefs,
   faction structure, settlement authority, settlement form, facilities,
   infrastructure, population groups, and provision distribution are independently
   readable and editable where they affect generation. Settlement placement,
   relations, population, land, access, services, civic development, economic and
   trade conditions, specialization, regional role, and history produce the saved
   settlement pattern and scale.
4. **Materialization.** The confirmed candidate creates native factions,
   settlements, residents, Ideoligions, organizations, buildings, provisions,
   geography, and faction relations. Derived capability reads faction era and
   local supports.
5. **Schema convergence.** Organizations save offices, groups, customs, security,
   agreements, policies, claims, relations, and decisions under those names.
   Political-belief effects use the faction's saved beliefs directly. The
   pre-release political tier, territorial constitution, old settlement population model,
   duplicate federation caches, separate political precepts, derived ideology
   classifier, old coast modes, and their migration paths are removed.

Immediate runtime gates, in operator order:

1. Restore the keyed authored composition without losing factions, settlements,
   population groups, world tendencies, arrival area, or confirmation.
2. Start generation from that exact candidate without “regional bundle
   incomplete” or “select an arrival area” failures.
3. Confirm that the generated map matches the preview's visual land shape and
   that selected geographic features resolve inside it.
4. Confirm that settlement identifiers, buildings, residents, facilities, and
   provisions appear on the intended land and reflect the authored settings.
5. Record performance and generation receipts for the selected 350-cell local
   scale and multi-area backing map.

After that runtime gate, continue the same model into off-map economy, reports,
claims, operations, diplomacy, conflict, and map re-entry. New records must attach
to current factions, settlements, population groups, and organizations instead of
creating another political identity tier.

## Strategic facility and toxic-waste continuation — A42–A47 / T-015, T-017

Build `a2-84` provides the observation boundary for the next local settlement
slice. `facility-siting-census` now distinguishes room footprint, enclosure,
natural-roof protection, mineable boundary, temperature, grid proximity,
habitation, access, stockpiles, programs, turrets, Defense programs, wastepacks,
and atomization capability without assigning a facility purpose. The controlled
playground receipt is the baseline comparison for the operator's exposed
top-left Kitchen intent and protected interior storage logic; no coordinate is a
portable rule.

The ranked continuation is:

1. Add receipt-bearing facility requirements to player-authored Kitchen, Freezer,
   Storage, Utility, and Defense programs. Compare candidates across loss
   criticality, hazard, service, logistics, habitation, defensive topology,
   bounded footprint, and expansion evidence. Do not collapse them into depth or
   market value.
2. Prove the exposed-Kitchen/protected-store distinction in the controlled
   playground, then repeat the same rule set on at least one developed mountain
   save and one aboveground save. Operator visual judgment decides whether the
   result reads correctly.
3. Add a toxic-waste lifecycle objective with explicit inventory, capacity,
   freezing, relocation, transport, destination, and consequence records. The
   first gate is bounded frozen staging; the mature gate is a detached,
   grid-connected, reachable, controlled, defended facility separated from
   habitation.
4. Extend that objective through the regional economy and conflict substrate so
   player and nonplayer settlements select among retention, remediation,
   negotiated acceptance, remote export, coercive dumping, retaliatory return,
   and hostile pollution according to actual capability, Knowledge, Authority,
   ideology, relationships, relative power, attribution risk, and expected
   response. Every strategy moves or transforms real waste and reconciles both
   actors' ledgers.
5. Verify abstract/materialized parity with at least three deterministic cases:
   a low-capability society that must retain or locally externalize waste, a
   capable society that remediates it, and a hostile society whose offloading
   creates pollution, grievance, and an attributable retaliation path.

Build `a2-85` adds the pre-authoring comparison receipt. It identifies the
operator-intended exposed Kitchen room in the controlled playground and separately
surfaces the strongest natural-roof store envelope plus its current-use and
defensive unknowns. This validates the evidence ordering only; player-authored
facility requirements and any construction consumer remain the next source gate.

Build `a2-86` fulfills steps 1 and 2 at the read-only evaluation boundary.
Existing player-authored Kitchen, Freezer, Storage, Utility, and Defense programs
now receive separate requirement axes plus distinct present-readiness and future-
potential receipts. The exact controlled Kitchen fixture and transient authored
Storage footprints in one developed mountain save and one aboveground save passed
under the same final DLL. No facility construction consumer was enabled. The next
ranked source gate is step 3's explicit toxic-waste lifecycle objective; this
receipt does not implicitly authorize construction, relocation, shipments, or NPC
strategy execution.

Build `a2-87` completes step 3's first, read-only frozen-staging gate. A saved
player-home objective now begins only from real recursively observed Wastepack
inventory and records lots, bounded authored capacity, actual freezing,
relocation need, local destination, transport, and consequences separately. The
developed mountain proof moved from honestly blocked with no authored candidate to
`FrozenStaged` only after the player authored an exact Storage footprint over an
existing native frozen store. Its deterministic receipt survived native
save/reload; the controlled and aboveground zero-inventory cases fabricated
nothing. No reservation, haul, construction, shipment, pollution, relationship,
or NPC strategy consumer is enabled.

The next ranked local source gate is a separately player-authorized relocation
reservation and native hauling consumer. Before it can act, staging capacity must
be proven through native haul-valid storage and reservation semantics rather than
the current physical-slot evidence, and its live fixture must begin with real
waste outside an adequate authored frozen destination. Facility construction and
the mature detached/defended toxic site remain later gates. Remote export,
contracts, coercive dumping, retaliation, and hostile pollution remain dependent
on the regional shipment, Authority, Knowledge, relationship, and consequence
substrate.

Build `a2-88` completes that local relocation gate. The existing program editor now
owns explicit authorize/revoke controls; a saved authorization binds exact source
units to one existing player-authored, presently ready destination, and a hidden
native Hauling WorkGiver performs only the reserved local move. The clean mountain
proof saved an active job with both reservations, reloaded it natively, completed
1/1 authorized unit with zero failures, then overwrote the continuing working test
save while retaining specific active and resumed checkpoints. The player's real
save and all original baselines remained unchanged.

The next gate is the smallest world-owned shipment/operation record that can carry
a real bounded Wastepack reservation from this completed local lifecycle without
yet choosing a destination or faction strategy. It must preserve separate origin,
carrier capacity, route, destination acceptance, Authority, Knowledge,
attribution, inventory, pollution, goodwill, grievance, and conflict fields; it
must remain inert until a later player or society decision supplies an actual
strategy and destination. Mature detached-facility construction remains a separate
local branch rather than an implicit consequence of this hauling proof.

Build `a2-89` corrects the binary assumptions in that continuation. An allowed
Wastepack now triggers autonomous local containment under the authority of an
existing player-authored native storage policy; no second per-haul authorization
is required. Roofed accepted capacity and actual placement are independent from
the timestamped temperature, frozen, and dissolution observations. Live proof
completed and persisted one native haul while the same room cycled from below to
above 0 C, retaining the destination and reporting `ContainedWithThermalRisk`.

The next ranked source gate is now a read-only, actor-capability strategy-feasibility
evaluator before any world shipment or NPC executor. It must preserve multiple
feasible responses rather than predict one predetermined outcome, and must be
verified against at least one industrial settlement with existing containment and
remediation, one low-technology society without refrigeration or waste-generation
technology, and one society for which acceptance, transport, or hostile offloading
is materially possible. Technology, infrastructure, transport, volume, urgency,
ideology and ethics, Authority, Knowledge, relationships, target knowledge,
relative strength, attribution risk, and expected retaliation remain separate
receipted axes.

A shipment or operation is created only after a selected strategy actually needs
one, names a real or abstract destination, reserves real waste, and can reconcile
inventory, pollution, Knowledge, grievance, goodwill, and conflict. Local
retention may reuse existing storage or atomization without fabricating redundant
construction. Cooling or freezing is one possible tactic, not a universal gate.
Parity tests compare conserved state, commitments, causal evidence, and
consequences for the branch taken; they do not require unseeded simulations to
converge on one outcome. Mature detached-facility construction remains a separate
local strategy branch rather than the automatic next step.

Build `a2-90` corrects the storage boundary before either future branch proceeds.
RimWorld's native stockpile footprint, filter, priority, storage buildings, and
mixed contents are now the durable-storage schema. The former CA category-zone
generator is retired through a save-compatible provenance migration, and new
Storage room programs are no longer authored. Facility receipts use actual
contents and native storage policy, treat thermal state as dynamic risk evidence,
and judge construction maturity locally. Controlled exposed-Kitchen, developed
mountain, aboveground no-program, and seven-record legacy-migration receipts passed
under the same final DLL without enabling construction.

The immediate local storage gate is a read-only shelf/capacity candidate evaluator
over existing native `Zone_Stockpile` footprints. It must read the loaded storage-
building footprint, current capacity and contents, native filter and priority,
doors and approaches, circulation, service cells, negative space, established
furnishing patterns, construction access, and actual material capability. It must
receipt why a shelf helps and why each rejected placement would obstruct or
degrade the zone. Mixed storage is not itself a defect and an empty cell is not by
itself a shelf requirement. Candidate proof must cover the developed mixed fridge,
at least one sparse aboveground stockpile, and the operator's visual judgment.

After that proof, the selected native zone receives a CA initiative ceiling using
Directed, Standard, Proactive, or Autonomous, defaulting to Standard. Effective
initiative is the lower of pawn autonomy and zone autonomy. Native priority and
filters remain unchanged and orthogonal. The zone control and its operative shelf
consumer ship together; there is no inert checkbox phase. The consumer may place
only bounded native shelf blueprints supported by current need and authority, and
must preserve Feng Shui, player edits, native construction work, and exact
provenance. It does not paint or reclassify a stockpile.

The toxic-waste continuation remains a separate read-only actor-capability
strategy-feasibility evaluator before any world shipment or NPC executor. It still
requires industrial, low-technology, and materially offloading-capable society
cases with technology, infrastructure, transport, volume, urgency, ethics,
Authority, Knowledge, relationships, target knowledge, relative strength,
attribution risk, and expected retaliation kept as separate axes. Neither the
shelf branch nor the strategy branch implicitly authorizes a detached facility,
shipment, bargain, coercive dump, retaliation, hostile pollution, or NPC action.

Build `a2-91` completes the read-only spatial-furnishing proof. One evaluator now
reads existing native stockpiles and player-authored room programs, preserves the
native room/stat/facility/storage graphs, exposes exact shelf-density transitions,
and discovers functional Core, expansion, and modded furniture from loaded
definitions. Controlled Kitchen, sparse aboveground, and developed mountain
receipts passed without a gameplay mutation. Pure visual props remain outside the
utility model unless an explicit adapter supplies truthful semantic evidence.

The next local gate is one shared per-space initiative-ceiling substrate using the
existing Directed, Standard, Proactive, and Autonomous levels. It must remain a
single parameter across native stockpile authorities and authored room programs,
while each UI surface appears only when an operative consumer exists. The first
bounded consumer is native shelf/capacity work inside a selected stockpile: it may
act only when density or access evidence justifies the change, must preserve the
exact zone-to-storage-building policy transition and Feng Shui axes, and must use
native blueprints and construction work. Bedroom, Barracks, Laboratory, Workshop,
and other room furnishing consumers follow through the same facility/stat graph,
not separate checkbox products.

Before treating an external asset pack as supported, create an isolated optional
compatibility profile. Start with Vanilla Furniture Expanded because its required
framework is already present in the test profile, then add one storage-focused
pack separately. Verify source provenance, room effects, facility links, storage
capacity, research, materials, save compatibility, and removal behavior without
making any content pack a hard CA dependency. Adaptive Storage Framework requires
its own audit because it intentionally extends storage capacity, rendering,
temperature, grouping, and selection semantics. No Workshop installation is
authorized by this roadmap entry.

Build `a2-92` completes the first operative per-space initiative slice. Native
stockpiles and player-authored room programs now share one saved Directed,
Standard, Proactive, or Autonomous ceiling, while only stockpiles expose a control
because only storage has a consumer. The consumer uses native storage definitions,
blueprints, settings, frames, jobs, and completion; it preserves the remaining
floor stockpile, current contents, access, Feng Shui, player cancellation and
deconstruction authority, and stops when real combined capacity removes the need.
Controlled, sparse aboveground, developed-mountain construction, and save/reload
receipts passed under the final DLL. The clean preplacement checkpoint and cleaned
continuing working save are both retained under specific existing names.

The next local furnishing gate is the first room-owned consumer on this same
substrate, not another setting product. It begins from an existing player-authored
Bedroom or Barracks footprint and an actual missing native facility or room-stat
relationship around its existing beds. Candidate definitions, including dressers,
end tables, plants, or loaded functional furniture, remain definition-driven and
must preserve residents, bed approaches, circulation, service cells, negative
space, culture/style evidence, research, materials, and player edits. The room
initiative control becomes visible only when that bounded consumer is operative.
Laboratory, Workshop, Medical, and other room consumers follow through the same
native room-role, stat, worktable-factor, and facility-link graph; none receives a
separate checklist.

Before an external furniture or storage pack becomes supported, keep its optional
compatibility profile isolated and verify source, facility links, room effects,
capacity, settings transition, rendering/grouping extensions, research, materials,
save/load, and removal. No Workshop subscription is implied by the native room
consumer gate.

The toxic-waste continuation remains the separate read-only actor-capability
strategy-feasibility evaluator before any world shipment or NPC executor. The
completed shelf work does not imply a detached facility, refrigeration, export,
contract, coercive dump, retaliation, hostile pollution, or society strategy.

Build `a2-93` completes the first room-owned consumer on the shared spatial
initiative substrate. The exact Dolly fixture now has eight authored residents
owning eight completed beds, and one native Dresser family uses two functionally
placed buildings to cover the split bed bank without consuming protected travel,
bed intervals, sleep approaches, or the room's role. Context comparison may
confirm the same geometric winner; the dedicated ranking-change regression stays
strict. Player blueprints and frames, other CA construction commitments, program
revocation, native facility links, and completed-building ownership are explicit
authority boundaries. Stable welfare memory also now separates semantic revision,
source time, and delivery freshness.

The next room-furnishing gate is a controlled derived-save authority regression,
not another furniture family. Prove at runtime that an in-room player facility
blueprint or frame pauses CA, that changing or deleting the authored sleep
authority cancels only a tracked pending CA blueprint, and that a completed
player-owned furnishing survives while its stale CA association retires. Retain
the operator's visual judgment over functional posture and negative space before
extending the consumer to Bedrooms, laboratories, workshops, medical rooms, or
optional furniture packs.

Animal infrastructure remains read-only. Verify the same evaluator on Academy,
where animal veneration may differ, and at least one additional society before
selecting any posture. Pen management, installed/minified beds, bonds, resident
rooms, training, veneration, and defensive potential remain separate. Existing
installable beds must be considered before new construction. A pen, animal-bed
placement, or sentinel consumer requires its own player-authority and initiative
boundary; sentinel behavior additionally requires real hostile perception,
grounded alarm/report semantics, and delivery to actual residents.

Critical-asset storage also remains read-only. Continue through actual native
destinations and any player-authored Medical or Defense footprint without
recreating storage filters or priorities. A natural-roof Important medicine cell
is partial positive evidence; relocation requires a real safer accepting
destination and must preserve treatment access, deterioration, temperature,
settlement integration, edge exposure, logistics, zone authority, and provenance
as separate axes. Historical legacy records describe zone origin only, never
later item movement.

The toxic-waste continuation remains the separate read-only actor-capability
strategy-feasibility evaluator before any world shipment or NPC executor. Room,
animal, welfare, and critical-asset work does not authorize a detached facility,
new refrigeration, export, contract, coercive dump, retaliation, hostile
pollution, or society strategy.

Build `a2-94` closes the controlled derived-save room-authority gate. A normal
player-faction facility blueprint and its real native frame transition both block
parallel CA origination without being adopted. Revoking sleep authority cancels
only tracked pending CA construction. Deleting the authored program after
completion retires only CA associations while the player-owned furnishings,
native beds, and bed ownership survive. Every transient mutation was exact-SHA
bound, wrote no save, and was followed by reload; both retained checkpoints
remained byte-identical.

The next ranked local gate is read-only animal-infrastructure verification on
Academy and at least one additional society. Reuse the existing evaluator and
receipt actual ideology veneration, native rope management, installed and
minified beds, bonds, masters, training, resident-room membership, pen or hitch
infrastructure, and genuine approach topology as separate axes. The proof must
show both a society where veneration materially changes the feasible posture and
a contrasting society where it does not, without treating veneration as a
universal exemption or requirement. It authorizes no pen, animal-bed placement,
assignment, sentinel behavior, Defense program, or construction consumer.

The next room-furnishing extension remains on the shared native facility and
room-stat graph after that evidence gate. Bedroom, Laboratory, Workshop, and
Medical consumers must each begin from actual authored purpose and native
relationships; none receives a separate furnishing checklist or Beauty-
maximization objective. Optional furniture packs remain isolated compatibility
profiles until their real facility, room-stat, research, material, style, save,
and removal behavior is verified.

The toxic-waste continuation remains the separate read-only actor-capability
strategy-feasibility evaluator before any world shipment or NPC executor. The
authority proof and animal verification do not authorize new refrigeration,
detached facilities, export, contracts, coercive dumping, retaliation, hostile
pollution, or society execution.

Build `a2-95` closes the read-only multi-society animal-infrastructure gate.
Faction identity, primary and minority ideologies represented by spawned free
colonists, venerated species, present matching animals, bonds,
assigned/effective masters,
training, sleeping, native pen management, installable assets, and defensive
topology now remain separately receipted. Academy and Red Cube both supplied
live veneration cases; five Red Cube-venerated horses inside enclosed native pens
prove that veneration constrains harmful and food-use treatment without erasing
containment. The Dolly fixture correction establishes that its no-veneration
result belongs to New Arrivals/Astropolitan, not Red Cube. No animal consumer is
enabled.

The next ranked local gate is a read-only authored Bedroom requirement comparison
on the existing spatial-furnishing substrate. Its exact retained test checkpoint
must contain a player-authored Bedroom with actual residents and native beds; the
evaluator reads that authority and never paints its own room. Privacy and love-
partner structure, resident ideologies, bed ownership, native facility and room-
stat relationships, current furnishings, approaches, circulation, negative
space, style, research, materials, and construction stage remain separate axes.
The comparison must prove present readiness separately from future potential and
must preserve an already functional room rather than manufacture a missing
object from an available bonus. No Bedroom construction consumer is enabled until
the receipt passes on the controlled checkpoint and a developed save.

Animal construction remains a later authority gate. A pen, sleeping-place, or
sentinel consumer requires an authored spatial authority and the same four-level
initiative ceiling; veneration remains a per-adherent constraint rather than a
mode. Sentinel behavior additionally requires actual hostile perception, a
grounded alarm/report channel, delivery to real residents, and genuine approach
topology.

Critical-asset and toxic-waste continuations remain separate read-only branches.
The animal proof and Bedroom comparison do not authorize storage relocation, new
refrigeration, detached facilities, shipments, contracts, coercive dumping,
retaliation, hostile pollution, or NPC strategy execution.

Build `a2-96` closes the read-only authored Bedroom requirement comparison.
Existing Player-authored Bedroom footprints now expose authority, residents,
native bed ownership and sharing, room classification and stats, spatial
evidence, current contents, construction stages, and optional future capability
without collapsing them into a scalar. A functional controlled Bedroom produced
no work from unused facility bonuses; the developed mixed room kept its empty
authored roster distinct from seven native bed owners; the aboveground save with
zero authored Bedrooms remained unpainted. New Bedroom origination and its
initiative UI remain disabled.

The next ranked local gate is a controlled authored-Bedroom construction-cause
regression, not general Bedroom furnishing activation. Begin with a saved
Player-authored Bedroom whose real roster has one concrete unmet native need,
such as an eligible resident lacking owned sleeping capacity inside the exact
footprint, paired with the retained functional Bedroom as a mandatory zero-work
control. The cause receipt must select the native action class that actually
addresses that deficit through the existing home-planning and spatial-initiative
substrate. Optional Dresser, EndTable, SleepAccelerator, Beauty, or other bonus
capacity remains future potential and supplies no cause.

Before any narrow Bedroom action is enabled, prove the four-level space ceiling,
single map-wide CA construction commitment, player blueprint/frame precedence,
resident and ownership preservation, protected approaches and negative space,
exact-SHA rollback, save/reload reconciliation, and operator visual judgment on
the controlled checkpoint. Re-run the developed and aboveground contrasts to
prove that mixed-room conflict and absence of authored authority still originate
nothing. Only the specifically proven unmet requirement may cross from evidence
to construction authority.

Build `a2-97` adds the operator-requested developer God Mode item mover without
changing the ranked gameplay gate. With Dev Mode and God Mode active,
Ctrl-left-drag moves the topmost spawned non-corpse item stack to one exact valid
cell; short Ctrl-click remains native selection. The tool preserves whole-stack
identity, rejects blueprint, frame, full-capacity, wiping, or physically invalid
destinations without mutation, refreshes native location caches, and grants no
storage, hauling, program, furnishing, building, or pawn authority. Live
buildings remain on RimWorld's native Reinstall path.

The final DLL and focused review are complete. Runtime activation and operator
interaction proof wait only for a safe full restart after the current in-memory
`Debug Base Expansion` work is saved. The next ranked local gameplay gate remains
the controlled authored-Bedroom construction-cause regression described above.

Build `a2-98` corrects the failed first interaction of the developer God Mode
item mover. A loose weapon was always an eligible item; the `a2-97` hook simply
ran after active spawn, designator, and targeting tools had consumed the press
and also rejected those states itself. Eligible Ctrl-left item gestures now take
first-priority ownership before those map consumers, preserve a competing tool
across a real drag, resolve the first eligible item beneath overlapping objects,
and accept immediate post-spawn dragging without a click-count gate. The final
DLL build is clean; live restart and operator-visible loose-weapon relocation
remain the immediate verification gate. The ranked gameplay continuation is
still the controlled authored-Bedroom construction-cause regression.

The safe `a2-98` full restart is complete: the isolated profile is responsive at
the main menu with no automatic game load. The sole remaining developer-mover
gate is operator-visible Ctrl-drag of one deliberately loaded loose weapon plus
the exact move receipt. No save should be loaded implicitly to obtain that proof.

Build `a2-99` closes the developer-mover mutation and cursor-independent
verification gate while correcting the earlier proof model. The first marked
synthetic drag was invalidated when its top-HUD negative control moved an item
behind the colonist bar. Marked sidecar pointer events are now rejected by the
mover; native physical Ctrl-left-drag is the sole UI gesture and fails closed over
the live WindowStack plus conservative known-vanilla HUD exclusions.

The deliberately loaded `Debug Base Expansion` fixture then proved the shared
transaction without fabricating UI semantics: a same-cell request was rejected,
and the exact one-weapon `Gun_Autopistol84354` moved from `(42, 0, 170)` to
`(42, 0, 171)` with identity, stack, position, source, and destination
postconditions independently confirmed. The test process closed unsaved and the
fixture remained byte-identical. Operator judgment of native gesture presentation
remains distinct from this receipt. The ranked gameplay continuation remains the
controlled authored-Bedroom construction-cause regression.

Build `a2-105` closes the remaining native physical gesture gate with one stable,
repeatable runtime route. The already-armed MouseMux Red seat is canonical;
direct SDK use is the same MouseMux path when no session tool surface is exposed,
and does not authorize configuration toggling or Computer Use input. Exact
`Gun_Autopistol84354` moved one cell under a native Ctrl-drag with transport,
application-log, and independent-scene receipts in agreement. The isolated run
closed unsaved and the protected fixture remained byte-identical.

This instrumentation does not change the ranked gameplay continuation. The next
local gate remains the controlled authored-Bedroom construction-cause regression:
one real unmet native resident need paired with the retained functional Bedroom
zero-work control, followed by the existing commitment, precedence, rollback,
reload, developed-save, aboveground-save, and operator-judgment proofs. Item
dragging grants no furnishing, storage, facility, construction, or pawn authority.

Build `a2-110` is the source-complete response to the observed assault, not a
claim that autonomous combat is finished. Hold now means a bounded local defense
envelope, automatic defense takes its post immediately, native weapon execution
gets a short target commitment, player orders remain sovereign, and diagnostic
traces plus future BattleLog entries retain event-time spatial evidence. The
final real-reference build and independent review are clean. Runtime proof waits
for the operator's next natural full restart; the current normal game is not a
disposable activation fixture and was not touched for verification.

After that activation, the next combat gate is a read-only, actor-neutral
`CALocalCombatAssessment` and `CACombatDecisionReceipt`. It must keep authority
and mission, perceived knowledge, Bio and competence, current condition, weapon
operation and cycling, terrain and topology, mutual support and role, target
commitment, and survival or narrative posture as separate receipted axes. Prove
the assessment on a repeated fixed snapshot, mirrored player and nonplayer
forces, an open plain or beach with no fabricated choke point, weapon swaps,
formation breakup, contrasting Bio and health cases, and the observed last-stand
case before adding another mutation consumer. Ranger or OSUT concepts enter only
through grounded individual mechanics and receipts, not as one undifferentiated
combat scalar.

Battery thermodynamics, the authored-facility evaluator, Bedroom construction
continuation, animal containment, and toxic-waste strategy execution remain
separate branches. The spatial log grants none of those systems new authority.

Build `a2-111` makes the immediate combat verification gate precise without
adding another doctrine layer. On the next natural full restart, verify that a
fighter with Moving Fire off retains strong authored ground, can make a small
post-cycle adjustment only after cover, angle, lane, support, or local pressure
materially changes, and then plants to complete the weapon cycle. Separately,
verify that a pawn with Moving Fire on preserves one target across an existing
move and plants when the next cell would invalidate the committed shot. Compare
generic assault pawns as the autonomous control because player defenders may
have been positioned manually. Default-on, throttled spatial traces are the
deterministic receipt; the operator remains the judge of whether the movement
looks and plays correctly.

No pursuit, disengagement, weapon-role doctrine, formation recovery, or new
withdrawal mode is authorized by this closeout. The previously stated
actor-neutral combat-assessment gate remains the next feature decision; this
batch only restores truthful initiative and execution boundaries needed to
observe it.

Build `a2-112` adds the evidence gate required before the next combat run. After
a natural full restart, use `combat-topology-export` to verify that pawn-proximal
knowledge expands monotonically, blocker shadows remain unknown, exact weapon
impacts produce only bounded sensor bursts, fully observed ruins and wrecks enter
as raw artifacts, and current hazards do not leak through stale knowledge. Run
the deterministic background receipt first, then compare a controlled
playground, at least one developed mountain context, and one aboveground open-
plain or beach context. The export must remain legible under a real extended
engagement and after reload.

The same run should align topology with BattleLog events, actor conditions, and
recovery behavior so the terminal phase can distinguish fighting, withdrawal,
search, rescue, field care, formal tending, death, and escape without relying on
operator reconstruction alone. Protective-apparel and weapon-report repetition
should be reduced or aggregated as diagnostic noise, but no causal medical or
combat correction follows merely from their frequency.

No gameplay consumer is enabled by `a2-112`. Player/nonplayer tactical
assessment, medical initiative, weapon-role doctrine, formation recovery,
pursuit, facility evaluation, construction, and toxic-waste execution remain
separate decision gates. The operator remains the judge of whether the next
observed battle looks and plays correctly.

Build `a2-113` supersedes `a2-112` as the combat-evidence activation gate. The
live `a2-112` engagement proved that the old tail-entry lookup fragmented one
native battle into singleton topology incidents and evicted its own history.
After a natural full restart, run the deterministic topology receipt, then a
fresh extended engagement. The export must show multiple BattleLog events and
retained LogIDs accumulating under one populated native battle alias and one
map-bound incident without a per-entry increase in capacity eviction. Export
again after reload before treating persistence as proven. The completed
`a2-112` battle remains a partial operator-plus-trace assessment, not a
recoverable full replay.

Build `a2-114` supersedes `a2-113` as the combat-evidence and visualization
activation gate while retaining its newest-entry association correction. After
a natural full restart, run the deterministic topology receipt and a fresh
extended engagement. The export must show one populated native battle alias,
one continuing map-bound incident, multiple retained LogIDs, no per-event
capacity eviction, one captured battlefield reference whose cell count equals
map width times height, zero unexpected legacy or reference-truncation counts,
and separate static-reference and dynamic-evidence byte totals.

Use that same run to prove that terrain and durable structures remain visible
throughout the replay, real destruction or construction applies at its recorded
tick, and remote static changes do not grant dynamic knowledge. Gas, pollution,
fire, health, faction, door, path, and other transient state must remain bounded
by current pawn evidence or approved weapon-impact bursts. Export again after
reload before persistence is considered proven.

Only then rebuild the aggregate after-action visualization from the v3 export.
The renderer should keep the recognizable map background, structures, water,
roofs, and cover visible while it animates pawn movement and dynamic evidence;
apply static changes by tick; keep the timeline and map simultaneously usable;
and support play as well as direct scrubbing. The pre-v3 terminal map is not a
historical backfill source.

Live `a2-114` now closes the fresh-engagement gate for stable engine-native
association, event retention, full battlefield capture, static-reference byte
accounting, thing spawn/removal replay, remote-change non-revelation, and a
playable continuous after-action surface in the current aboveground battle.
RimWorld emitted two legitimate overlapping native Battles, each with a stable
alias and incident; DR-76 therefore replaces the universal one-incident
criterion with stable native provenance, no artificial singleton fragmentation,
no capacity eviction, and one continuous operator-selected analyst timeline.

The remaining v3 persistence evidence is bounded: export the same retained
history after reload, and live-prove at least one terrain or roof mutation before
calling those change hooks verified in game. A nonzero hazard scenario is also
required before claiming that the current run proved hazard values; all 472
hazard samples here were structurally retained but zero-valued. None of these
evidence gaps authorizes a new gameplay consumer.

The combat result remains a loss despite materially better behavior. The next
feature decision remains the already-scoped actor-neutral
`CALocalCombatAssessment` and `CACombatDecisionReceipt`, keeping authority,
knowledge, Bio and competence, condition, weapon operation, terrain, mutual
support, target commitment, and survival posture separate. Pursuit, a new
withdrawal mode, broad formation doctrine, and unrelated systems do not enter
this gate merely because this engagement was lost.
