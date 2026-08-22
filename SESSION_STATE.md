| Document | Colonist Awareness Overhaul Session State |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.7.2.0-alpha` · closed batch tip `B19` · next `B20` |
<!-- cao:generated:version END -->
| Repository | `SESSION_STATE.md` |
| Status | CLOSED - B19 is built, verified, and recorded; the next action is the operator's in-game runtime test, then B20. |

# Session state

Updated 2026-08-21 (B19 closure). Read this before claiming where creation or
gameplay testing stands. Compile, deterministic acceptance, deployment, and
operator runtime evidence remain separate.

## B19 - Regional substrate realization and settlement composition closure

B19 is closed append-only. Source, deterministic acceptance, retained-suite
re-verification, the committed deployed-assembly identity, and the governed
records are complete. The batch record is
`Batches/B019-2026-08-21-regional-substrate-realization-and-settlement-composition-closure.md`;
receipts are in `Receipts/B19`.

What B19 closed: persistent world-wide regional topology and region-first setup
(`CA_RegionalTopology` order 450, `CA_RegionalWorldSettlementState` order 650);
the substrate/tendencies mechanical-truth pass (the opening feature runs the
real mechanics); the front-door rework to plain-language World character after
the rendered-graphics program was removed in totality (~1,900 lines);
settlement composition (one culture authority, one style authority, culture a
place's residents brought, a pure material kernel with its own receipts,
operational roles that each build something, one knowledge resolver);
material support reaches material; quarters follow population and support,
not the naming bar; the population false closure reverted; the anchor boundary
recorded, not softened; the security chain traced to pawn behaviour. The
chronology, including the reverted false closure, is preserved in the commits,
not rewritten.

## Governed runtime fixture

| Fact | Value |
|---|---|
| Runtime keyed plan | `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml` |
| Governed mirror | `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml` |
| Pair identity | 679,624 byte-identical bytes; SHA-256 `0381E19688C708DA9AA20E800155F56DD421F64D0E22AB0EBD7B856CA5C6832A` |
| Fixture composition | 3 factions, 4 settlements, 3 factionless frontier holdings, region `CA-RG-EB596A12`; supersedes B18's 3-settlement failed-run fixture `regional-plan-3c7bac0ada23c46d31feaae7b2c0bb08.xml` (region `CA-RG-CB2DF263`) |
| Current schemas | pending authoring epoch 15; regional plan 16; settlement population 2; frontier holding 2; frontier map plan 3; settlement record 10; campaign catalog 6; proposition knowledge 2 |

## Verification and deployment

| Gate | Result |
|---|---|
| B19 regional substrate acceptance | **PASS** - 34/34 deterministic kernel contracts, including the closing corrections (`quarters-follow-facts-not-the-naming-bar`, `support-reaches-treeless-ground`, `world-character-separation`) |
| Retained suites | **PASS** - B10 316 classified / 0 Critical-High; B14 24/24; B17 PASS (14/14 fact families, against the real game assembly); B18 9/10 (the one FAIL is fixture evolution past B18's failed-run, not a regression; B18's closed receipt stays 10/10) |
| Source compile | **PASS** - 0 warnings / 0 errors |
| Reproducible build | **SUBSTITUTE PASS / DEBT** - two byte-identical 9.0.316 builds, SHA-256 `6D1696EE...`, 4,805,632 bytes; the governed 8.0.423 receipt is named environmental debt (SDK 8.0.423 absent, no network) |
| Deployment | **PASS** - committed deployed assembly present at the junction-resolved loaded path, SHA-256 `63DF9C6E...`, 4,804,096 bytes; not re-deployed |
| Repository gates | **PASS** - version replay `1.7.2.0-alpha` (46 units cover A1-B19, B20 next) |

Current executable receipts in `Receipts/B19` establish source, deterministic
acceptance, retained-suite, build-substitute, and deployment-identity evidence.
They do not establish how the flow looks or plays.

## Build-toolchain debt (named, not closed)

The governed build contract pins .NET SDK 8.0.423 (`global.json`,
`rollForward: disable`). 8.0.423 is not installed in this environment (only
9.0.316 is present) and no network is available to install it. A 9.0.316
substitute proves the source compiles 0/0 and is byte-identical across two clean
builds; it is diagnostic evidence, not the governed artifact, and it differs
from the prior-pass deployed assembly. Restoring 8.0.423, or operator
ratification of a `global.json` roll-forward policy change, re-opens this one
receipt; it does not re-open B19's substance.

## Operator runtime boundary

A fresh operator launch is the next evidence boundary. The agent does not
choose values, advance creation, start the game, alter saves, or claim visual
or gameplay acceptance for the operator.

| Runtime focus | Operator check |
|---|---|
| Create World front door | Confirm World character reads as intended - nine named worlds, plain-language detail, named differences, tendencies one door deeper; copy asserts only witnessed mechanics. |
| Regional start | Confirm the fixture region loads and a start reaches a playable map with visible progress throughout. |
| Settlement composition | Visit generated settlements; confirm culture, style, material reach, quarters, and roles present and reachable. |
| Anchor boundary | Confirm the world-map settlement standing/population inspect-string reads honestly; entering a non-player settlement produces vanilla generation. |
| Save/reload | Save and reload the real campaign; note wall time (large regional saves carry a measured native load cost). |

## Environment and preserved evidence

- RimWorld target: 1.6.4871 rev591; managed assemblies at `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed`.
- .NET SDK installed: 9.0.316 only (`C:\Users\jleyv\AppData\Local\Microsoft\dotnet\sdk`); 8.0.423 (the `global.json` pin) absent; no network.
- Receipt harnesses and the main assembly build and run with 9.0.316 from a neutral working directory (outside the `global.json` pin) as diagnostic evidence.
- Operator saves, autosaves, and checkpoints were never modified.
- Operator control of time, pawn orders, windows, saves, and autosave remains unchanged unless explicitly requested.
