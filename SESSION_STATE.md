| Document | Colonist Awareness Overhaul Session State |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.7.0.0-alpha` · closed batch tip `B17` · next `B18` |
<!-- cao:generated:version END -->
| Repository | `SESSION_STATE.md` |
| Status | CLOSED - B17 is built, verified, and deployed; the next action is the operator runtime test. |

# Session State

Updated 2026-08-19 00:10 UTC / 17:10 PST.

Read this before claiming where creation or gameplay testing stands. Compile,
receipts, deployment, and operator runtime evidence remain separate.

## Version control and governed history

| Surface | State |
|---|---|
| Active checkout | `C:\Users\jleyv\Peanut Butter\AI Assisted Software Engineering Mass Repository\Projects\colonist-awareness\.claude\worktrees\rimworld-regional-multithreading-47e9ec` |
| Branch | `mallowfluff/b17-affiliation-epistemics` |
| B16 starting point | `28ed19859382f7ff16315c5f0939c6129aa39803` |
| Closed chronology | `A1-A102` and `B1-B17`; B17 is capability unit `VU-044`; `B18` is next. |
| Version | Semantic replay derives `1.7.0.0-alpha` from 44 contiguous capability units. |
| Publication route | The current branch publishes to protected `main` through the repository's hosted PR checks and squash-merge policy. |

## Current B17 contract

| Surface | Current state |
|---|---|
| Site owner | Every represented inhabited site stores a typed optional faction owner. `None` is a first-class complete value. |
| Material support | A separate typed optional faction reference; support never implies ownership or copies the supporter's society. |
| Resident affiliation | Owned by each population group and independent from site ownership and support. |
| Local society | A factionless or explicitly divergent site owns complete local Political Order, Technological Knowledge, and institutions; Culture and Ideoligion resolve from represented populations. |
| Faction-owned resolution | The owning faction remains canonical unless explicit local divergence exists. Removing an owner snapshots the effective local state exactly once. |
| Site transitions | Cabin, homestead, frontier holding, frontier settlement, and major settlement preserve owner and support; growth creates no faction. |
| Settlement creation | Authored creation authority is joined with current siting and material feasibility. Valid programs execute; changed or impossible proposals fail closed. |
| Broader pawn knowledge | Optional and disabled by default. It extends the existing proposition store with typed per-pawn observations, reports, confidence, uncertainty, contradiction, revision, supersession, staleness, scopes, and provenance. |
| Communication | A report copies the teller's remembered record and records the teller as immediate reporter; it does not query or refresh from live truth. |
| Fact coverage | Fourteen represented fact families use the same proposition contract and fact-specific retention. |

## Governed runtime fixture

| Fact | Value |
|---|---|
| Runtime keyed plan | `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml` |
| Governed mirror | `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml` |
| Pair identity | 679,624 byte-identical bytes; SHA-256 `0381E19688C708DA9AA20E800155F56DD421F64D0E22AB0EBD7B856CA5C6832A` |
| Current schemas | pending authoring epoch 15; regional plan 16; settlement population 2; frontier holding 2; frontier map plan 3; settlement record 10; campaign catalog 6; proposition knowledge 2 |
| Identity | world `alysaliu|1|Algorab Markab`; region `CA-RG-EB596A12`; candidate `613b1fe44104`; arrival tile `389638`; map scale 350 |
| Major composition | 3 factions; 4 settlements; 3 faction relations; 4 settlement population groups; current Culture, Ideoligion, Political Order, Technological Knowledge, institutions, programs, and relationships preserved |
| Frontier composition | 3 holdings; each has no owner, explicit regional material support, resident affiliation, complete local social state, institutions, and residence ledgers |
| Migration evidence | The exact schema-15 predecessor is retained under `Receipts/B17/evidence`; current output reparses and preserves invariant `D926BC4EDE917B42` |

## Verification and deployment

| Gate | Result |
|---|---|
| Affiliation execution | **PASS** - no-owner, support-only, mixed affiliation, local society, frontier transition, migration transaction, and Scribe readback contracts pass |
| Pawn epistemics | **PASS** - default-off, private observation, remembered relay, contradiction, revision, correction, retention, serialization, and inspection pass |
| Fact-family coverage | **PASS** - all 14 represented families use the same typed record and provenance contract |
| Retained suites | **PASS** - B10 75/75; B11 78/78; B12 113/113; B13 67/67; B14 authoring 24/24, generation 12/12, geography 12/12, environment 21/21, Society execution 22/22; B15 full execution; B16 54/54 |
| Behavior convergence | **PASS** - 98 verified assertions; 9 operator-runtime cases pending; 107 numbered cases total |
| Synthetic-state sweep | **PASS** - 184 occurrences classified; 0 unresolved Critical or High findings |
| Persistence census | **PASS** - 261 source-derived carriers; 89 catalog schemas; 5 explicit exclusions; 0 invalid routes |
| Reproducible Release build | **PASS** - .NET SDK 8.0.423; two clean byte-identical builds; 0 warnings and 0 errors; all 20 executable support projects pass; 4,461,056 bytes; SHA-256 `A62E23F78CE97831575D9E5C5FCC90B80D721D8212724D897FB11B82AF68C218` |
| Deployment | **PASS** - RimWorld closed; installed mod junction resolves to the active worktree; tracked and installed assembly views are the same verified file identity |
| Repository gates | **PASS** - version replay, version tests, project census, locked dependencies, portable CI, and repository verification pass |

Current executable receipts in `Receipts/B17` establish source, fixture,
persistence, causal behavior, build, and deployment evidence. They do not
establish how the flow looks or plays.

## Operator runtime boundary

After final deployment, a fresh operator launch is the next evidence boundary.
Codex does not choose values, advance creation, start the game, alter saves, or
claim visual or gameplay acceptance for the operator.

| Runtime focus | Operator check |
|---|---|
| No-faction authoring | Create or edit a major settlement and frontier site with no owner; confirm support and resident affiliations remain separately editable and legible. |
| Local social completeness | Confirm a factionless site retains Culture, Ideoligion, Political Order, Technological Knowledge, institutions, programs, provisions, and development rather than degrading to defaults. |
| Transition continuity | Move between frontier forms and a larger settlement; confirm owner and support do not change unless explicitly edited. |
| Creation execution | Confirm a valid authored program reaches generation while changed, unsited, or materially impossible proposals give a causal refusal. |
| Broader pawn knowledge | With the experimental setting off, confirm standard behavior is unchanged. When explicitly enabled, inspect two pawns receiving different or corrected reports without omniscient synchronization. |
| Current fixture | Confirm the selected region loads with the same world, region, arrival, scale, 3 factions, 4 settlements, 3 frontier holdings, relations, populations, programs, and Ideoligion links. |

## Environment and preserved evidence

- RimWorld target: 1.6.4871 rev591.
- RimWorld remained closed during final builds, deployment verification, and
  publication preparation; Codex did not launch or manipulate the game.
- `B17_SITE_AFFILIATION_AND_EPISTEMIC_CONTRACT.md`,
  `AUTHORING_ONTOLOGY_COVERAGE.md`, `MODULE_OWNERSHIP.md`,
  `SCHEMA_REGISTRY.md`, `PERSISTENCE_CENSUS.md`, and
  `CAMPAIGN_COMPATIBILITY.md` define the current boundary.
- `Receipts/B14`, `Receipts/B15`, and `Receipts/B16` remain the retained
  regional, Society, Technological Knowledge, and playable social-ontology
  floors. Current execution against those contracts is recorded under B17.
- Operator control of time, pawn orders, windows, saves, and autosave remains
  unchanged unless explicitly requested.
