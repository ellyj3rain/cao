| Document | Colonist Awareness Overhaul Session State |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.6.1.0-alpha` · closed batch tip `B16` · next `B17` |
<!-- cao:generated:version END -->
| Repository | `SESSION_STATE.md` |
| Status | CLOSED - B16 is built, verified, and deployed; the next action is the operator runtime test. |

# Session State

Updated 2026-08-18 UTC / 2026-08-18 PDT.

Read this before claiming where creation or gameplay testing stands. Compile,
receipts, deployment, and operator runtime evidence remain separate.

## Version control and governed history

| Surface | State |
|---|---|
| Active checkout | B16 worktree carrying pure `[REPO]` CI maintenance |
| Branch | `mallowfluff/repo-ci-foundation` |
| B16 merged baseline | `788fcd1ed009010f3e12a7dfbfaccde003e456d1` (PR `#4`) |
| Closed chronology | `A1-A102` and `B1-B16`; B16 is kohai capability unit `VU-043`; `B17` is next. |
| Publication maintenance | PR `#5` hosted checks pass (`ci-verify`, `dependency-scan`, and CodeQL); merge and `main` protection remain. No runtime, schema, fixture, version, or authored-state changes. |

## Current B16 contract

| Surface | Current state |
|---|---|
| Ideoligion | Native Ideoligion owns and executes explicit doctrine, sacred or prohibited conduct, ritual, memes, precepts, roles, and religious or social prescriptions. |
| Culture | Population-level appraisal, normative distribution, disagreement, confidence, and historical drift are owned independently from doctrine and conduct. Registry 3 contains 48 questions in 12 player decision categories. |
| Political Order | Faction-owned beliefs about legitimate political and economic arrangements remain independent from represented institutions. |
| Represented institutions | Rules and mechanisms actually in force remain factual state rather than duplicate Political Order controls. |
| Practice | Native events remain occurrences with exact provenance. Only repeated represented conduct can become practice evidence, and neither occurrence nor practice proves Culture approval. |
| Semantic adapters | Known definitions map only through exact package ID, definition kind, and `defName`. Unknown or target-specific doctrine remains native-only until its semantics are explicitly represented. Names never manufacture mappings. |
| Native events | Exact HistoryEvent adapters observe native execution afterward. Occurrence identity preserves participant or target provenance; retention is bounded by distinct occurrence within map, faction, locality, and practice. |
| Ownership | `CAFactionState` owns Culture, Political Order, and Technological Knowledge. Society composes them; Society presets snapshot and copy them without becoming runtime owners. |
| Supported extension | Audited `Ideology: More Precepts` definitions use the same exact registry seam. Unsupported or unknown mod definitions remain native Ideoligion facts. |

## Governed runtime fixture

| Fact | Value |
|---|---|
| Runtime keyed plan | `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml` |
| Governed mirror | `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml` |
| Pair identity | 530,039 byte-identical bytes; SHA-256 `004C5A0F2505594D36E89CBD0F02A4BBE46C1B044965E6A5F29AFD27AFC521AF` |
| Pending-plan schema | pending authoring epoch 14; regional plan 15; faction/founding/regional Culture owners 4; map-longitudinal owner 3; Culture 11 / registry 3; Political Order 10; Technological Knowledge 1 |
| Realized-campaign boundary | campaign catalog 5 binds the current owner schemas after realization; it is not serialized into the pending-plan XML |
| Culture content | 8 Culture owners; 12 valid owner/scope pairs each contain all 48 registry-3 questions; 577 live rows total; 289 compatible pre-B16 rows remain exact and the one non-constituent sparse row is preserved only as legacy evidence |
| Identity | world `alysaliu|1|Algorab Markab`; region `CA-RG-EB596A12`; candidate `613b1fe44104`; arrival tile `389638`; map scale 350 |
| Composition | 3 factions; 4 settlements; 3 faction relations; current populations, Ideoligion links, Political Orders, Technological Knowledge, and established programs preserved |

## Verification and deployment

