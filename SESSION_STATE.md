| Document | Colonist Awareness Overhaul Session State |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.0.1.0-alpha` · closed batch tip `B3` · next `B4` |
<!-- cao:generated:version END -->
| Repository | `SESSION_STATE.md` |
| Status | ACTIVE - current operational state. |

# Session state

Updated 2026-08-10 23:10 UTC / 16:10 PST.

Read this before claiming where the creation-flow work stands. Compile evidence
and operator runtime judgment remain separate.

## Version control

| Surface | State |
|---|---|
| Canonical project state | The current tracked local tree and governed records. `git status --short` identifies checkout-local changes. |
| Development closure | `B3` closes creation-flow interaction convergence; `B4` is next. The closing commit carries the implementation, verified assembly, README twin, receipts, and batch record. |
| Authority branch | `mallowfluff/b3-creation-flow-interaction`, based on closed B2 tip `2d615d3279c4a4737e35ee3ad4d2b41d65381276`. |
| Local Git history | May retain engineering history from before publication. It is supporting evidence, not the portable project history. |
| Published forge | No B3 publication action is part of this gate. The current local project and deployed assembly are the runtime-test authority. |

The project root is the live mod junction. The linked branch checkout is the
implementation authority for B3 until the closing commit; it is an execution
surface rather than a separate project.

## Development record

The A sequence is closed at `A102`; the chronology continues through closed
`B3`, and `B4` is the next development batch. `BATCH_LOG.md` is the
chronological index. `Batches/THREADS.md` classifies work across time without
changing chronology. `VERSION_MAP.md` partitions the closed chronology into
contiguous capability units and derives `VERSION`.

| Evidence | State |
|---|---|
| Chronology | 105 dated batches: `A1` through `A102`, followed by `B1-B3` |
| Thematic organization | 12 series-neutral `TF-*` families and 30 many-to-many `T-*` threads |
| Version chronology | 30 contiguous units cover A1-B3 exactly once; B3 is the `kohai` maturation unit `VU-030`, deriving `1.0.1.0-alpha` |
| Next work | `B4` remains the next ordinary batch; its content determines its version tier |

Closed batch records and their provenance are the portable project history.
Local Git may retain finer-grained engineering history. A forge publishes the
current project but does not define its identity or continuity.

## Current creation flow

The creation path is one coordinated interaction:

`World -> Landing -> Starting Region -> Founding society -> native Ideoligion -> Starting Pawns -> game start`

Each stage owns a distinct player question. Shared graphical choices preserve
the edited subject, show concise identity before detail, separate focused and
applied choices, and distinguish suggested, inherited, generated, and authored
state. Detail is progressively disclosed in a stable inspector rather than a
wall of text in a floating menu.

All eleven World tendencies begin on one semantic middle profile. Named presets
move independent tendencies together around that neutral reference. Visible
rows name a world property, its direct change, and its constraint; internal
generation mechanisms are not presented as player concepts.

Starting Region remains the spatial anchor. Factions, settlements, population
groups, origins, belief sources, relations, facilities, infrastructure,
provisions, authority, and federation use the shared choice grammar. Population
membership and belief source are independent causes. Shares always serialize as
a complete 100-percent composition with at least 20 percent assigned to the main
population.

Existing factions and settlements remain descriptive state: Culture,
Ideoligion, Political Beliefs, realized social order, institutions, relations,
and accumulated history already exist when encountered. The player authors a
founding moment:

`Culture + Ideoligion + Political Beliefs -> Founding terms -> institutions and historical practice through play`

Political Beliefs state what the founders consider proper. Founding terms state
the rules adopted at landing. Their agreement or disagreement is visible and
saved. RimWorld's native `Ideo` remains authoritative for Ideoligion. The
confirmed founding draft is world-owned, and its exact terms materialize once;
a durable applied-tick receipt prevents replay over later institutions.

## Build and deployment

| Artifact | SHA-256 | State |
|---|---|---|
| Built `Assemblies/ColonistAwareness.dll` | `B8BB52119599468C7AF6AB030F7096C970502C9660C018A83111E2A3575A81E2` | Full no-incremental Release build: 2,961,920 bytes, 0 errors, and the same 12 existing warnings. |
| Live root `Assemblies/ColonistAwareness.dll` | `B8BB52119599468C7AF6AB030F7096C970502C9660C018A83111E2A3575A81E2` | Deployed while RimWorld was closed and byte-identical to the verified B3 build. |

The correctness, coherence, structural, and surface reviews found no Critical
issue. All High findings were corrected. The interaction suite passes 64
assertions, the player-founding suite passes 44, and the World tendencies suite
passes 181. Static evidence establishes readiness for deployment and operator
scrutiny; it does not establish runtime visual or behavioral acceptance.

## Authored runtime fixture

World identity: `alysaliu|1|Algorab Markab`

Region: `CA-RG-EB596A12`

Candidate: `613b1fe44104`, confirmed, developer exercise

Arrival area: `389638`

Composition: 3 factions, 4 settlements, 9 population groups, local map scale 350

Current keyed runtime file:

`C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml`

The keyed file was last written by the operator's current run at 2026-08-10
20:41:50 UTC. It remains untouched at SHA-256
`491674C461C937E7C90ED47FE4CCE52A494C44F1CA9DFE9F69F46121EB5B8019`
and contains the current founding draft together with the authored regional
composition.

`C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml`
is a dormant B2 conversion artifact. The current runtime consumes the keyed
file; the root mirror is not synchronized and is not authority.

## Runtime handoff

The verified B3 DLL is deployed byte-identically in the live mod root. The
operator's next launch is the gate:

1. Confirm World tendencies open on one centered neutral state and that named
   profiles remain understandable after manual changes.
2. Confirm Starting Region restores the selected region, arrival area, three
   factions, four settlements, population assignments, and spatial links.
3. Exercise faction, settlement, population, origin, belief-source, relation,
   facility, infrastructure, provision, and authority choices. Confirm the
   subject stays visible, focus differs from selection, and no menu or inspector
   clips.
4. Continue to Founding society. Exercise Culture, Political Belief, Founding
   terms, and native Ideoligion preset/load/fixed/fluid paths; confirm Back and
   Continue retain the authored state.
5. Confirm belief-versus-practice comparisons are intelligible and that linked
   belief sources follow affiliation while independent sources remain fixed.
6. Continue through Starting Pawns and game generation. Confirm the composition
   is not dropped and the exact Founding terms materialize once.

Repository convergence does not select options, advance the game, alter saves,
or substitute static receipts for the operator's judgment of how the flow looks
and plays.

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
  no file in this convergence pass replaces them.
