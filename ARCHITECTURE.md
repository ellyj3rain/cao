# Architecture — the orthogonal framework

Ratified 2026-07-22. The mod is a framework over RimWorld, not a patch set: four
pillars, each a real substrate with its own model, projecting into the game's
native machinery. Every behavior is the composition **Knowledge triggers →
Disposition decides → Authority channels → Doctrine executes.**

## Pillar 1 — Disposition (psychology)

Humanlike pawns resolve courage, discipline, aggression, initiative, empathy,
conformity, and skepticism from traits, skills, backstory, ideology, and current
condition. Animals project the same pillar through animal terms: vigilance, nerve,
attachment, and defensive drive, resolved from species properties, stable
individual variation, training, relationships, and condition. Both profiles are
legible in one place. Every threshold reads a profile rather than a scattered
constant. The autonomy dial (see canon below) remains the player's CONTROL
surface; disposition is texture within permitted action and never overrides the
player's hand.

## Pillar 2 — Knowledge

Typed facts with provenance, held per-pawn: seen firsthand, or told by someone,
trust-gated. Facts propagate through comms and social interaction. Behaviors read
KNOWN facts, never global map truth. This is the substrate for: contact reports,
squad leaders and RTOs as information hubs, social fog (Layers 1–2), gossip and
misinformation, and the opt-in player-view fog. `ColonyContextComponent` is the
collective cache this layer samples, not a replacement for it.

Knowledge channels depend on the knower. Humanlikes can receive semantic reports
through valid communication edges. Animals currently retain only firsthand
sensory facts; hearing a weapon report supplies an approximate area, never a
shooter identity or a human report.

A remembered contact separates identity and history from its last honestly
observed state. `Active` alone is actionable combat knowledge; firsthand sight of
the same pawn downed, dead, captured, nonhostile, Nonthreatening, or
destroyed resolves the threat without erasing the fact. Resolution is per knower.
This first state slice does not relay resolved state, so a remote knower retains
their own last report until firsthand correction or staleness.

Event provenance can mint a bounded mission fact when the event itself constitutes
the fiction. The Man in Black arrival records only the downed player colonists the
incident generated him to save, their arrival cells, and gross downed state. It
grants no clinical severity, general hostile, item, or map knowledge; urgency
requires a local assessment.

Welfare is a separate typed fact family. A pawn may retain gross condition and a
last-known cell from firsthand sight, receive the teller's remembered cell and
source age through a valid same-faction communication edge, or acquire a coarse
clinical estimate only through a close sight/touch assessment. The store exposes
scalar IDs, cells, ticks, states, and estimates rather than a live pawn reference.
Mission-event evidence is owner-scoped and nonrelayable; a later independent visual
or clinical observation may supersede it and relay normally. An in-flight general
`CA_CheckWelfare` is bound to the fact's origin, semantic state tick, revision, and
copied cell. An exact non-stale Unassessed/Visual heartbeat refreshes source
freshness without changing that token. Stale reacquisition or a material state,
cell, clinical, mission, channel, reporter, or provenance change advances the
revision and cancels stale work before it can overwrite the newer fact.

A stable direct clinical assessment is durable meaning, not a periodic event.
Repeated downed/no-bleed observation renews freshness without advancing its
revision, emitting another floating diagnosis, or manufacturing trace history.
Finite bleed projections, dangerous temperature, expired relayed projections,
and materially changed clinical meaning remain refreshable because their action
value can change with time.

## Pillar 3 — Authority (roles, politics, ideology)

An explicit graph records who may command whom. Relayed orders use the giver's
command standing, the follower's discipline and conformity, and the follower's
opinion of the giver. Refusal remains a visible event. Organization public
support is separate: it measures whether current leadership can bind a
settlement and changes through losses, belief conflicts, and imposed policy.

Accountability is conditional, not a colony-wide military roll call. An expectation
exists only across a stored squad-leader or fire-team-leader responsibility edge
while both pawns initially share one CA tactical or stack-breach Lord. Real sight or
a valid voice, headset-radio, or mechlink-mental check-in confirms presence. A
missed check-in becomes overdue on a cadence shaped by the responsible pawn's
discipline, empathy, and initiative. Proactive leaders may inspect a nearby copied
last-confirmed cell; Autonomous leaders may travel farther. Timetables, allowed
areas, hidden health, death, jobs, and live positions do not prove or disprove
presence. The current first slice stores this record in `KnowledgeMapComponent`
while consuming Authority, Doctrine, Disposition, and Autonomy; moving policy and
storage to an Authority-owned component remains explicit structural debt.

