| Document | Colonist Awareness Overhaul Session State |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.3.0.5-alpha` · closed batch tip `B11` · next `B12` |
<!-- cao:generated:version END -->
| Repository | `SESSION_STATE.md` |
| Status | ACTIVE - current operational state. |

# Session State

Updated 2026-08-13 UTC / 2026-08-13 PDT.

Read this before claiming where creation or gameplay testing stands. Compile,
receipts, review, deployment, and operator runtime evidence remain separate.

## Version Control and Governed History

| Surface | State |
|---|---|
| Branch | `mallowfluff/b3-creation-flow-interaction` |
| Exact B11 baseline | `bd7f757e4825a921c4449062d41df45e474805c1` |
| B11 source closure | `d83fbadb4eec2625ba985fb7fd6f64365bd9c313` (`[B11] source: close durable campaign and compositional authoring`) |
| B11 governance closure | `8abfd501a9887748b5ca78bd02bf7aa92bebc549` (`[B11] governance: ratify durable campaign and authoring closure`) |
| B11 deployment receipt | PASS - `B11_DEPLOYMENT_RECEIPT.md` records closed-process replacement and an exact source/target byte comparison. |

The portable chronology contains 113 closed batches: `A1-A102` and `B1-B11`.
Thirty-eight contiguous version units cover that chronology exactly once. B11 is
`VU-038`, a patch unit deriving `1.3.0.5-alpha`. Series-neutral `T-*` threads
classify work without changing chronology. `B12` remains the next ordinary batch.

## Current B11 Contract

| Surface | Current state |
|---|---|
| Campaign durability | Preflight before Scribe load; complete executable schema manifest; stable identities; idempotent supported migration; truthful additive provenance; visible unsupported-state failure |
| Pending authoring | Separate epoch 11; replaceable before confirmation and unable to erase realized campaign history |
| Module ownership | One semantic, mutation, and cadence owner; subordinate modules follow state/lifecycle/cadence/failure boundaries rather than line count |
| Runtime observability | Fixed-key, bounded, disabled-by-default, resettable profiler; no saved state or semantic influence |
| Social ontology | 45 production social referents; meanings are population-scoped evaluations; 30 distinct concrete repeated practices carry evidence and consumers |
| Political ontology | Political Beliefs are normative and current order is instituted; several compatible mechanisms may coexist per subject |
| Presets | 12 partial belief sets and 10 partial current-order sets; copy-on-apply adds only listed mechanisms and preserves unrelated state |
| Authoring surface | One coherent Culture composer and content-driven political sections; no source-domain, empty, one-item, or duplicate schema-shaped navigation |
| Causal floor | All B10 causal owners remain in force: no hash/random/tendency/read-model substitute for memberships, institutions, authority, knowledge, capability, programs, or history |

## Governed Runtime Fixture

| Fact | Value |
|---|---|
| Active plan | `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml` |
| Keyed mirror | `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml` |
| Fixture SHA-256 | `F37218C5361B1112EBEA88FC31065C0C402A3ABE96FEE420D101D4956339DF33` on both files |
| File size | 66,670 bytes on both files |
| Schema | pending authoring epoch 11; regional plan 11; settlement record 8; program and entry 4; operational fact 3; program asset 1; provision 5; Culture and Political Beliefs 9 |
| Identity | world `alysaliu|1|Algorab Markab`; region `CA-RG-EB596A12`; candidate `613b1fe44104`; arrival tile `389638`; map scale 350 |
| Composition | 3 factions; 4 settlements; 9 population groups; 19 explicit established-program facts |
| Culture conversion | 8 practice rows use concrete `practiceKey` and `sourceOwner`; no practice is stored as a social-subject key |
| Political conversion | Exact B10 ownership/economy coexistence expands to every named mechanism; ambiguous B10 support is rejected before owner load rather than guessed |

The recovery masks remain historical evidence only. The fixture generator uses
production formation contracts, preserves the intended composition and current
identity, and omits unsupported or derived state. Active and mirror files parse,
round-trip, and agree byte for byte.

## Verification and Deployment

| Gate | Result |
|---|---|
| Ontology coverage | PASS - 36 mechanically observable fact families classified; 0 unclassified core mechanics; 45 subjects and 30 practices each have source and consumer |
| B10 acceptance | PASS - 75/75 retained causal receipts |
| B10 synthetic-state sweep | PASS - 152 active occurrences classified; 0 unresolved Critical/High |
| B11 acceptance | **78/78 PASS** |
| Five-lens review | **PASS** - 0 Critical, 0 High, 0 Medium, 0 Low |
| Release build | **PASS** - 0 warnings, 0 errors against source commit `d83fbad` |
| Assembly | 3,670,528 bytes; SHA-256 `2848F2B481F672DD7F50C288D97A34FA77FC5DCB18778F9F463606793D8E70C1` |
| Deployment | **PASS** - RimWorld was closed; source and active target are byte-identical at 3,670,528 bytes and SHA-256 `2848F2B481F672DD7F50C288D97A34FA77FC5DCB18778F9F463606793D8E70C1` |

Static evidence establishes the source, serialization, fixture, and deployment
candidate. It does not select options, advance the game, or establish visual and
gameplay acceptance.

## Operator Runtime Boundary

B11 ends at the durable campaign-start boundary. Publication and byte-verified
deployment are complete; the next runtime action is the operator test. Codex does not
advance creation, choose authoring values, start the game, alter saves, or claim
how the game looks and plays on the operator's behalf.

| Observation | Operator check |
|---|---|
| Creation load | Open the current pending plan and confirm all 3 factions, 4 settlements, 9 population groups, and 19 established operations remain present. |
| Culture composer | Confirm constituents, meanings, and concrete practices are distinct, relevant, navigable, and editable without source-domain or one-item tabs. |
| Political composer | Confirm beliefs and current order have distinct labels and edits; compatible mechanisms coexist; applying a partial set preserves unrelated facts. |
| Starting Region | Confirm object selection, map placement, details, relations, programs, provisions, and authored composition remain usable. |
| Generation | Start the selected campaign and confirm persisted state materializes without rerolling or dropping the composition. |
| Runtime health | Inspect initial play for errors, dropped work, impossible access, synthetic relationships, or unbounded stalls; export profiler evidence only if useful. |
| First retained save | Save the campaign after successful validation and retain it as the first live B11 durability baseline. |
| Future update proof | On a later CAO update, close RimWorld, back up the save, deploy the verified DLL, preflight, load the same campaign, validate migration, and save only after success. |

## Environment and Preserved Evidence

- RimWorld target: 1.6.4871 rev590.
- RimWorld was closed before B11 assembly replacement and remained closed for
  the post-copy byte comparison.
- `AUTHORING_ONTOLOGY_COVERAGE.md`, `MODULE_OWNERSHIP.md`,
  `SCHEMA_REGISTRY.md`, `PERSISTENCE_CENSUS.md`, and
  `CAMPAIGN_COMPATIBILITY.md` are current canonical
  boundaries.
- `B10_CAUSAL_PROVENANCE_AUDIT.md`, `B10_ACCEPTANCE_RECEIPTS.md`, and
  `B10_SYNTHETIC_STATE_SWEEP.md` remain the causal regression evidence.
- Operator control of time, pawn orders, windows, saves, and autosave remains
  unchanged unless explicitly requested.
