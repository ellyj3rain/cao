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