For animals, Authority is the native control relationship rather than a human
command graph: direct player-forced orders, assigned master, bond, training,
rope, Lord, and current forced state bound which self-initiated choices may run.

## Pillar 4 — Doctrine (tactical execution)

The executor, built on the game's native group-AI substrate (see
`GAME_ARCHITECTURE.md`): Lords own groups and state machines; DutyDefs are
standing behavior programs; custom JobDrivers own posture. Shipped: the door-stack
drill (`LordJob_CAStackBreach`: Form→Set→Breach/Clear→Reconvene), the standing
tactical lord (`LordJob_CATactical`) carrying holds/overwatch/ambush/hide duties,
formations and painted lines riding the same order surface. No job-stomping
re-issue loops anywhere; the player's hand always shadows a duty and the duty
resumes when it lifts.

Immediate humanlike combat is Doctrine at the individual scale. Eligible
undrafted Proactive+ colonists and exact generic settlement assaulters use the
same actor-local assessment of direct or copied threat facts, current condition,
visible firing geometry, nearby terrain, and visible non-hostiles. That assessment
may yield one finite native-shaped move, fight, or break-contact job. It does not
construct a group plan: allocation, sectors, battle drills, and coordinated
maneuver remain Lord/Duty work using facts that actually reached the responsible
leader. Animal Doctrine remains on its separate animal terms and think lane.

Post-contact casualty triage is also actor-local Doctrine. A Proactive+ Man in
Black with incident-bounded beneficiary facts yields to perceived active danger,
then approaches an arrival cell and performs a finite casualty assessment. Medical
skill drives its speed and precision while Intellectual assists; the result is a
coarse saved bleed-out estimate displayed over the casualty, not exact remote
health telemetry. Triage compares that estimate with the rescuer's similarly
skill-bounded self-assessment. The urgent patient is stabilized first; a stable
downed beneficiary is carried to a native bed when one exists. One mission-only
self-tend job supplies the narrow
exception RimWorld needs when global self-tend policy is off; it does not change
that policy. Hostile custody and execution remain later Authority/Doctrine
outcomes, not implicit medical cleanup.

General welfare response is the same finite-job pattern. A Proactive+ capable medic
may inspect a fresh firsthand or reported welfare fact, travel to its copied cell,
perform a visible local assessment, and only then tend from the resulting coarse
estimate. Automatic vanilla rescue is knowledge-gated only while Life safety or
Field medicine is enabled; forced player rescue and feature-off vanilla behavior
pass through unchanged. The finite check driver carries origin plus record revision
and mutates only the exact welfare or accountability record that launched it.

Animal Doctrine uses the same native execution principle at its own scale. A
bounded response giver follows vanilla immediate-danger fleeing on the animal
constant-think lane and owns one finite response job; it does not install a
job-stomping loop or manufacture an identified threat from ambiguous sound.

## Autonomy tier canon (ratified)

Explicit orders persist at every tier. The dial governs self-initiation only.

| Tier | Active meaning |
|---|---|
| Standard | Ordinary RimWorld agency, enabled CA safeguards and native augmentation, direct and accepted relayed orders, observation, and continuation of an owned intent. It does not originate discretionary plans or unowned collective work. |
| Proactive | One finite response to a current actionable fact, with bounded target, space, time, authority, completion, and interruption. |
| Autonomous | Persistent, comparative, adaptive, or multi-actor work inside explicit delegation, office, institution, ownership, and material limits. |

Old Directed `0` and old Standard `1` migrate to Standard. Old Proactive `2`
and Autonomous `3` migrate to the corresponding active tiers. The current save
schema and UI use only the three active identities.

Operational access is colony policy, not an aggregate of pawn initiative.
When enabled, the colony policy may reconcile ordinary visible non-quest items
with the native forbid system while preserving every explicit player denial.
Pawn initiative separately controls personal equipment choices. A Proactive pawn
may select one weapon or genuinely protective apparel only for a current
actor-held threat fact, through the exact behavior gate and native equipment job.
Restricted categories still require their own authority. Drafted control, current
and queued forced work, allowed areas, ownership, and standing tactical orders
remain authoritative.

## Behavior catalog and commitment gate

`BehaviorCatalogModule.cs` is the shared definition surface for every CA
behavior. Each stable key owns one primary domain and one form, plus actor
contexts, feature permission, minimum initiative, evidence requirement,
authority origins, execution lane, cadence, intent owner, completion, and
stand-down conditions. Domains organize ownership and settings; forms distinguish
observation, native augmentation, safeguards, finite responses, readiness,
coordination, persistent objectives, adaptive plans, institutional action,
direct orders, execution capabilities, presentation, and diagnostics.

