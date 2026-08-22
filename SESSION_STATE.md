| Document | Colonist Awareness Overhaul Session State |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.7.2.0-alpha` · closed batch tip `B19` · next `B20` |
<!-- cao:generated:version END -->
| Repository | `SESSION_STATE.md` |
| Status | CLOSED - B19 is built, verified, recorded, and published to main; the only remaining B19 boundary is the operator's in-game visual/gameplay acceptance, then B20. |

# Session state

Updated 2026-08-22 (B19 closure + publication). Read this before claiming where
creation or gameplay testing stands. Compile, deterministic acceptance,
deployment, CI verification, and operator runtime evidence remain separate.

## B19 - Regional substrate realization and settlement composition closure

B19 is closed append-only and published to `origin/main` (`1e97b75d`). Source,
deterministic acceptance, retained-suite re-verification, the committed
deployed-assembly identity, and the governed records are complete. The batch
record is
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
recorded, not softened; the security chain traced to pawn behaviour.

The corrections (wrong-coupling fix, reverted false closure, three false-absence
findings) are preserved in the B019 record. The post-B17 commit history was
reconstructed to the governed layer-split and logical-atomicity standard:
Claude's ~217 post-B17 commits (census-replay spam, assembly-rebuild spam, batch
interleaving) were replaced by 13 canonical commits (B18: 5; B19: 6; 2 [REPO]
normalization) on the clean published B17 base, preserving the exact final tree.

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
| Reproducible build (governed 8.0.423) | **VERIFIED by CI** - PR #16 ci-verify green: the governed 8.0.423 build, two byte-identical reproducible builds, and the tracked shipping assembly verified current. Locally 8.0.423 is unavailable (9.0.316 only); the local 9.0.316 build (SHA-256 `6D1696EE...`) is diagnostic only, not the governed artifact |
| Deployment | **PASS** - committed deployed assembly present at the junction-resolved loaded path, SHA-256 `63DF9C6E...`, 4,804,096 bytes; published to `origin/main` (`1e97b75d`) |
| Repository gates | **PASS** - version replay `1.7.2.0-alpha` (46 units cover A1-B19, B20 next); CI ci-verify + dependency-scan + CodeQL green on the published PR |

Current executable receipts in `Receipts/B19` establish source, deterministic
acceptance, retained-suite, build, and deployment-identity evidence. CI
(ci-verify) established the governed 8.0.423 reproducible-build receipt. None of
this establishes how the flow looks or plays.

## Reproducible build (CI-verified)

The governed build contract pins .NET SDK 8.0.423 (`global.json`,
`rollForward: disable`). 8.0.423 is not installed locally (only 9.0.316 is
present) and no local network is available to install it; the local 9.0.316
substitute (two byte-identical builds, SHA-256 `6D1696EE...`) is diagnostic
evidence only. The governed 8.0.423 reproducible-build receipt is satisfied by
CI: PR #16 `ci-verify` ran the governed 8.0.423 build, produced two
byte-identical reproducible builds, and proved the tracked shipping assembly is
current. CI is the governed arbiter for this receipt; it is not debt.

## Operator runtime boundary

A fresh operator launch is the next evidence boundary and the only remaining
B19 boundary. The agent does not choose values, advance creation, start the
game, alter saves, or claim visual or gameplay acceptance for the operator.

| Runtime focus | Operator check |
|---|---|
| Create World front door | Confirm World character reads as intended - nine named worlds, plain-language detail, named differences, tendencies one door deeper; copy asserts only witnessed mechanics. |
| Regional start | Confirm the fixture region loads and a start reaches a playable map with visible progress throughout. |
| Settlement composition | Visit generated settlements; confirm culture, style, material reach, quarters, and roles present and reachable. |
| Anchor boundary | Confirm the world-map settlement standing/population inspect-string reads honestly; entering a non-player settlement produces vanilla generation. |
| Save/reload | Save and reload the real campaign; note wall time (large regional saves carry a measured native load cost). |

## Environment and preserved evidence

- RimWorld target: 1.6.4871 rev591; managed assemblies at `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed`.
- .NET SDK: 8.0.423 (governed pin) verified by CI; locally 9.0.316 only (diagnostic), no local network.
- The B18 performance evidence boundary: curated per-run receipts and acceptance screenshots are tracked in Git; raw run logs and raw per-phase progress are kept local (gitignored) per `Receipts/B18/B18_PERFORMANCE_ARTIFACT_INDEX.md`.
- Operator working state on the pre-B17 governance line (`a2.5-orphan-preserved`, 146 changes) is preserved in `git stash@{0}`, not discarded.
- Operator saves, autosaves, and checkpoints were never modified.
- Operator control of time, pawn orders, windows, saves, and autosave remains unchanged unless explicitly requested.

## Publication and ref state

- `origin/main` = `1e97b75d` = the canonical B19-closed line (merged via PR #16, rebase-merge, linear, CI-verified).
- `origin/mallowfluff/b18-authoring-state-convergence` deleted (superseded by main).
- Local `archive/claude-post-b17-raw` (`91a30aa`) preserves Claude's raw post-B17 tip for forensic provenance.
- B20 is the next development batch; not started.
