| Document | Colonist Awareness Overhaul Session State |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.2.0.0-alpha` · closed batch tip `B5` · next `B6` |
<!-- cao:generated:version END -->
| Repository | `SESSION_STATE.md` |
| Status | ACTIVE - current operational state. |

# Session state

Updated 2026-08-11 07:55 UTC / 2026-08-11 00:55 PST.

Read this before claiming where creation-flow work stands. Compile and receipt
evidence, deployment, and operator runtime judgment remain separate.

## Version control

| Surface | State |
|---|---|
| Canonical project state | The current tracked local tree and governed records. `git status --short` identifies checkout-local changes. |
| Development closure | `B5` closes contextual cultural expression and settlement development; `B6` is next. Source closure is `2bca1e16b240a24a00ddab385909a81674a17cee`; the following governance closure carries generated version projections, the README twin, and the batch record. |
| Authority branch | `mallowfluff/b3-creation-flow-interaction`, following B4 governance tip `9efcd1df6efae6d7ec85064bbab8a88f680f392c`. |
| Local Git history | May retain earlier engineering history. It is supporting evidence, not the portable project history. |
| Published forge | No B5 push or publication action is part of this gate. |

The linked branch checkout is the implementation and governance authority. The
project root is the live mod junction and now carries the same verified B5
assembly.

## Development record

The A sequence is closed at `A102`; the chronology continues through closed
`B5`, and `B6` is the next development batch. `BATCH_LOG.md` is the chronological
index. `Batches/THREADS.md` classifies related work across time without changing
chronology. `VERSION_MAP.md` partitions the closed chronology into contiguous
capability units and derives `VERSION`.

| Evidence | State |
|---|---|
| Chronology | 107 dated batches: `A1` through `A102`, followed by `B1-B5` |
| Thematic organization | 12 series-neutral `TF-*` families and 30 many-to-many `T-*` threads |
| Version chronology | 32 contiguous units cover A1-B5 exactly once; B5 is the `minor` capability unit `VU-032`, deriving `1.2.0.0-alpha` |
| Next work | `B6` remains the next ordinary batch; its actual content determines its version tier |

## Current creation and authoring flow

The creation path is one coordinated interaction:

`World -> Landing -> Starting Region -> native Ideoligion -> Starting arrangements -> Starting Pawns -> game start`

RimWorld's native fixed, fluid, preset, customized, and loaded Ideoligion paths
remain first-class. Starting arrangements follows the native chooser and owns
the founders' carried cultural background, normative political beliefs, and
limited rules adopted at landing. Existing factions remain descriptive
established societies: their carried backgrounds, native Ideoligions, political
beliefs, realized structure, institutions, and accumulated histories are facts
when encountered.

Culture schema 3 owns only stable background identity, an optional name, an
optional native visual-tradition source, and provenance. It no longer owns public
gathering, hospitality, meal, memory, furnishing, ThingDef, or StuffDef recipes.
Missing style content is a neutral visual fallback rather than a compatibility
failure. Explicitly choosing no visual source remains durable.

Cultural expression is a deterministic contextual reading. Its causes include
carried background, actual Ideoligion commitments, political beliefs, population
groups and certainty, institutions or founding arrangement, provisions,
settlement form and role, infrastructure, facilities, economy, trade, geography,
relations, buildings, and history. It yields one persisted status, summary, and
signature plus five readable facets. Starting Region presents the result at the
settlement; faction views aggregate settlement results; materialized maps
reconcile against actual residents; world markers consume the persisted result.

Each settlement owns a Minimal, Contextual, or Extensive development profile.
The profile applies a bounded shift to generated access, services, and civic
development. Explicit infrastructure values win. Facilities are then derived
from the actual settlement and expose independent Generated, Include, or Omit
ownership. The final representation states every settlement's profile,
infrastructure provenance, generated facilities, explicit overrides, and
override count.

Information detail remains a presentation-only Compact, Standard, or Expanded
setting. Critical warnings, contradictions, and blockers remain visible at every
level. Founding remains earlier than an existing settlement: background,
Ideoligion, and beliefs arrive with the founders; only landing terms are adopted;
local institutions and historical culture develop through play.

## Build, receipts, and review

| Artifact | SHA-256 | State |
|---|---|---|
| Built `Assemblies/ColonistAwareness.dll` | `529A4327416070FE4832BF2CFD8514DA45DC1AB5BF6C247816136BD1BD9312C1` | Full no-incremental Release build: 3,039,232 bytes, 0 errors, and the same 12 existing warnings. |
| Live root `Assemblies/ColonistAwareness.dll` | `529A4327416070FE4832BF2CFD8514DA45DC1AB5BF6C247816136BD1BD9312C1` | Deployed while RimWorld was closed; byte-identical to the verified B5 build. |

| Executable evidence | Result |
|---|---|
| Authoring convergence | PASS - 126 assertions against mirror and 126 against keyed |
| Creation-flow interaction | PASS - 65 assertions |
| Player founding | PASS - 48 assertions across mirror and keyed |
| World tendencies causal contract | PASS - 185 assertions across mirror and keyed |
| Combined | PASS - 424 unique assertions; 550 assertions actually executed |

The correctness, structural, and surface review panel reports no remaining
Critical or High findings. The final correction replaced receipt-local cultural
expression and migration stand-ins with production-shared typed kernels. The
source/tool fingerprint remained
`8BC6CC02C41B9BC561B42348E9ABD92DF80B498B8397EF7911EB6D0EC0E0951C`
before the build, before each receipt run, and at the final gate.

`git diff --check` passed with no whitespace errors. The 12 compiler warnings are
the existing member-hiding and DefOf assignment warnings recorded by the build;
there are no B5 compile errors.

## Authored runtime fixture

| Field | Current authority |
|---|---|
| World | `alysaliu|1|Algorab Markab` |
| Region | `CA-RG-EB596A12` |
| Candidate | `613b1fe44104`, confirmed |
| Arrival/root tile | `389638` |
| Map scale | 350 |
| Composition | 3 factions, 4 settlements, 9 population groups |
| Schema | Regional plan 4; Culture 3; political beliefs 2 |
| Development | Four Contextual settlement profiles; exact facility state retained |

Active mirror:

`C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml`

Active keyed plan:

`C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml`

Both files were written at 2026-08-11 07:05:47 UTC. They are byte-identical at
27,220 bytes and SHA-256
`F60DC069CC30686E4B1694E91B61EF9458F37BD8E6E26FBCF621778FB916D583`.
Both parse in the current schema, round-trip the same composition, preserve
relations and population assignments, and carry the current realization hash.

## Runtime handoff

B5 is compiled, statically verified, and deployed. RimWorld remains closed. The
next action is the operator runtime test:

1. Load the preserved plan and confirm all 3 factions, 4 settlements, population
   groups, relations, arrival area, map scale, and founding state remain present.
2. Inspect settlement Culture in Starting Region. Confirm it reads as contextual
   expression rather than an authored furniture list and that faction views show
   distinct settlement results.
3. Change one carried background or political belief and confirm the expression
   changes without fabricating physical objects or resetting unrelated state.
4. Compare Minimal, Contextual, and Extensive settlement development; confirm
   only generated infrastructure shifts and explicit infrastructure survives.
5. Include or omit individual starting facilities, return all facilities to
   generated, and confirm the summary and final representation report exact
   provenance and override counts.
6. Start the world and confirm materialized settlement records, residents,
   Ideoligions, cultural summaries, and world markers survive generation without
   rerolling the authored state.

Static evidence does not select options, advance the game, alter saves, or
substitute for how the flow looks and plays.

## Environment and preserved evidence

- RimWorld target: 1.6.4871 rev590.
- Odyssey is installed. Its active runtime state is not asserted without a game
  launch.
- `PARALLEL_ONTOLOGY_AUDIT.md` remains a damaged consolidation; the recovered
  full copy remains in the preservation package.
- Preservation package:
  `Projects\colonist-awareness-preservation-20260806-0614Z-2314PDT`, with a
  verified mirror on `D:`.
- The RS-008 source save and recovered documents remain out-of-band evidence;
  no file in B5 replaces them.
