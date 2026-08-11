| Document | Colonist Awareness Overhaul Core |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.3.0.1-alpha` · closed batch tip `B7` · next `B8` |
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
persistent local Culture, Ideoligion, Political Beliefs, social order, starting
conditions, relationships, operations, economies, diplomacy, and conflict.
Loaded maps are detailed materializations and native physical executors. The
projection rule:

`pawn evidence → organization record → authorization → regional operation or
contract → native world/incident/quest/map/Lord projection → reconciled outcome`

Native RimWorld physical execution remains authoritative while materialized; this
mod owns the persistent organizational reason and continuity.

Culture is persistent longitudinal social-historical state. A local Culture
retains inherited origin, local identity, constituent populations, lived
observations, recognized practices, transitions, and provenance. Bounded
historical evaluations derive `Culture(T+1)` from `Culture(T)` and intervening
evidence. Spatial, social, institutional, political, and settlement-development
consumers may rank otherwise valid choices from that state, but Culture creates
neither permission nor material capability. Cultural expression is a read-only
interpretation of the relationship between Culture and current society; native
`CultureDef` is one optional visual inheritance.

Existing societies and player founding use the same Culture, native Ideoligion,
Political Beliefs, and social-order concepts at different points in time.
Existing faction and settlement records describe a society already present:
established Culture, realized institutions, material conditions, and an explicit
temporal basis. Player authoring records the inherited Culture, Ideoligion, and
Political Beliefs the founders bring and the exact rules they adopt at landing.
Later local Culture, institutions, and practice must be produced by play rather
than fabricated as pre-existing event history. Political Beliefs judge what is
practiced; they do not become organization customs or broader institutions merely
because the founders hold them.

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
  longitudinal social history; Ideoligion, Political Beliefs, institutions,
  adopted rules, and practice remain distinct; established societies and new
  founders retain their different temporal boundaries. Contextual explanation
  belongs to the owning decision, and Starting Region preserves its spatial map
  without universal detail, development-intensity, or facility-bundle controls.
- Stitching controls geographic continuity only, never population or political
  intensity. Vanilla `OverallPopulation` stays authoritative for major-settlement
  abundance.

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