The effective profile is a cached stable prefilter. It may exclude an impossible
setting/tier/actor combination, but it never authorizes a commitment. At the
actual mutation or native-job boundary, `CABehaviorGate.Evaluate` must separately
validate feature permission, initiative, authority ceiling, authority origin,
actor-held knowledge and its provenance, live target state, player ownership,
capability, material conditions, and current-intent compatibility. Every
CA-originated native job is registered with its behavior key, episode, origin,
controller, issuer, authority, owner, target, creation tick, and termination.

Player pawns and NPC institutions may consume the same planning facts without
sharing authority. Player action requires direct, accepted relay, native duty,
continuation, or explicit delegation. NPC settlements act from saved offices,
households, organizations, law, demands, and material means; player initiative is
not an NPC policy. Culture and disposition rank permitted alternatives but never
manufacture permission or authority. RimWorld jobs, duties, reservations,
blueprints, work designations, social interactions, and world objects remain the
physical executors.

## Settlement-objective projection

Domestic autonomy is expressed as a saved objective with explicit provenance,
not as a loop that repeatedly asks for a job. Need policy selects a provision;
the placement solver records its intended native blueprint; a prerequisite service
turns the exact cost into typed material deficits; authorized producer policies
may then expose native work designations. The original objective waits while
RimWorld owns harvesting, hauling, reservations, construction, and job priority,
then resumes when its material vector is satisfied.

## Native storage authority and construction reserves

Durable inventory uses RimWorld's native `Zone_Stockpile`, `StorageSettings`,
`StoragePriority`, slot groups, storage buildings, and hauling. Their footprints,
filters, priorities, labels, and mixed contents are the canonical storage policy.
Colonist Awareness does not manufacture category-named durable zones or treat a
room-purpose label as an item filter. Existing native storage remains player
authority.

The former differentiated-inventory records remain deserializable for save
compatibility. On load, CA retires their operative ownership and veto records but
preserves historical zone-origin provenance read-only; it does not attribute any
later item movement or current placement. Every native zone, cell, filter,
priority, label, and held item remains unchanged. Present player edit authority
and historical CA zone origin are separate facts. Legacy
`CASpacePurpose.Storage` programs remain readable by facility and toxic-waste
receipts, but Storage is absent from the new-purpose palette and an unchanged
legacy Storage footprint cannot be expanded. The player may clear it or convert
it to another current room purpose.

Policy may still read actual loaded item definitions and components as
non-exclusive risk evidence. Rottability, dissolution, roof exposure, hazard,
medicine, deterioration, and flammability can all apply to one definition.
Temperature changes rot or dissolution outcomes and is sampled separately; it is
not a content category or a universal native-destination gate. A mixed fridge or
freezer is valid whenever its native filter admits those contents. Native medicine
and medical-category drugs are medical supplies; a drug is not excluded merely
because it does not satisfy `ThingDef.IsMedicine`.

Critical-storage evidence keeps value, life-safety importance, roof and natural
overhang, room state, temperature, native destination and priority, Home
integration, reachability, treatment access, map-edge depth, authored defensive
topology, turret proximity, and placement provenance separate. A dedicated
Important cell under natural mountain roof is real protection evidence even when
the location is strategically incomplete. Relocation is not authorized unless a
genuinely safer native destination exists and accepts the item.

The only CA-originated stockpile path in this layer is an exact-objective
construction reserve. When one authorized blueprint lacks a specific material and
no existing native destination can accept the deficit, CA may create a small
Important-priority reserve filtered to that material, with saved provenance and a
veto for that exact construction objective. It is demand staging, not durable
inventory classification, and native hauling remains the physical executor.

`CASpatialInitiativeMapComponent` attaches one saved CA initiative ceiling to each
native stockpile and authored room program: Standard `0`, Proactive `1`, or
Autonomous `2`. An unspecified space is Standard. Old `0` and `1` values both
migrate to Standard; old `2` and `3` become Proactive and Autonomous. Effective initiative is
the lower of the acting pawn's autonomy and the space ceiling, so a space never
elevates a pawn. Native priority continues to choose among valid storage
destinations and native filters continue to define accepted items. The operative
storage consumer performs bounded shelf/capacity work inside an existing zone and
preserves doors, circulation, service cells, negative space, current contents, and
the established furnishing pattern.

## Living-colony spatial grammar

Functional space programs say what a place is for. A saved settlement planning
context says how those places relate and why that relationship belongs to this
colony. It is derived from actual residents and their relationships, plural
ideologies and cultures, authored programs, terrain and climate, available
resources, infrastructure, security history, and existing construction. Its
placement judgment combines semantic legibility, social fit, ideological
expression, environmental fit, operational coherence, historical continuity,
strategic topology, and bounded imperfection. Efficiency supplies a viability
floor rather than the single objective.

