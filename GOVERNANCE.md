| Document | Colonist Awareness Overhaul Governance |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.7.2.0-alpha` · closed batch tip `B19` · next `B20` |
<!-- cao:generated:version END -->
| Design authority | ellyj3rain |
| Repository | `GOVERNANCE.md` |
| Status | ACTIVE - operating discipline and model-facing instruction surface. |

# Colonist Awareness Overhaul — governance

Single instruction surface for any model working in this project. `AGENTS.md` is
the autoload shim pointing here. Host identity is a vantage, not
authority; the operator (ellyj3rain) is the design authority and the judge of how
the game LOOKS and PLAYS — code-level verification never settles that question.

`README.md` is the human entry point. This project carries the GZDS canonical
doc-pack: `MEMORY.md`, `CORE.md`,
`ARCHITECTURE.md`, `GOVERNANCE.md`, `DECISION_REGISTRY.md`, `FINDINGS.md`,
`BATCH_LOG.md`, `VERSION_MAP.md`, `ROADMAP.md`, `SESSION_STATE.md`, `VERSION`. Batch records live
under `Batches/` in one alphanumeric sequence. `MEMORY.md` indexes every root
document and states which surfaces are canonical, regulatory, append-only, or
superseded.

## What this is

A RimWorld 1.6 mod building a genuinely orthogonal framework over the game — not
compatibility patching. Four pillars (see `ARCHITECTURE.md`): **Disposition**
(psychology), **Knowledge** (epistemics), **Authority** (roles/politics/ideology),
**Doctrine** (tactical execution). Doctrine ships first and rides the game's native
substrate; the other three are what make the mod a framework.

## Ground rules

- **Verified APIs only.** Ground truth is the decompiled game source at
  `../rimworld-decompiled/` (sibling folder, NEVER committed or copied here) and
  `Data/*/Defs/`. Compile against the real ref assembly as arbiter. Never assert
  engine behavior from memory.
- **The game's own nomenclature and seams.** Think-tree insertion hooks, DutyDefs,
  Lords, JobDrivers, stats, hediffs. Harmony only where no seam exists.
- **Operator terminology.** Military behavior uses the operator's terms, gaps
  filled from authoritative sources (Ranger Handbook). Never invent doctrine or
  attribute invented intent.
- **Declarations are promises.** Settings copy and in-game text must match shipped
  behavior exactly.
- **Concepts are not copy.** Operator discussion and model terminology direct the
  design; they enter the UI only when explicitly supplied or approved as copy.
  Player text is written separately in concise, concrete RimWorld language at
  the level of the choice, its direct effect, and the object it changes.
- **Author causes, inspect consequences.** Simulation complexity does not justify
  exposing every analytical category as a player control. Authoring surfaces own
  concrete persistent facts with real consumers; continuous, relational,
  historical, and derived outcomes remain in the simulation and are explained
  where the relevant decision or inspection occurs.
- **No universal explanation mode.** Contextual explanation belongs to the object
  or decision that owns it. Presentation policy may not replace causal copy with
  global Compact, Standard, or Expanded authoring states or repeated detail
  toggles.
- **Social systems keep their boundaries.** Culture is persistent longitudinal
  social history. Social subjects are interpretive referents, meanings are
  population-scoped evaluations, and practices are concrete repeated conduct;
  each production entry has factual evidence and a consumer. Ideoligion is
  RimWorld's native religious and moral substrate;
  Political Order is the population's normative composition; institutions and adopted rules
  are realized order; observed practice is what people actually do. Agreement
  and contradiction remain representable rather than being collapsed for UI
  convenience.
- **Generated identity follows authored state.** Political names, accounts,
  summaries, presets, and suggestions are generated from the complete saved
  variables. Optional display-name editing never substitutes prose for causal
  state and never becomes the primary authoring path.
- **Publishable-portable.** No operator-specific hardcoding; license-clean bespoke
  implementations (reference reading of other mods is fine; copying is not).
- **Machine-learning corpus boundary.** Raw saves and snapshots, normalized
  per-layout records, exact cohort membership, screening/evaluation rows,
  partitions, and caches remain local and ignored. Preserve public methodology,
  aggregate statistics, and non-reconstructive dataset accounting under
  `DATASET_GOVERNANCE.md`; preserve exact private lineage in the ignored local
  compliance manifest.
- **DLL changes need a full game restart; Defs XML hot-reloads in dev mode.**
- **Test-save continuity.** Never overwrite or delete the operator's real game, an
  original test baseline, or a deliberately preserved checkpoint. Work from a
  derived test save and save meaningful state changes without waiting for an
  explicit request; ordinary continuation should overwrite that working save. When
  an exact earlier state is worth revisiting, first create a separate, specific,
  non-generic checkpoint named for what it proves, then continue from the working
  save. Pure observation with no meaningful state change may close unsaved.

## Governance

- `CORE.md` — project identity and canonical component set. `MEMORY.md` — index
  of every root document with its canonical/superseded status. `SESSION_STATE.md`
  — current operational state and immediate next work; read it before claiming
  where anything stands.
- `ARCHITECTURE.md` — ratified shape. `GAME_ARCHITECTURE.md` — decompile-grounded
  study of the engine. `ROADMAP.md` — thread map + backlog.
- **Durable project records contain current ratified decisions, verified facts,
  active invariants, and unresolved junctions.** Mutable canonical specifications
  replace superseded interpretations in place. The governed batch and provenance
  system is the portable project history: closed record substance is append-only,
  while catalogs, crosswalks, threads, and version projections are regulatory.
- `DECISION_REGISTRY.md` and `FINDINGS.md` append substantive decisions, findings,
  corrections, and supersession records. Project convergence may normalize an
  obsolete tool identity, filename, or path in current regulatory metadata when
  the recorded decision or finding does not change. Substantive changes require a
  later correction or supersession record.
  `BATCH_LOG.md` is the chronological index to the append-only records in
  `Batches/`. `Batches/THREADS.md` is the permanent, series-neutral thematic
  classification. `VERSION_MAP.md` is the chronological version-unit projection.
  `Batches/FORMER_LABELS.md` and `Batches/THREAD_ID_CROSSWALK.md` resolve former
  batch and temporary thematic identifiers. These indexes and projections are
  regulatory; batch substance and its recorded provenance are historical
  evidence. Local Git commits are supporting engineering evidence when retained.
- Batch identifiers share one namespace across letter eras. The A sequence is
  closed at `A102`; development continues with `[B#]` in the same directory and
  log. `B19` is closed and `B20` is next after the operator runtime-test boundary. A new letter does not create another
  history tree, generator, catalog, or projection layer.
- The records now under `Batches/` establish this append-only contract. The
  immediately preceding generated history tree was a regulatory projection, not
  a second set of closed batch records. Local Git may retain that engineering
  state; the portable project history does not depend on it.
- A closed batch's date, name, substance, commits, receipts, corrections, and
  provenance are not rewritten. Current regulatory references in its metadata and
  navigation, including series-neutral thread identifiers and forge locators, may
  be corrected without changing
  that substance. Later implementation, correction, or verification receives the
  next batch identifier. Small commits remain split by layer (governance / source /
  defs / reference), and subjects open with the development-batch prefix.
- Pure project maintenance uses `[REPO]`. It may correct navigation, current
  documentation, filenames, or regulatory indexes without changing runtime
  behavior, adding a version unit, or consuming the next batch.
- Automated tools do not receive Co-Authored-By trailers in commits.
- **An authorized unattended pass is used in full.** When the operator
  delegates an extended implementation period, the absence of operator
  runtime acceptance never authorizes stopping after audit, hardening,
  instrumentation, or preparing tests for later. A useful finding, a clean
  build, a completed commit, a passing receipt suite, newly exposed runtime
  uncertainty, or a list of morning tests is not a stopping condition.
  Remaining work is classified DONE, ACTIONABLE NOW, or GENUINELY
  OPERATOR-BLOCKED; "needs runtime verification" is not operator-blocked,
  because such work usually still contains implementation, integration,
  persistence, representation, and test work that is actionable without
  operator judgment. Record the narrow junction where product intent is
  genuinely absent and continue every independent path. Risk reduction does
  not substitute for assignment completion, and large scope is the reason
  work was delegated rather than a reason to return it. Where session
  mechanics genuinely force a stop, state that limitation explicitly rather
  than presenting a handoff that implies the period was used.
- The operator owns: architectural ratification, public remotes, destructive ref
  work, version-policy changes.

## Project history and publication

| Surface | Authority and purpose |
|---|---|
| Current local project tree | Canonical implementation, content, documentation, and governed records. |
| Governed batch and provenance system | Portable project history. `Batches/`, `BATCH_LOG.md`, thematic indexes, the version map, decisions, findings, receipts, and source provenance travel with the project. |
| Local Git history | Optional engineering continuity and supporting evidence. It may retain work that predates the published snapshot, but the portable project history does not require that commit graph. |
| Published forge history | Distribution history for the current hosted snapshot and later publications. It begins at the canonical snapshot selected for publication; a forge or remote is not the project identity. |

GitHub is the current forge. A later GitHub reset, GitLab mirror, or replacement
host changes publication history, not the governed project history. Commit hashes
recorded in batch files remain local engineering provenance even when a forge does
not expose those objects.

## Continuous integration and publication gates

The repository-owned CI contract lives in `tools/ci/`. GitHub Actions and GitLab
CI call those same scripts rather than maintaining separate build definitions.
Every pull request to `main` must pass the required `ci-verify` and
`dependency-scan` checks. `ci-verify` replays the governed version model, rejects
machine-local project references, restores the locked production dependency graph,
produces two byte-identical clean builds, proves the tracked shipping assembly is
current, and compiles every support project declared in `tools/ci/projects.txt`.
Historical fixture-conversion source that is retained only as provenance stays
outside that current executable manifest. The portable synthetic-state and
persistence censuses must also regenerate without changing their governed
projections. `dependency-scan` rejects
high or critical direct or transitive NuGet findings. CodeQL supplies an additional
security signal without replacing either deterministic required check.

`main` accepts changes through a pull request with required checks, signed commits,
linear history, conversation resolution, and force-push and deletion protection.
CI may publish verification artifacts. It does not deploy into RimWorld, launch or
operate the game, edit saves, or establish operator visual or gameplay acceptance.

## Batch, thread, and version discipline

- **Batch** is the atomic chronological development record. The closed history is
  `A1` through `A102`, followed by `B1` through `B19`; `B20` is the next identifier.
- **Thread and family** are permanent, series-neutral semantic classifications.
  `T-*` and `TF-*` may connect nonadjacent batches across any letter sequence, and
  one batch may participate in several threads.
- **Version unit** is a chronological capability unit containing one or more
  contiguous batches. Version units partition the batch chronology without gaps,
  overlap, or reordering. Each unit carries one tier and one resulting root
  version. Related work that returns after another version unit remains later.

The root hierarchy follows the Neo odometer mechanism:
`major > minor > kohai > patch > hotfix`, rendered as
`major.minor.kohai.patch-maturity`. Hard caps are `MINOR=12`, `KOHAI=16`, and
`PATCH=24`; overflow rolls mechanically to the next coordinate. `hotfix` uses
patch arithmetic. `minor` establishes a new player-visible simulation capability
or authoring/runtime contract. `kohai` extends, integrates, or structurally
matures an existing capability. `patch` repairs or verifies in place. `major`
marks a formal release, project-identity, or supported-compatibility boundary.
Maturity advances independently through `pre-alpha`, `alpha`, `beta`, `rc`, and
GA.

`tools/version-model.mjs` is the executable replay source. It derives `VERSION`,
the generated version stamps, and `VERSION_MAP.md`; validates exact A1-B19
coverage; checks the thematic namespace; and fails if the declared next batch has
already been consumed. Mechanically derived version facts are generated and
gated, not hand-typed. Forty-three evidenced version units derive
`1.7.0.0-alpha`.

When new development begins, the first batch opens the next contiguous version
unit. Immediately following batches may share that version only when they form one
continuous capability unit and the grouping reason is recorded. The unit's full
content determines its tier. A later return to the same thread opens a later
version unit; thread continuity never moves a batch backward or merges
noncontiguous time.

## B10 Causal Closure Contract

Aggregate runtime validation follows causal closure. A deterministic label,
hash, score, random perturbation, broad tendency, asset, read model, Political
Belief, or Culture value cannot occupy the place of a social fact. Hashes and
randomness select only among causally equivalent names, layouts, visual forms,
materials, or bounded schedules after eligibility exists.

Domestic units have persistent factual membership and represented transitions.
Practiced capability derives from domain-specific actors, organizations, work,
knowledge, material, and history. World tendencies summarize or guide; they do
not instantiate institutions. Every active settlement program has a concrete
need and complete operational contract. Culture affects meaning, legitimacy,
participation, priority, and form without granting execution capability.
Political Order remains distinct from represented institutions. Provision operators,
funding, stock, access, and behavior are actual facts. Assets are nodes of an
institution, never the institution itself.

Fixing one proxy does not close a repository-wide causal audit. B10 closed only
after the repeated sweep and independent reviews contain no unresolved Critical
or High synthetic-state finding, the corrected fixture reaches the generation
boundary, and the deployed assembly was byte verified.

## B11 Durable Campaign and Authoring Contract

Every persisted state family has one schema key, version, semantic owner, and
authorized mutation path. Current campaign state is preflighted before Scribe
load, supported migration is idempotent, and upgrade-time initialization records
truthful provenance. Pending authoring data has a separate replaceable epoch and
cannot erase realized campaign history. Unsupported live state fails visibly.

Every runtime cadence has one owner. Derived indexes name their authority,
invalidation, rebuild, stale-entry, and save policy. Coordinators do not become
secondary state owners; adapters do not invent state. Profiling is disabled by
default, bounded, runtime-only, and semantically inert. Future implementation
batches record module, campaign-compatibility, and performance impact.

Representational capacity, production vocabulary, runtime realization, and
control surface are separate completion layers. Extensibility is not breadth,
and demonstration examples do not become production ontology. Every production
social subject and concrete practice requires distinct mechanics, a factual
source, and a consumer. Internal source taxonomy is not automatically player
navigation; empty and one-item categories are omitted, and top-level categories
appear only when multiple meaningful groups contain enough content to aid
discovery.

Political Order, represented institutions, authority, economy,
self-identification, and observed practice remain distinct. Political Order is
a complete causal composition: each blendable question totals 100 and each
exclusive question selects one position. Presets and generated orders fill that
same state and remain editable. Several compatible positions may coexist only
inside an intelligible owning question; exclusivity requires a recorded engine
or model invariant. Generated names and accounts describe the saved variables
and never replace them. Shared layout primitives do not justify duplicate
semantic records or duplicate authoring surfaces.

B11 closed after retained B10 regressions, the complete mechanics coverage
matrix, five-lens review with no unresolved Critical or High finding, a clean
build, normal publication, and byte-identical closed-process deployment. Static
closure did not establish gameplay quality.

## B12 Cultural Cognition Contract

Culture owns population distributions over explicit questions. Social subjects
remain factual referents, and practices remain concrete repeated conduct.
Schema-10 migration creates a distribution only for an exact ordered construct
and preserves every former dimension and unmatched subject as evidence. A
convenient substitute is not a compatible migration.

Persistent psychology, dynamic condition, Culture, native Ideoligion, Political
Order, represented institutions, proposition knowledge, actions, and
practices are distinct causes. Cultural cognition, political cognition, and
proposition knowledge have separate durable world owners. Organizations own
legitimacy and sanction history. Each owner has one cadence, validator, profiler
key, migration path, and downstream contract.

Culture changes appraisal and the selection of discretionary CA-originated
action. It never revokes native legality, capability, material conditions,
authorization, or direct, relayed, or save-restored operator intent. Direct
observation is knowledge regardless of access norms. Novelty affects attention
and transmission, not truth, discovery, or native research completion.

B12 closes only after 113/113 causal receipts, current-schema fixture agreement,
retained B10/B11 regressions, causal, psychometric/statistical, structural,
correctness, and surface review, a clean build, normal publication, and
byte-identical closed-process deployment. Static evidence does not establish
psychometric validity, empirical calibration, or gameplay quality.

## B13 Culture Boundary

B13 retained B12's owners and closed its bounded Culture registry as twenty-four explicit
questions in eight categories. Every question has five ordered anchors,
represented evidence, research provenance, pawn appraisal, a substantive
consumer, and historical feedback. B16 supersedes that registry as the current
playable-mechanics coverage boundary while retaining B13 as historical evidence.
Manual editing, complete historical/social
presets, saved profiles, deterministic randomization, and generated completion
all write the same Culture object. One global diversity control owns default
population spread; per-question spread is advanced and local. Practices remain
observed historical conduct.

Salience and conviction, norm pressure and expected enforcement, inherited
source confidence and current knowledge confidence, visibility and expression,
and psychology and Culture identity remain separate causes. Campaign catalog 3
persists the separated cognition fields under schema 2 and requires Culture
question registry 2. Missing-event inference, identity-derived authoring, and
silent player rerolls are invalid.

B13 closed after 67/67 B13 receipts, retained 75/78/113 B10-B12 suites,
current-fixture agreement, independent causal/structural/surface review, a clean
build, and byte-identical closed-process deployment. Static evidence does not
  establish empirical calibration or gameplay quality.

## B14 Regional World and Society Creation Contract

B14 makes regional selection, preview, confirmation, and generation consume one
persisted geography composition. Starting Region's Region, Map, and Details
views reference one selected object graph; settlement and arrival placement use
actual region members. Political Order is one complete normative composition,
while represented institutions remain distinct realized facts.

Society is a joint control surface over faction-owned components rather than a
world owner. A Society preset applies independent snapshots atomically and
leaves no preset identity in realized state. Component editors remain available
for direct substitution. Regional environment, settlement programs, frontier
holdings, and autonomous construction consume represented facts without
rerolling confirmed authoring.

## B15 Technological Knowledge Contract

The faction owns Culture, Political Order, and Technological Knowledge as three
sibling canonical components. Society composes them; a Society preset snapshots
them. Founding and regional plans may stage all three before realization, then
copy them into the faction. Settlements reference faction state unless an
explicit local divergence is modeled.

Technological Knowledge records nine domains with separate understand,
construct, operate, and maintain ranks. One exact translation boundary maps
native research, buildables, manufactured items, recipes, plants, habitat requirements, and
compatibility checks. `FactionDef.techLevel` may seed an otherwise unauthored
faction and provide native metadata, but it is not effective runtime authority.
Native research completion remains factual.

Standard mode reads faction knowledge directly. Experimental Distributed
Knowledge changes only availability through accessible pawn, institution, and
record custody at the requesting map or settlement. Initial faction carriers
and later usable humanlike members receive idempotent custody projections;
teaching is not yet implemented. It cannot create a second owner or regenerate
lost custody during a query. B15 closes only after supported
B14-to-B15 migration, three-component atomic rollback, exact mapping,
standard/distributed behavior, current-fixture preservation, retained
regressions, independent review, reproducible build, and byte-identical
closed-process deployment.

## B16 Playable Social Ontology Contract

Ideoligion owns explicit doctrine, sacred or prohibited conduct, ritual, memes,
precepts, roles, and religious or social prescriptions. Culture owns
population-level social appraisal, distribution, disagreement, and historical
drift. Political Order owns beliefs about legitimate political and economic
arrangements. Institutions own rules and mechanisms actually in force. Practice
owns represented repeated conduct. Agreement or conflict between these owners
is meaningful state; no layer silently replaces another.

Loaded native mechanics are audited by semantic owner before CA interprets
them. `CAIdeoligionSemanticAdapterRegistry` maps only an exact package ID,
definition kind, and `defName` to bounded Culture evidence. Unknown modded
definitions remain native Ideoligion facts. `CANativeCultureEventAdapterRegistry`
uses the same exact identity rule for native events after RimWorld executes
them. Occurrences retain actor and source provenance; only repeated occurrences
can establish practice, and practice never establishes approval by itself.

Culture question registry 3 contains forty-eight questions in twelve categories
as the present result of the reverse playable-mechanics audit, not as a quota.
Catalog 5 upgrades compatible registry-2 Culture payloads additively: all former
rows remain byte-for-byte equivalent in meaning and the twenty-four new rows
enter each represented population scope as neutral, low-confidence, explicitly
unobserved state. Faction, founding, regional, and map-longitudinal owners
validate their complete nested state before publishing their new versions.

## B17 Site Affiliation and Pawn Epistemics Contract

Every persistent inhabited site records faction ownership and material support
as independent typed facts. `None` is a complete ownership value and never a
sentinel, failed lookup, fake faction, or hidden placeholder. Resident
affiliation remains population state. Support, local authority, Culture,
Ideoligion, Political Order, Technological Knowledge, institutions,
organizations, offices, programs, provisions, and practice never imply site
ownership.

Major settlements and existing frontier settlements, holdings, cabins, and
homesteads retain their distinct mechanics while using that shared relationship
contract. A factionless site owns complete local political, technological, and
institutional state and remains fully simulatable. Detachment snapshots
effective local state once. Support cannot become ownership, and scale changes
cannot create a faction without an explicit causal event.

The existing proposition store owns the optional Broader Pawn Knowledge mode.
It defaults off and does not replace the pawn-private tactical or welfare facts
already required for causal behavior. When enabled, typed pawn records preserve
source, immediate reporter, source-event and acquisition time, confidence,
uncertainty, contradiction, revision, supersession, scope, retention, and
provenance where meaningful. Communication copies the teller's remembered
record; it never grants the receiver current world truth. Player-view masking
remains a separate presentation concern.

Catalog 6 and pending-authoring epoch 15 bind the B17 ownership and knowledge
schemas. The adjacent conversion validates the complete proposed graph before
publishing any field or schema stamp. `B17_SITE_AFFILIATION_AND_EPISTEMIC_CONTRACT.md`
is the canonical detailed contract.
