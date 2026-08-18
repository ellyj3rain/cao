| Document | Colonist Awareness Overhaul Session State |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.5.0.0-alpha` · closed batch tip `B14` · next `B15` |
<!-- cao:generated:version END -->
| Repository | `SESSION_STATE.md` |
| Status | READY - B14 is closed at the built, receipted, and byte-verified deployment boundary; RimWorld remains closed for the operator runtime test. |

# Session State

Updated 2026-08-17 23:58 UTC / 16:58 PDT.

Read this before claiming where creation or gameplay testing stands. Compile,
receipts, deployment, and operator runtime evidence remain separate.

## Version control and governed history

| Surface | State |
|---|---|
| Active worktree | `.claude/worktrees/rimworld-regional-multithreading-47e9ec` |
| Branch | `mallowfluff/b14-generation-political-audit` |
| B14 baseline HEAD | `20ed6a3bf079fb4fc93f6735bed4cd926d7b552d` |
| B14 closure | The governed B14 audit, source, integration, governance, and receipt commit sequence is frozen at the verified runtime-test boundary on this branch. |
| Closed chronology | `A1-A102` and `B1-B14`; B14 is minor capability unit `VU-041`; `B15` is next. |

## Current B14 contract

| Surface | Current state |
|---|---|
| Regional geography | Preview, confirmation, and generation share one exact composition identity over scale, shape, arrival, biome, relief, water, links, stone, landmarks, and mutators. Base-game and open modded feature obligations fail closed when unresolved. |
| Map Preview integration | The preview and toolbar dock within the creation page without rewriting Map Preview's saved global position. The former synthetic `A`, number, and diamond overlay is removed. |
| Starting Region map | Region, Map, and Details are views of the same authored state. Visible labels resolve in reverse paint order. Arrival and settlement placement write the selected member tile, cannot occupy the same tile, and return to the compact map. The Region list and map invoke the same placement operation. |
| Culture | A generated local Culture name follows its parent identity. An explicitly authored local name remains independent. A Culture's own constituent identity label follows the Culture name. Twenty-two complete historical and social presets fill the same 24 editable questions through one grouped, searchable browser at founding and established-society scope. Historical titles name the recognized society, regime, or population; exact date ranges remain separate, and each regional catalog is chronological. |
| Political Order | One complete normative composition owns 26 causal questions and 110 supported positions. Blendable questions total 100; exclusive questions remain singular. Twelve ownership domains can differ independently. |
| Political identity | Names, summaries, and the full political account are generated from saved variables. Rerolling changes the generated projection; an optional custom display name does not replace the causal account. |
| Society presets | One shared grouped and searchable catalog exposes 22 independently identified starting recipes. Each owns a Culture reference and a frozen complete 26-question Political Order snapshot. Application validates copies, commits both canonical faction owners together, and restores both on exceptional commit failure. It writes no Society ownership, Ideoligion, founding arrangement, represented institution, or event history. |
| Presets and profiles | Culture and Political Order presets remain independent component substitutions. Global settings persist reusable Culture, Political Order, and two-component Society snapshots; applying any profile copies values without shared mutable data or campaign preset ownership. |
| Represented institutions | Existing offices, rules, ownership, security, and other instituted mechanisms remain separate realized facts and appear as read-only comparison evidence rather than a duplicate editor. |
| Founding | Founders carry Culture, Ideoligion, and Political Order. Their adopted landing arrangement remains a separate factual choice; later institutions develop through play. |
| Environment and habitat | Exact terrain, biome, growing period, ecology, temperature, water, light, pollution, hazards, and vacuum derive the physical functions a site requires. Advanced factions may consider hostile established-settlement ground, but confirmation evaluates actual saved programs. Frontier sites must materially close every requirement before residents appear. |
| Autonomous construction | Existing construction owners rank valid candidates through task-owned terrain fit, adjacency, throughput, circulation, expansion, environmental buffering, defensive separation, material cost, visual order, and stable ordering. The player-base corpus supplies reusable evidence, not copied layouts, a universal style score, or a player-facing mode. |

## Governed runtime fixture

| Fact | Value |
|---|---|
| Runtime keyed plan | `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml` |
| Governed mirror | `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml` |
| Pair identity | Byte-identical, 397,275 bytes, SHA-256 `6AE604BB8157FC5F12C58932A2476700A336F3C057F11D85441D09BFF151EF04` |
| Schema | pending authoring epoch 12; regional plan 13; Culture 10 / registry 2; Political Order 10; represented institutions 1; campaign catalog 3 |
| Identity | world `alysaliu|1|Algorab Markab`; region `CA-RG-EB596A12`; candidate `613b1fe44104`; arrival tile `389638`; map scale 350 |
| Composition | 3 factions; 4 settlements; 4 current population assignments; 19 explicit established-program facts |
| Culture state | 8 Culture records retain 290 distributions and 25 evidence records. |
| Political state | 4 complete Political Orders retain 26 normalized questions each; recovered mixtures and independent represented institutions survive serialization and readback. |

## Verification and deployment

| Gate | Result |
|---|---|
| Fixture conversion | **PASS** - schema 13; runtime/mirror byte identity; 3 factions; 4 settlements; 4 current population assignments; four complete Political Orders |
| Authoring convergence | **24/24 PASS** - `Receipts/B14/B14_AUTHORING_CONVERGENCE_STATIC_RECEIPT.md` |
| Society preset execution | **PASS** - 22/22 built-ins own and apply complete Culture plus frozen Political Order snapshots; causal Culture mutation ends a match; invalid apply rolls back; component substitutions remain isolated; saved Society schema survives Scribe serialization/readback |
| Regional geography | **12/12 PASS** - `Receipts/B14/B14_REGIONAL_GEOGRAPHY_STATIC_RECEIPT.md` |
| Generation | **12/12 PASS** - `Receipts/B14/B14_GENERATION_STATIC_RECEIPT.md` |
| Settlement environment | **21/21 PASS** - `Receipts/B14/B14_SETTLEMENT_ENVIRONMENT_STATIC_RECEIPT.md` |
| Culture completion | **67/67 PASS** - retained B13 suite against the schema-13 fixture |
| Retained acceptance | **78/78 PASS** - current source and converted fixture |
| Persistence census | **PASS** - 253 carriers; 86 catalog schemas; 5 explicit non-campaign exclusions; 0 invalid routes |
| Review panel | **PASS** - correctness and cross-file coherence re-reviews report no remaining Critical or High Society-convergence findings; independent build validation passed |
| Release build | **PASS** - two clean, no-incremental Release builds completed with 0 warnings and 0 errors and emitted byte-identical bytes |
| Assembly | 4,149,248 bytes; SHA-256 `A5A3D89A516B08156D0163E1F49D8DEC97FCFFED799B60531715DB9C9D495FC9` |
| Deployment | **PASS** - RimWorld was closed; the installed mod junction targets this worktree; worktree and installed DLL are byte-identical at the assembly identity above. The game was not launched. |

Static evidence establishes source, serialization, fixture, reproducible
assembly, and deployed tree identity. It does not establish how the flow looks
and plays.

## Operator runtime boundary

Prior fresh runs exposed and isolated the Vehicle Framework bundle, shared
resolver, and pre-`NeedDef` body-need faults. Their corrections and the current
Society convergence now share one verified deployed assembly. RimWorld is
closed. A fresh operator launch is the next evidence boundary. Codex does not
choose values, advance creation, start the game, alter saves, or claim
visual/gameplay acceptance for the operator.

| Runtime focus | Operator check |
|---|---|
| Political Order | Confirm variables are the primary controls; complete presets and generation produce a coherent editable order; the generated identity/account update from those variables; custom naming remains secondary. |
| Society presets | Choose one named society and confirm Culture and Political Order both change together while Ideoligion, rules at landing, and represented institutions remain unchanged. Save and reload a Society recipe, then edit either component and confirm the other remains stable and the derived match disappears after a causal edit. |
| Settlement placement | From either the Region list or Map view, choose an existing faction, a custom new society, or a catalog society. Confirm one ordinary settlement is created, the next map click assigns its broad area, and Culture, Political Order, population, faction, and settlement remain editable through their existing surfaces. |
| Mixed economy | Confirm essential, industrial, trade, finance, and luxury ownership can take different public, cooperative, private, and common mixtures without collapsing into binary pseudo-options. |
| Represented institutions | Confirm established institutional facts are visible for comparison but no duplicate institution grid is offered. |
| Culture identity | Rename a parent Culture and confirm dependent generated local identities follow it while an explicitly authored local name does not. |
| Historical Culture presets | Open Culture presets at founding and established-society scope; confirm the same grouped, searchable 22-preset library appears and each selection remains fully editable. |
| Regional preview | Confirm the stray `A` and diamond are absent, labels do not clip, and preview/settings remain docked and controllable. |
| Authored fixture | Confirm the selected region loads with 3 factions, 4 settlements, 4 current population assignments, and the recovered relationships intact. |
| Environment and settlements | Confirm hostile biomes state their requirements; advanced established settlements are admitted only when their authored programs satisfy those requirements; unsupported populations fail visibly. |
| Frontier habitats | Confirm frontier residents appear only after a liveable compact site exists with the required shelter, beds, food route, reserve, medicine, temperature, water, light, and protection for that ground. |
| Autonomous building | Observe whether construction preserves survival closure first, then useful adjacency, short work chains, circulation, expansion room, environmental buffering, defensible access, and deliberate visual order without selecting a style mode. |
| Generation | Start the selected campaign and inspect regional composition, Political Order, Culture, represented institutions, relations, settlements, and populations without reroll or loss. |
| Startup closure | Confirm the load reaches the creation flow without the prior missing `Vehicles.*` types, missing CA thirst/bladder stats, aborted faction definitions, or the resulting cross-reference cascade. |

## Environment and preserved evidence

- RimWorld target: 1.6.4871 rev591.
- PIDs `36604`, `11248`, `34064`, `5084`, and `16084` remain historical runtime
  evidence for the startup convergence. No RimWorld process is currently
  running; the deployed assembly is `A5A3D89A...`.
- `AUTHORING_ONTOLOGY_COVERAGE.md`, `MODULE_OWNERSHIP.md`,
  `SCHEMA_REGISTRY.md`, `PERSISTENCE_CENSUS.md`, and
  `CAMPAIGN_COMPATIBILITY.md` are the current canonical boundaries.
- `Receipts/B14` contains the authoring, geography, generation, transition,
  interaction, runtime, offline-authoring, and deployment evidence.
- Operator control of time, pawn orders, windows, saves, and autosave remains
  unchanged unless explicitly requested.