Existing player-authored Kitchen, Freezer, Utility, and Defense programs, plus
legacy Storage programs, are evaluated in place from their `CASpaceProgram`
footprints. The facility-requirement receipt keeps loss criticality, loaded-item
hazard, service
and grid access, logistics, habitation separation, defensive topology, bounded
footprint, expansion opportunity, current-use state, culture, construction
stage, present readiness, and future site potential as separate axes. Excavated
natural-roof cells and adjacent mineable expansion seams are different evidence.
Present readiness reads the current enclosure, roof, temperature, reachability,
native destinations, built contents, and room-serving temperature controls;
future potential may report a connectable grid or expansion seam without calling
an unfinished Freezer cold. Turret distance is proximity evidence only, and a
beachhead or defended-side claim requires a direct topology relation. Receipt
access is observational and creates no program, normalization, construction,
inventory move, or alternate room choice; any consumer requires a separately
authorized objective.

Bounded toxic-waste staging is a saved player-home lifecycle objective created
only from real loaded `Wastepack` inventory. Its inventory observation traverses
spawned things and nested holders, preserving each lot's direct holder, root cell,
dissolution state, native-atomizer containment, and player forbid state. Separate
saved records carry bounded capacity, present thermal state, relocation need,
local destination, transport, and consequences. A local destination may come only
from an existing player-authored Freezer, legacy Storage, or Utility program whose
exact footprint already contains native storage accepting wastepacks. Containment
cells must be roofed, reachable, free of beds, and expose enough native-haul-valid
capacity. Current temperature, frozen cells, and dissolution state remain separate
dynamic risk evidence; freezing is one capability-dependent tactic, not the
definition of containment or placement success.

Grid access, habitation distance, Defense-program distance, and turret proximity
remain separate evidence. A native atomizer's held waste is containment evidence,
not proof of powered or completed remediation, and turret proximity does not prove
a defended side. The objective's evidence signature changes only when semantic
evidence changes; observation cadence alone does not manufacture history. An
allowed local Wastepack plus an existing player-authored program and its native
Wastepack-accepting storage policy may authorize `CA_ToxicWasteResponse`; that
WorkGiver revalidates the exact source and destination while RimWorld owns
reservations, carrying, and placement. Player-forbidden state remains a veto. This
path creates no program, filter, priority, facility, shipment, remote operation,
pollution, goodwill, grievance, or conflict consequence. Cross-map shipment and
consequence ownership belongs to the regional world substrate when that substrate
is built; the current map component owns only the local containment evidence,
native-haul reconciliation, and lifecycle boundary.

Ideological influence follows actual adherents and relevant authority. Memes,
precepts, roles, rituals, culture, and resident beliefs can shape centrality,
privacy, communal use, separation, display, defense, material, and style. Minority
ideologies remain part of the settlement context; the faction's current primary
ideology is not a complete colony belief state. Completed construction and
player-authored programs remain durable history while later proposals adapt around
them.

The grammar may combine dense social cores, dispersed buildings, layered rings,
terrain-following mountain growth, reserved infrastructure, and frontier or
fallback positions. These are overlapping spatial expressions rather than mutually
exclusive colony modes. Operator and community examples provide comparative
evidence for the scorer; they are not layouts to reproduce cell-for-cell.

## Regional living-world projection

Colonist Awareness is one modular pawn-agency, settlement, and living-world
simulation overhaul. The four pillars remain continuous across pawn cognition,
tactical behavior, settlement autonomy and logistics, animals and ecology,
regional geography, factions, settlements, economy, diplomacy, and conflict.
These systems share stable identity, time, space, source records, Knowledge, and
Authority.

The root world-tile graph is the authoritative regional topology. A saved regional
plan owns its footprint, arrival area, factions, settlements, faction relations,
world tendencies, settlement pattern, scale, and player founding state. A
settlement plan owns direct facts: stable identity and location, faction,
population composition and sources, settlement form and role, provisions, and a
sparse exact exception mask where the operator must preserve or forbid a concrete
starting object. Access, services, civic capacity, facilities, infrastructure,
and settlement scale are realized consequences of population, land, geography,
technology, knowledge, institutions, economy, trade, material state, and history.
Persistent world objects mark settlements and visible moving actors. Maps
materialize player homes, entered settlements, encounters, and other events that
need pawn-, thing-, job-, or Lord-level detail.

