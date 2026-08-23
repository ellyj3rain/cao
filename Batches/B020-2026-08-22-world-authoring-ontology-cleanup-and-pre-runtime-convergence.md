# B20 - World-Authoring Ontology Cleanup and Pre-Runtime Convergence

| Field | Record |
|---|---|
| Batch | `B20` |
| Date | 2026-08-22 UTC / 2026-08-22 PDT |
| Name | World-Authoring Ontology Cleanup and Pre-Runtime Convergence |
| Status | Closed append-only batch. Source compiles with zero warnings and zero errors; deterministic substrate acceptance is 36/36 PASS; the governed 8.0.423 reproducible-build receipt and operator in-game visual/gameplay acceptance remain as named debt. |
| Threads | [`T-001`](THREADS.md#t-001), [`T-002`](THREADS.md#t-002), [`T-005`](THREADS.md#t-005), [`T-013`](THREADS.md#t-013), [`T-019`](THREADS.md#t-019), [`T-020`](THREADS.md#t-020), [`T-021`](THREADS.md#t-021), [`T-022`](THREADS.md#t-022), [`T-024`](THREADS.md#t-024), [`T-025`](THREADS.md#t-025), [`T-026`](THREADS.md#t-026), [`T-028`](THREADS.md#t-028), [`T-030`](THREADS.md#t-030) |
| Local Git commits | 10 layer-split commits on `b20-world-authoring-ontology-cleanup`, branched from the published B19-closed main (`1c61e205`). Kernel, policy/consumers, UI/data, reference, anchor integration, ontology correction, geography extension, and convergence — each a logically atomic layer. |
| Build | Source compiles with zero warnings and zero errors with .NET SDK 9.0.317 (local diagnostic; the governed 8.0.423 pin is verified by CI). |
| Receipts and verification | `Receipts/B19` substrate acceptance updated to 36/36 PASS (added `geographic-barriers`, `distant-founding`, `stability-cadence`; updated `world-settlement-facts`, `world-character-separation`, `urban-threshold`, `quarters-follow-facts-not-the-naming-bar`). The retained B10, B14, B17, and B18 suites are unchanged. |

## Record

B20 cleans the inherited world-authoring ontology into a causally honest surface and resolves the pre-runtime architectural gaps identified during the B19 audit.

**offMapActivityRate split.** The improperly coupled scalar is split into `distantFoundingRate` (authored — how often distant peoples found new settlements) and a derived political-belief pulse cadence (fixed per-organization 60000-tick round-robin, not rate-proportional). `OffMapActivityBudget` is removed from the kernel.

**History cause.** The hidden per-tile hash (`Unit(seed, tileId, 8317) * 4f`) in `WorldSettlementPopulation` is replaced by `worldDevelopment` — an authored world-level tendency (how developed the world's settlements are at the start). Per-settlement variation is derived from position in the settlement network: regional centers are +1, isolated outposts are -1. `WorldSettlementSupport` is fixed to use the real development level instead of a population proxy.

**urbanGrowthPropensity removed.** Under DR-114 (settlement scale is derived) and DR-115 (generic intensity controls are not authoring primitives), the town/city classification threshold is a fixed game constant (`TownThreshold = 62`, `CityThreshold = 74`), not authored state. All consumers updated. The "Urban development" dimension is removed from the UI and presets.

**Variability.** `worldVariability` implements coherent countertypical prevalence and extremity. At world generation, settlements can diverge from their faction's tech tier (deterministic, seeded by tile id). Prevalence (probability) and extremity (max divergence) both scale with the tendency. At materialization, divergent settlements get Culture norm-strength modulation — higher tech = more open to change (lower norm strength), lower tech = more bound by tradition (higher norm strength). This does not invent arbitrary cultures; it modulates the inherited culture's resistance to change based on the divergent tech level.

**Stability.** `worldStability` modulates state-transition resistance, not activity cadence. Political belief drain rate scales by `(1 - stability * 0.7)` — stable worlds hold political support longer. Culture practice shift weight increases with stability (prior holds more strongly) and decay rate decreases — stable worlds shift culture less and lose practices slower. The wrong initial implementation (cadence modulation) was removed.

**Geography-aware partition.** The partition kernel accepts an optional `barrierCost` function. The adapter computes barrier costs from: elevation (1500m = full barrier), hilliness (2-level jump = barrier), biome ecotone (0.2), coastal coherence (0.5 for coast-to-inland), river corridor affinity (-0.3, a corridor not a barrier), temperature transition (0.5 max), and rainfall transition (0.4 max). Valleys, watersheds, plains, and basins emerge implicitly from the combination of elevation and hilliness barriers. Frequency/size scalars are reclassified as authored targets modulated by geography.

**Anchor integration.** All settlements standing on the region's own member tiles — same-faction and different-faction — are preserved as authoritative anchors. Each keeps its vanilla physical layout, stays on the world map (not consumed), and gets CAO composition (culture, programs, capabilities, organizations) via `DeriveSettlementPattern` → `EnsureStartingComposition`. Only pool settlements from outside the region are reallocated and consumed.

**Morphology convergence.** Both initial settlement realization and autonomous construction share `CAMorphologyAdapter.Materialize` (physical execution). The autonomous construction path now consumes the corpus-derived spatial evidence: `FrontierPatternEvidence` computes an `EvidenceScore` from the `envelope-density` band, and `CAAutonomousBuildingPatternKernel.PreferFrontier` uses it as a final tie-breaker. The settlement growth path uses the full 13-band corpus-ranked sequential placement.

**Layered authoring surface.** Three layers over one canonical state: (1) World Character — nine named presets; (2) Intermediate — three intelligible controls ("How settled", "How diverse", "How dynamic") that project over multiple scalars; (3) Advanced — 10 individual sliders. Custom state is deterministic (derived from whether the state matches any preset). User-saveable presets persist to `Config/CAWorldPresets/*.txt` as key=value files.

## Boundary

B20 closes at the corrected source, deterministic acceptance, and current governed records. Remaining work is classified:

- DONE: offMapActivityRate split; history cause; urbanGrowthPropensity removal; variability (tech-tier + Culture norm-strength); stability (political drain + Culture shift/decay); geography-aware partition (elevation, hilliness, biome, coastal, river, temperature, rainfall); anchor integration (all footprint settlements); morphology convergence (corpus evidence in autonomous siting); layered authoring surface (intermediate controls + user presets).
- NAMED ENVIRONMENTAL DEBT (not closed): the governed 8.0.423 reproducible-build receipt (CI-verified; locally 9.0.317 diagnostic only).
- INTENTIONAL DEFERRALS (future depth, not current defects): deeper geographic feature recognition (explicit valley/watershed identification, peninsular formation analysis); richer morphology learning (culture-conditioned morphology from CAO's own longitudinal data); broader countertypical expression (Political Order divergence, institutional divergence beyond Culture norm-strength); preset-management polish (preset import/export, preset sharing, preset editing).
- GENUINELY OPERATOR-BLOCKED: visual and gameplay acceptance of the Create-World front door, the settlement inspect-string, the in-game regional start, and the anchor settlement composition. The operator is the design authority and judge of how the game looks and plays; deterministic and game-assembly evidence does not settle that question.

Static, source, deterministic-acceptance, and reported gate state do not establish visual or gameplay acceptance. The next development batch is `B21`; the next runtime action is the operator's in-game test.

## Addendum - runtime-test phase and corrections (2026-08-23 UTC / 2026-08-22 PDT)

B20's governance close (`b1dc25bb`) was followed by an operator runtime-test
session and five runtime-fix commits on the same branch. This addendum records
that phase without altering the closed record above.

### Runtime fixes after the governance close

| Commit | Fix | Runtime verification |
|---|---|---|
| `1c6b0e42` | Preset "Custom" glitch: `CAWorldPreset.Apply` now assigns `worldVariability` and `worldStability`; presets match deterministically again | Implemented and code-inspected; not exercised in game |
| `90a0638e` | "Use suggested" founding-arrangement button and auto-suggestion fallback removed per operator direction; a blank arrangement ("No terms chosen") is the fallback | Implemented and code-inspected; not exercised in game |
| `f863be87` | Topology lifecycle: `EnsureTopology` and `RebuildWorldSettlementStates` moved from the order-450 WorldGenStep (not executed by the Gravship/Odyssey lifecycle) into `FinalizeInit`, which runs for every path, idempotently | Implemented and code-inspected; not exercised in game |
| `abfd982c` | Regional preview absorbs mouse events instead of clicking through to the globe; the political order editor gains a visible "Done" close affordance | Implemented and code-inspected; not exercised in game |
| `b4ad888e` | Gravship landing crash: anchor settlements (`populationOrigin == CASettlementOrigin.Unset`) skip the CAO habitation-viability failure and log a warning; they are authoritative existing centers that keep their vanilla layouts. CAO-authored (reallocated) settlements must still pass | Implemented and code-inspected; not exercised in game |

### Runtime evidence captured

The operator's last in-game session loaded assembly SHA-256 `A383799E...`
(4,814,848 bytes, written 2026-08-22 19:04 PDT), a build from before every one
of the five fixes; the session before it loaded the B19 production assembly
`63DF9C6E...`. The game's own log captured the reported landfall crash
(`System.InvalidOperationException`: "settlement 0 cannot support permanent
habitation: Missing heating or cooling, a reliable food route, local medical
care" from `ValidateConfirmedComposition` via `EnsureDerivedRegion`, during
`GravshipUtility.ArriveNewMap`) and the topology gap behind it
("[CA][Topology] tile 98594 has no partition membership; deriving a legacy
visit-scoped region"). No runtime fix above has therefore been in-game
verified; each is a candidate for the operator's next launch.

### Corrections applied with this reconciliation

- Source encoding repair. The B20 editing passes double-encoded non-ASCII
  punctuation into mojibake (35 middle-dot sequences U+00C2 U+00B7, 15 em-dash sequences U+00E2 U+20AC U+201D, 1 en-dash sequence U+00E2 U+20AC U+201C; across 9 production source files and one receipts tool) and added
  UTF-8 byte-order marks to 19 source files. Pre-B20 main carries zero
  corruption and no BOMs outside `StackLord.cs` and bundled third-party
  sources; all of it is repaired back to that convention. The
  `Receipts/B18` convergence-receipt artifact carries mojibake captured during
  earlier runs; receipts are immutable evidence and are intentionally not
  rewritten.
- `tools/version-model.test.mjs` expectations advanced to the closed B20 state
  (`1.8.0.0-alpha`, A1-B20, 122 batches, B21 next). The stale expectations were
  the sole failure in PR #18's first `ci-verify` run (2026-08-23T02:12Z);
  `codeql` and `dependency-scan` were green.
- Retained-suite censuses regenerated (`B10_SYNTHETIC_STATE_SWEEP.md`,
  `PERSISTENCE_CENSUS.md`) for the line drift the runtime-fix commits
  introduced; classifications unchanged (B10 316 classified / 0 Critical-High;
  persistence census passes).
- The named environmental debt on the governed build is lifted: .NET SDK
  8.0.423 is now installed locally (the B19-era claim of no network no longer
  holds). Two clean governed production builds
  (Release / Rebuild / ContinuousIntegrationBuild / no debug symbols per
  `tools/ci/verify-dotnet.sh`) are byte-identical, and the tracked and
  junction-deployed assembly is their output: SHA-256 `ED49A028...`,
  4,813,824 bytes, replacing `63DF9C6E...` (B19 production build).

### Standing after this addendum

PR #18 is open and not merged. B20 closes further - and B21 opens - only after
the operator's explicit in-game acceptance of the deployed assembly, which
includes but is not limited to the five runtime fixes above. Open runtime
threads carried for the operator's judgment and next phases: stitching
visualization on the globe, the Gravship lifecycle's relationship to Starting
Region authoring, starting-population presuppositions, UI text encoding
(repaired source awaits visual confirmation), creation-screen latency, and the
administration/physical-space design directions recorded in the batch's
deferral list.

## Addendum 2 - selection/partition decoupling and the shared geographic measure (2026-08-23 UTC / 2026-08-22 PDT)

Runtime review exposed a wrong boundary: initial selection seeded the
candidate as exactly the persistent partition region, discarding configured
extent and orientation on partitioned ground. The operator's direction: the
globe's stitched topology is persistent, visible geographic structure and
evidence for plausible boundaries; regional selection is an authoring
decision under the established size and configuration rules; determinism
means the same world seed plus authoring inputs reproduce the selection, not
that a selection is tied to a pre-generated partition.

Changes on the branch (commit  48cea85):

- CreateAuthoredAt now builds the candidate through the established
  extent-and-orientation contract over connected usable land. The partition
  record is consulted only as a fallback when the connected-land builder
  cannot claim the ground at all (land already inside a realized region).
  Relocation of a designed region now honors its requested extent and
  orientation at the destination, matching the B14 interaction contract.
- CARegionalPlanUtility.TryAdoptPartitionIdentity owns the one-land-one-
  identity rule: a composition whose member set matches a partition
  region exactly inherits that region's identity. It now applies to
  authored (CreateAuthoredAt, CreateExplicit) and derived (Create)
  candidates uniformly.
- CARegionalGeometry.GeographicBarrierCost owns the single geographic
  barrier measure (elevation, hilliness, biome, coastal coherence, river
  corridor affinity, temperature, rainfall) consumed by both the partition
  kernel and, as soft growth evidence, the candidate bundle builder. This
  is stitching-as-evidence in selection: no selection is forced to follow
  a partition boundary, but geography still shapes plausibility.
- Correction: the rainfall transition term was computed and then dropped
  by the introduced code, contradicting this record's declared behavior.
  It now participates. Existing worlds keep their persisted partitions;
  new worlds partition with the complete measure.

Verification: source compiles 0/0 under the governed 8.0.423 build;
substrate acceptance re-run remains 36/36 PASS (the harness's kernel
contracts are unchanged by construction); B10 census regenerated for line
drift (316 classified / 0 Critical-High). Preview and realization were
traced to the same candidate member-set machinery; no second geographic
ontology exists for the preview. In-game confirmation of the new selection
behavior remains the operator's boundary.