| Gate | Result |
|---|---|
| B16 semantic acceptance | **PASS** - 54/54 exact doctrine, Culture appraisal, native-event, occurrence, locality, migration, rollback, and current-fixture checks |
| Playable ontology census | **PASS** - every loaded audited precept and meme definition is individually adapted or explicitly native-only; no family member can conceal an unresolved sibling |
| Culture migration | **PASS** - every compatible registry-2 row preserves its exact prior value and 24 new rows enter every valid represented scope as neutral, low-confidence, explicitly unobserved state; the governed fixture's one sparse row for a population no longer present in its settlement is removed from live coverage and retained as exact legacy evidence |
| Owner migration and rollback | **PASS** - faction, founding, regional, and map-longitudinal owners survive Scribe readback and publish no partial state after invalid nested Culture |
| Native practice retention | **PASS** - duplicate participant or observer emissions remain attached to one occurrence; 64 distinct occurrences are retained per map/faction/locality/practice without cross-settlement eviction |
| Retained suites | **PASS** - B10 synthetic 166 classified / 0 unresolved Critical or High; B10 75/75; B11 78/78; B12 113/113; B13 67/67; B14 24/24 plus Society execution; B15 full execution |
| Persistence census | **PASS** - 258 source-derived carriers; 87 catalog schemas; 5 explicit non-campaign exclusions; 0 unclassified or invalid routes |
| Reproducible Release build | **PASS** - the `[REPO]` compiler baseline pins .NET SDK 8.0.423 and the complete production dependency graph; two clean Release rebuilds, 0 warnings and 0 errors, emitted byte-identical 4,368,384-byte assemblies at SHA-256 `2710DDDAC506B4BA6910456F4EF965207C54431EA2D25E53CAFAA78E20047545`; 18 declared support projects compile and both portable generated censuses reproduce without drift |
| Deployment | **PASS** - RimWorld closed; both clean candidates, worktree assembly, and installed junction view are byte-identical at the current build identity above; B16 source behavior is unchanged |

Current executable receipts in `Receipts/B16` establish source, persistence,
and causal data behavior. They do not establish how the flow looks or plays.
`Receipts/REPO/20260818-1729Z-1029PST-CI_FOUNDATION_RECEIPT.md` records the
compiler, dependency, reproducibility, executable-tool, security, and forge gates.

## Operator runtime boundary

After final deployment, a fresh operator launch is the next evidence boundary.
Codex does not choose values, advance creation, start the game, alter saves, or
claim visual or gameplay acceptance for the operator.

| Runtime focus | Operator check |
|---|---|
| Ownership separation | Confirm native Ideoligion remains intact while Culture separately shows population appraisal; changing one does not silently rewrite the other. |
| Cannibalism example | Confirm doctrine, Culture appraisal, and actual eating or butchery can agree or conflict and remain separately inspectable. |
| Culture authoring | Confirm all 12 decision categories are comprehensible, the 48-question expansion does not expose registry language, and generated or preset state remains editable. |
| Society continuity | Apply a Society and confirm Culture, Political Order, and Technological Knowledge change together while Ideoligion and represented institutions remain independent. |
| Current fixture | Confirm the selected region loads with the same world, region, arrival, scale, 3 factions, 4 settlements, relations, populations, programs, and Ideoligion links. |
| B14-B15 regressions | Confirm regional placement, Society flow, Political Order, represented institutions, difficult-biome viability, and technological availability remain intact. |

## Environment and preserved evidence

- RimWorld target: 1.6.4871 rev591.
- RimWorld was closed during fixture conversion, clean builds, and deployment;
  Codex did not launch or manipulate the game afterward.
- `B16_PLAYABLE_SOCIAL_ONTOLOGY_AUDIT.md`,
  `AUTHORING_ONTOLOGY_COVERAGE.md`, `MODULE_OWNERSHIP.md`,
  `SCHEMA_REGISTRY.md`, `PERSISTENCE_CENSUS.md`, and
  `CAMPAIGN_COMPATIBILITY.md` are the current canonical boundaries.
- `Receipts/B14` remains the regional and Society floor.
  `Receipts/B15` remains the Technological Knowledge floor.
  `Receipts/B16` contains semantic coverage, migration, fixture,
  retained-suite, review, build, and deployment evidence.
- Operator control of time, pawn orders, windows, saves, and autosave remains
  unchanged unless explicitly requested.