A faction owns inherited Culture and its optional native visual tradition,
Ideoligion, Political Beliefs, instituted current order, and the authority shared
between its settlements. A settlement keeps a stable CA identity even when its
faction changes. Its realized record owns residents, population groups,
organizations, material state, relationships, materialization history, and one
persistent local Culture. Faction era records what the faction knows. Facilities
and infrastructure record what a settlement can support. Local capability is
derived from those facts; it is not a separately authored source of truth.

Local Culture is longitudinal social-historical state, not a profile reconstructed
from the current settlement summary. It records inherited origin, local identity,
constituent populations, inherited and local question distributions, inherited
and lived practices, typed observations, successive transitions, legacy evidence,
and complete provenance. Each question distribution owns a population scope,
center, spread, descriptive-norm prior, prestige signal, salience, norm strength,
visibility, confidence, divergence tolerance, optional subgroups, source identity,
and evidence signature. Social subjects remain factual interpretive referents and
practices remain concrete repeated conduct with participants, conditions,
authority, materials, evidence, and consumers. Neither substitutes for a
question distribution.

Pawn cultural cognition persists private attitude, public expression, perceived
descriptive and injunctive norms, confidence, moral conviction, identity
centrality, enforcement expectation, visibility, prestige, uncertainty, and
provenance. Its psychological evidence is a separate stable profile with dynamic
condition kept distinct. Native Ideoligion contributes doctrine pressure without
rewriting Culture. Sparse influence edges record represented source and target
pawns, separately timestamped Culture-question and political-axis exposures,
weight, trust, prestige, conformity, visibility, and contact time.
Bounded-confidence and repeated-exposure updates may therefore produce
private/public, perceived/actual, faction, and settlement differences without a
global opinion reroll.

Political cognition is a separate world owner. A pawn's issue position derives
from the relevant Culture distribution, psychology, native Ideoligion, material
interest, institutional experience, threat, held proposition knowledge, and a
majority perceived only through represented exposure. Strong compatible
positions may form
faction-bounded coalitions while retaining internal disagreement. Political
Beliefs remain normative authoring state and current order remains instituted
fact.

Organizations own institutional legitimacy and sanction history. Procedure,
performance, representation, competence, coercion, Culture, political fit,
treatment, trust, and public support remain independently inspectable inputs.
Law/custom fit, corruption, and Ideoligion fit remain neutral until comparable
facts are represented; evidence confidence attenuates the estimate. Enforcement,
compelled work or transfer, confiscation, taxation, and custody punishment create
bounded persisted sanction appraisals rather than silently changing Culture.

Proposition knowledge is another world owner. Claims retain holders, source,
channel, contradictions, trust, reliability, expertise, motive, corroboration,
plausibility, method, conflict, access, confidence, transmission, custody, decay,
and research receipts. Direct observation remains available regardless of access
norms; reports and testimony require represented standing. Novelty affects
attention and transmission rather than truth or discovery. Culture changes
appraisal and the selection of otherwise valid discretionary CA-originated
action. Native execution, direct or relayed operator intent, authorization,
knowledge, land, technology, labor, materials, treasury, and office remain
separate causes.

An existing faction's current order is realized state from a society with history.
An established settlement begins with an explicit temporal basis and may retain
mature institutions and local Culture, but authoring does not invent unobserved
event history merely to make it established. The player founding state is
intentionally earlier: it owns the inherited Culture and Political Beliefs brought
by the founders, a receipt for their native Ideoligion content and revision, and
the Founding Arrangement adopted at landing. Native Ideoligion validity is checked
before the receipt may authorize scenario notification. That arrangement
materializes once as exact, duration-aware founding relations. It is not expanded
into broader current-order answers that the player did not choose. Player
institutions and local historical Culture develop through simulation; the
established-faction generator does not fill them at game start.

Individual observation becomes collective action only through an explicit causal
chain: a pawn observes; a valid communication or reporting edge carries an
assertion; an organization records or rejects it; the faction's decision method
uses the records available to it; an authority issues an action; pawns execute it;
and observed consequences create new facts and records. Organization records keep
their source, place and time, reporting chain, confidence, access, replacement,
and loss. They may be stale, false, secret, corrupted, or absent.

Political subjects cover leadership, decisions, participation, dissent,
ownership, economy, work, support, membership, status, local order, defense, and
war conduct. Political Beliefs record normative mechanisms; current order records
instituted mechanisms. Several compatible mechanisms may coexist on one subject,
and only an explicit absence mechanism excludes standing leadership, local-order,
or defense mechanisms on that same subject. Partial belief and current-order sets
copy only listed mechanisms and preserve unrelated authored facts. They create no
political identity and are never shared mutable world owners. NPC generation
derives a belief only from same-subject current order, an explicit observed fact,
or the matching Culture question distribution and records the causal evidence used; unsupported
subjects remain unset. Settlement authority records what a faction's settlements
decide and provide together. Offices, votes, delegation, emergency powers,
succession, trade, mobilization, negotiation, surrender, and agreements use
those saved facts.

