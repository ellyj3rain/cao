| Document | Colonist Awareness Overhaul Session State |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.3.0.3-alpha` · closed batch tip `B9` · next `B10` |
<!-- cao:generated:version END -->
| Repository | `SESSION_STATE.md` |
| Status | ACTIVE - current operational state. |

# Session State

Updated 2026-08-12 UTC / 2026-08-12 PDT.

Read this before claiming where creation or gameplay testing stands. Compile,
receipts, review, deployment, and operator runtime evidence remain separate.

## Version Control and Governed History

| Surface | State |
|---|---|
| Branch | `mallowfluff/b3-creation-flow-interaction` |
| Exact B9 baseline | `a30677744f966aec0416695bd3c6413c3a75acdd` |
| B9 source closure | `20b181413a13a2b168c9c5d8dc23fb20241909c4` |
| B9 governance closure | The following governance commit carries this state, the B9 append-only record, generated version projections, and README twin. |
| Publication | Normal fast-forward publication to the current branch is part of B9 closure. |

The portable chronology contains 111 closed batches: `A1-A102` and `B1-B9`.
Thirty-six contiguous version units cover that chronology exactly once. B9 is
`VU-036`, a patch unit deriving `1.3.0.3-alpha`. Series-neutral `T-*` threads
classify work without changing chronology. `B10` remains next and cannot begin
before operator runtime validation.

## Current Creation Contract

| Surface | Current state |
|---|---|
| World | RimWorld world settings plus CA causal World Tendencies |
| Landing | Native landing tile plus confirmed regional footprint and arrival area |
| Starting Region navigation | Objects and Map are canonical; Details owns object-specific decisions and inspection; compact settlement Details supports previous/next comparison without regeneration |
| Established factions | Culture, native Ideoligion, complete Political Beliefs, realized structure, Settlement Authority, relations, and faction technology |
| Established settlements | Identity, population, local Culture, placement, 22-program open settlement composition, and causal provision arrangements |
| Player founding | Inherited Culture, native Ideoligion, complete Political Beliefs, and exact rules adopted at landing |
| Culture authoring | Semantic meanings and practices by default; exact continuous values through contextual fine tuning |
| Confirmation | Validates and consumes confirmed state; it does not reroll tendencies, programs, provisions, Culture, or relations |

One-option setup fields are facts rather than controls. Ordinary player UI does
not expose generation provenance, schema language, receipts, or architectural
exposition. Faction Relations is the visual reference for relation authoring.

## Settlement Programs and Provisions

The active registry contains 22 programs across 16 functional domains:
housing, food, storage, medicine, production, trade, governance, security,
defense, research, religion, social life, culture, agriculture, communications,
and transport. Candidate resolution uses loaded functional contracts independent
of content pack. Ported `Anon2CushionedChair` and `DankPyon_Bust` qualify through
the same functional evidence; visual-only props do not establish a program.

The seven-bit Starting Facilities mask, exception mask, exception values, and
facility-resolution persistence are absent. Provision operators are limited to
household, communal, and authority arrangements backed by real operator, access,
stock, funding, program, and behavior evidence. Vendor, religious, dues, and
abstract-distribution branches are absent.

## Governed Runtime Fixture

| Fact | Value |
|---|---|
| Active plan | `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml` |
| Keyed mirror | `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml` |
| Fixture SHA-256 | `1DDAA4CD9ADC2CD557471B01D754BA37AE191A3588155645B1A872680D8ED4D2` on both files |
| File size | 72,169 bytes on both files |
| Schema | authoring epoch 8; regional plan 8; settlement program 1; Culture and Political Beliefs 8; player founding 3 |
| Identity | world `alysaliu|1|Algorab Markab`; region `CA-RG-EB596A12`; candidate `613b1fe44104`; arrival tile `389638`; map scale 350 |
| Composition | 3 factions; 4 settlements; 9 population groups |
| Megaeth | 15 programs; 3 provision arrangements |
| Red Cervexa | 17 programs; 3 provision arrangements |
| Tascan Bramble | 13 programs; 2 provision arrangements |
| Black Delta | 13 programs; 1 provision arrangement |

Serialization/readback preserves ownership, settlement/faction relationships,
population assignments, programs, provision counts, and signatures. The active
and keyed surfaces are byte-identical.

## Verification and Deployment

| Gate | Result |
|---|---|
| B9 acceptance | 84 cases: 79 statically verified; 5 operator-runtime observations |
| B8 retained acceptance | PASS - 57/57 |
| World Tendencies | PASS - 185 assertions |
| Player founding | PASS - 48 assertions |
| Creation interaction | PASS - 54 assertions |
| Behavior convergence | 107 cases: 98 statically verified; 9 operator-runtime observations |
| Review lenses | Information architecture, settlement ontology, causality, UI, and playability: no unresolved Critical or High finding |
| Release build | PASS - 0 errors; 12 inherited warnings |
| Assembly | 3,404,288 bytes; SHA-256 `9AD4EE62359CF1961FFCB55A7C45C946914F99E264FD7C819F74342E51D0EE19` |
| Deployment | RimWorld was closed; active project DLL is byte-identical to the verified build at the same SHA-256 |

Static layout receipts cover 1280×720, 1366×768, 1600×900, 1920×1080, the
operator window dimensions represented by the fixture evidence, and supported UI
scale inputs. These receipts establish measured bounds, not visual acceptance.

## Operator Runtime Boundary

B9 is deployed and ready. The next action is the operator test; Codex must not
advance the creation flow, choose authoring values, start the game, alter saves,
or claim visual acceptance on the operator's behalf.

| Observation | Operator check |
|---|---|
| B9-33 | Inspect compact Starting Region layout and confirm no clipping or overlap. |
| B9-34 | Inspect wide layout and confirm the object rail, map, and Details hierarchy remain coherent. |
| B9-35 | Select factions and settlements from Objects and Map; confirm shared selection and `View Details`. |
| B9-36 | Compare settlements with previous/next controls and confirm natural ordering and scroll reset. |
| B9-75 | Fine-tune a Culture meaning, close and reopen it, and confirm exact values persist without replacing semantic copy. |
| Faction Relations | Confirm it remains the visual and interaction quality reference and shows relation state without provenance copy. |
| Settlement Composition | Inspect all four saved programs, provision operators, candidate assets, and any explicit fallback. |
| Generation | Confirm the selected region reaches map generation and materializes confirmed state without rerolling it. |
| Behavior 97-103, 105 | Complete creation, verify initialization and optional-asset fallback, observe Standard/Proactive/Autonomous boundaries, direct orders, and first-hour log health. |
| Behavior 104 | Run separately on a compatible-region save/reload fixture; the selected developer region is not its authority. |

Static evidence does not select options, advance the game, change saves, or
substitute for how the creation flow and generated colony look and play.

## Environment and Preserved Evidence

- RimWorld target: 1.6.4871 rev590.
- Odyssey is installed; active runtime state is not asserted without a launch.
- `PARALLEL_ONTOLOGY_AUDIT.md` remains a damaged consolidation; its recovered
  full copy remains in the preservation package.
- Preservation package:
  `Projects\colonist-awareness-preservation-20260806-0614Z-2314PDT`, with a
  verified mirror on `D:`.
- The RS-008 source save and recovered documents remain out-of-band evidence.
