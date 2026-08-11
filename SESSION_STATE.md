| Document | Colonist Awareness Overhaul Session State |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.3.0.0-alpha` · closed batch tip `B6` · next `B7` |
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
| B6 start | Clean B5 governance tip `73efd9dad2ec44de2034d6927e9f0b4c190d4d5d` |
| B6 source closure | `45e571d63c996fdba43cc7101b4edca7ca24f56f` |
| B6 governance closure | The following governance commit carries this state, B6's append-only record, generated projections, and the README twin. |
| Publication | No push or forge publication action is part of B6. |

The portable chronology contains 108 closed batches: `A1-A102` and `B1-B6`.
Thirty-three contiguous version units cover that chronology exactly once; B6 is
minor unit `VU-033`, deriving `1.3.0.0-alpha`. Twelve `TF-*` families and thirty
series-neutral `T-*` threads classify work without changing chronology. `B7` is
the next ordinary batch.

## Current behavior contract

The canonical chain remains:

`Knowledge triggers -> Disposition ranks -> Authority channels -> Doctrine executes through native RimWorld systems`

| Concern | Current rule |
|---|---|
| Initiative | Standard, Proactive, Autonomous. Old 0/1 map to Standard; old 2 to Proactive; old 3 to Autonomous. |
| Standard | Native agency, direct and accepted relayed orders, owned continuation, declared safeguards, survival floor; no open-ended discretionary plan. |
| Proactive | One bounded response to a current actionable fact with finite target, scope, ownership, and completion. |
| Autonomous | Persistent, adaptive, or coordinated action inside delegated or institutional authority. |
| Behavior definitions | 87 stable entries, each with domain, form, permission, tier/authority, evidence, execution, owner, cadence, and termination. |
| Independent causes | Permission, initiative, authority, knowledge, capability, material feasibility, culture, disposition, and player ownership remain separate. |
| Execution | Native jobs, duties, think trees, Lords, interactions, and world simulation remain the substrate. |

The catalog realizes 14 primary domains and 13 behavior forms. Settings are
grouped by domain and ownership kind. The pawn Behavior scope and both developer
censuses are read-only. Player autonomy never controls NPC institutional action.
Culture and disposition rank only otherwise permitted choices.

Squad support has separate owners for hold, field medicine, raid response,
flank/objective work, and command relay. Operational access is colony policy;
personal threat equipment remains pawn-specific. Proactive custody can secure
and stabilize, while ordinary irreversible resolution requires Autonomous
initiative plus valid law or office. All multi-actor and persisted CA work names
the actual actor, behavior, episode, authority, controller or issuer, owner, and
termination.

B5's Culture and spatial contracts remain intact. Player and NPC planning share
typed demands, assets, siting, material checks, and causal ranking. Creation
writes confirmed starting state directly. Player spatial action then requires
delegated authority; later NPC development requires a current named institution.

## Build, receipts, and review

| Evidence | Result |
|---|---|
| Behavior convergence | PASS - 107 numbered cases: 98 statically verified; 9 operator-runtime cases pending |
| Registered behavior domains | AnimalCare 2; CombatSelfPreservation 6; CustodyAndAftermath 5; Diagnostics 2; DomesticAndSpatial 5; HazardResponse 3; InstitutionalDevelopment 2; KnowledgeAndCommunication 9; LogisticsAndProvision 6; OperationalReadiness 6; Presentation 2; SurvivalAndImmediateSafety 7; TacticalCoordination 24; WelfareAndCare 8 |
| Registered behavior forms | AdaptivePlanning 12; CoordinatedResponse 5; Diagnostic 2; DirectOrder 14; ExecutionCapability 4; InstitutionalAction 6; NativeAugmentation 10; Observation 2; PersistentObjective 3; Presentation 2; PreventiveReadiness 5; ReactiveResponse 18; Safeguard 4 |
| Authoring convergence | PASS - 126 mirror and 126 keyed assertions |
| Creation-flow interaction | PASS - 65 assertions |
| Player founding | PASS - 48 assertions |
| World tendencies | PASS - 185 assertions |
| Retained total | PASS - 424 unique assertions; 550 executions |
| Frozen-tree review | Correctness and structural reviewers report no remaining Critical or High findings |
| Release build | Full `--no-incremental` Release build: 0 errors; the same 12 existing warnings |
| Built assembly | 3,215,872 bytes; SHA-256 `8737CDDD1AED9124C6D4A3CCBD738EAF8844E4666A60FCDCFC0AA58830145298` |
| Deployment | RimWorld was closed. The live project DLL was replaced and is byte-identical to the verified build at SHA-256 `8737CDDD1AED9124C6D4A3CCBD738EAF8844E4666A60FCDCFC0AA58830145298`. |

The twelve warnings are the existing member-hiding and DefOf assignment
warnings. No B6 warning or compile error remains. `git diff --check` passes.

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

Active mirror:

`C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml`

Active keyed plan:

`C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml`

Both files are byte-identical at 27,220 bytes and SHA-256
`F60DC069CC30686E4B1694E91B61EF9458F37BD8E6E26FBCF621778FB916D583`.
They parse and round-trip the current schema with the same world, region,
candidate, composition, population assignments, relations, founding state, and
realization hash.

## Operator runtime boundary

The B6 build is deployed and the static gate is complete. Cases 97-105 remain
for the operator in RimWorld:

| Case | Runtime observation |
|---|---|
| 97 | Complete creation and reach map generation. |
| 98 | Confirm player map generation completes and behavior components initialize. |
| 99 | Confirm missing optional assets remain nonfatal and expose their fallback receipt. |
| 100 | Observe ordinary native work at Standard. |
| 101 | Observe one bounded Proactive response. |
| 102 | Observe one persistent Autonomous objective. |
| 103 | Issue direct work around every initiative tier and confirm player authority. |
| 104 | Save, reload, and compare registered intent identity and ownership. |
| 105 | Run through the first in-game hour and inspect `Player.log` for repeating exceptions. |

Static evidence does not select options, advance the game, change saves, or
substitute for how the behavior looks and plays.

## Environment and preserved evidence

- RimWorld target: 1.6.4871 rev590.
- Odyssey is installed; active runtime state is not asserted without a launch.
- `PARALLEL_ONTOLOGY_AUDIT.md` remains a damaged consolidation; its recovered
  full copy remains in the preservation package.
- Preservation package:
  `Projects\colonist-awareness-preservation-20260806-0614Z-2314PDT`, with a
  verified mirror on `D:`.
- The RS-008 source save and recovered documents remain out-of-band evidence.