Organizations own offices, groups, customs, security, agreements, policies,
claims, relations, and decision history. Organization customs follow the social
order actually in force. Political Beliefs remain standards used to judge that
order and observed acts; they do not silently become adopted customs. There is no separate ideology
classification layer between those facts and their consequences.

Geography follows the visual land shape projected from the selected world areas.
Coasts, water depth, roads, rivers, caves, and added geographic features share one
projection. Settlement positions use valid land within that shape, not the center
or edge of the source world tile.

Relationships remain sparse and asymmetric between pawns, organizations,
settlements, and factions. Trust, grievance, fear, obligation, affinity, access,
dependence, and hostility update only the involved records. Native pawn opinion is
the individual relationship seam. Native faction goodwill and
Hostile/Neutral/Ally state are used when a faction-wide posture is justified; a
local dispute does not automatically become a faction war.

Regional conflict is a durable history rather than a raid roll. Incidents may lead
to demands, intimidation, retaliation, raids, feuds, mobilization, authorized war,
occupation, surrender, ceasefire, or treaty without following one mandatory
sequence. Formal war exists only for factions able to recognize or authorize
it. Conflict records own participants, grievances, declared status, aims, limits,
costs, and agreements. Operation records own source settlement, authority, force,
supplies, route, objective, constraints, withdrawal policy, losses, result, and
unresolved intent. Incident parameters, Lords, duties, quests, and maps execute or
present that state and reconcile their outcomes back into it.

Each settlement owns its economic needs, reserves, surplus, production, storage,
valuation rules, obligations, transport capacity, access, and security. Local
physical trade may settle through RimWorld's native trade window. Remote exchange
uses offer, authorization, reservation, shipment, route events, delivery, or
default. Barter, gifts, tribute, redistribution, and markets share this transport
and payment system without presuming one economic model or frictionless transfer.

Loaded maps and consequential physical encounters receive full simulation.
Inactive populations, production, reports, claims, relationships, shipments, and
operations advance through cached records and scheduled events. Promotion into
pawns, caravans, world objects, or maps and demotion back into records produce
receipts that preserve identity, goods, casualties, knowledge, and unresolved
actions. Abstract and materialized resolution must meet deterministic parity tests.
Player-facing regional views disclose only learned claims, confidence, age, and
source; omniscient truth belongs to developer instruments.

## Spatial furnishing evidence

`CASpatialFurnishingModule` is the read-only convergence layer for storage and
room furnishing. Its durable inputs are existing native `Zone_Stockpile`
authorities and player-authored `CASpaceProgram` footprints. Its semantic inputs
are the loaded `ThingDef`, `RoomRoleDef`, `RoomStatDef`, worktable-role,
`CompAffectedByFacilities`, `CompProperties_Facility`, native placement, research,
material, pathing, and reservation-compatible construction contracts. The module
returns a receipt and owns no saved state.

Candidate discovery is definition-driven and retains `modContentPack` provenance.
Storage buildings, facility-linked furnishings, and positive-Beauty ambient
furnishings remain separate classes of evidence. A native classified room without
an authored CA footprint is observable but cannot yield a CA placement candidate.
A visual-only prop without a gameplay or construction contract is not assigned
fabricated utility.

The operative room-construction consumer currently begins only from an existing
player-authored Barracks and follows the native bed-facility graph. It originates
one functionally grounded facility family at a time; more than one building of
that family may be required when native link distance divides a large bed bank
into coherent groups. It does not add a second family merely because another
positive stat is available.
An existing player blueprint or frame for any linkable facility inside the
authored room blocks CA origination until native construction resolves it. Home
planning and spatial initiative also share one map-wide CA construction
commitment: a pending blueprint or retained home prerequisite in either lane
prevents the other from claiming materials or starting parallel work.
Functional posture, wall backing, usable facing space, current use, protected
traffic lanes, bed-bank intervals, service and sleep approaches, circulation,
negative space, research, materials, culture/style evidence, and native room role
remain separate. Native Beauty is observed but is never the placement objective.

