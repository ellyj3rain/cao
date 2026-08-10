| Document | Colonist Awareness Overhaul Session State |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `0.12.3.0-alpha` · A sequence closed at `A102` · next `B1` |
<!-- cao:generated:version END -->
| Repository | `SESSION_STATE.md` |
| Status | ACTIVE — current operational state. |

# Session state

Updated 2026-08-10 09:35 UTC / 02:35 PST.

Read this before claiming where the regional work stands. Compile evidence and
operator runtime judgment are separate.

## Version control

| Surface | State |
|---|---|
| Canonical project state | The current tracked local tree and governed records. `git status --short` identifies checkout-local changes. |
| Development closure | The final development batch, `A102`, closes at local engineering commit `03df661256da7b13ef20a5877d2f47c0af1c597b`; later `[REPO]` maintenance does not consume `B1`. |
| Local Git history | May retain engineering history from before publication. It is supporting evidence, not the portable project history. |
| Published forge | `origin/main` at `git@github.com:ellyj3rain/cao.git`; publication history begins at parentless snapshot `18b1034eee35f21158817727f4bc39af80dadd2a`. |

The project root is the live mod junction. Linked checkouts are temporary
execution surfaces and are not part of the project's identity.

## Development record

The A sequence is closed at `A102`; `B1` is the next development batch.
`BATCH_LOG.md` is the chronological index. Every batch record lives directly
under `Batches/`, including future letter eras. `Batches/THREADS.md` classifies
work across time without changing chronology. `VERSION_MAP.md` partitions the
closed chronology into contiguous capability units and derives `VERSION`.

| Evidence | State |
|---|---|
| Chronology | 102 dated batches, `A1` through `A102` |
| Thematic organization | 12 series-neutral `TF-*` families and 30 many-to-many `T-*` threads; temporary identifiers have a complete crosswalk |
| Version chronology | 27 contiguous units cover A1-A102 exactly once; Neo hierarchy and caps derive `0.12.3.0-alpha` |
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
| Region | selected world areas, arrival area, world tendencies, settlement pattern, settlement scale |
| Faction | source faction, culture, Ideoligion, political beliefs, faction structure, settlement authority, faction era |
| Settlement | owner, world area, regional role, form, starting facilities, access, services, civic development, population groups, starting provisions |
| Organization | offices, groups, customs, security, agreements, policies, claims, relations, decision history |
| Relations | faction-to-faction relations; organization relations for settlement authority and delegation |

The same vocabulary is used by setup UI, generation, save fields, types, Defs,
comments, and canonical documentation. Capability is derived from faction era and
local supports. Starting provisions are generated from current causes and allow a
distribution override per provision.

The convergence pass removes the superseded political tier, territorial
constitution, old settlement population model, duplicate federation caches, custom
political precepts, separate belief state, derived ideology classifier, old coast
modes, old settlement type names, and migration or alias code used only by
abandoned pre-release schemas. Earlier experimental saves are not supported.

## Build and deployment

| Artifact | SHA-256 | State |
|---|---|---|
| Built `Assemblies/ColonistAwareness.dll` | `2C409E448CE5CB90C1D5E34607913AA188D17E583530BAEB3AF3622AEDFA0A01` | Final convergence artifact. Clean Release builds produced 0 errors and the same 12 existing warnings. |
| Live root `Assemblies/ColonistAwareness.dll` | `2C409E448CE5CB90C1D5E34607913AA188D17E583530BAEB3AF3622AEDFA0A01` | Deployed and byte-identical to the built artifact. RimWorld has not run against it yet. |

No current regional, political, onboarding, or generation result is
runtime-verified. The next evidence belongs to the operator's in-game test.

Static verification is closed for this gate: three independent coherence lanes
reviewed the repository convergence; all 141 tracked XML files, including 120 Def
files, parse; 102 batch records, 93 former boundaries, 30 thematic threads, and
285 associated commits reconcile; retired vocabulary and filenames scan clean;
and the Release build completes with 0 errors and the same 12 existing warnings.
`README.docx` has native headings, lists, code styling, table geometry, and eight
hyperlinks with complete Markdown-source parity. LibreOffice is unavailable and
the Word fallback did not complete, so page-image inspection is not claimed.

## Authored runtime fixture

World identity: `alysaliu|1|Algorab Markab`
Region: `CA-RG-EB596A12`
Candidate: `613b1fe44104`, confirmed, developer exercise
Arrival area: `389638`
Composition: 3 factions, 4 settlements, 9 population groups, local map scale 350

Current keyed file:

`C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml`

Converted backup mirror:

`C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml`

Both converted files have SHA-256
`588812BA0BB125FF0B196F5DBDF2B1A0F5D35CBF82B3666F8D136870228C88EE`.
They use the current schema. Starting provisions are intentionally regenerated
from the current faction, facility, infrastructure, scale, role, and population
parameters instead of carrying the former stacked entries.

A pre-convergence copy of the dirty implementation and original fixture exists
at:

`C:\Users\jleyv\AppData\Local\Temp\ca-convergence-preclean-20260809-121414`

## Runtime handoff

The operator's next test is the gate:

1. Open the existing world setup flow and confirm that the keyed composition is
   restored with the same arrival area and options.
2. Start generation and confirm that no incomplete-bundle or missing-arrival
   error appears.
3. Compare the generated visual land, coast, selected geographic features,
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
