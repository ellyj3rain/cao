| Document | Colonist Awareness Overhaul Session State |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.3.0.1-alpha` · closed batch tip `B7` · next `B8` |
<!-- cao:generated:version END -->
| Repository | `SESSION_STATE.md` |
| Status | ACTIVE - current operational state. |

# Session state

Updated 2026-08-11 UTC / 2026-08-11 PST.

Read this before claiming where creation or gameplay testing stands. Compile,
receipt, deployment, and operator runtime evidence remain separate.

## Version control and governed history

| Surface | State |
|---|---|
| Branch | `mallowfluff/b3-creation-flow-interaction` |
| B7 baseline | Exact pushed B6 governance tip `ff1d7659e48baad03ca1e02b018a5f951e29befe` |
| B7 source closure | `3043e6b737472ffb34cf47d856b880c4e6a07811` |
| B7 governance closure | The following governance commit carries this state, B7's append-only record, generated projections, and the README twin. |
| Publication | No push or forge publication action is part of B7. |

The portable chronology contains 109 closed batches: `A1-A102` and `B1-B7`.
Thirty-four contiguous version units cover that chronology exactly once; B7 is
patch unit `VU-034`, deriving `1.3.0.1-alpha`. Twelve `TF-*` families and
thirty series-neutral `T-*` threads classify work without changing chronology.
`B8` is the next ordinary batch.

## Current creation and authoring contract

Creation exposes direct facts and inspects derived consequences. Starting Region
retains the selected region and arrival area, map, factions, settlements,
locations, population groups and sources, ownership, relations, provisions, and
sparse concrete starting exceptions. Generic development, transport, local
services, civic capacity, research capability, facility profiles, per-facility
Generated/Include/Omit rows, and global Compact/Standard/Expanded information
detail are not ordinary authoring objects.

Settlement scale, infrastructure, access, services, facilities, knowledge and
research capability, spatial form, and other material conclusions are realized
from actual population, land, geography, technology, knowledge, institutions,
economy, trade, material state, relationships, and history. Contextual
explanations belong to the inspected object or decision that owns them.

The founding flow coordinates four distinct concepts:

| Concept | Current meaning |
|---|---|
| Culture | Inherited and local longitudinal social history |
| Ideoligion | RimWorld's native religious, ritual, and moral system |
| Political Beliefs | What the founders consider legitimate, proper, permitted, or obligatory |
| Rules at landing | The authority, work, voice, and supply arrangement actually adopted at founding |

Political Beliefs remain normative. Adopted rules, institutions, and lived
practice remain realized state. Agreement and contradiction both survive.

Existing and founding societies share this ontology at different temporal
boundaries. Established settlements explicitly predate the scenario and may
carry mature institutions and local Culture without invented event records. New
founders bring inherited state and establish initial rules; local institutions
and Culture develop through play. A typed established-player-start scenario is
the only current exception to that founding boundary.

## Persistent Culture contract

Each settlement Culture saves stable inherited and local identities,
constituent Cultures and population shares, typed observations of lived history,
recognized practices and their sources, transition sequence, predecessor and
evidence signatures, changed domains, and temporal provenance.

The bounded world-simulation behavior `culture.longitudinal_update` evaluates
historically meaningful changes. Inspection and drawing never advance Culture.
Unchanged history does not manufacture transitions. Qualified lived practice or
elapsed population, institutional, and political changes deterministically
produce successor state.

Production consumers use the same pure causal kernel:

| Consumer family | Current use |
|---|---|
| Spatial | Placement and road/development ranking |
| Social | Ordinary gathering preference |
| Institutional | Legitimacy, friction, and research priority |
| Political | Belief/practice contradiction and habituation |
| Settlement development | Player-delegated and NPC institutional choice among otherwise valid candidates |

Culture ranks permitted and feasible possibilities. It does not create
authority, knowledge, technology, land, labor, materials, treasury, or native
execution capability. Cultural expression is a nonmutating interpretation of
Culture in its current society. Native `CultureDef` remains optional visual
inheritance, not Culture itself.

## Build, receipts, and review

| Evidence | Result |
|---|---|
| B7 authoring acceptance | PASS - 50 creation-ontology assertions |
| Longitudinal Culture | PASS - 15 causal assertions |
| Creation-flow interaction | PASS - 65 assertions |
| Player founding | PASS - 48 assertions |
| World tendencies | PASS - 185 assertions |
| Behavior convergence | PASS - 107 numbered cases: 98 statically verified; 9 operator-runtime observations pending |
| Frozen-tree reviews | Causality, ontology, UI, and playability: no unresolved Critical or High source finding |
| Release build | Full Release rebuild: 0 errors; the same 12 inherited warnings |
| Built assembly | 3,255,808 bytes; SHA-256 `2CF3982C3ECA6E83D335BD7BCEEDF0E4BE98DDF2B218A0DAF854F72EB630A4E1` |
| Deployment | RimWorld was closed. The active project DLL is byte-identical to the verified build at SHA-256 `2CF3982C3ECA6E83D335BD7BCEEDF0E4BE98DDF2B218A0DAF854F72EB630A4E1`. |

The twelve warnings are the existing member-hiding and DefOf assignment
warnings. No B7 compile error remains. `git diff --check` passes with only
working-tree line-ending notices.

## Authored runtime fixture

| Field | Current authority |
|---|---|
| World | `alysaliu|1|Algorab Markab` |
| Region | `CA-RG-EB596A12` |
| Candidate | `613b1fe44104`, confirmed |
| Arrival/root tile | `389638` |
| Map scale | 350 |
| Composition | 3 factions, 4 settlements, 9 population groups |
| Schema | Regional plan 5; Culture 5; political beliefs 2 |
| Temporal basis | Four existing settlements are explicitly established before scenario start; no synthetic event history was invented |
| Compatibility | `developerExercise=true`; transient runtime materialization only; durable saving disabled |

Active mirror:

`C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml`

- 33,209 bytes
- SHA-256 `849DA7D8EF3976DD3BB7C9BFBDF6A12CEDB2A3FA4DB651F42FD82E7D80A35A82`

Active keyed plan:

`C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml`

- 34,065 bytes
- SHA-256 `A300889CE4B19445F8CEE28B28A1383CA3B42D65245BACCB46FE9EE951DEFDE9`

Both files parse and round-trip the current schema with the same world, region,
candidate, composition, population assignments, relations, founding state, and
Culture state. Their normalized XML content is identical. Their byte hashes
differ because the keyed surface retains CRLF while the active mirror retains
LF; this is formatting, not state divergence.

The selected region contains unsupported world mutators. B7 preserves that
truth through the explicit transient developer-exercise route: the plan is
registered for this run, resolved, projected, and materialized, but not scribed.
The compatibility backstop warns and continues only for that explicit stamp.
Durably incompatible plans still fail.

## Operator runtime boundary

The B7 build is deployed and the static gate is complete. The selected fixture is
ready for disposable runtime cases 97-103 and 105:

| Case | Runtime observation |
|---|---|
| 97 | Complete creation and reach map generation. |
| 98 | Confirm player map generation completes and behavior components initialize. |
| 99 | Confirm missing optional assets remain nonfatal and expose their fallback receipt. |
| 100 | Observe ordinary native work at Standard. |
| 101 | Observe one bounded Proactive response. |
| 102 | Observe one persistent Autonomous objective. |
| 103 | Issue direct work around every initiative tier and confirm player authority. |
| 104 | Not runnable on this transient fixture. Use a later compatible-region save/reload run to compare registered intent identity and ownership. |
| 105 | Run through the first in-game hour and inspect `Player.log` for repeating exceptions. |

Static evidence does not select options, advance the game, change saves, or
substitute for how the creation flow and generated colony look and play.

## Environment and preserved evidence

- RimWorld target: 1.6.4871 rev590.
- Odyssey is installed; active runtime state is not asserted without a launch.
- `PARALLEL_ONTOLOGY_AUDIT.md` remains a damaged consolidation; its recovered
  full copy remains in the preservation package.
- Preservation package:
  `Projects\colonist-awareness-preservation-20260806-0614Z-2314PDT`, with a
  verified mirror on `D:`.
- The RS-008 source save and recovered documents remain out-of-band evidence.