A player-authored Bedroom instead has a read-only requirement comparison. It
reads only existing `CASpaceProgram` footprints through the observation surface
and does not choose a room, normalize program state, expose an initiative control,
or authorize construction. Program resident authority and actual saved roster,
native bed ownership, love/family relations, `BedUtility` sharing willingness,
resident ideologies and culture, native Bedroom role/worker composition, room
stats, approaches, circulation, negative space, present furnishings, and
completed, blueprint, and frame stages remain separate evidence. Present native
function is reported separately from future optional facility capacity, research,
technology, and map resource counts. An available Dresser, EndTable, or other
bonus is not a missing Bedroom requirement, and Beauty remains telemetry rather
than an aesthetic target.

The broader sleep-program predicate remains available only to validate historical
associations safely. It grants no new Bedroom origination authority. Legacy
Bedroom pending metadata becomes inert and retires without canceling or adopting
a player blueprint or frame; a completed player-owned building likewise survives
association retirement.

Room authority is revalidated while a CA blueprint is pending and while a
completed CA association is retained. If the player deletes the program, changes
its purpose, removes the footprint, or eliminates the native bed relationship,
the tracked pending CA blueprint is canceled and refunded through native cancel;
an already completed player-owned building remains, while only the stale CA
association is retired. Pending work uses the native potential-link predicate;
completed work uses the facility's actual reciprocal linked-building graph.

Settlement-context comparison is evidence whether or not it changes the geometric
winner. Verification requires a complete receipted pre-context and selected
comparison; it does not require the selected cell to differ. Context may confirm
that the already strongest Feng Shui candidate remains correct. A dedicated
regression whose purpose is to prove that context can change a ranking retains
its stricter `ranking changed yes` assertion.

`CAAnimalInfrastructureModule` and `CACriticalStorageEvidenceModule` are read-only
observers. Animal containment, sleeping, companionship, veneration, training,
defensive potential, and existing installable assets remain separate. A native
animal receipt distinguishes the player faction, its primary and minor
ideologies, and the actual ideology of each spawned free colonist on the observed
map. Veneration is adherent-and-species-specific; a settlement label, primary
ideology, observed free-colonist minority, and currently present matching animal
are not interchangeable.

The loaded `AnimalVenerated` precept constrains hunting and slaughter through
native willingness and supplies death, meat, and living-presence thoughts. It
does not alter `AnimalPenUtility` and creates no native pen exemption or sleeping-
location mandate. Assigned and effective masters, follow settings, bonds,
obedience, other training, installed or minified beds, and current pen state are
receipted independently. A native call sound is not a semantic alarm, and
sentinel placement requires a real hostile perception, report channel, reachable
residents, and actual approach topology. Minified animal beds are owned assets
but supply no installed sleeping function. Neither evaluator paints a program,
changes storage, assigns a bed, constructs, or relocates an item.

The command bridge exposes these evaluators, deterministic regression contracts,
and fixed observation or derived-fixture loads. Observation commands do not save.
Mutating fixture commands are bound to the exact loaded-save SHA256 and use native
blueprints, jobs, storage settings, facility links, and specifically named derived
milestone saves.

The God Mode item mover is transient developer instrumentation rather than a
simulation consumer. While Dev Mode and God Mode are both active, Ctrl-left-drag
may relocate the topmost selectable spawned non-corpse item stack to one exact
physically valid, non-wiping cell. The gesture retains stack identity and count,
never splits or merges, and refreshes native room, slot-group, hauling, mergeable,
region, thing-grid, and cover evidence after the move. It owns no saved state,
storage
policy, program, job, or construction authority. Region-affecting definitions and
live buildings remain outside this path; minifiable buildings continue through
RimWorld's native Reinstall contract.

## Starting Region Information Architecture

`Page_CAStartingRegion` keeps one shared object selection across Objects, Map,
and Details. Object-rail selection may enter Details; map selection stays on the
map and exposes an explicit details action. Compact settlement Details traverses
the object-rail order and resets only its scroll position. Drawing and navigation
do not regenerate saved state.

The Details contract distinguishes choice, essential fact, blocking warning,
and secondary inspection. `CAContextualChoicePresentation` renders a control for
multiple valid choices, a readout for one essential choice, no surface for one
inessential choice, and a warning only when absence blocks confirmation. Default
settlement Details owns identity, Population, Culture, Settlement Composition,
and the separated destructive action. Faction and region Details expose the
same facts at their actual owning scope.

## Settlement Program Architecture

