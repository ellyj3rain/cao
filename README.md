# Colonist Awareness Overhaul

Colonist Awareness is a RimWorld 1.6 framework and simulation overhaul. Pawns
act on what they know. Settlements persist as real places with people, supplies,
institutions, beliefs, and authority. Factions make decisions and carry their
consequences forward instead of resetting to isolated game events.

<!-- cao:generated:version BEGIN -->
Current version: `1.5.0.0-alpha`. Implementation is complete through batch `B14`; `B15` is the next development batch. SESSION_STATE records the verified assembly and live deployment state. Static verification does not substitute for how the game looks and plays.
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
with a portable exact cohort manifest and lineage-safe experimental partitions;
source-truncated fragments remain quarantined from complete-base evidence.

Culture is persistent social history with substantive inherited state. Its
current authoring authority is a population distribution over twenty-four
explicit questions in eight categories: relationships and family, gender and
social authority, status and hierarchy, membership and outsiders, public
authority and social order, property and provision, violence and punishment,
and knowledge and tradition. Each question has five ordered anchors, a center,
population spread, norm and evidence settings, provenance, historical feedback,
and material consumers. Concrete social subjects remain factual referents;
practices remain repeated conduct with actors, conditions, authority, materials,
evidence, and consumers. Neither is silently promoted into a Culture question.

The Culture composer groups all eight categories, exposes one Overall
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

The same concepts have different temporal meanings on the two creation sides:

| Existing society | Player founding |
|---|---|
| Culture, Ideoligion, Political Order, represented institutions, material state, and established historical basis are facts about a society already present. | Founders bring inherited Culture, native Ideoligion, and Political Order, then choose the rules instituted at landing. Local institutions and historical Culture develop through play. |

Society presets are the shared starting surface above the separate composers.
Each independently named Society recipe carries one Culture reference and one
frozen complete Political Order composition. Applying it validates and copies both
components together into the faction's ordinary Culture and Political Order;
the preset key is not retained. The resulting components remain independently
editable, and their own preset browsers remain optional component-replacement
actions rather than parallel Society workflows. The existing Society section
also saves reusable two-component user recipes. Applying one to an established
faction does not rewrite its represented institutions; applying one to the
founders does not choose their Ideoligion or rules at landing. The same catalog
can place a new local faction and settlement in Starting Region: the page
records an explicit scenario population and arms its existing map-area
assignment, while the recipe still writes only Culture and Political Order.
No settlement type, catalog ownership, or preset mode is retained.

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
| `B13_CAUSAL_CONTRACT.md`, `CULTURE_RESEARCH_CORPUS.md` | Current twenty-four-question Culture ownership, source basis, causal separations, consumers, feedback, and calibration limits |
| `PLAYER_BASE_PATTERN_CORPUS.md`, `Corpus/PlayerBaseLayouts/`, `tools/PlayerBaseLayoutExtractor/` | Full-save and structured-snapshot layouts, exact provenance and evidence limits, normalized spatial data, and reusable construction relationships; no copied plan or style mode |
| `CULTURAL_COGNITION_RESEARCH.md`, `B12_CULTURE_QUESTION_AUDIT.md` | B12 research and migration evidence retained beneath the B13 completion |
| `B13_ACCEPTANCE_RECEIPTS.md`, `B13_REVIEW_RECEIPT.md` | Executable B13 closure and independent causal, structural, and surface review evidence |

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
