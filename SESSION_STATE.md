| Document | Colonist Awareness Overhaul Session State |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `0.12.4.0-alpha` · closed batch tip `B1` · next `B2` |
<!-- cao:generated:version END -->
| Repository | `SESSION_STATE.md` |
| Status | ACTIVE — current operational state. |

# Session state

Updated 2026-08-10 12:48 UTC / 05:48 PST.

Read this before claiming where the regional work stands. Compile evidence and
operator runtime judgment are separate.

## Version control

| Surface | State |
|---|---|
| Canonical project state | The current tracked local tree and governed records. `git status --short` identifies checkout-local changes. |
| Development closure | `B1` closes the causal World tendencies convergence; `B2` is next. The closing commit is recorded in the batch file. |
| Authority branch | `mallowfluff/world-tendencies-causal-convergence`, based on current GitHub `origin/main` `9a6153a8ce7282c241e90ad8459cb92c88468a78`. |
| Local Git history | May retain engineering history from before publication. It is supporting evidence, not the portable project history. |
| Published forge | `origin/main` at `git@github.com:ellyj3rain/cao.git`; publication history begins at parentless snapshot `18b1034eee35f21158817727f4bc39af80dadd2a`. |

The project root is the live mod junction. Linked checkouts are temporary
execution surfaces and are not part of the project's identity.

## Development record

The A sequence is closed at `A102`; the chronology continues through closed
`B1`, and `B2` is the next development batch.
`BATCH_LOG.md` is the chronological index. Every batch record lives directly
under `Batches/`, including future letter eras. `Batches/THREADS.md` classifies
work across time without changing chronology. `VERSION_MAP.md` partitions the
closed chronology into contiguous capability units and derives `VERSION`.

| Evidence | State |
|---|---|
| Chronology | 103 dated batches, `A1` through `A102`, followed by `B1` |
| Thematic organization | 12 series-neutral `TF-*` families and 30 many-to-many `T-*` threads; temporary identifiers have a complete crosswalk |
| Version chronology | 28 contiguous units cover A1-B1 exactly once; Neo hierarchy and caps derive `0.12.4.0-alpha` |
| Former ledger map | All 93 former boundaries mapped; split and joined boundaries are explicit |
| Provenance | Associated commits, builds, receipts, corrections, and source relations are recorded per batch where available |

Closed batch records and their recorded provenance are the portable project
history. Chronological and thematic navigation and the former-label map are
regulatory. Local Git may retain finer-grained engineering history and superseded
catalog machinery. The published forge starts at the canonical snapshot and does
not need the earlier local commit graph.

## Current regional model

| Level | Current saved concepts |
|---|---|
| Region | selected world areas, arrival area, causal world-policy snapshot, source-settlement receipts, realized faction relations, frontier holdings, settlement pattern, relation pattern, settlement scale |
| Faction | source faction, culture, Ideoligion, political beliefs, faction structure, settlement authority, faction era |
| Settlement | owner, world area, regional role, form, resident population, land capacity, access, services, civic development, economic capacity, trade connectivity, specialization, history, urban support, realized scale, population groups, starting facilities, starting provisions |
| Organization | offices, groups, customs, security, agreements, policies, claims, relations, decision history |
| Frontier holding | member area, household size, land capacity, material level, site form, faction state |
| Relations | one saved canonical faction-pair result; organization relations for settlement authority and delegation |

The same vocabulary is used by setup UI, generation, save fields, types, Defs,
comments, and canonical documentation. Each World tendencies row owns one direct
cause. Concentration changes settlement placement before pattern classification;
urban propensity changes a support threshold rather than city status; frontier
frequency owns count and frontier size owns household and material form. Realized
facts are saved once and runtime consumers do not reroll policy. Capability is
derived from faction era and local supports. Starting provisions are generated
from current causes and allow a distribution override per provision.

The convergence pass removes the superseded political tier, territorial
constitution, old settlement population model, duplicate federation caches, custom
political precepts, separate belief state, derived ideology classifier, old coast
modes, old settlement type names, and migration or alias code used only by
abandoned pre-release schemas. Earlier experimental saves are not supported.

## Build and deployment

| Artifact | SHA-256 | State |
|---|---|---|
| Built `Assemblies/ColonistAwareness.dll` | `D7AE79BFC5316A8330FDE8C88CC077D56CEB1686B53E9A140D88CDA4736669D3` | Two full no-incremental Release builds were byte-identical: 0 errors and the same 12 existing warnings. |
| Live root `Assemblies/ColonistAwareness.dll` | `D7AE79BFC5316A8330FDE8C88CC077D56CEB1686B53E9A140D88CDA4736669D3` | Deployed from the verified build and byte-identical. RimWorld was closed during deployment and has not run against this assembly yet. |

No current regional, political, onboarding, or generation result is
runtime-verified. The next evidence belongs to the operator's in-game test.

Static verification is closed for this gate: the initial three-lane review and
final coherence re-audit have no outstanding findings; the independent build
validator confirms two reproducible Release builds; all 141 tracked XML files,
including 120 Def files, and both active fixtures parse; 103 batch records, 30
thematic threads, and 28 contiguous version units reconcile; the version tests
pass 4/4; `git diff --check` reports no whitespace errors; and the causal suite
passes 179 assertions.

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

Both active files have SHA-256
`4D90AD4738F3B0E983BB7D1BC2DBA9E26C984BDE48A157263B913393D735579A`.
They use schema 2 and agree on the causal world-policy snapshot, realization
source hash, 3 factions, 4 settlements, 9 population groups, saved relations,
frontier rows, economic capacity, urban support, and realized scale. The prior
tendency preset is now explicit, so new band-aligned defaults cannot change the
confirmed fixture. Starting provisions are intentionally regenerated from current
settlement and faction facts instead of carrying former stacked entries.

A pre-convergence copy of the dirty implementation and original fixture exists
at:

`C:\Users\jleyv\AppData\Local\Temp\ca-convergence-preclean-20260809-121414`

## Runtime handoff

The operator's next test is the gate:

1. Open the existing world setup flow and confirm that the keyed composition is
   restored with the same arrival area, options, factions, and settlements.
2. Inspect World tendencies and confirm that each row states its direct cause,
   constraints, and derived outcome in the corrected settlement/frontier flow.
3. Start generation and confirm that no incomplete-bundle or missing-arrival
   error appears.
4. Compare the generated visual land, coast, selected geographic features,
   settlement positions, factions, residents, facilities, and provisions with
   the authored candidate.

Repository convergence stops before this test. It does not launch RimWorld,
select options, advance the game, or alter saves. The live DLL is already deployed
for the operator's test.

## Environment and preserved evidence

- RimWorld target: 1.6.4871 rev590.
- Odyssey is installed. Its active runtime state is not asserted without a game
  launch.
- `PARALLEL_ONTOLOGY_AUDIT.md` remains a damaged consolidation; the recovered
  full copy is held in the preservation package.
- Preservation package:
  `Projects\colonist-awareness-preservation-20260806-0614Z-2314PDT`, with a
  verified mirror on `D:`.
- The RS-008 source save and recovered documents remain out-of-band evidence;
  no file in this convergence pass replaces them.
