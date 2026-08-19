# Colonist Awareness Overhaul

Colonist Awareness is a RimWorld 1.6 framework and simulation overhaul. Pawns
act on what they know. Settlements persist as real places with people, supplies,
institutions, beliefs, and authority. Factions make decisions and carry their
consequences forward instead of resetting to isolated game events.

<!-- cao:generated:version BEGIN -->
Current version: `1.7.0.0-alpha`. Implementation is complete through batch `B17`; `B18` is the next development batch. SESSION_STATE records the verified assembly and live deployment state. Static verification does not substitute for how the game looks and plays.
<!-- cao:generated:version END -->

## Framework

Every CA behavior composes four independent concerns:

`Knowledge triggers -> Disposition ranks -> Authority channels -> Doctrine executes`

| Concern | Owns |
|---|---|
| Disposition | Individual judgment, willingness, and risk texture. |
| Knowledge | Actor-held facts, observation, provenance, communication, memory, and uncertainty. |
| Authority | Permission, command, responsibility, organizations, institutions, and political order. |
| Doctrine | Native RimWorld jobs, duties, Lords, movement, combat, care, and material execution. |

Standard retains ordinary RimWorld agency, enabled safeguards, direct and
accepted relayed orders, and continuation of owned work. Proactive permits one
bounded response to a current fact. Autonomous permits persistent or coordinated
action inside delegated or institutional authority. Initiative never creates
knowledge, permission, office, materials, or capability, and direct player orders
remain authoritative at every tier.

## World and founding authoring

Starting Region authors direct world facts: the selected region and arrival
area, factions, settlements, population composition, ownership and relations,
and explicit exceptional starting conditions. Region lists the factions and
settlements already present, Map owns spatial selection and area assignment,
and Details edits the selected region, faction, or settlement. One valid option
is shown as a fact only when it matters, not as a button, and generation
provenance remains diagnostic rather than player copy.

Settlement scale, access, services, civic capacity, and material composition are
realized from actual population, land, geography, knowledge, institutions,
economy, trade, work, and history. An open registry defines 22 possible program
contracts across housing, food, storage, medicine, production, trade,
governance, security, defense, research, religion, social life, culture,
agriculture, communications, and transport. A program becomes active only when
its concrete need, operator, standing, knowledge, labor, materials, target,
access, funding or maintenance, behavior, and failure contract are represented.
Unsupported programs stay absent. Buildings and rooms are nodes used by a
program; they do not prove an institution exists.

Domestic provision belongs to a real pawn or a persistent domestic unit formed
from represented partner, kin, residence, or explicit co-residence facts. Pawn
ordering and hashes never create a household. Communal and authority provisions
require real organizations, workers, stock, access, funding, and distribution;
tax-funded provision also requires an adopted tax policy and collection.
Practiced capability is a domain evidence summary over actual actors,
organizations, work, knowledge, materials, and history, without random jitter.
World tendencies guide or summarize their owning facts and do not create the
institutions they describe.

Biome and terrain are also causal. The selected ground derives explicit
requirements for shelter, temperature control, food, reserves, medicine, safe
water, light, environmental protection, and breathable space. Advanced
populations may consider hostile ground, but established settlements must
satisfy its requirements through their actual programs and faction capability.
Frontier residents appear only after the smaller site has materialized its
supported habitat; unsupported frontier hazard and vacuum sites fail closed.
Autonomous construction ranks otherwise valid candidates by composable
functional, circulation, expansion, environmental, defensive, material, and
visual-order evidence drawn from `PLAYER_BASE_PATTERN_CORPUS.md`, without a
style toggle or copied base plan. One extractor now normalizes full `.rws` saves
and structured Real Ruins `.bp` snapshots. The operator's early/combat-precarity
evidence remains distinct from broad anonymous layout screening and from the
complementary mature mid/end-game evidence it does not supply. The current broad
screen contains 4,238 complete byte-unique layouts across 80 biome definitions,
with an exact local cohort manifest and lineage-safe experimental partitions;
the repository publishes only aggregate evidence and dataset governance, while
source-truncated fragments remain quarantined from complete-base evidence.

