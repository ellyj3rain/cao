# Decision Registry (append-only)

- **DR-1** (2026-07-22) — **Native-substrate medium.** Behavior rides the game's
  own media: think-tree seams, DutyDefs, Lords, custom JobDrivers. Wait_Combat
  pinning + MapComponent re-issue loops rejected as a medium ("we'll never get
  intelligent behavior if what mediates it is watching-for-targets" — operator).
- **DR-2** (2026-07-22, restating standing canon) — **Autonomy tiers.** Explicit
  orders persist at all tiers; Directed = most controlled + survival floor;
  Standard = floor + vanilla; the dial governs self-initiation only; order surface
  never tier-gated.
- **DR-3** (2026-07-22) — **Four-pillar orthogonal framework.** Disposition /
  Knowledge / Authority / Doctrine per `ARCHITECTURE.md`. Everything the operator
  has specified ships, established on what the game provides, flavored by the
  operator's experience and authoritative doctrine sources. "Orthogonal framework,
  not piecemeal compatibility patching."
- **DR-4** (2026-07-22, inherited standard) — **Declarations are promises.**
  Player-facing copy matches shipped behavior exactly; found lies are fixed on
  sight (holds persistence copy, eat-smart scope).
- **DR-5** (2026-07-22) — **Decompile-grounded engineering.** Claims about the
  engine cite the decompiled source or Defs; the compiler against the real ref
  assembly is the arbiter; the decompiled tree is never committed.
- **DR-6** (2026-07-22) — **Publishable-portable.** No operator-specific
  hardcoding; license-clean bespoke implementations.
- **DR-7** (2026-07-22) — **Repo establishment.** The mod becomes a governed git
  project in the neo shape: NEO.md single instruction surface with host shims
  (that file is `GOVERNANCE.md` from DR-90 onward),
  append-only ledgers, [A#] batches, layered commits, no model commit trailers.
  Public remote is an operator decision, not taken here.
- **DR-8** (2026-07-22) — **Obedience gates RELAYED orders only.** The with-who
  group surface is in-fiction command through the selected pawn; squadmates run
  the obedience check against the relayer. The player's direct hand on a selected
  pawn always complies (canon: explicit orders persist). Disposition has no
  on/off toggle: it is the formula substrate, not switchable behavior - reverting
  to constants would be a dual-path lie.

- **DR-9** (2026-07-23 23:26 UTC / 16:26 PST) — **Half speed is a first-class
  player control.** Live verification depends on what the operator can actually
  see and judge, so 0.5x belongs in RimWorld's native speed gradient rather than
  behind an agent sidecar. The surface is Pause → Half → Play → Fast →
  Superfast; the operator retains camera and pause control throughout observation.

- **DR-10** (2026-07-24 02:12 UTC / 19:12 PST) — **Shared cognition,
  evidence-limited awareness, faction-specific doctrine.** Humanlike pawns use the
  same private contact and audible-evidence substrate; faction and Lord duties
  supply their objectives rather than a separate intelligence model. Sight,
  damage, and future sensors can identify a contact. Gunfire alone remains an
  approximate, ambiguous cue. A mayday relays a teller's actual contact — its
  remembered cell and source time — and never upgrades an unidentified sound into
  a hostile. Facts advance one physical voice or radio edge per 60-tick pass,
  retain the immediate reporter, and preserve age. Player radio follows explicit
  squad-leader/direct-report and fire-team-leader/member edges. Immediate
  recognition belongs on the native 30-tick constant-think lane; cover planning,
  reconnaissance, and objective selection remain slower deliberative work.

- **DR-11** (2026-07-24 04:00 UTC / 21:00 PST) — **Communication medium is
  not organization.** This supersedes any reading of DR-10 under which faction,
  technology, or Lord membership automatically supplies a command hierarchy;
  DR-10's shared cognition and evidence limits otherwise stand. Voice is a local,
  same-faction, recipient-uncapped physical edge unless the teller is in CA's
  concealed/quiet posture. Remote radio exists only when both endpoints actually
  wear an Airwire, Array, or Integrator headset. A mechlink is a separate mental
  medium between two implanted endpoints, bounded at each end by native bandwidth
  left after controlled mechs and gestation. Faction technology is an equipment
  prior, not a hard capability veto: actual worn or implanted gear is
  authoritative. A nonplayer Lord scopes an incident group for peer reporting;
  it appoints no leader, grants no rank, and creates no shared knowledge. Player
  squads retain their explicit squad-leader and fire-team structure. Nonplayer
  doctrine and leadership remain contextual products of society, culture,
  strategy, composition, capable pawns, roles, training, and relationships.
  Technology and organization are independent axes. The current +3/+6/+9 human
  radio-line mapping, one mental peer line per free bandwidth point, reliable-
  command formula, and hard capacity cutoff are provisional CA semantics; the
  richer degradation model and exact nonplayer doctrine topology remain future
  work.

- **DR-12** (2026-07-24 07:13 UTC / 00:13 PST) — **Independent live-verification
  pointers preserve both operators.** On Windows in Dev Mode, a MouseDown or
  MouseUp query for `UI.MousePositionOnUI` resolves from the pointer carried by
  the current IMGUI event. This lets the operator and an agent click the same
  RimWorld instance through separate MouseMux pointers without force-hardware
  motion or ownership of Windows' process-global cursor. Outside those two button
  events, outside play state, outside Dev Mode, and on Linux/Steam Deck, RimWorld's
  native input path remains authoritative. Independent hover, edge scrolling,
  held-button state, and dragging are not claimed by this bridge.

- **DR-13** (2026-07-24 10:00 UTC / 03:00 PST) — **Lost contact produces a
  bounded search, not erased memory or hidden omniscience.** Concealment removes
  live targetability but does not delete a pawn's existing contact fact. An
  assaulter may search only copied last-known cells, through a finite sweep whose
  size reflects the fact's age and uncertainty. New honest evidence redirects the
  search; completing or failing to reach one exact source-time-and-cell fact
  prevents it from regenerating indefinitely. Concealment discovery accumulates
  clear observation against one stable observer threshold instead of rerolling a
  binary outcome every scan. The current concealment registry still reveals the
  hider across its owning map when any hostile observer wins that contest;
  observer-relative reveal remains future work and is not claimed here.

- **DR-14** (2026-07-24 10:18 UTC / 03:18 PST) — **Operational withdrawal
  requires constitutive reasons.** A group does not receive a magical collective
  mood or one universal give-up roll. Disposition supplies each member's resolve
  and current condition; Knowledge bounds what has been witnessed or reported;
  Authority determines who may convene, decide, and relay the decision; Doctrine
  values the objective, acceptable cost, regrouping, and return. A real squad or
  fire-team structure can aggregate member reports through its actual communication
  edges, while an unstructured group remains unstructured. Faction and settlement
  relationships, refuge, support, and unresolved obligations are strategic facts in
  that decision. Technology changes available equipment, communications, transport,
  and sustainment; it does not itself create hierarchy or cohesion.

- **DR-15** (2026-07-24 11:15 UTC / 04:15 PST) — **Animals project through the
  four pillars rather than forming a fifth system.** Animal Disposition uses
  animal terms—vigilance, nerve, attachment, and defensive drive—resolved from
  species properties, stable individual variation, training, relationships, and
  current condition. Knowledge admits firsthand sensory evidence appropriate to
  the animal but no semantic voice, radio, or mental report; a weapon report is an
  approximate area and never a shooter identity. Native master, bond, training,
  rope, Lord, and direct-player state bound Authority and control. Doctrine acts
  through bounded jobs on the animal constant-think lane. Hostile relevance follows
  what an animal actually does—fighting, aggro state, an assigned/enemy target, or
  its own recent engagement—not species aggression and not `Pawn.IsCombatant()`,
  whose vanilla bookkeeping can be caused merely by another pawn selecting it.

- **DR-16** (2026-07-24 11:28 UTC / 04:28 PST) — **Animal threat relevance
  follows live conduct, not a retained enemy-target reference.** This supersedes
  DR-15 only where it listed `enemyTarget` as sufficient conduct. RimWorld scribes
  that reference, and cancelling a trained-animal attack clears
  `training.attackTarget` without necessarily clearing `mindState.enemyTarget`.
  Active relevance therefore requires present fighting, aggro state, a live
  trained attack target, or the animal's own short `lastEngageTargetTick` window.
  Another pawn targeting the animal and a stale saved reference remain
  insufficient.

- **DR-17** (2026-07-24 19:53 UTC / 12:53 PST) — **Operational access removes
  automatic startup locks without erasing player policy.** RimWorld's scenario
  arrival code forbids all shared starting cargo before the player can express an
  item decision. When the colony's default stance is Proactive or Autonomous,
  that one automatic forbid is suppressed so weapons, protective apparel,
  medicine, food, and other starting supplies are available. Every later player
  forbid remains authoritative. A Proactive+ pawn who knows a threat may issue
  one native equipment job from reachable, reservable, allowed supplies; dressing
  yields when the perceived threat is close, and drafting, forced work, withdrawal,
  concealment, breach drills, and explicit tactical orders retain priority. This
  is the first bounded item-access slice. It does not make higher autonomy ignore
  `IsForbidden`, and it does not claim the still-unbuilt category and action-policy
  surfaces.

- **DR-18** (2026-07-24 20:10 UTC / 13:10 PST) — **Stance-driven Allow is
  visible, spatially bounded, and subordinate to explicit denial.** This expands
  DR-17 from the starting-cargo and equipment slice to the operator's requested
  ongoing item behavior. Autonomous colonists maintain ordinary non-quest items
  as allowed only when the item is unfogged and in colony storage, the home area,
  or a local radius. Proactive+ colonists extend that behavior during a
  pawn-specific known threat to nearby weapons, genuinely protective apparel,
  and medicine. The native item toggle and Forbid/Allow designators write a
  scribed player-denial registry; splits and merges carry the denial, and CA never
  auto-allows a registered item. Because older saves contain only RimWorld's
  originless forbidden bit, their current red-X items are conservatively seeded
  as protected until the player allows them. Category policy and action-class
  controls remain unbuilt; direct orders, allowed areas, reservations, and native
  compatibility remain authoritative.

- **DR-19** (2026-07-24 21:11 UTC / 14:11 PST) — **Immediate humanlike combat is
  actor-local and side-neutral; group maneuver remains Doctrine.** Eligible
  undrafted Proactive+ colonists and exact generic settlement assaulters share one
  individual assessment primitive. A fresh external-violence impact is a direct
  pawn fact and may bypass recognition latency to compare live injury, bleeding,
  cover, ability to engage, combat skill, and Disposition. A currently visible
  contact may additionally drive a bounded comparison of nearby firing cells by
  shot quality, directional protection, target exposure, useful range, travel,
  and visible non-hostiles in the prospective shot corridor. The result is one
  finite native-shaped move, fight, or break-contact job; an unchanged destination
  continues and only a newer impact may redirect a combat move. Drafting, forced
  work, emergency jobs, withdrawal, concealment, breach drills, explicit tactical
  orders, and existing CA automatic-defense duties remain authoritative. This
  decision does not create shared knowledge, infer NPC hierarchy, or claim leader
  allocation, squad maneuver, guaranteed friendly-fire prevention, or animal
  behavior; those remain separate substrates and later Doctrine.

- **DR-20** (2026-07-24 21:42 UTC / 14:42 PST) — **Remembered identity and
  actionable threat state are separate knowledge.** A contact remains a typed,
  provenance-bearing fact after its subject ceases to threaten the knower.
  Combat consumers may act only on a contact whose last honestly observed state
  is Active. Awake firsthand sight can resolve that state as Downed, Dead,
  Captured, Nonhostile, Nonthreatening, or Destroyed without erasing the memory.
  Resolution remains per knower and respects concealment. This first correction
  does not relay outcome state; a remote knower retains their own last active
  report until firsthand correction or staleness.

- **DR-21** (2026-07-24 21:42 UTC / 14:42 PST) — **Event provenance grants only
  the mission facts constituted by that event.** A successful Man in Black join
  records the alive downed player colonists present at that arrival as that
  rescuer's finite beneficiary set. It grants their identity and casualty
  condition, not enemy locations, supplies, or whole-map awareness. A Proactive+
  rescuer yields to perceived active danger, then compares self and beneficiaries
  by native projected blood-loss death time, urgent-tend state, downed state, and
  health. The more urgent patient is stabilized first and an exact medical tie
  favors the beneficiary; a stable downed beneficiary is rescued when a native
  bed exists. A mission-only self-tend action leaves global player policy intact.
  Hostile stabilization, custody, abandonment, and execution remain separate
  Doctrine/Authority outcomes.

- **DR-22** (2026-07-24 22:07 UTC / 15:07 PST) — **Clinical priority is acquired
  information, not event telemetry.** This supersedes DR-21 only where it described
  beneficiary casualty condition as incident-granted and directly comparable. The
  Man in Black event grants its finite beneficiary identities, their arrival cells,
  and gross downed state. A local finite assessment acquires clinical severity.
  Medical skill drives assessment time and estimate precision while Intellectual
  contributes; low skill retains broad uncertainty and no hidden health percentage
  breaks ties. The diagnosis is shown over the casualty and in a neutral message.
  Subsequent triage compares the saved estimate with a similarly skill-bounded self
  estimate. Developer ground truth remains instrumentation, never pawn knowledge.

- **DR-23** (2026-07-25 00:21 UTC / 17:21 PST) — **Welfare is acquired,
  pawn-private knowledge; mission authorization is not a broadcast.** Gross
  welfare state and a last-known cell may come from firsthand sight. A valid
  same-faction voice, headset-radio, or mechlink-mental report carries the teller's
  remembered cell and source age rather than a live target. Clinical severity is
  acquired only through a close sight/touch assessment and retained as a
  Medicine/Intellectual-bounded estimate. Mission-event evidence remains scoped to
  its incident owner and cannot relay; independently acquired visual or clinical
  evidence can supersede it. Every general `CA_CheckWelfare` location check is bound
  to the launching fact's origin, monotonic revision, and copied cell, so a later
  report or observation interrupts the old check and cannot be overwritten by its
  result.
  Forced rescue remains the player's direct hand. When both Life safety and Field
  medicine are off, vanilla automatic rescue is unchanged.

- **DR-24** (2026-07-25 00:21 UTC / 17:21 PST) — **Accountability is a
  conditional responsibility consequence, not universal roll call.** An
  accountability expectation requires a stored squad-leader or fire-team-leader
  edge and an initially shared CA tactical or stack-breach Lord. Direct sight or a
  valid strategic check-in confirms presence. A missed check-in becomes overdue on
  a stable cadence shaped by the responsible pawn's discipline, empathy, and
  initiative. Proactive leaders may inspect nearby last-confirmed positions;
  Autonomous leaders may travel farther. Timetables, allowed areas, remote jobs,
  hidden health, death, downed state, and live position are not confirmation.
  Direct orders and explicit tactical/stack duties remain authoritative. This first
  slice advances the record inside `KnowledgeMapComponent`; Authority-owned record
  policy and cleaner Knowledge/Doctrine boundaries remain declared structural debt
  requiring operator-ratified refactoring.

- **DR-25** (2026-07-25 01:56 UTC / 18:56 PST) — **Welfare freshness and semantic
  revision are separate.** An exact non-stale Unassessed/Visual heartbeat with the
  same cell, gross state, clinical fields, channel, reporter, and provenance renews
  source freshness only; it does not change acquired tick, state tick, or revision.
  Stale reacquisition and material semantic or provenance changes advance revision.
  A welfare check retains source tick as scribed provenance but matches origin,
  subject, semantic state tick, revision, and copied cell, so freshness alone cannot
  restart or invalidate finite assessment work.

- **DR-26** (2026-07-25 01:56 UTC / 18:56 PST) — **Command authority and
  accountability use distinct explicit edges.** The assigned squad leader commands
  every assigned member of that squad; an assigned fire-team leader commands the
  members assigned to that team. An assigned squad leader is never subordinate to a
  fire-team leader, and competence fallback creates neither command nor
  accountability. Direct accountability remains narrower: the squad leader owns
  unteamed members and assigned fire-team leaders, while each fire-team leader owns
  that team's other members. The current responsibility edge and active CA tactical
  or stack-breach duty must still exist when an accountability check launches and
  while it runs. The player's direct hand remains outside this relayed-order gate.

- **DR-27** (2026-07-25 12:40 UTC / 05:40 PST) — **The independent-pointer bridge is
  state-independent.** This supersedes DR-12 only where it scoped the bridge to play
  state; DR-12's other limits stand unchanged — Windows only, Dev Mode only, MouseDown and
  MouseUp only, no claim on hover, movement, scrolling, held-button state, or independent
  dragging, and Linux/Steam Deck excluded. A second-seat operator that cannot reach the
  load dialog cannot begin a session at all, so the seam DR-12 identified applies wherever
  RimWorld resolves a target from the process-global pointer, not only inside a running
  game. Separately, this decision records that a second pointer never required the bridge
  to activate ordinary widgets: `Verse.Mouse.IsOver` reads `Event.current.mousePosition`.
  The bridge exists for map and pawn targeting.

- **DR-28** (2026-07-25 23:56 UTC / 16:56 PST) — **A fighting withdrawal is one
  persistent native job per participant, coordinated by a scribed shared plan.**
  `CA_FightingWithdrawal` owns movement and fire for the duration of the maneuver.
  It changes bound destinations through the pawn's native pather and begins ranged
  casts through the pawn's current verb; phase transitions do not create `Goto` or
  `AttackStatic` jobs. `WithdrawalMapComponent` owns save state, pair arbitration,
  public order/cancel queries, settings teardown, and bounded integrity repair; its
  periodic tick does not supervise phases or replace jobs.

  The persistent job is non-player-forced and player-interruptible. Immediate or
  queued forced work, draft-state change, explicit tactical/hide/stack ownership,
  native higher-priority emergency jobs, incapacity, path failure, and settings
  disable end its exact claim. Civilian hostility response yields only while that
  plan owns the pawn, and damage-triggered ordinary work reevaluation is disabled
  for this job because the maneuver already owns pursuit response. Finish cleanup
  stops only the ending driver's path, clears its moving-fire state, and mutates a
  plan only when pawn identity and job load ID both match; it never broadly releases
  destinations, ends the partner's job, or starts a successor.

  Save migration is deliberately conservative. A legacy supervised plan may adopt
  only the exact non-forced `Goto` or `AttackStatic` whose load ID the plan saved.
  A different current job cancels that member rather than resurrecting old Doctrine
  over newer engine, mod, or player control. Cover-aware halt selection is a later
  refinement and is not part of this decision.

- **DR-29** (2026-07-26 07:29 UTC / 00:29 PST) — **Home-area fire response belongs
  to native emergency-work arbitration.** Proactive and Autonomous may interrupt
  only unforced leisure or idle work after the pawn's native `FightFires`
  scanner confirms a reachable, permitted Home-area fire and is present in that
  pawn's emergency-work list. CA then ends the leisure job; it does not manufacture
  the Home-area `BeatFire` job. Direct CA assignment is the bounded extension for a
  parentless fire on a player building outside Home, because the native scanner
  intentionally rejects that location. That extension retains native forbiddance,
  duplicate-handler, reachability, and distant-reservation gates. Player-forced
  work, firefighting policy, Lord-issued HighPriority work, incapacity, draft,
  mental state, sleep, and non-leisure work remain superior authority.

- **DR-30** (2026-07-26 09:06 UTC / 02:06 PST) — **Actionable contact immediately
  ends a withdrawal member's inherited no-threat final run.** A withdrawal may be
  ordered before either participant knows a pursuer, so a direct path to the final
  destination remains correct while no threat is actionable. Once shared knowledge
  becomes actionable, any member still carrying that direct final-run phase first
  enters the existing holding state and then uses the ordinary solo or pair bound
  selector. The maneuver keeps its exact persistent job identity; contact acquisition
  changes physical phase, not job ownership. This prevents late knowledge from
  turning a bounded withdrawal into one long sprint before the first role swap.

- **DR-31** (2026-07-27 04:41 UTC / 21:41 PST) — **Domestic planning is an
  opt-in colony policy expressed through the Home area.** Enabling Home planning
  authorizes eligible Proactive colonists to originate missing sleeping and eating
  essentials inside existing claimed shelter; Autonomous colonists may continue
  with household seating, one light fixture per furnished room, and one usable
  recreation option. Disposition and skills choose the planner, but the setting and
  player-drawn Home area supply the authority for this bounded colony action.

  The planner places at most one native blueprint and never creates or supervises
  the construction job. Research, available materials, construction skill and work
  policy, allowed areas, reachability, reservations, and RimWorld's ordinary
  construction work remain authoritative. It does not create rooms, change walls,
  or replace existing furniture. Canceling its blueprint pauses that furnishing
  kind for one in-game day. Deconstructing the exact completed automatic furnishing
  vetoes that kind until Home planning is toggled off and on; the reset generation
  survives unloaded maps. Direct player construction and ordinary transient
  power/fuel/flick state are never interpreted as permission to add duplicates.

  This first domestic slice is colony-policy planning, not a relayed social order,
  so it does not pass through Authority or pawn-private Knowledge. Individualized
  household priorities, new-room growth, resource budgets, work departments, and
  social authorization require a later ratified planning boundary rather than
  expansion of this map-component monolith.

- **DR-32** (2026-07-27 06:12 UTC / 23:12 PST) — **Proactive and Autonomous make
  ordinary colony items operational by default.** This supersedes DR-18's
  Autonomous-only storage, Home, and local-radius restriction and its migration of
  every existing red X into protected player policy. Whenever a colony map has any
  Proactive or Autonomous colonist, ordinary visible non-quest items on that map
  are maintained as allowed. The access policy excludes corpses, hidden stashes,
  burning or fogged things, quest-tagged things, and another faction's property.

  Existing saves clear the former originless player-denial registry once when they
  enter this policy version. From that boundary forward, an explicit native item
  toggle or Forbid designator records a durable player denial that automatic access
  does not reverse; split and merge propagation remains in force. Known-contact
  equipment selection, reachability, reservations, apparel compatibility, allowed
  areas, forced work, drafting, and direct orders retain their existing authority.
  Category-level item controls and action permissions remain the next control
  surface rather than a reason to leave basic supplies locked.

- **DR-33** (2026-07-27 19:52 UTC / 12:52 PST) — **Player-painted functional use
  is the spatial-intent layer between the Home boundary and domestic placement.**
  Planned use is a scribed cell grid independent of Home and pawn allowed areas.
  Home continues to authorize the current planner, while planned use says what a
  painted portion of that space is for. Sleeping, Dining, and Recreation markings
  bind the corresponding automatic bed, table/seat, and recreation footprints.
  Kitchen, Freezer, Storage, Workshop, Medical, Animal, Utility, and Defense
  markings keep those furnishing kinds off their exact cells. Lighting retains its
  present room-level behavior.

  A matching marking anywhere on the map makes that furnishing kind obey the
  marking. Without a matching marking, existing-save behavior continues on
  unmarked cells while every other explicit use remains protected. Mixed uses may
  divide one RimWorld room; marking one part never reserves the whole room. Native
  room eligibility, Home, research, materials, work policy, reachability, and
  construction remain authoritative. This slice does not create rooms or walls.
  Strategic topology such as central core, frontier, reserve, and fallback is a
  separate future intent axis rather than another functional room label.

- **DR-34** (2026-07-27 20:57 UTC / 13:57 PST) — **Authored space programs
  supersede atomic functional-use markings.** A planned colony space is a saved,
  separately identifiable program with its own cells, player-facing name, purpose,
  planning style, maximum occupancy, capability requirements, and authorship. This
  supersedes DR-33's one-to-one Sleeping/bed, Dining/table, and Recreation/building
  binding. A purpose names the social and operational character of a place; it does
  not prescribe one furniture class. A Barracks may support sleeping, bonded
  animals, storage, seating, and recreation while its requirements decide which of
  those capabilities must actually be provided.

  Sleep is therefore a capacity requirement, not a bed order. An austere or
  materially constrained Barracks may satisfy it with sleeping spots; an established
  Barracks may choose beds; a resident couple may prefer a shared provision when
  relationships, privacy, materials, and local conditions permit. Maximum occupancy
  bounds how many residents the program is intended to serve rather than commanding
  a fixed furniture count. Adjacent Barracks remain distinct programs so their
  occupants, styles, and constraints may diverge.

  The programs are an overlay owned by Colonist Awareness, not native RimWorld
  `Zone` instances. Decompiled `ZoneManager` keeps one `Zone` reference per cell, so
  using that store would make spatial programs mutually exclusive with stockpiles
  and growing zones. The overlay may coexist with those logistics layers, Home, pawn
  allowed areas, and the separate future strategic-topology axis.

  Player-authored cells and parameters are durable authority. Proactive pawns may
  fulfill a player program but do not redraw or rewrite it. Autonomous pawns may
  later originate and negotiate pawn-authored programs; a player edit takes
  authority over what was edited and is not automatically reversed. Unprogrammed
  space remains unprogrammed until evidence or an author supplies a purpose. The
  first implementation is limited to player-authored program identity and bounded
  Barracks sleep fulfillment; relationship-based occupancy, privacy, social
  negotiation, autonomous program creation, and strategic asset prioritization must
  enter through this schema rather than another room-label switch.

- **DR-35** (2026-07-27 21:34 UTC / 14:34 PST) — **Sleep-capable programs may
  carry an explicit player-authored resident roster.** A resident may belong to at
  most one Barracks or Bedroom program at a time. Assigning that pawn to another
  sleep program transfers the residence; removing the assignment does not alter
  the pawn's native bed ownership. Maximum occupancy remains a hard ceiling, so a
  program cannot accept more explicit residents than its cap.

  Explicit residents determine that program's required sleep capacity up to the
  cap. Colonists without a resident assignment continue to use the existing
  capacity distribution across sleep programs that have no explicit roster, after
  accounting for usable capacity outside authored sleep programs. A rostered
  program does not absorb unrelated colonists merely because it has empty capacity.

  Completed single-slot beds and sleeping spots that Colonist Awareness planned
  for a rostered program are assigned to the next unprovided resident through
  RimWorld's native `Pawn_Ownership.ClaimBedIfNonMedical` seam. An explicit
  residence may therefore move that pawn from an outside bed when the program's
  provision completes. Colonist Awareness does not assign a multi-slot bed inside
  a program until relationship, privacy, and ideology policy are implemented, and
  it does not change player-built or unrelated bed ownership. Program residents
  are a saved planning parameter, not a new pawn allowed area or movement
  restriction.

- **DR-36** (2026-07-27 22:08 UTC / 15:08 PST) — **Resident rosters carry their
  own authorship and Autonomous negotiation never rewrites a player-authored
  roster.** A sleep program's resident roster begins unassigned. Direct player
  assignment, removal, or an explicit decision to keep the current roster makes
  that roster player-authored, including an intentionally empty roster. The player
  may hand the current roster back to residents. Existing saves with a nonempty
  roster and no resident-author field migrate as player-authored.

  When at least one eligible Autonomous colonist is present, an unassigned or
  resident-authored roster may fill incrementally. Existing valid resident choices
  remain stable. Native love clusters seed an available Bedroom and may join a
  partner's resident-authored sleep program when capacity permits. Remaining
  colonists choose among resident-authored or unassigned Barracks using mutual
  opinion of current residents, with occupancy breaking otherwise-equal choices.
  This first negotiation slice adds unassigned residents; it does not evict a valid
  resident because opinions drift.

  Program purpose and maximum occupancy remain authoritative. Negotiation changes
  planning intent, not pawn allowed areas, jobs, or movement. Bed use continues
  through native ownership. Co-residence does not itself authorize a shared bed;
  any later shared provision must separately pass native love-partner and ideology
  willingness checks.

- **DR-37** (2026-07-27 22:53 UTC / 15:53 PST) — **A non-austere Bedroom may
  satisfy a share-willing two-person love cluster with one native double bed.** The
  program must have exactly two eligible adult residents, both must belong to the
  same native love-partner relation, and `BedUtility.WillingToShareBed` must approve
  the pair under both pawns' ideologies. The Bedroom must currently have no usable
  or planned sleep capacity. Otherwise its existing single-slot planning path
  remains authoritative.

  An approved double-bed blueprint counts as two planned places only while that
  resident pair remains share-willing. When the CA-planned bed completes, both
  residents are assigned through native `Pawn_Ownership.ClaimBedIfNonMedical`
  after the same relationship, ideology, assignment, program-footprint, and
  ownership checks are revalidated. An existing unowned double bed inside the
  program may be claimed through the same gate during negotiation. Colonist
  Awareness does not manufacture a relationship, waive an ideology, or touch a
  bed outside the authored program.

  Barracks, austere Bedrooms, unrelated co-residents, unwilling partners, and
  partially provided Bedrooms continue to receive single-slot provisions. A
  shared residence therefore permits a shared provision only under current native
  willingness; it never makes communal lovin acceptable or turns co-residence into
  a general intimacy permission.

- **DR-38** (2026-07-28 01:39 UTC / 18:39 PST) — **An authorized settlement
  objective retains its exact prerequisite demand instead of failing when loose
  stock is short; forestry is one source policy, not a furniture exception.** A
  demand records the originating provision, program, author, intended cell and
  rotation, selected material, exact cost, available amount, and remaining
  deficit. Existing allowed stock remains the first source. Proactive essential
  provision and Autonomous comfort provision inherit only enough sourcing
  authority to satisfy that already-authorized objective; this does not authorize
  speculative stockpiling or a general action class.

  Producer policies satisfy typed material deficits through RimWorld's native
  work machinery. The first producer is harvestable wood. A qualified Proactive+
  pawn with Plant Cutting enabled authors a bounded set of native `HarvestPlant`
  designations; `WorkGiver_PlantsCut`, reservations, ideology willingness,
  allowed areas, skill-shaped speed and yield, hauling, and construction remain
  engine authority. Colonist Awareness neither starts nor supervises those jobs.
  The source set carries CA provenance so it can remove only its own outstanding
  designations when the demand disappears or is satisfied. A player cancellation
  is a durable source veto; an existing player designation is never replaced.

  Trees remain terrain, habitat, cultivation, and tactical cover while they are
  also possible material sources. The initial forestry policy rejects fogged,
  forbidden, burning, cultivated, cutting-protected, important, already
  designated, or authored-program vegetation. It preserves the claimed Home
  perimeter, Defense programs and nearby defensive works as a generic security
  reserve even when nobody knows a current enemy. Plants skill, Intellectual,
  initiative, and discipline select the responsible forester and shape source
  efficiency; the forester's own fresh threat facts suspend sourcing rather than
  inventing a raid direction from global map truth. Future strategic topology may
  add frontier and fallback reserves when that separate axis exists. The first
  player-colony adapter does not claim nonplayer settlement logistics are already
  implemented, but the saved demand and producer contract is faction-neutral so
  faction parity can supply its own authority boundary later.

- **DR-39** (2026-07-28 02:58 UTC / 19:58 PST) — **Storage is a situated
  logistics program projected through native stockpiles, not one generic pile or
  a synonym for a room.** A storage decision combines an accepted item set,
  capacity, priority, environmental envelope, security and hazard separation, and
  distance to producers and consumers. One colony may therefore keep a temporary
  construction reserve outdoors, medicine under roof near treatment, food at
  different priorities between kitchen, freezer, dining, and animal workflows, and
  dangerous material isolated or frozen. These are outcomes of current items,
  facilities, climate, work, and authored intent rather than universal room names.

  Classification begins with loaded `ThingDef`, stat, category, and `ThingComp`
  data. Temperature-sensitive rot, environmental deterioration, dissolution,
  pollution or gas effects, flammability, value, medical use, and actual rate of
  consumption remain distinct signals. Temperature does not substitute for a roof,
  and a deterioration-prone item is not automatically food. Modded items enter
  through the same runtime properties; explicit compatibility policy is reserved
  for behavior that those properties cannot express.

  A player-authored Storage space program may declare or constrain those
  parameters. Proactive pawns may provision that authored program; Autonomous
  pawns may later originate a provisional storage program when a current inventory,
  production, construction, medical, food, or hazard need supplies evidence. A
  player edit takes authority over the edited filter, priority, footprint, or
  environment, and deleting an automatic projection vetoes automatic recreation
  for the same objective until its authority is renewed. Temporary demand staging
  remains separate from durable inventory storage so a construction buffer does
  not silently become the colony warehouse.

  Execution uses RimWorld's native `Zone_Stockpile`, `StorageSettings`,
  `StoragePriority`, slot groups, destination selection, and hauling work. Colonist
  Awareness may create or configure a provenance-bearing native destination after
  the storage objective is authorized; it does not assign haul jobs or maintain a
  parallel inventory mover. Strategic frontier, core, and fallback topology may
  later change risk and distance scores without being encoded as storage purpose.

- **DR-40** (2026-07-28 03:33 UTC / 20:33 PST) — **Regional living-world is a
  ratified architectural program, not a new or speculative design.** Colonist
  Awareness is one modular pawn-agency, settlement, and living-world simulation
  overhaul. The existing operational-record, settlement-relationship, local-
  capability, surveying, charting, expanded-settlement, and dynamic-organizational-
  identity plans are ratified-but-unbuilt canon. Their former `operator spec`,
  `proposed`, or queued labels describe missing governance capture or implementation
  status, not uncertainty about whether they belong in the product. Built behavior,
  ratified design, unresolved measurement, and long-term ambition remain declared
  separately.

- **DR-41** (2026-07-28 03:33 UTC / 20:33 PST) — **Regional space uses a deliberate
  hybrid over RimWorld's root world-tile graph.** A saved regional world component
  owns persistent continuity; world objects own settlement loci and visible moving
  actors; maps materialize player homes, entered settlements, encounters, and other
  events requiring pawn-level fidelity. Oversized maps remain configurable,
  performance-gated active-theater profiles. Connected maps may form a bounded
  active cluster, never a permanently loaded regional quilt. The ratified expanded-
  landmass options remain product targets, while their exact dimensions and support
  profiles require live performance and AI-behavior receipts.

- **DR-42** (2026-07-28 03:33 UTC / 20:33 PST; revised by DR-91) — **Regional
  actors retain stable identity while affiliation and capability change.** Pawns,
  factions, settlements, population groups, and organizations carry stable CA
  identities with dated residence, membership, office, and source records. A
  faction owns culture, Ideoligion, political beliefs, faction structure, and
  settlement authority. A settlement owns its population groups, material state,
  organizations, economy, relationships, and materialization history. Faction era,
  facilities, infrastructure, production, logistics, communications, medicine,
  fortification, weapons, training, organization, culture, and local resources
  remain separate facts. `FactionDef` supplies the current faction-era baseline and
  native compatibility; local capability is derived from that era and the
  settlement's actual supports.

- **DR-43** (2026-07-28 03:33 UTC / 20:33 PST; revised by DR-91) — **Organization knowledge is
  reported and retained knowledge, never the sum of omniscient member facts.** A
  pawn's observation enters an organization only through a valid communication,
  reporting, deliberation, or recordkeeping path. The resulting record preserves
  subject and assertion, source actor, observation place and tick, report chain,
  receiving office, recorded tick, confidence, access, supersession, and retention
  or loss. An organization may therefore act on stale or false reports, hide records,
  fail to learn a fact, or lose knowledge when custodians or media disappear. Oral,
  customary, written, and digital records differ in propagation and retention
  without forming an intelligence hierarchy. Decisions retain the record IDs on
  which they relied.

- **DR-44** (2026-07-28 03:33 UTC / 20:33 PST; revised by DR-91) — **Land use and
  claims are layered activity, enforcement, and recognition rather than one
  ownership color.**
  Settlement, travel, patrol, extraction, hunting, farming, construction,
  observation, defense, communicated claim, tolerated access, and external
  recognition remain distinct evidence. Claims may overlap; each actor may know or
  recognize different boundaries; core use, contested borderland, route access,
  and formal jurisdiction need not coincide. Violence can reduce opposition but
  does not transfer a claim by itself. Subsequent occupation, use, patrol,
  enforcement, abandonment, agreement, or recognition changes the saved state.

- **DR-45** (2026-07-28 03:33 UTC / 20:33 PST; revised by DR-91) — **Faction
  structure and settlement authority determine political action.** Faction
  structure records leadership, decisions, participation, dissent, ownership,
  economy, work, support, membership, status, local order, defense, and war
  conduct. Settlement authority records what several
  settlements decide and provide together. Trade, negotiation, mobilization,
  surrender, appointment, and agreements require the authority saved for that
  action and produce durable receipts. Native Ideoligion roles, faction
  leadership, titles, social skill, force, expertise, and personal relationships
  are inputs; none replaces the saved faction structure.

- **DR-46** (2026-07-28 03:33 UTC / 20:33 PST; revised by DR-91) — **Relationships remain sparse,
  asymmetric, and specific to their parties.** Pawn, population-group,
  organization, settlement, and faction relations remain distinct. Trust,
  grievance, fear, obligation, affinity, access, dependence, and hostility update
  from relevant events and reports rather than one global scalar. Native pawn
  opinion remains the individual seam. Native faction goodwill and
  Hostile/Neutral/Ally state receive only the faction-wide projection
  justified by CA state; harming one settlement or member does not automatically
  flatten every local relationship into global war.

- **DR-47** (2026-07-28 03:33 UTC / 20:33 PST; revised by DR-91) — **Conflict is a durable,
  non-mandatory escalation history, and formal war is one faction state
  within it.** Incidents may produce reports, grievances, demands, signaling,
  intimidation, retaliation, raids, feuds, mobilization, authorized conflict,
  occupation, surrender, ceasefire, or treaty without following one linear
  sequence. Decentralized groups may sustain hostility without declaring war;
  formal declaration exists only when a faction can recognize or authorize
  it. Conflict records own participants, attribution, declared status, aims,
  limits, costs, losses, and agreements. Operation records own source settlement,
  authority, force, supplies, route, objective, constraints, withdrawal policy,
  result, and unresolved intent. Agreement records own signatories, authority,
  clauses, land or access scope, obligations, expiry, breach, and successor
  binding. Native incidents, Lords, duties, maps, and quests execute or present
  those records and reconcile physical outcomes back into them.

- **DR-48** (2026-07-28 03:33 UTC / 20:33 PST; revised by DR-91) — **Regional exchange arises from
  local economy, authorization, and physical logistics.** Each settlement owns
  needs, reserves, surplus, production, storage, valuation rules, obligations,
  transport capacity, route access, trust, and security. Remote exchange proceeds
  through offer or bid, legitimate authorization, reservation of actual goods or
  payment, shipment, route events, delivery, default, and relationship consequence.
  Local exchange with physically present goods may settle through RimWorld's native
  trade window. Barter, gifts, tribute, redistribution, and markets share the same
  source and delivery records without presuming one economic model or
  frictionless global inventory.

- **DR-49** (2026-07-28 03:33 UTC / 20:33 PST) — **Regional fidelity is bounded by
  promotion, materialization, reconciliation, and parity contracts.** Loaded maps
  and consequential physical encounters receive full simulation. Inactive
  populations, production, consumption, reports, claims, relationships, shipments,
  and operations advance through indexed records, sparse edges, event queues, and
  staggered cadence buckets. Promotion into full pawns, caravans, world objects, or
  maps and demotion back into records produce receipts preserving identity, goods,
  casualties, knowledge, and unresolved actions. Abstract and materialized
  resolution must meet deterministic parity tests. CA does not keep every
  settlement map, resident world pawn, or land cell fully ticking.

- **DR-50** (2026-07-28 03:33 UTC / 20:33 PST) — **Configuration preserves one
  coherent causal substrate and presentation respects Knowledge.** Stable identity,
  time, space, source records, Knowledge, and Authority are mandatory. Land use,
  regional economy, faction structure, conflict, and high-fidelity materialization may
  expose depth and budget settings, but a reduced resolver must still emit the
  records required by dependent domains; disabling detail never grants
  omniscience, instant transport, or authority without procedure. Player-facing
  maps and panels show only learned claims, confidence, age, and source. Developer
  instruments may compare that projection with ground truth. The product remains
  one integrated mod until real external dependents, APIs, or independently
  releasable modules justify a separate public framework.

- **DR-51** (2026-07-28 06:02 UTC / 23:02 PST) — **Living-colony placement is a
  contextual, ideology-bearing settlement grammar rather than one optimal floor
  plan.** Each settlement carries a saved planning context derived from its
  residents, relationships, ideologies and cultures, authored programs, terrain,
  climate, resources, infrastructure, security history, and existing built form.
  That context guides future proposals across semantic legibility, social fit,
  ideological expression, environmental fit, operational coherence, historical
  continuity, strategic topology, and bounded imperfection. Operational
  efficiency is a viability constraint within that judgment, not its sole score.

  The grammar is compositional and may express a social core, dispersed village,
  layered compound, mountain accretion, infrastructure-first reserve, frontier,
  or fallback in the same settlement. It may drift as the colony changes without
  erasing provisional rooms, completed construction, player authorship, or the
  visible history of earlier needs. A coherent living settlement may therefore be
  inefficient in one dimension while remaining intelligible, culturally grounded,
  and fit for its inhabitants.

  Ideology is an active causal input. Memes, precepts, roles, rituals, culture,
  adherent count, resident assignment, and the beliefs of the people who authorize,
  inhabit, or build a program may change what is central, communal, private,
  segregated, displayed, decorated, defended, materially styled, or permitted.
  Colony planning reads the ideologies of actual pawns and preserves minority
  participation; `Faction.ideos.PrimaryIdeo` alone is not the settlement's belief
  model. A style projection such as `GetStyleFor` remains one downstream expression
  of that context, not a substitute for it.

  DR-34 space programs and DR-39 storage programs remain the authored functional
  surfaces. Strategic topology and settlement grammar relate those programs and
  future pawn-authored proposals without inferring player intent from room geometry
  or rewriting a player-authored footprint. Corpus examples supply evidence and
  variation, never a floor plan to copy. Operator visual judgment remains the
  acceptance authority for whether a generated settlement reads as lived-in.

- **DR-52** (2026-07-28 08:39 UTC / 01:39 PST) — **Feng Shui is actionable
  spatial causality, not decorative randomness or uniformity for its own sake.**
  Furnishing placement preserves circulation, service cells, functional
  separation, directional alignment, relationships between paired objects, and
  usable negative space. A clean established pattern outranks an unexplained
  irregularity. A pawn may depart from that pattern only when current, realized
  evidence supplies a reason that changes the actual placement or objective;
  flavor text, an imagined future object, or ungrounded personality color cannot
  justify the departure.

  Social and animal relationships are valid causes when they are actionable. For
  example, a resident's bonded animal may justify proximity only when that animal
  has an existing assigned bed whose location can participate in the decision.
  Ideology, privacy, status, accessibility, security, construction stage, and
  material limits may later provide other grounded exceptions through the same
  receipt-bearing contract. Regularity remains a default consequence of absent
  contrary evidence, not a universal aesthetic law.

- **DR-53** (2026-07-28 08:39 UTC / 01:39 PST) — **Facility siting and toxic-waste
  disposition are staged, multi-axis settlement decisions shared by player and
  nonplayer societies.** Facility choice keeps loss criticality, hazard
  externality, environmental envelope, service and grid access, logistical reach,
  habitation separation, defensive topology, bounded footprint, expansion
  opportunity, culture, aesthetics, and construction stage as separate evidence.
  Protected value does not reduce to "deeper is better." An exposed but enclosed
  room may be the sensible kitchen because its loss costs less than protected
  stores; a food cooler, long-duration freezer, armory, power system, or hazardous
  store may warrant greater depth. An excavated room and an adjacent mineable
  expansion seam remain distinct facts.

  Toxic waste is its own lifecycle strategy class. A constrained settlement may
  begin with bounded frozen staging inside its defended footprint. A capable
  mature settlement prefers a deliberately detached facility away from
  habitation while retaining refrigeration, grid connection, access, control,
  bounded capacity, and defensibility. Relocation requires actual labor,
  transport, capacity, knowledge, authority, and a destination; it never erases
  inventory by changing a room label.

  Each society selects among retaining and freezing waste, expanding capacity,
  remediating it, contracting or bargaining for acceptance, exporting it to a
  remote site, coercively dumping it on another actor, returning it in
  retaliation, or deliberately using pollution as a hostile operation. The choice
  follows local technology, infrastructure, transport, volume, urgency, ideology
  and ethics, government procedure, relationships, target knowledge, relative
  strength, attribution risk, and expected retaliation. No faction type receives
  one universal behavior. Every offloading action reserves real waste, creates a
  shipment or operation, reaches a real or abstract destination, and reconciles
  pollution, inventory, knowledge, grievance, goodwill, and conflict consequences.
  Loaded maps and inactive settlements use DR-49 promotion and reconciliation to
  preserve the same causal result at different fidelity.

  Build `a2-84` supplies observation only: it exposes room footprint, roof,
  natural roof, mineable boundary, temperature, power, access, habitation,
  programs, stockpiles, turrets, Defense programs, and current waste capability.
  It does not yet site, build, relocate, ship, or select a faction strategy.

- **DR-54** (2026-07-28 20:31 UTC / 13:31 PST) — **Local toxic-waste
  relocation begins only with an explicit player authorization against an existing
  player-authored, presently ready destination and executes through native
  Hauling.** The saved authorization names its exact program, origin, revision,
  bounded unit count, and source Thing IDs. A native WorkGiver may reserve, carry,
  and place only those units into cells that still pass the program footprint,
  native storage filter, reachability, capacity, temperature, roof, and reservation
  checks. Player edits, revocation, changed readiness, or exhausted authority stop
  future work without painting a footprint, changing a filter or priority, or
  inventing another destination.

  Started, succeeded, and failed jobs; source and destination reservations; carried
  units; destination cells; and successful Thing IDs remain separate saved
  evidence. Local completion reconciles the bounded authorization and inventory
  placement only. It does not create a shipment or operation, imply remote
  transport, or fabricate pollution, goodwill, Knowledge, grievance, or conflict
  consequences. Direct pawn orders do not bypass the player-program authority
  seam.

- **DR-55** (2026-07-28 20:31 UTC / 13:31 PST) — **Test saves are continuous
  working state, not universally disposable runs.** The operator's real game,
  original test baselines, and deliberately retained checkpoints are immutable.
  Meaningful mutations in a derived working test game are saved without waiting
  for an explicit request, and ordinary continuation may overwrite that working
  save. A separate, specific, non-generic checkpoint is created only when the exact
  earlier state is worth revisiting. Pure observation with no meaningful state
  change may close unsaved. Absence of an explicit save request is never evidence
  that completed progress should be discarded.

- **DR-56** (2026-07-28 21:30 UTC / 14:30 PST) - **Transient observations are
  receipted evidence, not universal action gates or predetermined success
  invariants.** This is a project-wide simulation rule. Situation samples such as
  temperature, power, path availability, threat, social state, relationship, and
  market conditions retain their observation time and provenance. They may change
  risk, urgency, readiness, or which tactics are feasible, but one sampled value
  does not erase an otherwise real facility, invalidate an action that actually
  completed, or force every actor toward one expected world state. Tests control
  their inputs and reconcile the branch that occurred; they do not require every
  valid run to converge on one outcome.

  Evaluation keeps observed state, actor capability and infrastructure, Knowledge,
  Authority, feasible tactics, selected intent, physical execution, and reconciled
  consequences as separate records. Capabilities constrain a strategy space rather
  than naming one universal response. Abstract and materialized parity therefore
  compares causal inputs, commitments, conservation, and consequences across the
  branch actually taken; it does not demand a single unseeded outcome.

  For local toxic waste, a real allowed Wastepack plus an existing player-authored
  native Wastepack storage policy is sufficient authority for an autonomous
  response; player-forbidden state remains a veto. Roofed accepted capacity is
  containment evidence. Temperature, frozen state, and `CompDissolution` are
  separate dynamic risk observations. Refrigeration is one capability-dependent
  tactic, not the definition of containment or success. A low-technology society
  may lack freezing and waste-generation technology while still having some
  feasible combination of roofing, storage, cooling, isolation, bargaining,
  export, refusal, or other locally grounded responses. Which options exist must
  come from its actual technology, infrastructure, transport, volume, urgency,
  ethics, Authority, Knowledge, and relationships. No NPC strategy execution is
  supplied by this decision.

  This decision supersedes DR-54's explicit per-haul authorization and instantaneous
  freezing requirements as canonical local-response rules, while retaining their
  saved legacy record shapes for checkpoint compatibility. It also narrows DR-53's
  frozen-staging language to one possible tactic for a society that actually has
  that capability. Existing suitable storage or remediation equipment is reused;
  the evaluator does not invent redundant apparatus merely because waste arrived.

- **DR-57** (2026-07-28 22:06 UTC / 15:06 PST) - **Native stockpile policy owns
  durable storage; CA autonomy limits initiative inside that policy without
  reclassifying its contents.** A `Zone_Stockpile` footprint, native filter,
  priority, storage buildings, label, and held items are the canonical storage
  surface. Mixed contents are valid whenever the filter admits them, including a
  developing colony's fridge or freezer. A room program may describe Kitchen,
  Freezer, Utility, Defense, or another semantic use, but it does not duplicate
  the stockpile's item ontology.

  Colonist Awareness creates no category-named durable inventory zone. The former
  differentiated-inventory DTO and Scribe keys remain for compatibility, while
  load-time retirement removes only CA provenance and veto records and transfers
  every extant native zone intact to player authority. Legacy Storage programs
  remain readable by receipts but are not offered for new painting or expansion
  while still Storage; they may be cleared or converted. This supersedes DR-39's
  automatic durable-inventory projection and only DR-34's new Storage-purpose
  authoring, while preserving the rest of the space-program contract and the
  separate exact-objective construction reserve.

  Rottability, dissolution, roof exposure, hazard, medicine, deterioration, and
  flammability are non-exclusive evidence derived from actual loaded definitions.
  Current temperature affects risk and physical outcome; it does not become a
  universal content category, facility verdict, or native-destination gate.
  Facility maturity is local to the observed program rather than inferred from
  sophistication elsewhere in the colony.

  A native stockpile's CA initiative ceiling uses the existing four autonomy
  levels: Directed `0`, Standard `1`, Proactive `2`, and Autonomous `3`.
  Unspecified zones default to Standard. Effective initiative is
  `min(pawn autonomy, zone ceiling)`, so a zone can restrict but never elevate the
  acting colonist. Filter and priority remain orthogonal and authoritative. The
  first consumer is bounded shelf/capacity work within an existing native zone;
  its control and operative consumer ship together only after read-only candidate
  proof. Shelf placement preserves doorway approaches, circulation, service
  cells, usable negative space, and established furnishing patterns under DR-52.
  No per-zone control or shelf consumer is built by `a2-90`.

- **DR-58** (2026-07-28 22:39 UTC / 15:39 PST) - **RimWorld's loaded room,
  facility, furniture, and storage definitions are the furnishing schema; content
  packs extend that vocabulary without becoming Colonist Awareness
  dependencies.** `Room.Role`, related `RoomStatDef` values, worktable room-role
  factors, `CompAffectedByFacilities`, `CompProperties_Facility`, native
  placement, `Zone_Stockpile`, and `Building_Storage` remain authoritative.
  Colonist Awareness reads those contracts inside an existing player-authored
  footprint and does not create a second room classification, furnishing catalog,
  storage ontology, or item filter.

  Floor stockpiles and shelves are complementary native storage forms. A shelf
  may improve density over a bounded part of a stockpile while the remaining
  cells retain bulk stacks. Because a non-overlapping storage blueprint removes
  only its exact occupied cells from the stockpile and the resulting
  `Building_Storage` owns independent settings, a later consumer must receipt that
  exact policy transition rather than treating shelving as a cosmetic upgrade or
  replacing the surrounding stockpile.

  Core, expansion, and modded furnishings participate through the same loaded
  definition evidence: source content pack, function, room-stat contribution,
  facility links, storage capacity, research, materials, construction access,
  native placement, current use, circulation, service and sleep approaches,
  negative space, and existing patterns. Functional furniture packs therefore
  require no hard-coded CA item list. A visual-only prop with no native stat,
  facility, storage, or construction contract remains visual vocabulary;
  appearance alone is not fabricated as utility. An explicit adapter may later
  expose truthful semantic tags for such a pack without copying its assets.

  Build `a2-91` is advisory only. It stores no initiative ceiling, creates no
  blueprint, changes no zone or program, and authorizes no pawn. The existing
  four-level CA autonomy scale remains the future per-space initiative parameter,
  defaulting to Standard and able to restrict but never elevate pawn autonomy.
  A control becomes visible only with an operative consumer for that space.

- **DR-59** (2026-07-28 23:20 UTC / 16:20 PST) - **One shared spatial
  initiative ceiling restricts pawn autonomy; native storage density is its first
  bounded consumer.** Native stockpiles and player-authored `CASpaceProgram`
  footprints use the same saved Directed, Standard, Proactive, or Autonomous
  parameter. Unspecified spaces default to Standard. Effective initiative is
  `min(pawn autonomy, space ceiling)`, so a space never manufactures authority a
  colonist does not have. A control appears on `Zone_Stockpile` because storage
  now has an operative consumer; room-program controls remain hidden until a
  room consumer exists.

  Directed and Standard originate no storage construction. Proactive requires
  80 percent occupied stack slots across the associated floor-and-CA-storage
  domain; Autonomous requires 50 percent. This ratio is local evidence of a
  capacity transition, not a universal facility score, content category, or
  claim that mixed storage is defective. Completed CA shelves contribute their
  real native capacity and held stacks, so one improvement can make further work
  unjustified without imposing a fixed shelf count.

  An authorized transition selects only a loaded player-buildable furniture
  `Building_Storage` whose native `Blueprint_Storage` can carry policy, whose
  research and real materials are available, and whose positive density delta
  fits wholly inside the existing stockpile while leaving floor storage. Current
  items must remain accepted by both the zone filter and the storage definition's
  fixed native policy. Door, service, sleep, current-use, access, negative-space,
  wall, and existing-pattern evidence remain separate and lexicographically
  ordered. The blueprint copies the zone's current `StorageSettings` once; the
  frame and finished building inherit them through RimWorld's native transition,
  after which the shelf is independent player-owned storage rather than a
  permanently synchronized CA projection.

  Only one CA storage blueprint may be pending on a map. RimWorld's ordinary
  hauling, resources, reservations, frames, construction work, and completion
  remain execution authority. Explicit cancellation pauses that stockpile for one
  day, deconstructing a completed CA shelf vetoes more work until the ceiling
  changes, and deleting the zone retires its initiative record without deleting
  an already completed shelf. No room furnishing consumer, content-pack
  dependency, stockpile repainting, toxic shipment, or NPC strategy executor is
  supplied by build `a2-92`.

- **DR-60** (2026-07-29 01:10 UTC / 18:10 PST) - **Player notification,
  audible presentation, and physical proximity do not by themselves create pawn
  knowledge.** A pawn may adopt a threat posture only from its own grounded
  perception or a valid report that reached it. A player-facing raid letter is not
  such a report. If an incident produces no spawned or perceivable hostile, it is
  an invalid combat-response test rather than evidence that pawns should have
  coordinated against an unseen threat.

  Native animal call and angry-call sounds are presentation only; they do not call
  `GenClamor` or create a semantic CA alarm. A sentinel animal posture therefore
  requires an actual hostile-perception channel, a grounded alarm or report, and a
  path by which that report reaches real residents. Turret or charger proximity
  does not establish an approach, beachhead, defended side, or alarm relation.

- **DR-61** (2026-07-29 01:10 UTC / 18:10 PST) - **Animal containment,
  sleeping, companionship, culture, training, and defensive potential are
  separate evidence axes.** `AnimalPenUtility.NeedsToBeManagedByRope` decides the
  native pen-management case. Actual ideology decides veneration. Bonds, masters,
  training, resident bed ownership, authored Bedroom or Barracks membership, and
  approach topology may affect sleeping or sentinel potential without becoming a
  universal pen or near-owner rule.

  Existing installed and minified animal beds are inventoried before any new
  construction is considered. A minified bed is an owned installable asset but
  supplies no sleeping function until installed. The current evaluator is
  read-only: it assigns no bed, paints no Animal or Defense program, builds no pen,
  and creates no sentinel behavior.

- **DR-62** (2026-07-29 01:10 UTC / 18:10 PST) - **Critical storage keeps
  partial intelligence and strategic incompleteness visible at the same time.**
  Life-safety importance, market value, roof and natural overhang, room state,
  temperature, native filter and priority, Home integration, reachability,
  treatment access, map-edge depth, authored defensive topology, turret proximity,
  and placement provenance remain separate. A dedicated Important medicine cell
  under natural mountain roof is rational protection evidence even when it is not
  an ideal strategic location.

  Native medicine and medical-category drugs both count as medical supplies. A
  higher-priority native destination is an available option, not a relocation
  order; absence of such a destination forbids inventing a safer one. Historical
  CA zone-origin evidence remains readable after operative projection ownership
  is retired and is distinct from the player's present authority to edit the
  native zone. It never proves who later moved or placed a particular item. The
  evaluator changes no zone, filter, priority, item, program, or designation.

- **DR-63** (2026-07-29 01:10 UTC / 18:10 PST) - **Settlement context may
  confirm an already-correct placement, and one native facility family may require
  more than one functional building.** Context verification requires a receipted
  pre-context winner, selected candidate, and comparison; it does not require the
  winner to change. A clean Feng Shui bed-bank candidate remains valid when social,
  operational, historical, ideological, environmental, and strategic context
  independently agree with it.

  A regression dedicated to proving that settlement context can alter ranking
  still requires `ranking changed yes`. Production authorization instead requires
  a complete comparison and accepts either result; these are separate predicates.

  An authored Bedroom or Barracks may originate one grounded native bed-facility
  family. More than one building in that family is permitted when native link
  distance divides the bed bank into coherent groups, provided every building has
  an operative link and the live bed-side graph covers the residents' beds. Native
  relinking may move a bed from an earlier facility's original focus set to a
  closer same-family facility; historical focus and present coverage are different
  receipts. Positive Beauty is never a placement objective, and another available
  stat bonus does not authorize a second family.

  A player-authored blueprint or frame for a linkable in-room facility blocks CA
  origination. Home planning and spatial initiative also admit only one active CA
  construction commitment across the map, including a retained home prerequisite.
  Pending CA room work is canceled if the authored sleep purpose, footprint, or
  potential native bed relationship is revoked. Completed records use the actual
  reciprocal facility-link graph rather than testing an additional potential
  link. Completed buildings remain player-owned; only their stale CA association
  retires.

- **DR-64** (2026-07-29 01:10 UTC / 18:10 PST) - **Stable clinical meaning
  renews freshness without manufacturing revisions, text, or history.** An
  identical direct downed/no-bleed assessment remains the same welfare fact.
  Re-observation can renew freshness but does not advance revision or emit another
  floating diagnosis. Finite bleed estimates, dangerous temperature, expired
  relayed projections, and material clinical changes remain refreshable because
  their action value changes with time or evidence. Evidence-source time remains
  distinct from delivery freshness: an equal-source relayed heartbeat may renew
  the social memory's delivery freshness without rewriting its source tick or
  revision.

- **DR-65** (2026-07-29 01:52 UTC / 18:52 PST) - **Room-furnishing authority is
  phase-specific: native player intent blocks origination, pending CA work
  follows current program authority, and completed property survives
  association retirement.** A player-faction `Blueprint_Build` or its real
  native `Frame` transition exposes the intended facility through
  `entityDefToBuild`. When that facility is linkable to beds inside the authored
  room, CA treats it as extant player construction and originates no parallel
  work. It does not adopt the object or attach CA provenance merely because the
  object matches a candidate family.

  A tracked pending CA blueprint remains subordinate to the current
  `CASpaceProgram`. Changing the authored purpose so the sleep relationship is
  no longer operative cancels and refunds only that pending construction through
  native cancellation; existing beds and other buildings remain untouched. Once
  construction completes, the building is independent player-owned property.
  Deleting the program retires only the stale CA association and never deletes
  the building, its linked beds, or bed ownership.

  Deterministic authority regressions may exercise these transitions only
  against exact SHA-bound derived checkpoints. They pause simulation, use the
  same production program and native construction APIs, write no save, and
  reload the retained checkpoint after each intentionally transient mutation.

- **DR-66** (2026-07-29 02:08 UTC / 19:08 PST) - **Animal posture is computed
  per observed free-colonist ideology and per animal; veneration constrains
  treatment without replacing containment, sleeping, or companionship.** The
  player faction name,
  `FactionIdeosTracker.PrimaryIdeo`, minority ideologies, and each observed free
  colonist's actual `Ideo` are distinct evidence. A primary-ideology label cannot
  be projected onto
  every observed free colonist, and a free-colonist veneration result cannot be
  projected onto the whole settlement. Receipts identify each observed
  ideology's loaded venerated species and the matching player animals actually
  present.

  Where `AnimalVenerated` applies, native willingness constrains hunting and
  slaughter while death, meat-consumption, and living-presence thoughts create
  additional consequences. The precept has no `AnimalPenUtility` branch and
  therefore supplies neither a pen exemption nor a sleeping-location mandate.
  Rope management, current pen, installed or minified bed, bond, assigned and
  effective master, follow settings, training, resident-room context, and real
  defensive topology remain independent axes.

  A future animal consumer must preserve this parameterized posture space. It
  may not turn veneration into a universal loose-animal rule, a universal near-
  owner rule, or a binary pen/no-pen mode. Build `a2-95` remains read-only and
  authorizes no pen, bed, assignment, sentinel behavior, program, or
  construction.

- **DR-67** (2026-07-29 03:32 UTC / 20:32 PST) - **Authored Bedroom
  readiness is a receipted vector; optional capacity cannot manufacture a
  requirement or construction authority.** The evaluator reads only existing
  player-authored Bedroom `CASpaceProgram` footprints through
  `ProgramsForObservation`. It never chooses or paints a room, normalizes saved
  authority, changes a roster or bed, exposes an initiative control, or creates
  a blueprint, frame, designation, facility, or authorization.

  Program authorship and the actual saved resident roster, native bed owners,
  love/family relations, native sharing willingness, ideology and culture,
  Bedroom role/worker composition, room stats, circulation, approaches, negative
  space, present furnishings, and completed, blueprint, and frame stages remain
  separate. Native Bedroom classification is bed-and-owner composition rather
  than a privacy or Beauty scalar. A native bed owner is not inferred to be an
  authored program resident. Beauty and related room stats are telemetry, not an
  optimization target.

  Present readiness remains separate from future site potential. Loaded optional
  facility links, unused link capacity, research, technology, material counts,
  and spatial opportunity may describe future capability but do not identify a
  missing object. A functional Bedroom therefore produces no work merely because
  it could accept a Dresser, EndTable, SleepAccelerator, or other bonus.

  New room-facility origination remains Barracks-only. The broader sleep-program
  predicate may validate historical associations but grants no Bedroom
  construction authority. Legacy Bedroom pending metadata becomes inert and
  retires without canceling, adopting, or deleting a player blueprint, frame, or
  completed building.

- **DR-68** (2026-07-29 04:25 UTC / 21:25 PST) - **God Mode item dragging is
  transient operator instrumentation with exact item scope, not storage or
  construction authority.** With Dev Mode and God Mode active, Ctrl-left-drag
  arms only the native top hit when it is a spawned non-corpse
  `ThingCategory.Item` whose definition does not affect regions. A short
  Ctrl-click remains native selection. A real drag moves the whole existing stack
  to the exact indicated cell without splitting, merging, spawning, destroying,
  painting, or selecting a nearby fallback.

  Destination bounds, native physical spawn validity, non-wiping occupancy,
  blueprint and frame occupancy, and available item-stack capacity are independent
  gates. Invalid release is a no-mutation rejection. Map change, source movement,
  despawn, lost button hold, window or application focus loss, active designator
  or targeter,
  debug tool activation, and loss of Dev or God Mode cancel the transient gesture.
  Source and destination room, slot-group, haulable, mergeable, region, thing-grid,
  cover, and map-mesh evidence are refreshed around a successful relocation.

  The mover owns no saved state, stockpile filter, storage priority, program, job,
  facility, pawn decision, or construction permission. Pawns, corpses, live
  buildings, blueprints, frames, and region-affecting modded item definitions are
  outside its authority. Building relocation remains RimWorld's native Reinstall
  path. Activation and live interaction proof require a full process restart; an
  actively edited in-memory base is not discarded merely to cross that boundary.

- **DR-69** (2026-07-29 05:21 UTC / 22:21 PST) - **God Mode item dragging
  owns an eligible item gesture before competing map tools; a loose weapon is an
  item, not a separate authority class.** This amends DR-68's input lifecycle.
  The `a2-97` low-priority hook could not arm after a persistent Spawn Thing
  debug tool, selected designator, or targeter had consumed the press, and its
  own admission guard rejected those exact states. That was a functional
  failure in the operator's active debug-base workflow, not a weapon exclusion.

  Build `a2-98` gives the mover first-priority observation at
  `MapInterface.HandleMapClicks`, after interface windows but before map
  designators, targeters, debug tools, and the selector. It claims input only
  when Dev Mode and God Mode are active and Ctrl-left actually hits an eligible
  spawned `ThingCategory.Item`. An activated drag then owns its drag and release
  while leaving the competing tool selected to resume afterward. A short press
  selects the item; pre-activation cancel also reaches the already selected
  tool. Pointer events consumed by an interface surface cancel without a drop.

  Item resolution searches the native ordered hit list for the first eligible
  item instead of rejecting a weapon merely because a pawn or building is the
  first overlapping object. Drag activation no longer depends on click-count,
  so an immediate post-spawn drag is valid. The existing whole-object identity,
  intact-stack, exact-cell, non-wiping, capacity, no-mutation rejection, and
  developer-only authority boundaries remain unchanged. Runtime interaction
  proof remains required after the full `a2-98` restart.

- **DR-70** (2026-07-29 23:12 UTC / 16:12 PST) - **God Mode item relocation
  separates the native player gesture from cursor-independent developer
  verification.** A marked synthetic pointer sequence is not a valid proof of
  Ctrl-drag behavior. The first `a2-99` sequence moved an item between map cells,
  but a required negative release over the colonist bar also committed behind
  that HUD. The mover now rejects every `RimWorldAgentInput.v1:*` event before
  arming. Native physical Ctrl-left-drag is the sole UI gesture path.

  The native gesture retains first-priority item ownership and now admits only
  root-coordinate points outside real or immediate windows and conservative
  known-vanilla top, resource, alerts/global-control, message, bottom-tab, and
  gizmo exclusions. Tutorial and pointer-following room or beauty inspectors
  also fail closed. This is known-vanilla HUD protection, not a claim that an
  arbitrary mod cannot draw an undiscoverable overlay. Source and release over
  excluded UI reject without relocation.

  Cursor-independent verification is the single-shot developer command
  `ca-move-item <exact ThingID> <x> <z>`, not a fabricated drag. The sidecar and
  mover independently require an armed development session, active player map,
  player control, Dev Mode, God Mode, no active native gesture, one ordinal exact
  current-map ThingID, a distinct physically valid non-wiping destination, no
  blueprint or frame, and available item capacity. Native drag and the command
  share the same mutation core. Success must preserve ThingID and stack count and
  prove spawn state, position, destination thing-grid membership, and source
  absence. A failed post-mutation postcondition is indeterminate and aborts any
  ordinary retry claim. This authority remains transient developer
  instrumentation and grants no storage, program, construction, hauling, or pawn
  behavior authority.

- **DR-71** (2026-07-30 01:35 UTC / 18:35 PST) - **The deterministic
  operator-independent RimWorld input route is the already-armed MouseMux Red
  seat, not a per-session toggle or a competing Computer Use technique.** Tool
  discovery drift does not imply MouseMux configuration drift. The persistent
  MCP configuration remains untouched; when a direct tool surface is absent,
  the raw SDK client at `mousemux-window-seat/mousemux-raw-seat-gesture.ps1`
  talks to the same local MouseMux server and exact capture devices. Computer
  Use may observe but does not supply this input, move the operator pointer, or
  reposition the game window.

  Each transaction begins in exact native mode, holds a cross-process Red-seat
  mutex, confirms mouse 8192 and keyboard 8193, validates every ACK-bearing
  request serially, and checks mode, seat fingerprint, foreground HWND, process,
  UI thread, geometry, and point hit target before normal input. Possibly active
  releases use a release-only mode and seat check so target drift cannot leave a
  button or Control down. Restoration changes mode only from the transaction's
  own exact mode 6 and confirms exact native mode afterward. MouseMux 3.0.7
  exposes no per-seat active-window query or restoration request; receipts must
  state that limitation instead of claiming invisible routing restoration.

  The mover independently separates Control authorization from device-hold
  continuity. Native Control remains authoritative when carried by the mouse
  event. A preceding unconsumed Control key event may authorize only the existing
  one-shot fallback when native Control is absent. When that separate event is
  followed by a valid native left `MouseDrag`, it may also establish bounded
  independent-device hold tracking because Unity's global mouse-button state is
  not authoritative for another MouseMux seat. Explicit MouseUp, focus and
  pointer boundaries, malformed events, and lease expiry remain terminal. This
  runtime instrumentation grants no storage, hauling, facility, construction,
  pawn, or gameplay-planning authority.

- **DR-72** (2026-07-30 08:11 UTC / 01:11 PST) - **A combat order is
  standing spatial intent, and its diagnostic evidence is captured at event
  time.** The live `a2-109` assault showed that a literal Hold tile could become
  paralysis, repeated global target reconsideration could prevent a weapon from
  completing a useful cycle, and a capable melee fighter could fail to enter a
  local fight. The hostile force's real numerical and positional advantage
  remains part of the incident; the loss is not reduced to one pawn-AI defect.

  Build `a2-110` makes Hold a local six-cell defense envelope with native
  bounded ranged and melee jobs, target-specific attack commitments, immediate
  visible-threat reacquisition, and return to the authored anchor without
  unbounded pursuit. Automatic defense interrupts civilian work to take its
  post; Hold still permits bounded needs. A new standing order owns its entry
  job, an older or queued direct player order remains sovereign, and survival
  release does not re-enter job selection. Direct combat movement is available
  at every autonomy level; autonomy continues to govern self-authored action,
  not whether the player may issue an order.

  Diagnostic pawn traces now receipt the game tick, map, stable actor and target
  identities, event-time actor and target cells, and real contact, destination,
  and anchor cells when known. RimWorld BattleLog additions separately snapshot
  absolute tick, map, concern order and identity, event-time cells, and the exact
  vanilla Bullet impact cell. The saved snapshot is immutable-at-capture evidence
  keyed by LogID and pruned with RimWorld's retained BattleLog; it never
  substitutes later positions or feeds a coordinate back into combat selection.
  Its player-facing suffix is visible only while Behavior trace is enabled.
  Pre-`a2-110` incidents cannot be reconstructed retroactively.

  This implementation does not establish full player/nonplayer combat symmetry,
  weapon-role doctrine, formation recovery, patrol-base doctrine, or proactive
  operational command. Those claims require a separate actor-neutral assessment
  and mirrored runtime evidence. Source commit `fd4c756` and the clean
  real-reference build prove the artifact only; the operator's normal game was
  not restarted, advanced, saved, or otherwise touched for runtime proof.

- **DR-73** (2026-07-30 17:12 UTC / 10:12 PST) - **Moving Fire never
  authorizes repositioning; a fighter retains good ground until separate,
  actor-local evidence justifies a bounded post-cycle move.** The operator's
  repeated assault showed why the distinction is causal rather than cosmetic.
  The defenders' limestone ruin was deliberately selected and most defender
  movement was player-authored; Moving Fire was off. Remaining planted there
  was therefore often correct. The generic assault force's uncommanded use of
  ruins and ancient war wrecks is the cleaner autonomy observation.

  Generic `AssaultColony` pawns and drafted player fighters now reach the same
  firing-position judgment through separate authority-preserving schedulers.
  The current cell is the incumbent answer. A finite move requires a material
  improvement in real line of fire, directional cover, friendly lane, visible
  mutual support, or visible local pressure, must clear the actor-shaped score
  threshold, and pays for travel. A Hold or automatic-defense move must keep its
  full path inside the authored envelope and remains owned by that tactical
  Lord. The search is bounded to 96 deterministic candidate cells, reuses one
  actor-visible context, and performs reach and path work only for a candidate
  that could actually replace the current ground.

  Moving Fire observes an already-running path only. A stable target remains
  owned for the movement job; warmup and an active burst advance only when the
  next cell preserves the shot, an obstructed lane receives a bounded grace
  period, and planting removes the moving accuracy penalty. Solo suppressive
  withdrawal, covered bounded movement, and harm-driven break contact retain
  their distinct owners and execution paths. No pursuit or disengagement
  mechanic was added in this batch.

- **DR-74** (2026-07-30 19:18 UTC / 12:18 PDT) - **Combat topology is
  accumulated pawn-proximal knowledge, and projectile impacts may provide
  bounded sensor bursts.** Build `a2-112` extends the existing saved RimWorld
  BattleLog evidence rather than creating a second combat or room ontology.
  Each qualifying hostile incident retains the map dimensions, but its initial
  cell payload is explicitly unknown outside the involved pawns' radius-eight
  line-of-sight bubbles and valid line-of-sight corridors. Later evidence may
  reveal additional cells; it never converts prior knowledge into continuous
  surveillance or reads or mutates RimWorld's FogGrid.

  An exact copied projectile-impact cell may reveal a radius-six line-of-sight
  burst and valid visible corridors toward involved pawns. This is a sensor
  event, not omniscience. Gas, pollution, and fire samples use only the current
  pawn reveal or approved impact burst, intersected with already accumulated
  knowledge. Instance artifacts enter evidence only when their complete
  occupied footprint is observed. Engine-event deltas likewise require current
  observer proximity and sight. Unknown cells remain unknown, including behind
  blockers, and unrelated distant things are never enumerated into the receipt.

  The export preserves raw terrain, roof, path, cover, gas, pollution, water,
  thing, faction, health, door, fire, and BattleLog evidence. It does not label
  a ruin, choke point, defended side, beachhead, route, or tactical role. It is
  read-only and grants no combat, construction, facility, storage, or medical
  authority. Source commit `6ee3453` and deterministic validation prove the
  artifact only; the running game still has `a2-110` loaded.

- **DR-75** (2026-07-30 21:07 UTC / 14:07 PST) - **Combat replay owns a
  full battlefield reference while transient combat evidence remains pawn-
  proximal.** A battle map's terrain and durable geometry normally persist
  across the engagement, so erasing them outside each pawn reveal makes the
  analyst replay less truthful rather than more epistemically careful. At the
  first qualifying hostile BattleLog event, each new topology incident now
  captures one full-map reference: effective, top, and under terrain; roof;
  water and shoreline; natural and thick roof; and map-wide buildings, walls,
  doors, natural rock, turrets, chunks, and other things with positive engine
  `BaseBlockChance`. The artifact exports raw definition and geometry fields
  and does not infer a ruin, choke point, defended side, route, or beachhead.

  This decision supersedes only DR-74's treatment of unknown static cells and
  distant static artifacts. DR-74 remains controlling for pawn knowledge,
  current path and door state, health and faction state, gas, pollution, fire,
  and other transient evidence: those facts still require pawn-proximal sight
  or an approved weapon-impact sensor burst. The full reference is analyst
  ground truth, is never added to the pawn-known mask, and grants no combat or
  gameplay authority.

  Terrain, roof, and reference-artifact changes form a separate ticked stream.
  Compact cell fingerprints suppress no-op shoreline records and detect the
  installed engine's Odyssey gravship unsafe-grid removals at the path-
  recalculation event emitted after each completed cell. Reference changes are
  bounded independently at 4096 changes and one mebibyte per incident; they do
  not consume the two-mebibyte dynamic-incident or eight-mebibyte dynamic-
  history budgets. Pre-`a2-114` incidents remain explicitly `legacy-
  unavailable` and are never backfilled from a later map state.

  Source commit `6d2a757`, the clean real-reference Release build, and the
  deterministic v3 receipt prove the artifact only. The completed battle that
  exposed the visualization defect cannot be reconstructed honestly, and a
  fresh extended engagement after a natural full restart remains required for
  runtime and visual proof.

- **DR-76** (2026-07-30 21:41 UTC / 14:41 PST) - **One continuous
  operator-visible engagement may contain multiple overlapping RimWorld native
  Battles; after-action presentation composes them without erasing native
  provenance.** The fresh `a2-114` engagement produced `Battle_0` and `Battle_1`
  on the same map with overlapping absolute-tick intervals. Both retained stable
  aliases, map-bound topology incidents, ordered LogIDs, and bounded reference
  state. This is materially different from the `a2-112` tail-entry defect, which
  created an artificial incident per event and evicted its own history.

  Native Battle identity remains the source record for event association and
  persistence. The analyst surface may order overlapping records into one
  operator-selected continuous timeline, preserve every incident ID and native
  alias, and display one shared battlefield reference where their captured
  references reconcile. It does not invent tactical phases, merge engine state,
  or claim that native Battle count equals the semantic number of battles the
  player experienced.

  Runtime acceptance therefore requires stable association to the actual native
  Battle or Battles, no artificial singleton fragmentation, no unexplained
  capacity eviction, and explicit provenance in the aggregate. A universal
  one-native-incident checkbox is not an evidence standard.

- **DR-77** (2026-08-05 06:38 UTC / 23:38 PDT; terminology revised
  2026-08-09) - **War conduct includes one
  axis - how far force may go against a person who cannot or does not fight
  back - and every position on it is SILENT on ordinary combat.** Operator
  ratified three positions (`strength decides` / `the beaten are spared` /
  `only fighters may be struck`) over a two-position binary, and ratified that
  `struck-unarmed` stays a recorded fact that no row reads.

  The silence on `mutual-combat` is an engine constraint, not a preference.
  Verified in the decompile: `Thought_Memory.TryMergeWithExistingMemory` does
  not add a new memory once `stackLimit` of that def exist - it renews the
  oldest and DISCARDS the new one, with its `detail` text. The removed prototype
  had `stackLimit` 3. A row that judged every exchanged shot would therefore
  destroy the individual leg as a signal channel: a probe of a 63-hit firefight
  showed 60 of 63 judgments' words discarded, and an atrocity witnessed before
  the fight evicted entirely by ordinary rounds that followed it.

  `struck-unarmed` is unjudged because the site cannot distinguish a fleeing
  civilian from a melee brawler whose fists are their weapon - it sees only
  that `equipment.Primary` is null, which is also true of every pawn the engine
  has just downed (`MakeDowned` calls `DropAndForbidEverything`). The fact is
  recorded; the claim is not made until a site can tell the two apart.

  The permissive position is permissive by APPROVING exactly what the others
  condemn, not by saying nothing. This gives it a real consumer under the
  no-labels-without-consumers rule, holds its volume at the atrocity rate
  rather than the bullet rate, and makes the mirror explicit: the act that
  delegitimizes one administration satisfies another people entirely.

- **DR-78** (2026-08-05 06:38 UTC / 23:38 PDT; revised by DR-92) - **The protected circle is
  UNIVERSAL at the start; narrowing it to a category is a political act
  performed in play.** Operator ratified this over immediately scoping the
  belief through a separate standing table.

  Scoping through the removed standing table would have shipped the belief
  inert. Current founding writes direct organization relations for members and
  temporary members; it does not silently narrow a faction's war-conduct belief
  to those relations. A member-only protection belief would judge nothing
  during a raid,
  which is the only situation that produces violence events. A people that
  holds the beaten are spared holds it for the beaten, not for their own
  beaten, until someone in play decides otherwise. This keeps the player start
  LIGHT per the four-layer architecture and leaves the narrowing to the charter
  surface, where it is an announced political choice rather than a silent
  derivation from the founding arrangement.

- **DR-79** (2026-08-05 07:34 UTC / 00:34 PDT; terminology revised 2026-08-09) -
  **Political-belief memories merge by event identity and group by political
  belief. A pawn may retain every distinct grievance it formed.** The memories
  remain native pawn memories so mood, social, and need systems can consume them.

  Event identity means the same belief judging the same kind of act, done in the
  same way, to the same person, by the same responsible organization. Repeated
  blows in one beating renew one grievance; a different victim, act,
  circumstance, lethal result, or responsible organization creates another.
  The raw event ID is not the merge key because that would turn one sustained
  event into many memories.

  Mood groups memories by political belief. Repeated violations of one belief
  saturate together under RimWorld's normal stacking, while another belief has
  its own row and total. The mood tab therefore names the actual belief rather
  than showing an undifferentiated violation label.

- **DR-80** (2026-08-05 08:58 UTC / 01:58 PDT; superseded by DR-91) - **The regional setup screen is
  a failed prototype and will be redesigned from the player's objects inward,
  not repaired from the existing controls outward.** Operator's decision, stated
  bluntly: stop adding controls, treat the current layout as disposable, do not
  preserve it merely because the controls already exist.

  CURRENT CLOSURE. The prototype and its faction-group framing were removed.
  Current authoring uses regions, factions, settlements, population groups,
  and a landing area directly. DR-91 and `SETTLEMENT_SYNTHESIS_MODEL.md` are the
  implementation contract; the remainder of this entry records the diagnosis.

  Diagnosis accepted: the UI was built from the available fields rather than
  from the decisions a player makes, so it exposes data without explaining the
  world. The one-object draft-to-generation chain is sound - the audit found no
  dead controls and no wrong consumers - which localises the failure to the
  presentation layer.

  Ordered sequence: (1) scope map of every field and consumer - DONE, see
  `SETUP_SCOPE_MAP.md`; (2) redesign around world / region / faction group /
  settlement-site / player start; (3) only then code. Each screen or inspector
  states exactly what it applies to, highlights that target on the map, and
  shows only the decisions belonging to that scope. Controls affecting all
  generated settlements are labelled global; controls affecting one group or
  site appear only when that object is selected.

  Two clipping defects found earlier (`BornWithSummary` overflowing a 453px
  button so `Widgets.ButtonText` centre-clips both ends; the `Shares a town`
  label at 80x28 cutting its second line) are NOT to be fixed in place - they
  are properties of a layout being discarded.

  Held open for the operator, because it is an architecture question rather
  than a layout one: whether the four founding questions stay as one block, or
  are read against the matching CA precept on each axis so the
  profess-versus-practice gap is visible at setup.

- **DR-81** (2026-08-05 10:12 UTC / 03:12 PDT; revised by DR-91 and DR-92) -
  **Political beliefs, starting rules, and later practice remain separate
  facts.** Authored founding terms win. Open terms are generated from the
  scenario, current faction structure, and political beliefs. A difference is
  recorded rather than silently corrected.

  Political beliefs state what ought to happen; the founding arrangement states
  what was adopted; play records what actually happened. The removed setup
  comparison and custom-precept framing are historical. Current generation reads
  the saved faction model directly and does not add another constitution or
  ideology layer.

- **DR-82** (2026-08-05 10:12 UTC / 03:12 PDT; superseded by DR-91) - **The setup screen is
  reorganised into four scopes on four tabs; the backing data is NOT
  restructured yet.** Operator's ruling: the UI must present coherent scopes
  first, and data objects can be split later where that improves provenance or
  prevents misuse. So `plan.worldPolicy` still carries both everywhere-fields
  and later-region fields on one object; the tabs now keep them apart, and
  `setup_probe.py` asserts no tab writes another scope's fields, which holds the
  line until any split happens.

  Scopes: **Player start** (landing tile with a visible cross-reference to the
  outer tile selector, the ground beneath it, the founding arrangement, and the
  political-belief comparison), **Starting region** (faction groups, settlements and
  placement, select-and-inspect on the map, faction and settlement names with
  vanilla-style generation and reroll), **World rules** (everything that holds
  everywhere, groundwater tuning included), **Later regions** (only the
  policies governing regions generated after this one).

  Also ratified: generated political and economic history stays out of setup
  and belongs to NPC settlement synthesis. Naming and settlement visibility are
  immediate setup requirements and shipped here.

- **DR-83** (2026-08-05 09:41 UTC / 02:41 PDT; superseded by DR-91) - **The pre-landing screen is a
  STARTING-REGION POPULATION EDITOR. Its subject is who already lives on this
  land, and everything else leaves.** Operator's ruling, restating the original
  requirement that two redesigns had drifted from.

  CURRENT CLOSURE. The surviving principle is that setup edits the starting
  region rather than the player colony. Its current objects and vocabulary are
  defined by DR-91; the former faction-group and custom-precept instructions
  below are historical.

  What it keeps, because it is the subject: the regional footprint and landing
  tile as the region's own identity; the faction-group list; the settlements
  belonging to each group; add, remove, name, reroll and place; visible
  technology, settlement form and affiliation; and map highlighting showing
  exactly which object is selected. All of it external to the player colony,
  which does not exist yet.

  What left, and where to: **CA precepts** to the vanilla Ideology creator
  (already shipped there). **Groundwater** to the land panel, where the landing
  tile is actually being chosen. **World liveliness, frontier rules and
  later-region policy** to a separate `Dialog_CAWorldGeneration` reached from
  the land panel, not from the editor. **The founding arrangement and its political-belief
  comparison** into play, at `StartedNewGame`, which is also the only place the
  comparison can read a real ideoligion (F-118).

  Diagnosis accepted, and it is the important part: the failure was not layout.
  CA had grown founding relations, customs, administration, labour and
  ownership, and each new primitive was given a matching pregame control -
  losing the distinction between systems that must exist in the simulation and
  things a player should configure on this particular screen. The F-116 audit
  then catalogued the controls already present, found every one had a consumer,
  and concluded they needed clearer scopes. That produced tabs over the same
  wrong premise. **No amount of scope labelling repairs a screen answering a
  question nobody asked.**

  Standing rule from this: a control earns its place by belonging to the
  screen's subject, never by having a consumer. Ask what the screen is FOR
  before asking what its controls write.

  Deferred, not lost: world-generation controls belong on
  `Page_CreateWorldParams` beside overall population (which the cost line
  already multiplies against). They cannot move there until `CARegionalPlan` -
  which owns `worldPolicy` and `groundwater` - exists earlier than the
  landing-site page. The separate dialog is the honest middle ground until then.

- **DR-84** (2026-08-05 11:18 UTC / 04:18 PDT; superseded by DR-91) - **The six settlement axes are
  separated in the data model, and the faction supplies defaults rather than
  dictating outcomes.** Operator's ruling, taken before any interface work,
  because an interface can only expose choices the model actually has.

  The six, as the questions a person asks: who are these people (identity);
  what can they build (technology); what does it look like (settlement form);
  what do they believe (ideology); what can they materially do (capabilities);
  how are they organized (institutions).

  Mechanism: ONE resolver, `CASettlementAxes`, owns every derivation. The
  precedence is uniform - an authored value wins, otherwise the faction supplies
  a default - and `-1` means "nobody decided, derive it", generalising the
  sentinel `institutionMask` already used correctly. The plan carries authored
  intent (`authoredTechLevel`, `authoredForm`, `authoredCapabilities`); the
  record carries resolved outcome (`techLevelResolved`, `morphFormResolved`)
  plus `axisProvenance` naming which axes a person decided.

  Consumers were rewired so no module re-derived the old axes: the former
  facility-seeding and morphology paths gained record overloads and every
  record-bearing call site uses them; `RegionalWorldModule.Create` resolves
  through the resolver; the old private `CapabilityBasis` and `Capability`
  helpers are deleted. The tier boundary is now defined in exactly one place.

  Ideology made consequential: `SeedConventions` takes the ideoligion as a
  parameter instead of silently reading the player's, and an NPC settlement is
  seeded from `CASettlementAxes.IdeoOf(record.faction)` - its own faction's
  political beliefs. The colony still seeds from the player's, explicitly.

  SAFETY PROPERTY, proven rather than asserted: the split is INERT until
  somebody authors something. An unauthored settlement resolves identically to
  the pre-split code on all eight tech levels and all three capability rolls.

  Consequence worth naming: `CAMorphForm.Medieval` becomes reachable for the
  first time. It was implemented and unreachable because no vanilla faction has
  medieval technology.

  Still open, deliberately not decided here: whether CA authors its own faction
  templates to widen the palette, and whether the six unconsumed capabilities
  gain consumers or stay as defaults for institutions. Both are content
  questions, not model questions.

- **DR-85** (2026-08-05 12:40 UTC / 05:40 PDT; superseded by DR-91) - **Four settlement-generation
  decisions ratified through Crucible; two initial framings corrected.**
  This records a removed proposal; DR-91 defines the current faction and
  settlement model. Full historical model at `SETTLEMENT_SYNTHESIS_MODEL.md`.
  Mousecat was closed, so the
  interaction ran on the native surface; the Crucible interaction
  `skill-a3906fcbac96d160` remains open and unanswered there.

  RATIFIED. (1) **A generated people may span several engine Factions** - a
  culture tier, so kin can schism, war with each other and hold separate
  diplomacy while sharing origin and belief. Chosen over the lighter
  one-people-one-faction shape even though the DR-84 axis split already
  delivers per-settlement variation, because schism and kin-war need it.
  (2) **A settlement's belief is a persisted composition plus a separate
  separate settlement doctrine** - majority and minority strands, with customs
  seeded from doctrine and internal contradiction seeded from the
  doctrine/majority gap. Must work for settlements with no instantiated pawns,
  so it can never be computed from residents.

  CORRECTED - **capability is a derived assessment, never an independent
  cause.** All three initial options were wrong: two treated the
  capability number as deciding what is born, the third was the black box
  already rejected. "Medicine 4" is a compressed description of doctors, beds,
  medicine, water, power, transport and authority - if the infirmary burns and
  the medicine runs out, capability falls because the material basis changed.
  Generation creates supports; runtime consumes supports; any capability value
  is a computed summary or cache. Off-map summaries are permitted for
  performance but must be backed by aggregate material records and must
  materialize consistently - an aggregate is a summary of specific facts, not a
  licence to invent them on arrival.

  CORRECTED - **seeded synthesis, but axis-specific causal rules, not one
  uniform resolver.** Precedence is **authored override -> axis-specific seeded
  synthesis -> minimal compatibility fallback**, with faction demoted from
  normal default to last-resort fallback and treated as one input among many.
  Technology, settlement form, ideological composition and political-economic
  order each synthesize from their own world facts. Capabilities are NOT
  synthesized - synthesis produces their supports. A generic synthesis function
  would replace one god-variable with another.

  CONSEQUENCE, recorded against the prior implementation:
  `CARegionalBasePlan.authoredCapabilities`, added in the DR-84 batch, is now
  wrong - it presents capability as authored player-facing intent. It is to be
  removed. The authored overrides for technology and form remain correct.

- **DR-86** (2026-08-05 13:05 UTC / 06:05 PDT; revised by DR-91) - **GOVERNING RULE: when RimWorld
  already represents the thing, extend that system into CA rather than creating
  a parallel abstraction.** Operator's ruling, and the rule that should have
  governed this entire redesign - every failure in this line of work was a
  version of breaking it.

  Ideology is `Ideo` and `PreceptDef`. Technology is `ResearchProjectDef` and
  its unlock graph. Settlements are real world objects and maps. Capabilities
  emerge from actual pawns, buildings, stocks, skills, research and
  institutions. CA adds persistence, institutional ownership, knowledge limits,
  regional simulation and behaviour - it does NOT replace the game's concrete
  ontology with summary numbers.

  APPLIED TO TECHNOLOGY, which removes an axis rather than adding one. There is
  no synthesized technological profile projected into `TechLevel`. A society's
  technology IS its persistent research state: completed `ResearchProjectDef`s,
  explicitly lacked knowledge, techprints held, active projects, and the
  researchers, benches, archives, power and materials supporting the work -
  plus how knowledge moves by trade, capture, migration, diplomacy and
  institutional collapse.

  Content then follows with no new machinery, because
  `ResearchProjectDef.UnlockedDefs` already names what a project makes
  buildable and craftable. A society can make a thing when it holds the
  research AND the physical means - the same supports test the capability model
  uses, so technology and capability stop being two systems. Losing a bench
  does not erase knowledge, it stops further research; capturing technology
  means taking real techprints, records, equipment or knowledgeable pawns.

  This yields the variation the earlier design was trying to invent: a
  settlement that knows advanced medicine but has no radio; one carrying traded
  firearms it cannot manufacture; two settlements of one people knowing
  different things because institutional reach differs.

  `TechLevel` survives ONLY as a coarse compatibility label, inferred from real
  research state where an engine system demands one, cached, never authored as
  truth.

  THE CORRECTED CHAIN: history and contact seed completed research -> research
  unlocks existing game content -> actual facilities, personnel and resources
  determine what can be produced or used -> coarse tech labels are derived only
  when an engine system requires them.

  CONSEQUENCES against existing work, all recorded rather than left to be
  found: `authoredCapabilities` (DR-85) removed; `authoredTechLevel` is not
  authorable as truth and `techLevelResolved` demotes to a derived cache; and
  `record.researchStock` / `record.researchMilestones` - an integer counting to
  25/75/150 driving a three-case switch to stand in for what a society knows -
  are the anti-pattern already in the codebase and must be replaced by real
  per-society research state. `ResearchManager` is player-scoped, so that
  persistence is legitimately CA's to own; the VOCABULARY must remain the
  engine's.

- **DR-86a** (2026-08-05; revised by DR-91) - **`researchStock` / `researchMilestones` are an
  isolated stub, not a systemic parallel.** `researchStock` increments only
  while a settlement has a live `SimpleResearchBench` and one of its own pawns
  stands within five cells. At 25/75/150 it spawns one hardcoded Thing - rifle
  or longsword, then medicine, then a lamp - and records a story line. Complete
  consumer set: `StartingFacilitiesModule` and `RegionalWorldModule`. Nothing
  derives technology, capability or unlocks from either field.

  Under DR-86 it should be rebuilt on `ResearchProjectDef` when per-society
  research state lands. Unrelated: 75 and 150 coincide with ruck capacities in
  `PackModule` (45/75/100/150).

- **DR-87** (2026-08-05 15:10 UTC / 08:10 PDT) - **CA stops inventing treasury,
  business, payment and market machinery until the existing economic mod stack
  is audited against native systems.** Operator directive.

  The audit is organised by ECONOMIC FUNCTION, not by mod:
  **native RimWorld object -> existing mod implementation -> CA requirement ->
  source/license -> reuse strategy -> missing ontology.**
  First pass covers payment settlement, business premises, staffing, inventory
  ownership, customer demand, pricing, banking, currency, treasury, taxation,
  contracts, market prices, and conservation. Only afterwards may CA retain or
  expand its own treasury and business primitives.

  Stack named, with what each supplies and its licence position:
  **Hospitality** (visitors and spending; GPLv3 source, art CC BY-SA),
  **Storefront** (staffed shop, sale area round a register, customer
  preferences, personal budgets, negotiation effects, item transfer, silver
  payment; source published), **Gastronomy** (restaurants, menus, prices,
  hours, waiters, paid meals; source published but the repository carries
  CONFLICTING signals - a no-derivatives README statement alongside a GPL
  marker - to be resolved), **Cash Register** (part of the same stack).
  **Vanilla Trading Expanded** (fluctuating supply/demand prices, banks, loans,
  contracts, news shocks, a stock market; self-described as arcade rather than
  simulation; CC BY-NC-ND, so REFERENCE OR COMPATIBILITY TARGET ONLY - no
  porting or derivative incorporation without separate permission).
  **RimBank** (banknotes, denominations, ATM exchange, mixed banknote/silver
  settlement, trader acceptance, change; MIT, source public - directly reusable
  for physical currency, supplies no firms, accounting, wages, taxation or
  treasuries). **Empire Refactored** (self-governing colonies, taxes in silver
  or goods, settlement production and upgrades, roads, faction-wide edicts,
  unrest; current for 1.6; source and licence to be inspected before deciding
  port source vs dependency vs reference). Plus smaller monetised-service mods
  - casinos, spas, vending machines, hospitals - as patterns for paying for
  ACCESS TO A SERVICE rather than buying a Thing.

  None individually covers CA's political economy: Storefront models
  transactions not firms or labour contracts; VTE models a planetary market not
  materially grounded regional production; RimBank models currency media not
  accounting; Empire models taxes and dependent settlements but not
  conserved on-map causality; Hospitality models customers not the
  institutional order beneath them. Together they likely cover most of the
  mechanical work.

  BLOCKER, stated at time of ruling: none of these mods is installed. The
  workshop directory holds 21 items and none matches Hospitality, Storefront,
  Gastronomy, Cash Register, RimBank, Empire or Vanilla Trading Expanded. The
  native-object and CA-requirement columns can be filled from the decompile and
  this codebase; the mod-implementation, source and licence columns cannot be
  filled until the mods are present.

- **DR-88** (2026-08-05) - **Name: "Colonist Awareness Overhaul: A Living World
  Framework". Licence: GPL-3.0.**

  The exact title is used in metadata and not shortened. Workshop subtitle: "A
  systemic RimWorld overhaul integrating persistent settlements, material
  logistics, organizations, politics, economics, and conflict into one
  living-world framework." "Overhaul" states what a player installs; "Living
  World Framework" states that ported systems are adapted into CA's common
  ontology rather than bundled beside each other. Scope coheres because
  pickling, sanitation, banking, research, labour and combat all resolve
  through the same structure: real assets, limited knowledge, organizations,
  authority, intent, persistent consequences.

  GPL-3.0. The project adapts GPL-covered work and aligns with community norms
  supporting open inspection and reuse under reciprocal terms. No additional
  motivation should be inferred. `LICENSE` holds the canonical GPL-3.0 text;
  the project note lives in `CREDITS.md`. Component terms that are more
  specific stand: Hospitality artwork CC BY-SA 4.0, RimBank MIT, ported
  Rimshare / Boats / Vehicle Framework MIT.

  **Empire is adapted behind CA organizations.** Taken: the off-map
  settlement economy - `FactionFC : WorldComponent`, its tax ledger, settlement
  comps, levy flow, settlement production and upgrades. It is the only
  implementation that runs a settlement economy while its map is unloaded.
  Not taken: Empire's political shape, which routes taxes from the player's
  dependent colonies to the player's home map. CA's organizations hold
  treasuries and pay one another with no player involved, so the machinery sits
  under CA's organizations and customs, and a levy arrives as an act the
  political-belief effects judge as taxation or requisition.
  Source: `github.com/matathias/Empire-1_6-Continued`, GPL-3.0, branch
  `development`.

- **DR-89** (2026-08-05) - **CA owns the economic source of truth. Goods move
  only when paid for, or when explicit terms authorise credit. An obligation is
  created by a basis, never by an empty purse.**

  Architecture: goods or services transfer -> payment where available -> a
  shortfall becomes an obligation ONLY where terms, contract, custom or
  legitimate authority create one -> CA records the complete transaction.

  A sale on short payment requires explicit credit terms - allowed, a due tick,
  a named basis, an optional guarantor. Without them the sale is refused:
  nothing moves and no debt exists. Anything else makes every seller a lender
  by default.

  Transfers that are not sales run their own path under a named cause - gift,
  subsidy, levy, requisition, procurement, contribution, custody, common title
  - recording title, possession, authority claimed, consent given and
  valuation. Compensation creates an obligation only when a basis is supplied.

  **Debt is a state; default is the act.** Obligations run Outstanding -> Due
  -> Satisfied / Defaulted / Renegotiated / Forgiven. Default fires only on an
  obligation already Due. The act record judges default, never the
  existence of credit.

  One schema for every pair - pawns, firms, public bodies, settlements, the
  player: buyer and seller; owner and operator; goods or service; quantity and
  valuation; cause; terms; authority claimed; consent given; payment medium and
  amount; unpaid balance; debtor and creditor; timestamp, provenance, status.

  On a loaded map real Things move and stay authoritative; off-map the same
  call adjusts aggregates and writes the same record, so a materialising
  settlement reproduces its inventory, money and debts rather than rerolling.
  The ledger is a WorldComponent; every audited mod scoped its economy to a
  map, which is why none settles between two unloaded settlements.

  Out of scope pending separate evaluation: customer browsing and preference
  behaviour. NOT wired into founding supply pooling - that is a
  property-and-allocation arrangement, not a sale, and awaits its own
  ratification.


- **DR-90** (2026-08-06 08:10 UTC / 01:10 PDT) - **This repository carries the
  GZDS canonical doc-pack.** Operator ruling: the neo governance convention is
  more disciplined and this project adopts it.

  The pack is defined in code at
  `Projects/zero/neo (the harness)/core/governance/authortime/doc-pack.js` →
  `CANONICAL_PACK` = `MEMORY.md, CORE.md, ARCHITECTURE.md, GOVERNANCE.md,
  DECISION_REGISTRY.md, FINDINGS.md, BATCH_LOG.md, ROADMAP.md,
  SESSION_STATE.md` + `VERSION`. The code is authority; if a prose summary and
  the code disagree, re-read the code.

  APPLIED. `NEO.md` becomes `GOVERNANCE.md` — same role, canonical name, git
  history preserved through the rename; the `AGENTS.md` shim follows it. Three
  files are new. `CORE.md` states repository identity, the
  canonical composition, and the governing constraints already ratified
  elsewhere. `SESSION_STATE.md` states current operational reality — branches,
  build and deployment hashes, environment limits, blocking work, known damage,
  and out-of-band artifacts. `MEMORY.md` indexes every root document with an
  explicit status, so "is this canonical or superseded" is answerable without
  reading the document.

  WHY, stated as cause rather than preference. Two failures in the 2026-08-05
  work traced directly to the two files that did not exist. Without a session
  state, the only description of where things stood was a stale 2026-08-02
  orientation handoff that was simultaneously carrying architecture, a work
  order, and the sole copy of the RS-008 save identity — which is why deleting
  it was unsafe. Without a pack index, eight audit and synthesis documents sat
  at the root with no declared standing, which is how `PARALLEL_ONTOLOGY_AUDIT.md`
  came to be overwritten and two documents holding unique content came to be
  proposed for deletion.

  INVARIANT. Nothing at the repository root is unclassified. A document whose
  disposition is genuinely unresolved carries an explicit unresolved status in
  `MEMORY.md`; that is a junction for the operator, never a gap to be filled by
  inference.

  NOT CHANGED. `packageId` stays `ellyj3rain.colonistawareness` — the display
  name and the repository name moved, the technical identity did not, because
  changing it breaks existing saves and Workshop identity. `VERSION` remains
  0.2.2; this batch ships no code.

- **DR-91** (2026-08-09) - **The regional authoring model converges on factions,
  settlements, and concrete starting conditions. Pre-1.0 experimental schemas
  are not compatibility targets.**

  A region owns its selected land, arrival area, world tendencies, settlement
  pattern, scale, factions, settlements, and faction relations. A faction owns
  culture, Ideoligion, political beliefs, current faction structure, and the
  authority shared between its settlements. A settlement owns its form, role,
  starting facilities, access, services, civic development, population groups,
  and starting provisions.

  Political beliefs describe what a population believes. Faction structure
  describes the arrangement now in force. Settlement authority describes what
  several settlements of one faction decide and provide together. These are the
  current political concepts; the earlier extra political tier and territorial
  constitution do not survive as hidden implementation objects.

  Starting provisions are regenerated from population, faction structure,
  facilities, infrastructure, settlement scale, and role. A per-provision
  override changes distribution only. Capability is derived from faction era and
  local supports; it is not authored independently. Settlement identifiers are
  placed on valid visual land inside the projected geography.

  The current schema writes these concepts directly. Obsolete type names, save
  fields, aliases, projection caches, and migration routines are removed rather
  than retained behind new labels. Old experimental saves may fail. The current
  authored regional composition is converted once as a test fixture.

  Player-facing text uses concise game terms and the same vocabulary as the
  model. Canonical documents and implementation comments follow that vocabulary.
  `POLITICAL_ARCHITECTURE_RATIFICATION.md` remains only as a superseded design
  record. DR-42 through DR-48 and DR-84 through DR-86 are revised or superseded
  where they conflict with this decision.

- **DR-92** (2026-08-09) - **Organizations, political beliefs, and their saved
  consequences use one direct model and vocabulary.** An organization owns
  offices, groups, customs, security, agreements, policies, claims, relations,
  and decision history. Inter-organization diplomacy is saved as agreements.
  Political-belief effects compare faction beliefs with current rules and
  observed acts; the removed ideology recognizer does not assign an academic
  political label between those facts and their effects.

  Current types, filenames, save tags, receipts, and UI use those names. The
  former bloc, convention, compact, conviction, and political-ledger names are
  removed from the implementation. Policy keys use player-readable terms such
  as `tax rate`, `prisoner treatment`, `meal rules`, `defense posture`, and
  `defense construction`. No aliases or load-time rewrites retain the removed
  pre-release names.

  Organization relations save `delegatedResponsibilities` and
  `retainedResponsibilities` using the current responsibility catalog.
  Regional authoring groups prospective members with `federationKey` and
  `federationKind`; materialization records membership as one federation-member
  relation. Agreements use direct kinds such as `shared warnings`, `military
  access`, and `protection`; protection records a `protectorKey`. Organization
  `publicSupport` is separate from tactical `CommandStanding`.
  Political-belief effects consume the same concrete belief names generated by
  faction authoring.

  The current pending-plan schema is version 1. It saves the authored region,
  factions, settlements, population groups, and prospective federation fields
  `federationKey` and `federationKind`. Materialized world state saves
  `organizationKey`, explicit organization kinds, typed settlement and
  federation membership, and separate provenance for roster membership and
  delegated terms. Faction structure and political beliefs use the same
  thirteen axes: leadership,
  decisions, participation, dissent, ownership, economy, work, support,
  membership, status, local order, defense, and war conduct.

  Exact old type names may remain only inside an explicitly superseded record
  that explains what was removed. They do not define current behavior or schema.
  The current authored composition is converted to the current schema as a test
  fixture; other experimental saves are not compatibility targets.

- **DR-93** (2026-08-10 05:17 UTC / 22:17 PST) - **Historical information is
  append-only; organization is regulatory.** The developmentally coherent
  chronology derived from the former ledger and first-parent Git history is the
  canonical batch sequence. `A1` through `A102` are closed; `B1` is the next
  development batch.

  Every batch record lives directly under `Batches/` in one alphanumeric
  namespace. A new letter marks a development era, not a separate history or
  catalog. `BATCH_LOG.md` is the chronological index, `Batches/THREADS.md`
  connects related work across nonadjacent dates, and
  `Batches/FORMER_LABELS.md` resolves the former ledger boundaries. Closed batch
  records are not rewritten. Corrections to navigation or classification are
  repository maintenance and do not consume `B1`; the former file layouts and
  exact pre-convergence records remain available in Git history.

  The records under `Batches/` establish this append-only baseline. The generated
  history tree in the immediately preceding repository state was a regulatory
  projection and is not a second set of closed batch records.

- **DR-94** (2026-08-10 08:49 UTC / 01:49 PST) - **Batch, thread, and version are
  separate development layers.** A batch is the atomic chronological record.
  `A1` through `A102` remain the closed historical sequence and `B1` remains the
  next ordinary batch identifier.

  Thread families and threads are permanent, series-neutral, many-to-many
  classifications over batches. They may span A, B, C, and later sequences and
  may connect work that returns after intervening development. The thematic
  catalog uses `TF-*` families and `T-*` threads. The temporary `ATF-*` and
  `AT-*` identifiers are retained only in
  `Batches/THREAD_ID_CROSSWALK.md` for traceability.

  A version unit is a capability-coherent chronological unit containing one or
  more contiguous batches. Version units partition chronology without gaps,
  overlap, reordering, or noncontiguous returns. Each unit carries one semantic
  tier. Threads help explain a unit's continuity but do not define its temporal
  boundary.

  CAO adopts Neo's root hierarchy and arithmetic:
  `major.minor.kohai.patch-maturity`, ordered
  `major > minor > kohai > patch > hotfix`, with hard caps `12`, `16`, and `24`
  on minor, kohai, and patch. The replay begins at the historical
  `0.1.0.0-pre-alpha` genesis. Twenty-seven evidenced units cover A1-A102 and
  derive `0.12.3.0-alpha`. A22's reproducible assembly is the evidence-backed
  maturity transition from pre-alpha to alpha.

  `tools/version-model.mjs` owns the replay, cap arithmetic, generated current
  version, generated doc stamps, and structural checks. `VERSION_MAP.md` is its
  human-readable chronological projection. The historical three-coordinate
  values remain evidence; the hand-frozen `0.2.2` value no longer presents
  itself as current state. This regulatory reconstruction consumes no batch and
  leaves `B1` unused. Extends DR-93 and corrects the accidental A-series scope of
  the first thematic catalog.

- **DR-95** (2026-08-10 09:35 UTC / 02:35 PST) - **Project history, local Git
  history, and published forge history are separate surfaces.** The current local
  project tree is the canonical implementation and documentation state. The
  governed batch and provenance system is the portable project history: batch
  records, chronological and thematic navigation, version units, decisions,
  findings, receipts, and source provenance travel with the project.

  Local Git may retain prior engineering commits and superseded working states.
  Those objects are supporting evidence and continuity, not a portability
  dependency. Recorded hashes remain valid local provenance even when a host does
  not publish them.

  Published forge history is a distribution record. The current GitHub `main`
  begins at parentless canonical snapshot
  `18b1034eee35f21158817727f4bc39af80dadd2a`; later commits record changes
  published after that baseline. Replacing, resetting, or mirroring a forge does
  not replace the project or rewrite its governed history. This corrects DR-93's
  reliance on Git history as the portable preservation layer. It is `[REPO]`
  maintenance, adds no version unit, and leaves `B1` unused.

- **DR-96** (2026-08-10 12:25 UTC / 05:25 PST) - **World tendencies author
  causes; realized world state is saved once and consumed.** Each editable row
  owns one policy variable with a direct effect, explicit constraints, derived
  outcomes, and independent neighboring controls. Starting-region authoring
  replaces the matching default only at that owning surface.

  Major-settlement abundance comes from RimWorld's world-population settlement
  pool unless a scenario explicitly overrides it. Concentration changes
  settlement placement before spatial pattern classification. Urban-growth
  propensity changes the support threshold; actual population, land, access,
  services, civic development, economic capacity, trade connectivity,
  specialization, regional role, and history determine realized scale.
  Reallocation source variety changes the owners represented among selected
  source settlements without changing abundance or final local ownership.

  Frontier frequency owns site count. Frontier size owns household, material
  level, and form after a suitable site exists. Regional plans and ordinary maps
  save the same realized holding facts before physical generation. Generated
  faction relations, relation pattern, settlement pattern, scale, population,
  and source receipts are persisted canonical facts. Generation and runtime
  consume those facts and do not reroll their tendencies. A confirmed regional
  plan that fails structural validation is rejected rather than silently
  regenerated. This extends DR-86's native-substrate rule and DR-4's declaration
  contract across the World tendencies flow.

- **DR-97** (2026-08-10 18:06 UTC / 11:06 PST) - **Established societies and
  the player founding share an ontology but not a temporal conclusion.** An
  existing faction or settlement is descriptive state: its Culture,
  Ideoligion, Political Beliefs, social order, institutions, relations, and
  history already exist when the player encounters it. Its setup surface
  answers what that society is already like.

  The player surface authors a founding moment. Culture, Ideoligion, and
  Political Beliefs are brought by the founders. Political Beliefs state what
  they consider proper. A Founding Arrangement states what they institute at
  landing. Agreement or conflict between those states is retained. Only the
  immediate arrangement materializes before play; mature institutions and
  historical practice develop through simulation.

  Both surfaces use the same Culture and Political Beliefs models, presets,
  vocabulary, and editors. RimWorld's native `Ideo` remains the Ideoligion
  substrate. The coordinated player page replaces the vanilla preset page as
  the top-level creator while retaining native fixed, fluid, and loaded
  Ideoligion editing. The general faction pass may complete established
  societies but may not fabricate a mature social order for the player. This
  corrects the unauthorized absence recorded by the prior start-surface map and
  restores the belief-to-arrangement relationship identified in
  `SETUP_SCOPE_MAP.md`.

  The confirmed player draft belongs to a world component so the same contract
  covers regional, ordinary, and forced-map starts. A durable applied-tick
  receipt prevents map or regional resolution from replaying the founding over
  institutions developed later. The exact arrangement creates duration-aware
  founding relations; it does not invent broader faction-structure answers.
  The native Ideoligion receipt follows content and revision rather than load
  ID alone, and RimWorld's structural Ideoligion checks run before scenario
  notification, including after native editor Back.
  Political Beliefs remain standards for judging practice. Organization customs
  come from the social order actually in force, not from belief alone.

- **DR-98** (2026-08-11) - **The active initiative ladder has three tiers.**
  Standard permits ordinary RimWorld behavior, enabled CA safeguards, direct and
  accepted relayed orders, observation, and continuation of an owned intent.
  Proactive adds finite responses to current facts. Autonomous adds persistent,
  adaptive, or collective action inside delegated authority. Old Directed `0`
  and old Standard `1` both migrate to Standard; old Proactive `2` and Autonomous
  `3` retain their meanings at the new compact identities. This supersedes the
  four-tier portions of earlier autonomy decisions without weakening explicit
  player orders.

- **DR-99** (2026-08-11) - **Permission, initiative, authority, knowledge,
  capability, and material conditions are separate gates.** A feature setting
  permits a behavior family. Initiative permits the actor to originate that form
  of action. Authority identifies who may commit it. Actor-held knowledge
  supplies the actionable fact. Native capability and material conditions decide
  whether it can be executed. Passing one gate never implies the others.

- **DR-100** (2026-08-11) - **Every CA behavior has one stable typed
  definition.** A behavior key owns its domain, form, actor contexts, minimum
  initiative, permission, required evidence, authority class, interruption rule,
  native execution lane, owner, and termination condition. Each behavior belongs
  to one primary domain even when it relates to others.

- **DR-101** (2026-08-11) - **Runtime authorization, settings presentation,
  tracing, diagnostics, and receipts read one behavior catalog.** Module-local
  labels and raw tier comparisons do not define public behavior semantics. The
  behavior census is a read-only view of that shared catalog and saved decisions;
  it does not become a scheduler.

- **DR-102** (2026-08-11) - **Player delegation and NPC institutions share
  planning facts but use different authority.** Player pawns may act only through
  operator, accepted relay, native duty, continuation, or explicitly delegated
  authority. NPC settlements and institutions act through their saved offices,
  households, organizations, laws, demands, and material means. NPC initiative is
  not presented as a player pawn setting.

- **DR-103** (2026-08-11) - **Culture and disposition rank permitted choices;
  they do not create permission or authority.** Cultural background, Ideoligion,
  political beliefs, local expression, skills, traits, mood, and pain may alter
  priority, confidence, method, or willingness inside the valid choice set. They
  cannot authorize a behavior that its setting, initiative, knowledge, office,
  ownership, or material conditions deny.

- **DR-104** (2026-08-11) - **Direct operator intent remains authoritative.**
  Player orders, drafted control, queued forced work, player-authored spaces,
  permissions, ownership, and explicit denials override self-originated CA work.
  Saved autonomous intent must carry its behavior key, episode, origin,
  controller, issuer, authority basis, owner, target, creation tick, and
  termination condition so it can be rechecked rather than silently replacing
  operator intent.

- **DR-105** (2026-08-11) - **RimWorld remains the physical execution
  substrate.** Authorized CA behavior uses native think trees, jobs, duties,
  reservations, blueprints, work designations, social interactions, world
  objects, and settlement records. Consequences that depend on a native job are
  committed only after that job succeeds. The behavior contract classifies,
  authorizes, records, and explains action; it is not a parallel central
  scheduler.

- **DR-106** (2026-08-11) - **Simulation complexity does not imply equivalent
  authoring complexity.** Creation exposes persistent causes the operator can
  meaningfully choose. The simulation may derive many layered consequences from
  those facts without turning every consequence, cache, or diagnostic into a
  control.

- **DR-107** (2026-08-11) - **A descriptive analytical category is not
  automatically a constitutive simulation variable.** Labels used to inspect,
  compare, or explain realized state do not become saved causes merely because
  they make a convenient selector. Every retained authoring control owns one
  concrete persisted fact and has a real downstream consumer.

- **DR-108** (2026-08-11) - **Continuous, relational, plural, and historical
  social state is not discretized merely for UI convenience.** A universal
  ordinal may be used only where the underlying phenomenon actually possesses
  that ordered scale. Overlapping provision, affiliation, belief, practice,
  access, and cultural relations retain their own causes and can remain
  contradictory.

- **DR-109** (2026-08-11) - **Culture is persistent longitudinal
  social-historical state.** A local Culture retains stable identity, inherited
  origin, local development, constituent populations, typed observations,
  recognized practices, transitions, and predecessor/evidence/domain
  provenance. Bounded historical evaluation derives `Culture(T+1)` from
  `Culture(T)` plus lived evidence. Unchanged history does not manufacture a
  transition. Cultural expression is a read-only contextual interpretation of
  Culture, and native `CultureDef` is one optional visual inheritance rather
  than the cultural model. This supersedes the insufficient cultural-background
  and derived-expression portions of DR-103 while retaining its authority
  boundary.

- **DR-110** (2026-08-11) - **Culture, Political Beliefs, Ideoligion,
  institutions, adopted rules, and actual practice remain distinct.** Culture
  records socially reproduced historical pattern. Political Beliefs record what
  populations hold proper. RimWorld Ideoligion owns religious, ritual, moral,
  and spiritual commitments. Institutions and adopted rules record realized
  order. Observed practice records what people actually do. Agreement,
  adaptation, plurality, and contradiction are meaningful state and are not
  collapsed to make summaries agree.

- **DR-111** (2026-08-11) - **Established societies and new founders occupy
  different temporal boundaries.** An established settlement may begin with
  mature institutions and local Culture because it predates the scenario, but
  authoring records an explicit temporal basis rather than inventing unobserved
  events. New founders bring inherited Culture, Ideoligion, and Political
  Beliefs, adopt rules at landing, and acquire local institutions and historical
  Culture through play. Player identity alone does not decide maturity; an
  explicitly established player-start scenario may carry earlier history.

- **DR-112** (2026-08-11) - **Native Ideoligion remains first-class inside one
  integrated CA founding flow.** Native presets, saved Ideoligions, fixed and
  fluid creation, memes, precepts, roles, rituals, validation,
  `Scenario.PostIdeoChosen`, and native persistence remain authoritative. The
  surrounding flow preserves inherited Culture, Political Beliefs, and landing
  rules across entry, Back, and return; the native chooser is neither forked nor
  treated as the complete founding ontology.

- **DR-113** (2026-08-11) - **Contextual explanation belongs to the decision or
  inspected object, not to a universal detail mode.** The global Compact,
  Standard, and Expanded authoring policy and repeated local detail toggles are
  removed. A decision states its fact, direct effect, constraints, and relevant
  consequences where it occurs. Diagnostics may retain technical provenance
  outside ordinary player copy.

- **DR-114** (2026-08-11) - **Starting Region preserves spatial authoring while
  exposing only meaningful direct facts and realized state.** The selected
  region, arrival area, map, factions, settlements, locations, population
  composition, ownership, relations, and concrete exceptional starting
  conditions remain authorable. Access, services, civic capacity, facilities,
  infrastructure, and settlement scale are derived from the people, land,
  institutions, technology, economy, material state, and history that actually
  exist.

- **DR-115** (2026-08-11) - **Generic settlement-intensity and facility-bundle
  controls are not canonical authoring primitives.** Minimal, Contextual, and
  Extensive development profiles; unrelated ordinal infrastructure controls;
  and per-facility Generated, Include, and Omit menus do not define arbitrary
  societies. Sparse exact exceptions may preserve or forbid a concrete starting
  object when that fact matters, but they remain secondary to realization and
  never become a universal settlement recipe.

- **DR-116** (2026-08-11) - **Culture at T0 contains substantive inherited
  meanings and practices.** Culture persists identity, constituents, inherited
  and current social meanings, inherited and lived practices, observations,
  transitions, maturity, temporal basis, and provenance. Approval, normality,
  prestige, and salience describe one named social subject; they are not
  universal Culture axes. Name and native visual tradition alone are invalid.

- **DR-117** (2026-08-11) - **Cultural subjects form an open registered causal
  contract.** A namespaced subject identifies its owning module, factual source,
  applicability, player account, and real consumers. Valid unknown keys survive
  persistence, but editors expose only registered source-and-consumer contracts.
  Informed pawn reactions and subject-specific group patterns mediate later
  cultural evidence; no closed topic enum or map-global response owns Culture.

- **DR-118** (2026-08-11) - **Political Beliefs are complete answer vectors,
  not profile identities.** Player authoring is question-first across the
  thirteen canonical normative questions. A built-in or saved profile copies a
  complete transparent vector and leaves every answer editable; the world stores
  only the answers. NPC derivation uses same-axis realized structure, explicit
  observed facts, or scored cultural meaning, records stable evidence, and
  leaves unsupported axes unset rather than filling them arbitrarily.

- **DR-119** (2026-08-11) - **The B8 authoring-data epoch is a destructive
  pre-release boundary.** Incompatible CA-owned Culture, Political Beliefs,
  profile, founding-draft, social-interpretation, and regional-authoring state is
  cleared with one diagnostic. The current runtime contains one schema and does
  not retain abandoned fields, aliases, partial preset inheritance, profile
  identity, or migration machinery for unsupported development objects.

- **DR-120** (2026-08-12) - **Objects and Map remain the canonical Starting
  Region navigation surfaces.** Details is reached from object selection or an
  explicit map action and shares the same selected object.

- **DR-121** (2026-08-12) - **Details exposes meaningful decisions and essential
  inspection.** Secondary facts move to object-specific inspectors; persistence
  alone does not justify permanent UI.

- **DR-122** (2026-08-12) - **One valid option is not an ordinary control.** It
  becomes a concise readout only when essential; absence becomes visible only
  when it blocks confirmation.

- **DR-123** (2026-08-12) - **Implementation provenance belongs to
  diagnostics.** Generated-source labels, schema facts, and receipt language do
  not appear in ordinary player copy.

- **DR-124** (2026-08-12) - **Setup titles use conventional title
  composition.** Explicit domain copy owns capitalization; dynamic names are not
  mechanically title-cased.

- **DR-125** (2026-08-12) - **Established settlement composition is an open
  derived program.** The seven-bit facility mask and its persistence are retired
  without alias or migration.

- **DR-126** (2026-08-12) - **Functional loaded assets participate through
  actual contracts.** Core, expansion, and ported content use the same native
  evidence; visual appearance or suggestive names alone do not qualify.

- **DR-127** (2026-08-12) - **Provision operators must exist socially,
  materially, and behaviorally.** A generated arrangement requires a real
  operator, access, stock, funding, program support, and consumer.

- **DR-128** (2026-08-12) - **Faction technology is faction state.** It is a
  factual faction readout rather than a settlement setting.

- **DR-129** (2026-08-12) - **Settlement research requires a real research
  institution.** It appears only when a research program and loaded functional
  asset support it.

- **DR-130** (2026-08-12) - **Culture is not linear.** Normal authoring uses
  semantic social interpretation; exact continuous values remain contextual
  advanced controls and round-trip without loss.

- **DR-131** (2026-08-12) - **Faction Relations is the relational-authoring
  visual reference.** Relation state is shown directly without generated-source
  provenance.

- **DR-132** (2026-08-12) - **B9 is the final pre-runtime correction batch.**
  B10 remains next and cannot begin before the operator validates the deployed
  creation flow and generated game.
