| Document | Colonist Awareness Overhaul Core |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.7.0.0-alpha` · closed batch tip `B17` · next `B18` |
<!-- cao:generated:version END -->
| Author | ellyj3rain |
| Repository | `CORE.md` |
| Status | ACTIVE - genesis identity for this project. |

# Colonist Awareness Overhaul — core

**Colonist Awareness Overhaul: A Living World Framework** is a RimWorld 1.6
framework and simulation overhaul, not a set of compatibility patches. Colonists
act on what they and their trusted contacts know rather than on global omniscience
or oblivion. Settlements around the player are real places with their own
organizations, technology, beliefs and material means, and they persist whether or
not anyone is looking at them.

The design objective is to raise pawn competence so the player operates at the
level of intent instead of constant micromanagement. The autonomy control governs
self-initiation; explicit player orders persist at every tier. Skill changes
latency, breadth, precision, coordination and execution quality — low skill never
licenses cartoonish exposure, silent failure, an infinite watch, a known
friendly-fire crossing, or abandonment of an actionable teammate.

## Identity

| Field | Value |
|---|---|
| Display name | Colonist Awareness Overhaul: A Living World Framework (DR-88) |
| Project key | `cao` |
| `packageId` | `ellyj3rain.colonistawareness` — **stable**; changing it breaks existing saves and Workshop identity |
| Licence | GPL-3.0 (`LICENSE`), because the mod adapts GPL-covered work |
| Target | RimWorld 1.6 |
| Declared dependencies | Harmony, Biotech, Ideology (`About/About.xml`) |
| Author | ellyj3rain |

The local project tree is canonical. Its governed batch and provenance records
are the portable project history. Git remotes and forge histories publish that
state; they do not define the project's identity or historical continuity.

## Canonical composition

Every behavior is the composition:

`Knowledge triggers → Disposition decides → Authority channels → Doctrine executes`

The four pillars are separate substrates, each with its own model, projecting into
the game's native machinery. `ARCHITECTURE.md` holds the ratified shape.

| Pillar | Owns | Does not own |
|---|---|---|
| Disposition | courage, discipline, aggression, initiative, empathy, conformity, skepticism; willingness and risk texture | facts, permission, physical execution |
| Knowledge | pawn-private typed facts, provenance, observation, communication, memory, uncertainty, battlefield perception | omniscient map truth, automatic permission |
| Authority | command, responsibility, roles, command standing, organization public support, politics, ideology, authorization | tactical execution, invented knowledge |
| Doctrine | Lords, Duties, jobs, tactical movement, fire, holds, ambush, withdrawal, triage, custody | global truth, personality, organization public support |

Doctrine ships first and rides the game's native substrate; the other three are
what make the mod a framework rather than a feature set.

Every CA-originated behavior also crosses one typed authorization contract.
Its stable definition names the behavior domain, form, applicable actor,
permission, minimum initiative, required evidence, authority, native execution
lane, intent owner, completion, and stand-down condition. The dynamic gate keeps
permission, initiative, authority, actor-held knowledge, capability, material
conditions, and player ownership separate. The same catalog supplies runtime
authorization, settings text, tracing, diagnostics, and executable receipts.

The active initiative ladder is Standard, Proactive, and Autonomous. Standard
retains ordinary RimWorld agency, enabled safeguards, observation, direct and
accepted relayed orders, and continuation of owned work. Proactive may originate
one finite response to a current fact. Autonomous may retain, compare, or
coordinate persistent work inside delegated authority. Initiative never creates
knowledge, office, materials, access, or permission, and explicit player intent
remains authoritative at every tier.

## The regional program

The regional work is not a detached map-size mod. It is the physical theater for
the same framework. Regions contain factions, settlements, population groups,
persistent local Culture, Ideoligion, Political Order, represented institutions, starting
conditions, relationships, operations, economies, diplomacy, and conflict.
Loaded maps are detailed materializations and native physical executors. The
projection rule:

`pawn evidence → organization record → authorization → regional operation or
contract → native world/incident/quest/map/Lord projection → reconciled outcome`

Native RimWorld physical execution remains authoritative while materialized; this
mod owns the persistent organizational reason and continuity.

Culture is persistent longitudinal social-historical state with a substantive
inherited baseline. Its current normative authority is a population distribution
over forty-eight admitted Culture questions in twelve categories, not a broad
meaning row or factual social subject. A distribution owns its center, spread,
descriptive norm, prestige, salience, norm pressure, visibility, inherited-prior
confidence, divergence tolerance, subgroup mixture, provenance, and evidence
signature. Social subjects remain factual
referents and practices remain concrete repeated conduct with evidence and
consumers. Exact B11 meanings migrate only where an ordered question exists;
only weighted approval and salience acquire current question semantics, and
every former dimension otherwise remains explicit legacy evidence.
The admitted registry is the current result of a reverse playable-mechanics
audit and is not a permanent quota. Native Ideoligion doctrine, represented
institutions, and actual practice remain separate facts. Exact package, kind,
and definition identity is required before native or supported-mod content can
be interpreted as Culture evidence; unknown content remains native-only.

Pawns retain deterministic private and public attitudes, attention, inherited-
prior strength, perceived norms and social pressure, observation likelihood,
knowledge confidence, moral conviction, and uncertainty. Question-specific
psychology supplies only a bounded private deviation. Sparse represented influence may
move those attitudes over time. Persistent psychology, Culture, native
Ideoligion, Political Order, represented institutions, organizations, proposition
knowledge, acts, and practices are separate causes and durable owners. Culture
contributes appraisal and discretionary CA action selection without changing
native legality, direct player authority, knowledge, office, material capacity,
or completed research. Native `CultureDef` is an optional visual tradition only.

Existing societies and player founding use the same Culture, native Ideoligion,
Political Order, Technological Knowledge, and institutional concepts at
different points in time.
Existing faction and settlement records describe a society already present:
established Culture, faction-owned Technological Knowledge, realized
institutions, material conditions, and an explicit temporal basis. Player
authoring records the inherited Culture, Ideoligion, Political Order, and
Technological Knowledge the founders bring and the exact rules they adopt at
landing.
Later local Culture, institutions, and practice must be produced by play rather
than fabricated as pre-existing event history. Political Order records what the
population considers proper; it does not become an organization custom or broader
institution merely because the founders hold it.

The faction owns one canonical Culture, Political Order, and Technological
Knowledge composition. Society is the joint control surface over those three
owners. A Society preset is an independently identified reusable snapshot of
all three, and one validated operation copies all three into the faction's
canonical state. The preset supplies a coherent starting point without becoming
a Society owner or materializing Ideoligion, institutions, practice, or history;
its identity does not persist in the faction. Culture, Political Order, and
Technological Knowledge presets remain independent component substitutions.
Starting Region may use the same Society preset while creating an ordinary
local faction and settlement; the page separately owns its explicit scenario
population and broad-area map assignment. The applied preset does not survive
as a settlement type or mode.

Technological Knowledge records practical knowledge by domain and competency.
The standard availability model reads faction knowledge directly. Experimental
Distributed Knowledge projects the same state through living pawn carriers and
persistent institutional or recorded custody, so redundancy and isolated loss
can change effective capability without creating another technology authority.
Native research remains the concrete project graph and completion record;
`FactionDef.techLevel` is only a one-time initialization source or a pre-world
compatibility fallback after authored faction state becomes authoritative.

## Governing constraints

- **DR-86.** When RimWorld already represents the thing, extend that system rather
  than creating a parallel abstraction. Ideology is `Ideo` and `PreceptDef`;
  technology is `ResearchProjectDef` and its unlock graph; settlements are real
  world objects and maps. This mod adds persistence, organizational ownership,
  knowledge limits, regional simulation and behaviour — it does not replace the
  game's concrete objects and rules with summary numbers.
- **DR-85.** Capability is a derived assessment, never an independent cause.
  Generation creates supports; runtime consumes supports; any capability value is
  a computed summary or cache.
- **DR-89.** Goods move only when paid for, or when explicit terms authorise
  credit. An obligation is created by a basis, never by an empty purse.
- **DR-106 through DR-115.** Authoring exposes meaningful persistent causes,
  not every analytical category the simulation can derive. Culture is persistent
  longitudinal social history; Ideoligion, Political Order, institutions,
  adopted rules, and practice remain distinct; established societies and new
  founders retain their different temporal boundaries. Contextual explanation
  belongs to the owning decision, and Starting Region preserves its spatial map
  without universal detail, development-intensity, or facility-bundle controls.
- **DR-116 through DR-119, as superseded by DR-148 through DR-173.** Culture at
  T0 contains substantive question distributions and concrete practices with
  real source-and-consumer contracts. Political Order is a complete composition
  over concrete questions; represented institutions remain separate factual
  state. Presets and deterministic generation fill the same complete variables
  and remain editable. Pending-authoring epoch 15 may reject incompatible
  unconfirmed drafts, while realized campaign state follows the durable B11
  boundary and current owner-specific compatibility rules. Manual, preset,
  profile, random, and generated Culture share one object; causal fields remain
  separate and authored player state is never silently rerolled.
- Stitching controls geographic continuity only, never population or political
  intensity. Vanilla `OverallPopulation` stays authoritative for major-settlement
  abundance.

## Starting Region and Settlement Composition

Starting Region has one shared selection across Region, Map, and Details.
Region lists the region, its factions, and their settlements. Map selects those
same facts, assigns broad settlement and arrival areas, and can begin the same
settlement-placement operation. Details owns the selected subject's decisions
and essential inspection. A field becomes a control only when more than one
valid choice exists. Generated-source labels, schema facts, receipts, and
architectural explanation remain outside ordinary player copy.

An established settlement owns an open causal settlement program. Each active
entry begins with a concrete need and records its actual operator, standing,
knowledge, labor, material inputs, nodes, target population, access, funding,
maintenance, behavior consumer, and failure condition. Broad tendencies and
capability scores are summaries or planning pressures; they never instantiate a
program. Loaded functional assets are selected only after the social and
operational contract exists. A room, building, or worktable is a material node,
not an institution.

Domestic provision belongs to actual pawns or persistent domestic units formed
from represented partner, kin, residence, or retained co-residential facts.
Unresolved residents self-provision individually rather than becoming a
hash-selected household. Communal and authority provision require actual
organizations, workers, stock, access, funding, and distribution. Tax-funded
provision additionally requires a legitimate authority, adopted tax policy,
tax base, collection, and expenditure. Political Order remains normative;
it does not create represented institutions or provision systems.

Practiced capability is a domain-specific evidence assessment over actors,
organizations, active work, knowledge, material nodes, history, blockers, and
confidence. It contains no random jitter and cannot be the sole cause of the
programs it summarizes. These causal-ownership constraints are ratified in the
B10 audit and DR-133 through DR-144.

Culture authoring begins with descriptive anchors for population distributions
and keeps concrete practices and historical evidence distinct. Exact continuous
values remain available as contextual fine tuning, not the default description
of a population. Faction Relations remains the visual reference for relational
authoring. The production vocabulary, question registry, category policy,
political composition, and duplicate-surface constraints are inventoried in
`AUTHORING_ONTOLOGY_COVERAGE.md`, `B13_CAUSAL_CONTRACT.md`,
`B15_TECHNOLOGICAL_KNOWLEDGE_CONTRACT.md`, and
`CULTURE_RESEARCH_CORPUS.md`.

## Ownership

| Surface | Owner | Purpose |
|---|---|---|
| `CORE.md` | governance | Project identity and canonical composition. |
| `ARCHITECTURE.md` | governance | Ratified framework shape. |
| `GAME_ARCHITECTURE.md` | governance | Decompile-grounded study of engine seams and limits. |
| `BATCH_LOG.md`, `Batches/` | history | Portable project history: one append-only atomic batch sequence with chronological navigation, provenance, and historical crosswalks. |
| `Batches/THREADS.md` | governance | Permanent series-neutral thematic classification across nonadjacent batches. |
| `VERSION_MAP.md`, `tools/version-model.mjs`, `VERSION` | governance | Contiguous version units, tier replay, generated current version, and mechanical validation. |
| `Source/` | implementation | The mod assembly. |
| `Defs/`, `Patches/` | implementation | Declarative game content. |
| `About/`, `LICENSE`, `CREDITS.md`, `UPSTREAM_SOURCES.md` | distribution | Shipping identity, licence, attribution and provenance. |