Culture is persistent social history with substantive inherited state. Its
current admitted authoring registry is a population distribution over
forty-eight explicit questions in twelve categories: relationships, family, and
sexuality; gender and social authority; status and hierarchy; membership and
outsiders; public authority and social order; property, labor, and provision;
violence, captivity, and punishment; knowledge and tradition; body, health, and
death; food and substances; animals and environment; and daily life and
technology. Each question has five ordered anchors, a center,
population spread, norm and evidence settings, provenance, historical feedback,
and material consumers. The registry is the current result of a reverse audit
from playable Core, DLC, CA, and explicitly supported-mod mechanics; its size is
not a permanent quota. Concrete social subjects remain factual referents;
practices remain repeated conduct with actors, conditions, authority, materials,
evidence, and consumers. Neither is silently promoted into a Culture question.

The Culture composer groups all twelve categories, exposes one Overall
disagreement control, and keeps per-value disagreement under More. Twenty-two
complete historical and social presets across the Americas, Europe, and Asia,
manual editing, saved profiles, and
deterministic randomization all write the same Culture object; there is no
preset mode or blend state. It also exposes constituents, inherited and current
concrete practices, historical evidence, continuity, and the optional native
visual style. Exact values round-trip without loss. At runtime a pawn
receives a deterministic private position, public expression, attention,
inherited-prior strength, perceived social pressure, observation likelihood,
knowledge confidence, moral conviction, and uncertainty. Question-specific
psychology contributes a small bounded private deviation. Sparse represented
influence may change those attitudes over time. Direct historical observations
persist measured position, spread, represented population, continuity, and
source identity before stable pawn appraisal; expertise deference uses
demonstrated skill in the factual subject's explicit domain. One event,
unchanged evidence, or opening an editor cannot manufacture a Culture change.

Persistent psychology, Culture, native Ideoligion, Political Order, represented
institutions, proposition knowledge, actions, and practices remain
separate causes and owners. Culture contributes appraisal and the selection of
otherwise valid discretionary CA action. It does not override direct player
intent or create knowledge, permission, authority, resources, technology,
office, land, relationships, institutions, or completed research.
Regional pawn appraisal retains the exact settlement institution resolved from
the pawn's represented population assignment; local social history and
institutional jurisdiction are not reconstructed from faction membership.

Each authored faction also owns one Technological Knowledge composition beside
its Culture and Political Order. Nine practical domains separately record what
the faction can understand, construct, operate, and maintain. Native research,
buildings, recipes, crops, habitat requirements, and autonomous development use
one explicit translation into those domains. `FactionDef.techLevel` may seed a
new unauthored faction once and remains a pre-world compatibility fallback; it
does not remain the authority after faction state exists. Environmental needs,
available knowledge, labor, materials, and realized facilities remain separate
causes. Current execution consumers and unused forward-capacity cells are
enumerated separately in the B15 consumer matrix.

The same concepts have different temporal meanings on the two creation sides:

| Existing society | Player founding |
|---|---|
| Culture, Ideoligion, Political Order, represented institutions, material state, and established historical basis are facts about a society already present. | Founders bring inherited Culture, native Ideoligion, and Political Order, then choose the rules instituted at landing. Local institutions and historical Culture develop through play. |

Society is the shared control surface above the separate component composers.
It edits the faction's Culture, Political Order, and Technological Knowledge
together without becoming another state owner. Each independently named Society
preset carries a complete reusable snapshot of those three components. Applying
one validates all three copies, commits them atomically into the faction's
ordinary state, and retains no preset key or Society ownership. Each component
remains independently editable, and its own preset browser is an optional
component-replacement action rather than a parallel Society workflow. The same
surface saves reusable three-component user presets. Applying one to an
established faction does not rewrite represented institutions; applying one to
the founders does not choose Ideoligion or rules at landing. Starting Region may
use the same preset while creating an ordinary local faction and settlement;
the page separately records its scenario population and map-area assignment.
No settlement type, catalog ownership, or preset mode is retained.

