| Document | Colonist Awareness Overhaul Session State |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.4.1.0-alpha` · closed batch tip `B13` · next `B14` |
<!-- cao:generated:version END -->
| Repository | `SESSION_STATE.md` |
| Status | ACTIVE - current operational state. |

# Session State

Updated 2026-08-13 23:30 UTC / 16:30 PST.

Read this before claiming where creation or gameplay testing stands. Compile,
receipts, review, deployment, and operator runtime evidence remain separate.

## Version control and governed history

| Surface | State |
|---|---|
| Branch | `mallowfluff/b3-creation-flow-interaction` |
| Exact B12 baseline | `e9ad9377186b79598a9ac15a21252d0dd2b132cd` |
| B13 baseline audit | `96a9587` (`[B13] audit: establish B12 fidelity baseline`) |
| B13 source closure | `e89d9367b83652c6250a62d2419d605c4e2f9b03` (`[B13] source: refresh cognition across jurisdiction changes`), following implementation `23dbd335d7054e37eeb12837b2e0ece307776242` and context/index correction `d55380ecac5064c73080d82ab7b55188ce70f92e` |
| B13 governance closure | Pending the coherent governance commit containing this record. |
| B13 deployment receipt | Pending closed-process copy of the verified assembly. |

The portable chronology contains 115 closed batches: `A1-A102` and `B1-B13`.
Forty contiguous version units cover that chronology exactly once. B13 is
`VU-040`, a kohai unit deriving `1.4.1.0-alpha`. Series-neutral `T-*` threads
classify work without changing chronology. `B14` is the next ordinary batch.

## Current B13 contract

| Surface | Current state |
|---|---|
| Culture authoring | Twenty-four population questions in eight categories; five ordered anchors per question; one global diversity control; advanced per-question spread; complete editable presets; deterministic randomization; manual, preset, profile, and generated paths use one Culture object |
| Causal cognition | Private position, public expression, attention, inherited-prior strength, perceived social pressure, observation likelihood, knowledge confidence, conviction, and uncertainty persist separately; question-specific psychology supplies only a bounded private deviation |
| Historical feedback | Seven direct represented-fact observation routes persist position, spread, participation, represented population, continuity, and source identity before stable pawn appraisal; seventeen exact factual-subject adapters cover the remaining questions; absence of an event is not negative evidence |
| Ideoligion | Native doctrine remains native and contributes pressure without rewriting Culture |
| Political development | Pawn issue positions, represented majorities, qualified issue links, and faction-bounded coalitions persist separately from normative beliefs and instituted order |
| Institutions | Organizations own legitimacy and sanctions assembled from represented procedure, performance, support, coercion, competence, fit, and treatment facts |
| Institutional context | Population identity, local reaction scope, and institutional jurisdiction remain separate; regional cognition retains the exact settlement organization resolved from the pawn's population assignment |
| Knowledge | Propositions retain holders, sources, per-route acquisition, provenance, contradictions, access, trust, confidence, transmission, custody, decay, and research receipts; every built-in factual subject maps expertise deference to demonstrated RimWorld skill domains |
| Authority | Culture changes appraisal and bounded selection of discretionary CA action; native legality and direct, relayed, or restored operator intent remain authoritative |
| Compatibility | Campaign catalog 3 carries cultural-cognition schema 2; Culture schema 10 requires question registry 2; other B12 owners retain their current schemas |

## Governed runtime fixture

| Fact | Value |
|---|---|
| Active plan | `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml` |
| Keyed mirror | `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml` |
| Fixture SHA-256 | `27354E00647007BD54EC5D5E0B29E7E408E40CEEBE4A3CAAADE068D23BDCA101` on both files |
| Schema | pending authoring epoch 12; regional plan 11; Culture 10 / registry 2; Political Beliefs 9; campaign catalog 3; act ledger 2; organization 2; cultural cognition 2 |
| Identity | world `alysaliu|1|Algorab Markab`; region `CA-RG-EB596A12`; candidate `613b1fe44104`; arrival tile `389638`; map scale 350 |
| Composition | 3 factions; 4 settlements; 9 population groups; 19 explicit established-program facts |
| Culture state | Eight Culture records contain 192 complete root distributions, 2 retained authored local distributions, and 26 preserved evidence records |

The B13 fixture generator uses the current schema, preserves the intended
composition and current identity, completes only absent root questions,
validates before atomic pair replacement, and verifies serialization readback.
The active and keyed surfaces are byte-identical.

## Verification and deployment

| Gate | Result |
|---|---|
| B10 acceptance | **75/75 PASS** |
| B11 acceptance | **78/78 PASS** |
| B12 acceptance | **113/113 PASS** |
| B13 acceptance | **67/67 PASS** |
| Fixture conversion | **PASS** - 8 Culture records; 194 distributions; 26 evidence records; active/mirror identity and composition preserved |
| Version model | **4/4 PASS** - 40 units cover A1-B13; `1.4.1.0-alpha`; B14 next |
| Independent review | **PASS** - causal, structural, and player-facing surface review close with 0 Critical/High/Medium/Low; `B13_REVIEW_RECEIPT.md` |
| Release build | **PASS** - two no-incremental Release builds from source commit `e89d936` produced 0 warnings, 0 errors, and identical bytes |
| Assembly | 3,973,120 bytes; SHA-256 `889F81BD696627359F6EFB096304303CAE3066D7870F001E1316DAD2D4324372` |
| Deployment | Pending closed-process copy and post-copy byte comparison |

Static evidence establishes the source, serialization, fixture, and deployment
candidate. It does not select options, advance the game, or establish visual and
gameplay acceptance.

## Operator runtime boundary

B13 ends at the operator runtime-test boundary after final deployment. Codex
does not advance creation, choose authoring values, start the game, alter saves,
or claim how the game looks and plays on the operator's behalf.

| Observation | Operator check |
|---|---|
| Creation load | Open the current pending plan and confirm all 3 factions, 4 settlements, 9 population groups, and 19 established operations remain present. |
| Culture composer | Confirm the twenty-four questions are grouped into eight categories; the global diversity control, complete presets, randomization, five anchors, advanced spread, evidence, and practice history are legible and editable at founding and established-society scope. |
| Causal controls | Confirm ordinary controls state one direct cause and advanced fields remain optional; preset, random, and manual edits remain freely editable in one Culture object. |
| Political development | Confirm normative Political Beliefs and instituted order remain separate while summaries expose realized tensions without inventing state. |
| Ideoligion | Confirm none, pending, one, mixed, and unresolved states appear accurately and the native chooser preserves neighboring CA state. |
| Starting Region | Confirm object selection, map placement, details, relations, programs, provisions, and authored composition remain usable. |
| Generation and play | Start the selected campaign and inspect Culture materialization, persistence, relations, knowledge, organizations, jobs, and runtime health without rerolled or dropped composition. |
| First retained save | After successful validation, retain the campaign as the first live B13 durability baseline. |

## Environment and preserved evidence

- RimWorld target: 1.6.4871 rev590.
- RimWorld remains closed through verification and assembly replacement. Codex
  does not launch it afterward.
- `B13_CAUSAL_CONTRACT.md`, `CULTURE_RESEARCH_CORPUS.md`,
  `AUTHORING_ONTOLOGY_COVERAGE.md`, `MODULE_OWNERSHIP.md`,
  `SCHEMA_REGISTRY.md`, `PERSISTENCE_CENSUS.md`, and
  `CAMPAIGN_COMPATIBILITY.md` are the current canonical boundaries.
- `B13_ACCEPTANCE_RECEIPTS.md`, `B13_FIXTURE_RECEIPT.md`, and the final review,
  build, and deployment receipts are the current capability evidence.
- Operator control of time, pawn orders, windows, saves, and autosave remains
  unchanged unless explicitly requested.