`CASettlementProgramRegistry` is open and namespaced. Each definition records
domain, owner, operational source contract, loaded-candidate contract,
materialization contract, inspection summary, maintenance or runtime consumer,
failure result, and functional evidence. Applicability is decided only by
`CASettlementProgramOperationalEvidence`: need, actual operator, standing,
knowledge, labor, material, target population, access, funding, stock, policy,
and maintenance. Broad axis thresholds are not program predicates. Each saved
entry retains those causes alongside selected candidates, required extent,
materialization state, placed identities, blockers, and a stable signature.
`CASettlementProgramMaterializer` consumes the confirmed entry and records
native results; it does not reroll authoring.

The initial 22 programs span housing, food, storage, medicine, production,
trade, governance, security, defense, research, religion, social life, culture,
agriculture, communications, and transport. Candidate discovery prefers native
class, component, room-role, recipe, linking, and placement contracts. Explicit
adapters are permitted only when no stronger native contract exists and the
owning module records a real consumer. A decorative object alone establishes no
program.

Provision arrangements share the same causal boundary. Individual, domestic
unit, communal organization, and authority are the current factual operator
types. A persistent `CADomesticUnit` is formed only from represented partner,
kin, shared residence, or authored co-residence evidence; a hash, pawn order, or
population adjacency cannot form one. Communal and authority arrangements need
their actual organization or jurisdiction, workers, physical program support,
access, funding, stock, and distribution behavior. Tax support additionally
needs adopted policy and collection. Confirmation freezes the derived program
and provision records; map generation materializes those records rather than
reinterpreting tendencies.

## Causal social ownership

`CASettlementCapabilityResolver` derives eight practiced domains from typed
evidence containing actors, organizations, active operations, knowledge,
material nodes, history, blockers, confidence, assessment tick, and a source
signature. It has no random jitter and does not create a program. World axes are
read models or generation pressures and cannot feed the reality they summarize.

Political Beliefs remain normative positions. Current order, offices, policies,
ownership, taxation, work, and provision are separately persisted facts that
change through represented decisions and transitions. Culture owns population
distributions over admitted questions and may affect appraisal, legitimacy,
participation, prestige, stigma, funding willingness, maintenance, siting, form,
resistance, and discretionary CA action selection. It cannot grant authority,
knowledge, labor, technology, materials, land, treasury, office, execution
capability, or permission.

Native rooms and things are typed as material, work, storage, access,
communication, symbolic, or spatial nodes. An institution exists only when its
operator, members or target population, authority, activity, inputs, outputs,
access, and maintenance connect to those nodes. The complete ownership ledger
and repeated synthetic-state sweep live in `B10_CAUSAL_PROVENANCE_AUDIT.md`.

## Durable campaign and authoring ownership

`CACampaignSchemaCatalog` is the executable list of live semantic state families.
`CACampaignCompatibilityWorldComponent` owns the boundary, manifest, and migration
receipts. Streaming preflight accepts current state or the controlled B10 envelope
before Scribe load, and unsupported state fails visibly without partial mutation.
Creation drafts use a separate pending-authoring epoch; they cannot erase realized
campaign history. Supported migrations preserve stable semantic identity, are
idempotent, and describe upgrade-time initialization without inventing earlier
history.

`AuthoringOntologyKernel` owns immutable semantic-kind, production-vocabulary,
partial-set, category-cardinality, and control-contract registries. It does not
own Culture, political, faction, settlement, or world state. The current
production inventory contains 45 social referents, 30 concrete practices, 52
political mechanisms, 12 belief sets, 10 current-order sets, 33 explicit
control contracts, and 13 Culture questions. `AUTHORING_ONTOLOGY_COVERAGE.md`
maps the active mechanics,
sources, consumers, category counts, duplicate-surface audit, and exclusions.

`CACulturalCognitionWorldComponent`, `CAPoliticalCognitionWorldComponent`, and
`CAPropositionKnowledgeWorldComponent` separately own psychology/attitudes/
influence, political attitudes/issue links/coalitions, and proposition/research
state. `CAOrganizationWorldComponent` owns legitimacy and sanction records.
Their bounded cadences and validators are catalogued in `MODULE_OWNERSHIP.md`
and `SCHEMA_REGISTRY.md`; no coordinator or UI surface is a second owner.

`CAModuleProfiler` is runtime-only, disabled by default, bounded to a fixed key
set, resettable, and excluded from save causality. The module and mutation graphs,
cadence inventory, indexes, and invalidation paths are governed in
`MODULE_OWNERSHIP.md`; schema and operator update procedures are governed in
`SCHEMA_REGISTRY.md` and `CAMPAIGN_COMPATIBILITY.md`.

## Non-goals / boundaries

- No copying from other mods (reference reading only); no shipping decompiled
  game source.
- No behavior that fights engine semantics (constant-tree, job churn guards,
  reservation system) — find the seam or build a native-shaped one.
