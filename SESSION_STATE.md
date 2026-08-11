| Document | Colonist Awareness Overhaul Session State |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.1.0.0-alpha` · closed batch tip `B4` · next `B5` |
<!-- cao:generated:version END -->
| Repository | `SESSION_STATE.md` |
| Status | ACTIVE - current operational state. |

# Session state

Updated 2026-08-11 03:01 UTC / 2026-08-10 20:01 PST.

Read this before claiming where the creation-flow work stands. Compile and
receipt evidence, deployment, and operator runtime judgment remain separate.

## Version control

| Surface | State |
|---|---|
| Canonical project state | The current tracked local tree and governed records. `git status --short` identifies checkout-local changes. |
| Development closure | `B4` closes culture, politics, and responsive authoring; `B5` is next. Source closure is `60aa03b5bec83d780533cc3e5abaf9325d624e17`; the governance closure carries the generated version projections, README twin, and batch record. |
| Authority branch | `mallowfluff/b3-creation-flow-interaction`, based on closed B3 tip `dbc2d4fad9b54cdf9bfc1fbebfbc6c3b88bf5074`. |
| Local Git history | May retain engineering history from before publication. It is supporting evidence, not the portable project history. |
| Published forge | No B4 push or publication action is part of this gate. |

The linked branch checkout is the implementation authority for B4. The project
root remains the live mod junction and still carries the verified B3 assembly.

## Development record

The A sequence is closed at `A102`; the chronology continues through closed
`B4`, and `B5` is the next development batch. `BATCH_LOG.md` is the chronological
index. `Batches/THREADS.md` classifies related work across time without changing
chronology. `VERSION_MAP.md` partitions the closed chronology into contiguous
capability units and derives `VERSION`.

| Evidence | State |
|---|---|
| Chronology | 106 dated batches: `A1` through `A102`, followed by `B1-B4` |
| Thematic organization | 12 series-neutral `TF-*` families and 30 many-to-many `T-*` threads |
| Version chronology | 31 contiguous units cover A1-B4 exactly once; B4 is the `minor` capability unit `VU-031`, deriving `1.1.0.0-alpha` |
| Next work | `B5` remains the next ordinary batch; its actual content determines its version tier |

## Current creation and authoring flow

The creation path is one coordinated interaction:

`World -> Landing -> Starting Region -> native Ideoligion -> Starting arrangements -> Starting Pawns -> game start`

RimWorld's native fixed, fluid, preset, customized, and loaded Ideoligion paths
remain first-class. Starting arrangements follows the native chooser and owns
the founders' Culture, normative political beliefs, and limited rules adopted
at landing. Existing factions remain descriptive established societies: their
Culture, native Ideoligion, political beliefs, realized structure, institutions,
and accumulated history are already facts when encountered.

Information detail is a persistent presentation-only setting with Compact,
Standard, and Expanded levels. Critical warnings, contradictions, and blockers
remain visible at every level. Local full-detail expansion does not alter the
global preference, generation, or saved world state.

Culture schema 2 separates native visual style from four independent cultural
practices: public gathering, hospitality, communal meals, and public memory.
Each field has stable keyed choices, deterministic generation, per-field
authored/preset provenance, shared founding and established-faction editing,
and a concrete starting-facility or furnishing consumer. Four built-in profiles
and global user profiles use stable identities separate from display labels.

Political beliefs and realized faction structure preserve the same thirteen
saved axes while remaining separate states. One grouped composer covers
Governance, Civic participation, Property/economy, Membership/order, and
Security/conflict; established factions expose one norm-versus-practice
comparison. User culture and political profiles support save, load, rename,
duplicate, and delete with copy-on-apply isolation.

Starting Region has three explicit layout modes. Wide retains object rail, map,
and inspector; Medium preserves a viable map and measured secondary detail;
Compact uses Objects, Map, and Details tabs while retaining the existing map
selection. Faction technological knowledge is separate from a settlement's
local material capability and research. Short-screen authoring dialogs scroll
their body while keeping a native Close footer available.

## Build, receipts, and review

| Artifact | SHA-256 | State |
|---|---|---|
| Built `Assemblies/ColonistAwareness.dll` | `4517255101815744D11A2FF657F5D1E689B470AA8A314B913539296A4B454459` | Full no-incremental Release build: 3,008,000 bytes, 0 errors, and the same 12 existing warnings. |
| Live root `Assemblies/ColonistAwareness.dll` | `B8BB52119599468C7AF6AB030F7096C970502C9660C018A83111E2A3575A81E2` | Still the 2,961,920-byte verified B3 build. It is intentionally byte-distinct because B4 has not been deployed. |

| Executable evidence | Result |
|---|---|
| Authoring convergence | PASS - 157 assertions |
| Creation-flow interaction | PASS - 65 assertions |
| Player founding | PASS - 33 assertions |
| World tendencies causal contract | PASS - 100 assertions |
| Combined | PASS - 355 assertions |

The correctness, structural, and surface review panel reports no remaining
Critical or High findings. Review corrections added regional culture
compatibility gates, per-domain furnishing failure containment and reporting,
nonmutating Ideoligion page inspection, content-aware expansion controls, and
fixed non-scrolling dialog footers. Remaining Medium notes concern later
architectural consolidation and deeper executable UI behavior, not a known B4
compile or persistence failure.

The static layout receipt covers the effective width/UI-scale matrix:

| Resolution | 100% | 125% | 150% |
|---|---|---|---|
| 1280 wide | Medium | Compact | Compact |
| 1600 wide | Wide | Medium | Compact |
| 1920 wide | Wide | Medium | Medium |

This matrix establishes layout-mode selection and source contracts. It does not
replace in-game visual acceptance under the operator's actual DLC, scenario,
font, and UI-scale combination.

## Authored runtime fixture

World identity: `alysaliu|1|Algorab Markab`

Region: `CA-RG-EB596A12`

Candidate: `613b1fe44104`, confirmed, developer exercise

Arrival area: `389638`

Composition: 3 factions, 4 settlements, 9 population groups, local map scale 350

Current keyed runtime file:

`C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml`

The keyed file is 18,826 bytes, last modified 2026-08-11 02:05:16 UTC, at
SHA-256 `728900FBE6ABAC2DA7E29A22BC1B5DEA90494B6EFA0E74485A24C2C9A96C3872`.
It carries culture and political schema 2 while retaining the current plan
identity, region, candidate, arrival area, map scale, factions, settlements,
population assignments, ownership, belief sources, and founding state. All four
receipt suites parse it; the authoring suite verifies stable XML readback.

`C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml`
remains a dormant B2 conversion artifact. The current runtime consumes the keyed
file; the root mirror is not synchronized and is not authority.

## Runtime handoff

B4 is compiled and statically verified but not deployed. RimWorld was closed at
the verification boundary. A deliberate copy of the verified B4 assembly to the
live mod root is required before the operator can test this implementation.

After deployment, the operator runtime gate is:

1. Check Compact, Standard, and Expanded explanation depth without losing any
   warning or changing generated state.
2. Exercise Starting Region at the operator's resolution and UI scale; confirm
   object, map, and inspector modes preserve selection and do not clip.
3. Open Culture for a founder and an established faction; apply, edit, save,
   rename, duplicate, load, and delete profiles, then verify per-field
   provenance and materialized practices.
4. Compare political beliefs with established structure and founding terms;
   confirm normative positions remain distinct from realized or adopted rules.
5. Exercise native Ideoligion preset, fixed, fluid, load, customize, Back, and
   Continue paths; confirm the surrounding CA draft survives without duplicate
   notifications or unrelated established-Ideoligion mutation.
6. Generate the current three-faction/four-settlement plan and confirm the
   cultural furnishing domains materialize independently and report any failed
   placement without suppressing the remaining domains.

Repository verification does not select options, advance the game, alter saves,
deploy the assembly, or substitute for how the flow looks and plays.

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
  no file in B4 replaces them.