Standard play reads the effective social owner's Technological Knowledge
directly: the faction for a non-divergent owned site, or the site's canonical
local state when factionless or explicitly divergent. Experimental Distributed
Knowledge changes only where that same knowledge is available:
living pawns, institutions, and records carry domain competencies, redundancy
protects them, and the loss or incapacity of an isolated carrier can remove a
practical capability. Recruitment, departure, research, and represented custody
changes operate on the same ontology; teaching is not yet a shipped transfer
mechanism. The mode does not create a second technology system.

Every persistent inhabited site separately records faction ownership and
material support. Major settlements and existing frontier settlements,
holdings, cabins, and homesteads share this relationship contract without losing
their distinct mechanics. `No faction` is a complete ownership value. Support,
resident affiliation, Culture, Ideoligion, Political Order, Technological
Knowledge, institutions, organizations, offices, local authority, programs,
provisions, and practice remain separate facts. Factionless sites therefore
retain complete local social state and can develop without a faction appearing
at a scale transition.

The default-off Broader Pawn Knowledge mode extends the existing proposition
store to represented people, sites, affiliation, social state, events,
technology, geography, and routes. Each pawn keeps a separate typed record with
source age, confidence, uncertainty, reporter, contradiction, revision,
supersession, scope, retention, and provenance where meaningful. A report copies
what the teller remembers; it never gives the receiver refreshed world truth.
The pawn inspector describes the pawn's belief and evidence. Existing required
tactical and welfare knowledge remains unchanged when this broader mode is off.

Political Order is a complete normative composition over twenty-six concrete
questions covering authority, civic life, ownership by economic domain,
exchange, work, provision, and security. Blendable questions distribute 100
points among compatible positions; exclusive questions select one answer. Eight
complete presets and deterministic generation fill that same editable state.
The configured variables generate the order's name, short summary, and political
account; an optional custom display name never substitutes prose for the causal
model. Existing institutions are shown as separate represented facts instead of
a duplicate editable axis grid. During play, pawn political attitudes remain
a separate realized state formed from Culture, psychology, Ideoligion, material
interest, institutional experience, threat, held beliefs, proposition knowledge,
and represented issue networks. Faction-bounded coalitions can emerge from that
state without rewriting Political Order or represented institutions.

RimWorld's native Ideoligion chooser remains first-class inside this coordinated
flow. Native presets, saved Ideoligions, fixed and fluid creation, memes,
precepts, roles, rituals, validation, and persistence remain native. CA preserves
the neighboring Culture, Political Order, and founding-order draft when the
player enters and returns from that chooser. Political Order states what a
population considers proper; adopted rules and observed practice state what is
actually in force. Agreement and contradiction both remain meaningful state.

## Requirements

- RimWorld `1.6`
- Harmony
- Biotech
- Ideology

The stable package identifier is `ellyj3rain.colonistawareness`. Dependencies and
load order are declared in [`About/About.xml`](About/About.xml).

## Build

From the project root:

```powershell
dotnet build Source/ColonistAwareness.csproj -c Release
```

The Release assembly is written to `Assemblies/ColonistAwareness.dll`. DLL
changes require a full RimWorld restart.

## Project map

