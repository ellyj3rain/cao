| Document | Colonist Awareness Overhaul Session State |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.0.0.0-alpha` · closed batch tip `B2` · next `B3` |
<!-- cao:generated:version END -->
| Repository | `SESSION_STATE.md` |
| Status | ACTIVE - current operational state. |

# Session state

Updated 2026-08-10 19:46 UTC / 12:46 PST.

Read this before claiming where the regional or player-founding work stands.
Compile evidence and operator runtime judgment remain separate.

## Version control

| Surface | State |
|---|---|
| Canonical project state | The current tracked local tree and governed records. `git status --short` identifies checkout-local changes. |
| Development closure | `B2` closes player founding authoring; `B3` is next. The closing commit carries the batch record. |
| Authority branch | `mallowfluff/b2-player-founding-authoring`, based on closed B1 tip `c78a441b9bda30c0eb2c3efc998844db51a44084`. |
| Local Git history | May retain engineering history from before publication. It is supporting evidence, not the portable project history. |
| Published forge | No B2 publication action is part of this gate. The current local project and deployed assembly are the runtime-test authority. |

The project root is the live mod junction. Linked checkouts are temporary
execution surfaces and are not part of the project's identity.

## Development record

The A sequence is closed at `A102`; the chronology continues through closed
`B2`, and `B3` is the next development batch. `BATCH_LOG.md` is the
chronological index. `Batches/THREADS.md` classifies work across time without
changing chronology. `VERSION_MAP.md` partitions the closed chronology into
contiguous capability units and derives `VERSION`.

| Evidence | State |
|---|---|
| Chronology | 104 dated batches: `A1` through `A102`, followed by `B1-B2` |
| Thematic organization | 12 series-neutral `TF-*` families and 30 many-to-many `T-*` threads |
| Version chronology | 29 contiguous units cover A1-B2 exactly once; Neo hierarchy and caps derive `1.0.0.0-alpha` |
| Next work | `B3` remains the next ordinary batch; its content determines its version tier |

Closed batch records and their provenance are the portable project history.
Local Git may retain finer-grained engineering history. A forge publishes the
current project but does not define its identity or continuity.

## Current society model

Existing factions and settlements are descriptive state. Their Culture,
Ideoligion, Political Beliefs, realized social order, institutions, relations,
and accumulated history already exist when encountered.

The player surface authors a founding moment:

`Culture + Ideoligion + Political Beliefs -> Founding Arrangement -> institutions and historical practice through play`

Culture and Political Beliefs use the same models, presets, and editors as
established societies. RimWorld's native `Ideo` remains authoritative for
Ideoligion. Political Beliefs state what the founders consider proper. The
Founding Arrangement states the immediate rules actually adopted for authority,
work, voice, shared supplies, and duration. Agreement or disagreement is saved
and summarized as meaningful state.

The confirmed draft is world-owned for regional, ordinary, and forced-map
starts. The regional fixture retains a serialized projection. A stable live
draft survives Back/Next and native-editor round trips. The native Ideoligion
receipt follows content and revision rather than load ID alone, and native
structural validity is checked before scenario notification. At game start,
the exact arrangement becomes duration-aware relations once; a durable applied
tick prevents replay over later institutions. The player faction receives no
generated mature structure.

Political Beliefs remain standards. Current organization customs derive from
the realized social order rather than being copied from belief. Established
humanlike factions still receive realized structures appropriate to societies
whose histories precede the game.

## Build and deployment

| Artifact | SHA-256 | State |
|---|---|---|
| Built `Assemblies/ColonistAwareness.dll` | `DF57515B1F2A0F4635DB7471DB9460BD5788B545694B41E35538CE73503BB2B4` | Two full no-incremental Release builds were byte-identical: 0 errors and the same 12 existing warnings. |
| Live root `Assemblies/ColonistAwareness.dll` | `DF57515B1F2A0F4635DB7471DB9460BD5788B545694B41E35538CE73503BB2B4` | Deployed while RimWorld was closed and byte-identical to the verified build. |

Static verification is closed for this gate. The three-lane correctness,
coherence, and structural review has no outstanding Critical or High findings.
The player-founding suite passes 44 assertions; the World tendencies fixed-seed
suite passes 179. Both receipt tools build with 0 errors and 0 warnings. The
main project builds with 0 errors and the same 12 existing warnings. The version
model covers 104 batches in 29 units, its tests pass, all tracked XML parses,
and `git diff --check` reports no whitespace errors.

No B2 player-founding result is operator runtime-verified. The next evidence
belongs to the in-game test.

## Authored runtime fixture

World identity: `alysaliu|1|Algorab Markab`

Region: `CA-RG-EB596A12`

Candidate: `613b1fe44104`, confirmed, developer exercise

Arrival area: `389638`

Composition: 3 factions, 4 settlements, 9 population groups, local map scale 350

Current keyed file:

`C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml`

Active mirror:

`C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml`

Both active files use schema 3 and are byte-identical at SHA-256
`40BA69E3B41770EACA0C6C2A2D430F7D7AE0C056967C919F204FA49FD287BF1A`.
They agree on identity, arrival area, causal world policy, three factions, four
settlements, nine population groups, faction and settlement relationships,
population assignments, relations, frontier rows, and realized settlement
facts.

The converted `playerFounding` state is deliberately unconfirmed and contains
no invented Culture, Political Beliefs, Ideoligion receipt, or Founding
Arrangement. The recovered fixture did not contain a valid current-schema
player choice; the restored page is the authority where the operator authors it.

Exact pre-conversion copies are preserved at:

`C:\Users\jleyv\AppData\Local\Temp\ca-b2-player-authoring-recovery\pre-live-conversion-20260810-1915Z`

## Runtime handoff

The operator's next test is the gate:

1. Open the existing colony-creation flow and confirm that the authored region,
   arrival area, World tendencies, three factions, and four settlements restore.
2. Continue to Founding society and confirm that it replaces the vanilla
   preset-only page with Culture, Ideoligion, Political Beliefs, and Founding
   Arrangement on one coordinated surface.
3. Exercise Culture, Political Belief, and arrangement presets and editors.
   Confirm that generated suggestions remain editable and that belief-versus-
   arrangement differences are intelligible.
4. Exercise native Ideoligion preset, load, fixed, fluid, Continue, and Back
   paths. Confirm that edits persist, invalid native configurations remain
   blocked, and the flow returns to the coordinated page.
5. Continue through Starting Pawns and world generation. Confirm that no
   incomplete-bundle, missing-arrival, or dropped-composition error appears.
6. Inspect the founded colony. Confirm that carried Culture, Ideoligion, and
   Political Beliefs are present; the exact selected Founding Arrangement is
   materialized once; and no mature institutional history was invented.

Repository convergence stops before this test. It does not launch RimWorld,
select options, advance the game, alter saves, or substitute static receipts for
the operator's judgment of how the flow looks and plays.

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
