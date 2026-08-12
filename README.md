# Colonist Awareness Overhaul

Colonist Awareness is a RimWorld 1.6 framework and simulation overhaul. Pawns
act on what they know. Settlements persist as real places with people, supplies,
institutions, beliefs, and authority. Factions make decisions and carry their
consequences forward instead of resetting to isolated game events.

<!-- cao:generated:version BEGIN -->
Current version: `1.3.0.3-alpha`. Implementation is complete through batch `B9`; `B10` is the next development batch. SESSION_STATE records the verified assembly and live deployment state. Static verification does not substitute for how the game looks and plays.
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
and explicit exceptional starting conditions. Objects and Map remain its primary
navigation; Details provides concise object-specific inspection and settlement
comparison. One valid option is shown as a fact only when it matters, not as a
button, and generation provenance remains diagnostic rather than player copy.

Settlement scale, access, services, civic capacity, and material composition are
realized from population, land, geography, technology, institutions, economy,
trade, history, and loaded functional objects. An open 22-program registry
records housing, food, storage, medicine, production, trade, governance,
security, defense, research, religion, social life, culture, agriculture,
communications, and transport requirements. Programs can require several rooms,
assets, operators, stocks, and access paths; they are not a facility checklist or
development slider. Household, communal, and authority provisions appear only
when their actual social and material operators exist.

Culture is persistent social history with substantive inherited state. Each
Culture records constituent populations; inherited and current meanings about
concrete social subjects; inherited and lived practices; observations;
transitions; and provenance. A meaning records approval, normality, prestige, and
salience for one namespaced subject. Twelve initial subjects connect factual
sources to real spatial, social, institutional, political, and
settlement-development consumers, and later modules can register more without a
Culture schema change. RimWorld's native `CultureDef` remains a separately
labeled optional visual tradition, not the cultural model.

The Culture composer edits overview, semantic social meanings, inherited
practices, visual tradition, and a factual causal preview. Guided meaning cards
are the normal authoring surface; exact continuous values remain available as
contextual fine tuning and round-trip without loss. Name plus visual tradition is
not a valid Culture. Saved profiles copy inherited meanings and practices without
world identity or historical state. In play, informed pawn responses aggregate
into subject-specific social patterns; qualified repeated evidence can produce
`Culture(T+1)` from `Culture(T)`. One event, unchanged evidence, or opening an
editor cannot. Culture ranks otherwise valid possibilities and creates no
knowledge, authority, resources, technology, office, or land.

The same concepts have different temporal meanings on the two creation sides:

| Existing society | Player founding |
|---|---|
| Culture, Ideoligion, Political Beliefs, realized social order, institutions, material state, and established historical basis are facts about a society already present. | Founders bring inherited Culture, native Ideoligion, and Political Beliefs, then choose the rules instituted at landing. Local institutions and historical Culture develop through play. |

Political Beliefs are authored question-first across thirteen normative
questions. Complete profiles are transparent copy accelerators rather than
political identities, all answers remain independently editable, and missing
player positions are never filled randomly. NPC positions are derived from
same-axis realized structure, explicit observed facts, or scored cultural
meaning with a stable causal receipt; unsupported positions remain unset.

RimWorld's native Ideoligion chooser remains first-class inside this coordinated
flow. Native presets, saved Ideoligions, fixed and fluid creation, memes,
precepts, roles, rituals, validation, and persistence remain native. CA preserves
the neighboring Culture, Political Beliefs, and founding-order draft when the
player enters and returns from that chooser. Political Beliefs state what a
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
state, World tendencies, behavior authorization, Culture persistence and
transition, consumer reach, fixture round-trip, and derived-state stability.
Static receipts, builds, hashes, and reviews establish that a candidate is ready
for the operator; they do not establish how the game looks or plays. See
[`SESSION_STATE.md`](SESSION_STATE.md) for the exact current assembly, deployment,
fixture, and remaining RimWorld observations.

## License and provenance

Colonist Awareness is licensed under GPL-3.0. See [`LICENSE`](LICENSE),
[`CREDITS.md`](CREDITS.md), and [`UPSTREAM_SOURCES.md`](UPSTREAM_SOURCES.md) for
attribution and incorporated-source provenance.