| Path | Purpose |
|---|---|
| `Source/` | C# implementation |
| `Defs/`, `Patches/` | RimWorld definitions and integration patches |
| `Languages/English/` | Player-facing English text |
| `About/` | Mod metadata and dependency declaration |
| `ARCHITECTURE.md` | Current framework architecture and ownership |
| `GOVERNANCE.md` | Project operating rules |
| `SESSION_STATE.md` | Current build, deployment, fixture, and runtime-test boundary |
| `Batches/` | Append-only batch records in one alphanumeric namespace |
| `BATCH_LOG.md` | Chronological batch catalog |
| `Batches/THREADS.md` | Series-neutral thematic classification |
| `VERSION_MAP.md` | Chronological, tier-bearing version units |
| `tools/version-model.mjs` | Version replay, generated stamps, and structural checks |
| `B10_CAUSAL_PROVENANCE_AUDIT.md` | Current causal owners, corrected proxies, program contracts, and institution-asset classifications |
| `MODULE_OWNERSHIP.md` | Authoritative module, mutation, cadence, cache, and diagnostics ownership |
| `SCHEMA_REGISTRY.md`, `PERSISTENCE_CENSUS.md`, `CAMPAIGN_COMPATIBILITY.md` | Durable campaign schema, independent source-writer census, preflight, migration, update, and rollback contracts |
| `AUTHORING_ONTOLOGY_COVERAGE.md` | Production social/political vocabulary, mechanics coverage, control contracts, categories, and exclusions |
| `B16_PLAYABLE_SOCIAL_ONTOLOGY_AUDIT.md`, `AUTHORING_ONTOLOGY_COVERAGE.md` | Current forty-eight-question Culture registry, exact Ideoligion and native-event semantic adapters, ownership audit, mechanics coverage, consumers, and fail-closed mod boundary |
| `B17_SITE_AFFILIATION_AND_EPISTEMIC_CONTRACT.md` | Typed ownership/support across inhabited sites, complete factionless social state, scale-transition rules, and the optional broader pawn-knowledge contract |
| `B13_CAUSAL_CONTRACT.md` | Historical twenty-four-question B13 Culture boundary retained beneath B16 |
| `CULTURE_RESEARCH_CORPUS.md` | Current Culture research and playable-mechanics corpus: B13 source basis plus B16 reverse ownership audit, causal separations, consumers, feedback, and calibration limits |
| `REGIONAL_GEOGRAPHY_CONTRACT.md` | Shared B14 preview and generation identity, supported geography combinations, and fail-closed realization rules |
| `B15_TECHNOLOGICAL_KNOWLEDGE_CONTRACT.md` | Faction-owned technological domains, Society composition, native translation, and standard/distributed availability |
| `DATASET_GOVERNANCE.md` | Public corpus strata, provenance, acquisition, processing, privacy, IP, cohort, and use-accounting boundary; underlying examples remain local |
| `PLAYER_BASE_PATTERN_CORPUS.md`, `Corpus/PlayerBaseLayouts/`, `tools/PlayerBaseLayoutExtractor/` | Layout methodology, evidence limits, aggregate profile, extraction schema, and reusable construction relationships; normalized spatial records and exact cohort membership remain local and ignored |
| `CULTURAL_COGNITION_RESEARCH.md`, `B12_CULTURE_QUESTION_AUDIT.md` | B12 research and migration evidence retained beneath the B13 completion |
| `B13_ACCEPTANCE_RECEIPTS.md`, `B13_REVIEW_RECEIPT.md`, `Receipts/B14/`, `Receipts/B15/`, `Receipts/B16/`, `Receipts/B17/` | Executable closure, retained-regression, review, fixture, build, and deployment evidence |

## Project history

The governed batch and provenance records are the portable project history.
They travel with the current tree and do not depend on a particular Git host.
`A1` through `A102` and the continuing `B` batches share one chronological
namespace under [`Batches/`](Batches/). [`BATCH_LOG.md`](BATCH_LOG.md) is the
chronological view, [`Batches/THREADS.md`](Batches/THREADS.md) connects related
work across time, and [`VERSION_MAP.md`](VERSION_MAP.md) partitions contiguous
development into capability-coherent version units under the four-coordinate
odometer.

Run the governance check with:

```powershell
node tools/version-model.mjs --check
```

Local Git may retain engineering history. A published forge is a distribution
surface, not the project identity or the authority for its portable history.

## Verification boundary

Executable receipts cover authoring ownership, creation navigation, founding
state, World tendencies, behavior authorization, Culture distributions,
psychology, private/public attitudes, sparse influence, political formation,
institutional legitimacy and sanctions, proposition knowledge, Culture history,
domestic identity, capability evidence, operational programs, provision
operators, fixture/runtime equivalence, and derived-state stability.
Static receipts, builds, hashes, and reviews establish that a candidate is ready
for the operator; they do not establish how the game looks or plays. See
[`SESSION_STATE.md`](SESSION_STATE.md) for the exact current assembly, deployment,
fixture, and remaining RimWorld observations.

## License and provenance

Colonist Awareness is licensed under GPL-3.0. See [`LICENSE`](LICENSE),
[`CREDITS.md`](CREDITS.md), and [`UPSTREAM_SOURCES.md`](UPSTREAM_SOURCES.md) for
attribution and incorporated-source provenance.
