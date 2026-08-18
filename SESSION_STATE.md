| Document | Colonist Awareness Overhaul Session State |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.6.0.0-alpha` · closed batch tip `B15` · next `B16` |
<!-- cao:generated:version END -->
| Repository | `SESSION_STATE.md` |
| Status | CLOSING - B15 implementation, real-fixture conversion, retained verification, reproducible build, and closed-process deployment pass; final review and publication are in progress. |

# Session State

Updated 2026-08-18 UTC / 2026-08-17 PDT.

Read this before claiming where creation or gameplay testing stands. Compile,
receipts, deployment, and operator runtime evidence remain separate.

## Version control and governed history

| Surface | State |
|---|---|
| Active worktree | `.claude/worktrees/rimworld-regional-multithreading-47e9ec` |
| Branch | `mallowfluff/b14-generation-political-audit` |
| B15 baseline HEAD | `e1156de15dd91605f6d27190d770d118ed3c8624` |
| Closed chronology | `A1-A102` and `B1-B15`; B15 is minor capability unit `VU-042`; `B16` is next. |

## Current B15 contract

| Surface | Current state |
|---|---|
| Canonical ownership | `CAFactionState` owns Culture, Political Order, and Technological Knowledge. Society is their joint control surface; a Society preset is a reusable snapshot. Neither becomes a campaign owner. |
| Authoring flow | Founding and regional plans stage all three values before faction realization. Confirmation validates them together and copies them once into the realized faction. Settlements reference faction knowledge unless an explicit local divergence is represented. |
| Society presets | The shared 22-entry catalog and reusable user profiles snapshot all three components. Apply validates deep copies, commits atomically, restores all three on failure, and writes no preset ownership. Culture and Political Order component presets remain independent substitutions. |
| Technological Knowledge | Nine domains separately record understand, construct, operate, and maintain ranks from zero through five, exact known native research, origin, revision, and optional custody/availability history. |
| Native authority | `FactionDef.techLevel` may seed otherwise unauthored factions and provide compatibility metadata. Effective research, construction, production, agriculture, habitat, frontier, and autonomous-development capability comes from faction knowledge through one exact requirement translator. Native project completion remains factual. |
| Standard availability | Faction knowledge is effective social capability and does not depend on individual pawn custody. Death, incapacity, departure, or faction change cannot transfer or erase canonical ranks. |
| Experimental distribution | The same domain state is projected through accessible pawn, institution, and record custody at the requesting map or settlement. Redundancy, isolated-carrier loss, incapacity, recovery, and transfer alter practical availability without creating another owner or technology system. |
| Viability | Environment requirements, technological knowledge, and represented labor/material/program ability remain separate. A selected capable Society can satisfy the knowledge requirement without being granted missing facilities or supplies. |
| B14 continuity | Regional composition, Starting Region navigation, Political Order, represented institutions, and the current authored three-faction/four-settlement fixture remain the B14 floor. |

## Governed runtime fixture

| Fact | Value |
|---|---|
| Runtime keyed plan | `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml` |
| Governed mirror | `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml` |
| Pair identity | 407,892 byte-identical bytes; SHA-256 `A0FF1B43CF3A5AFAFE72DA34360B247B7D06CB03DE903AEA251F8FD5D5297C82` |
| Schema | pending authoring epoch 13; regional plan 14; nested player founding 4; settlement record 9; Culture 10 / registry 2; Political Order 10; Technological Knowledge 1; campaign catalog 4 |
| Identity | world `alysaliu|1|Algorab Markab`; region `CA-RG-EB596A12`; candidate `613b1fe44104`; arrival tile `389638`; map scale 350 |
| Composition | 3 factions; 4 settlements; 4 current population assignments; 19 explicit established-program facts; four staged faction/founding knowledge compositions |

## Verification and deployment

| Gate | Result |
|---|---|
| Technological Knowledge execution | **PASS** - 22/22 three-component Society presets; atomic rollback; user-profile readback; standard/distributed availability; local custody; redundancy; isolated loss; incapacity/recovery; persistent retention; placement/realization separation; base consumers; supported migrations; fixture readback; settlement receipt-only realization |
| Native mapping | **PASS** - 122/122 installed native research projects have exact domain mappings; unknown projects fail closed |
| Ownership and consumer review | **PASS after correction** - no Society owner, synthetic project completion, hidden runtime `FactionDef` authority, stale settlement capability authority, query-time custody initialization, or standard-mode pawn transfer remains |
| Retained suites | **PASS** - B10 synthetic 166 classified / 0 unresolved Critical/High; B10 75/75; B11 78/78; B12 113/113; B13 67/67; B14 24/24; B15 executable suite passes |
| Persistence census | **PASS** - 257 source-derived carriers; 87 catalog schemas; 5 explicit non-campaign exclusions; 0 unclassified or invalid routes |
| Final review panel | **PASS** - independent build validation, cross-file coherence review, and final code/static review found no remaining B15 source defect after correction; see `Receipts/B15/B15_FINAL_REVIEW_RECEIPT.md` |
| Reproducible Release build | **PASS** - two clean Release rebuilds, 0 warnings and 0 errors, emitted byte-identical 4,233,216-byte assemblies at SHA-256 `DE6312FF9CF2F4B052AACDE82E93C113AF231D36494F93667266FB2D415F0764` |
| Deployment | **PASS** - RimWorld closed; verified candidate, worktree assembly, and installed junction view are byte-identical at the build identity above |

Current executable receipts in `Receipts/B15` establish source and causal data
behavior. They do not establish how the flow looks or plays.

## Operator runtime boundary

After final deployment, a fresh operator launch is the next evidence boundary.
Codex does not choose values, advance creation, start the game, alter saves, or
claim visual/gameplay acceptance for the operator.

| Runtime focus | Operator check |
|---|---|
| Society composition | Apply a historical Society and confirm Culture, Political Order, and Technological Knowledge change together while Ideoligion, founding arrangement, and represented institutions remain unchanged. Edit one component and confirm the others remain stable. |
| Technology editor | Confirm the nine domains and four competencies are intelligible, directly editable, and summarized without exposing backend ownership or serialization language. |
| Selected-society viability | Place a more capable Society in difficult terrain and confirm the UI distinguishes missing knowledge from missing labor, materials, facilities, and supplies. |
| Standard mode | Confirm construction, production, crops, medicine, research, frontier, and autonomous choices follow faction knowledge without changing because one pawn dies or leaves. |
| Experimental mode | Enable Distributed Knowledge in a disposable test and inspect redundancy, isolated knowledge, incapacity/recovery, recruitment, departure, and settlement-local availability. |
| Authored fixture | Confirm the selected region loads with the same world, region, arrival, scale, 3 factions, 4 settlements, relations, populations, and programs. |
| B14 regressions | Confirm Regional preview/placement, Society flow, Culture identity, Political Order, represented institutions, frontier habitats, and generation remain intact. |

## Environment and preserved evidence

- RimWorld target: 1.6.4871 rev591.
- No RimWorld process was running during fixture conversion or deployment, and
  Codex did not launch the game afterward.
- `B15_TECHNOLOGICAL_KNOWLEDGE_CONTRACT.md`,
  `AUTHORING_ONTOLOGY_COVERAGE.md`, `MODULE_OWNERSHIP.md`,
  `SCHEMA_REGISTRY.md`, `PERSISTENCE_CENSUS.md`, and
  `CAMPAIGN_COMPATIBILITY.md` are the current canonical boundaries.
- `Receipts/B14` remains the regional/Society floor. `Receipts/B15` contains
  technological execution, exact mapping, migration, fixture, retained-suite,
  review, build, and deployment evidence.
- Operator control of time, pawn orders, windows, saves, and autosave remains
  unchanged unless explicitly requested.
