| Document | Colonist Awareness Overhaul Session State |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.8.0.0-alpha` · closed batch tip `B20` · next `B21` |
<!-- cao:generated:version END -->
| Repository | `SESSION_STATE.md` |
| Status | ACTIVE - B20 is closed in governance records with five runtime fixes committed on top; the deployed assembly awaits the operator's in-game acceptance, and PR #18 is open and unmerged. |

# Session state

Updated 2026-08-22 PDT / 2026-08-23 UTC (B20 governance close, operator
runtime-test phase, and the correction pass that reconciled both). Read this
before claiming where creation or gameplay testing stands. Compile,
deterministic acceptance, deployment, CI verification, and operator runtime
evidence remain separate.

## B20 - world-authoring ontology cleanup and pre-runtime convergence

B20 is closed append-only; the batch record is
`Batches/B020-2026-08-22-world-authoring-ontology-cleanup-and-pre-runtime-convergence.md`,
including its runtime-phase addendum. Development lives on
`b20-world-authoring-ontology-cleanup` (branched from the published B19 line
`1c61e205`): ten governed layer commits, the governance close (`b1dc25bb`),
and five runtime-fix commits made after the close while the operator tested.

| Commit | Fix | Runtime verification |
|---|---|---|
| `1c6b0e42` | Preset "Custom" glitch: `CAWorldPreset.Apply` now assigns `worldVariability` and `worldStability` | Implemented, code-inspected; not exercised in game |
| `90a0638e` | "Use suggested" founding-arrangement button and auto-suggestion removed per operator direction; blank arrangement fallback | Implemented, code-inspected; not exercised in game |
| `f863be87` | Topology lifecycle: `EnsureTopology` and `RebuildWorldSettlementStates` moved from the order-450 WorldGenStep (not run by the Gravship/Odyssey lifecycle) into `FinalizeInit`, idempotent, all lifecycle paths | Implemented, code-inspected; not exercised in game |
| `abfd982c` | Regional preview absorbs mouse events; political order editor gains a visible "Done" button | Implemented, code-inspected; not exercised in game |
| `b4ad888e` | Anchor settlements (`populationOrigin == CASettlementOrigin.Unset`) skip the CAO habitation-viability failure; anchors keep vanilla layouts as authoritative centers | Implemented, code-inspected; not exercised in game |

What runtime testing established: the operator's last session loaded assembly
SHA-256 `A383799E...` (4,814,848 bytes, written 2026-08-22 19:04 PDT), built
before any of the five fixes; so no runtime fix has been exercised in game.
The log captured both defects behind the crash: `[CA][Topology] tile 98594 has
no partition membership; deriving a legacy visit-scoped region`, then
`System.InvalidOperationException` ("settlement 0 cannot support permanent
habitation: Missing heating or cooling, a reliable food route, local medical
care") from `ValidateConfirmedComposition` via `EnsureDerivedRegion` during
`GravshipUtility.ArriveNewMap`.

Corrections applied 2026-08-22 PDT with this reconciliation, all committed on
the branch: B20 editing passes had double-encoded non-ASCII punctuation into
mojibake (35 middle-dot, 15 em-dash, 1 en-dash sequences across 9 source
files and one receipts tool) and added byte-order marks the repository does
not use; repaired mechanically to main's convention. `version-model.test.mjs`
expectations advanced to the closed B20 state - their staleness was the sole
failure in PR #18's first `ci-verify` run; `codeql` and `dependency-scan` were
green. Retained-receipt censuses regenerated for runtime-fix line drift
(classifications unchanged). The B19 named environmental debt is lifted: .NET
SDK 8.0.423 is now installed locally, and the governed production build below
is its byte-identical output.

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
| Regional substrate acceptance | **PASS** - 36/36 deterministic kernel contracts, re-run 2026-08-22 against branch-tip source including all five runtime fixes |
| Retained suites | **PASS** - B10 316 classified / 0 Critical-High and B11 persistence census (267 carriers, 89 schemas, 5 exclusions, 0 invalid) regenerated and re-run 2026-08-22; B14 24/24, B17 PASS, B18 9/10 unchanged from the B19 close record |
| Source compile | **PASS** - 0 warnings / 0 errors, governed SDK 8.0.423 |
| Reproducible build (8.0.423) | **PASS locally** - two clean governed builds byte-identical; CI re-verification runs on push |
| Deployment | **PASS** - committed deployed assembly at the junction-resolved loaded path, SHA-256 `843D37CF6C314AC217DAF5AC6194F760835A60AADED361C1E582990A451DA900`; supersedes `87468871...` (population truth and frame churn), `2B0C8C2D...` (landing authoring), `AA60AC8B...` (selection decoupling), and the B19 production build `63DF9C6E...` |
| Repository gates | **PASS locally** - `verify-repository.mjs` green after the test-expectation refresh; PR #18 first `ci-verify` run failed only on those stale expectations |

## Operator runtime boundary

The operator's next in-game launch is the current evidence boundary. The agent
does not choose values, advance creation, start the game, alter saves, or claim
visual or gameplay acceptance for the operator.

| Runtime focus | Operator check |
|---|---|
| Deployed build | Confirm the loaded assembly is `843D37CF...` (the `[CA][Build]` line in the player log records it) and not the pre-fix `A383799E...` |
| Runtime fixes | Presets hold their names instead of "Custom"; no "Use suggested" button; preview clicks do not reach the globe; political order editor closes by "Done"; Gravship landing on fresh ground opens the landing-region author (confirm authors and registers at materialization; land-without-authoring derives as before; cancel does not launch) and reaches a map without the habitation crash |
| Topology | Confirm stitched regions exist and are visible on the globe under the standard and Gravship lifecycle paths |
| Create World front door | Nine named worlds, layered authoring (World Character, intermediate controls, Advanced), user presets |
| Settlement composition | Visit generated settlements; culture, style, material reach, quarters, roles |
| Anchor boundary | World-map inspect-string reads honestly; non-player settlements generate vanilla on entry |
| Save/reload | Save and reload the real campaign; note wall time |

Selection model (Addendum 2): clicking new ground now authors by the configured extent and orientation with geographic-barrier evidence, and only an exact composition match inherits a partition identity - confirm the resulting shapes read as intended. Open runtime threads beyond the fixes: stitching visibility on the globe, the operator friction policy for mid-campaign authoring entry
(per-landing author, where offered, whether caravan settling joins), whether the
variable-pawn-set behavior reads correctly under Prepare Carefully (the pawn set is now the count truth wherever it exists), further creation-screen latency profiling with the game instrumented, and the administration and physical-space design directions. None of
these is accepted or settled by this record.

## Environment and preserved evidence

- RimWorld target: 1.6.4871 rev591; managed assemblies at `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed`.
- .NET SDK: 8.0.423 (governed pin) installed locally at `C:\Users\jleyv\dotnet-8.0.423` on 2026-08-22; 9.0.317 remains the system SDK; network is available (the B19-era local-network restriction no longer holds).
- Mod install path `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\ColonistAwareness` is an NTFS junction to this worktree; the game loads this repository's tracked assembly.
- The B18 performance evidence boundary: curated per-run receipts and acceptance screenshots are tracked in Git; raw run logs and raw per-phase progress are kept local (gitignored) per `Receipts/B18/B18_PERFORMANCE_ARTIFACT_INDEX.md`. The `Receipts/B18` convergence-receipt artifact retains mojibake captured during earlier runs; receipts are immutable evidence.
- Operator working state on the pre-B17 governance line (`a2.5-orphan-preserved`, 146 changes) is preserved in `git stash@{0}`, not discarded.
- Operator saves, autosaves, and checkpoints were never modified.
- Operator control of time, pawn orders, windows, saves, and autosave remains unchanged unless explicitly requested.

## Publication and ref state

- `origin/main` = `1c61e205` = the canonical B19-closed line (PR #16 rebase-merge `1e97b75d`, PR #17 session-state convergence, CI-verified).
- Branch `b20-world-authoring-ontology-cleanup`: ten B20 layer commits, governance close `b1dc25bb`, five runtime-fix commits through `b4ad888e`, and the correction commits of this reconciliation.
- PR #18 (`b20-world-authoring-ontology-cleanup` -> `main`) is open and unmerged; first `ci-verify` run red on stale version-model test expectations only, corrected here.
- The operator has not given in-game acceptance for the deployed build. Do not merge PR #18 and do not declare B20 runtime-accepted without the operator's explicit acceptance.
- Local `archive/claude-post-b17-raw` (`91a30aa`) preserves Claude's raw post-B17 tip for forensic provenance.
- B21 is the next development batch; it opens after the operator's B20 runtime-acceptance boundary.
