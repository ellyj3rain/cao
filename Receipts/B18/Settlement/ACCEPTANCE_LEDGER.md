# B18 settlement acceptance ledger

The active acceptance boundary, jointly. No item disappears because another
starts passing. Every fix names its semantic owner first; the convergence
exercise instruments and falsifies, it never defines success. Status is
updated only on in-game evidence, never on receipts alone.

## Operator verdict, run 12 (2026-08-19) -- FAILED product acceptance

| Verdict item | Verdict |
|---|---|
| Corpus acquisition / governance | PASS |
| Relationship extraction / evidence shipping / runtime lookup | PASS |
| Meaningful learned influence over settlement phenotype | FAIL -- kitchen/granary/annex selection inside an unchanged deterministic morphology is not settlement synthesis |
| Coherent living settlement organization | FAIL -- pairwise plausibility does not make a globally sensible settlement |
| In-map boundary dissolution | FAIL -- any consumer terminating at the abstract body boundary keeps the artifact; fields hiding part of the mask do not solve it |
| Regional / UI continuity | FAIL -- the user experience, not backend identity: selection, preview, travel must operate as locations inside one persistent continuous region |
| Pawn <-> settlement bidirectional behavior | UNPROVEN -- operator names in receipts are not integration; prove actor -> work -> asset -> use -> changed state with actual pawns |
| Longitudinal development | UNFINISHED -- begin as accumulated history, then keep changing additively without regeneration |
| Streets / circulation | FAIL |

Standing target: `represented state -> requirements/capabilities ->
candidate spatial structures -> corpus-informed relational ranking ->
materialization -> actual use -> persistent history -> later development`.
Every accepted placement alters the state later placements are evaluated
against. Corpus teaches relational priors, never templates; different
societies must produce genuinely different valid organizations; no
universal quality scalar; no growing pile of handwritten morphology rules
in place of missing learned structure. The next acceptance run must show a
visibly and behaviorally different class of result -- if only a kitchen or
annex moved inside the same morphology, keep working.

| # | Item | Status 2026-08-19 | Evidence | Semantic owner(s) |
|---|---|---|---|---|
| 1 | Differentiated causal initial synthesis | passing in run 10, keep under watch | Boundary/Requirements receipts: authored causes {18/1/0, 60/2/1, 140/2/3, 320/3/2} realized verbatim; bodies 990/2501/5292/11123; compositions gathering-hamlet / farm village / production town (+production,+storage) / coastal market (+trade); operator has not yet accepted visually | `CARegionalSettlementPlan` authored causes; `DeriveSettlementPattern`; `StartingProgramKeys` |
| 2 | Functional/organic material habitation | FAILING (operator, run 10 live) | "each building is not furnished or their resources established in ways that seem organic"; fit-out is room-label -> furniture preset | provision arrangements (`CAProvisionArrangement.stockSource` et al.), `SettlementProgramMaterializer` (program assets), domestic provision demands, technological knowledge, economy/trade; fit-out must consume these owners, not presets |
| 3 | Established-settlement viability / self-sufficiency | FAILING (operator, run 10 live) | "lack of building and established structure for settlements to be self sufficient"; agriculture:4x1 has zero ground footprint (no fields), no fuel/water/maintenance substrate | program definitions + `SettlementProgramMaterializer` (a program's spatial/material substrate is its asset contract); habitat viability facts (required vs represented capability); legitimate deficits stay representable |
| 4 | No artificial universal gradient/border phenotype | FAILING (operator, run 10 live) | "the gradient border still exists for every single one" | every physical consumer of the generated body/extent mask in `SettlementMorphology` + `CAMorphologyAdapter.GroundCulture` (paving, vegetation purge, terrain mutation, lot eligibility); mask may allocate, must not paint |
| 5 | Ports/harbors where causally warranted | passing in run 10, keep under watch | Delmen (trade, coastal-seeking): "pier built (14 cells); harbor works (2 placed)"; Port receipt shows waterfront sought (water fraction 0.01 -- weak site, honest) | `TryFindSettlementRect` water band; `BuildPier`/`BuildHarborWorks` gated on Trade/Transport program |
| 6 | Unified stable regional map / world-map behavior | FAILING (operator, twice; do not drop) | preview re-randomizes on member selection; members still act as independent vanilla destinations despite 4277db8 size-postfix fix + member gizmo contract in run 10 build | the confirmed-regional-state -> world representation -> selection -> preview -> map-open/travel path; B-series regional contract is authoritative, not vanilla assumptions; `CARegionMapWidget`, preview plan resolution, member world objects |
| 7 | Additive longitudinal development | wired, unproven in play | Development demand + TryDevelop (bed-infill, room-extension) through the real authorization contract; no live specimen yet of an extension completing | `BuildInstitutionalProposal` (housing pressure), `CASettlementWorksMapComponent.TryDevelop` |
| 8 | Street/circulation defect (desire streets 0 cells) | FAILING, parked by directive priority | run 10: "desire streets 0 cells from 73 lots" at every settlement; next hypothesis: `lot.doorIndex` semantics never verified against its assignment | `SettlementMorphology.DeriveDesireStreets` + lot door assignment |
| 9 | Acceptable runtime performance | regressed vs quiet baseline, cause identified as population-proportional | idle 26-30 ms/tick vs 22.0-22.3 baseline; listNormal 10-27 (pawn count), steady ~8.2 constant (GetValidRegionAt layer, pre-existing); no new CA regression signature in run 10 | steady-tick `GetValidRegionAt` layer; pawn job-search cost scales with fixture population |
| 10 | Pawn <-> settlement causality is first-class | FAILING as an instrumented property (operator directive 2026-08-19) | stocks/assets/institutions must trace to specific represented actors (pawns, households, population groups, organizations, offices) and changes propagate both directions; a settlement never passes on `Workshop x1, Beds 10/10, abstract stock` alone | operational facts (`operatorIdentity` per program), population groups, provision arrangements, domestic provision demands, organization keys, works-pulse pawn duties; durable social facts (institutions, records, wealth, Culture) explicitly outlive individual actors |
| 11 | Learned spatial relationships rank valid candidates (corpus contract) | first vertical path shipped 2026-08-19, mostly open | miner + holdout (test coverage 0.733-0.970, receipt SPATIAL_EVIDENCE_HOLDOUT.json) + shipped aggregate `Evidence/spatial-relationships.xml` + two consumers (fit-out kitchen/granary assignment, extension anchor); open: morphology lot/door/street seams, frontier siting, richer corridor and terrain-envelope features, .rws ground resolution, operator-strata conditioning; settlements must develop coherent spatial life, not per-object provenance alone | `tools/PlayerBaseLayoutExtractor` (RelationshipMiner), `CASpatialRelationshipEvidence`, consumers at each placement decision; lineage-safe partitions preserved, validation partition reserved |

## Interactive acceptance findings: region-composition flow (operator, 2026-08-20, 14 screenshots)

| Finding | Status after the opening-flow slice |
|---|---|
| Accidental ocean click hijacks selection with a doomed footprint | FIXED: blocked roots (ocean, impassable) refuse to build a prospect; prior selection survives |
| Impassable mountains silently claimed, consume map, not surfaced | FIXED: card states "Inside the outline but unusable: N sea / M impassable mountain areas" |
| Preview window unmovable on landing screen; CAO needs its OWN deterministic, extent-scaled, cross-screen preview showing settlements/info | SUPERSEDED by the two-view model (operator direction, later 2026-08-20): the standalone CA diagram workspace this row's fix belonged to was rejected and removed; the landing page now augments the real globe and docks the adapted generated-terrain preview. The kernel diagram survives as the composition widget on later screens. |
| Extent/stitching not represented in preview | FIXED at the diagram (member seams baked into the ground texture) plus the genstep-order fix below for the generated-terrain preview; under the two-view model the docked preview additionally carries the seam overlay at exact composed backing |
| Rotation inefficient/latent/clunky | IMPROVED: 8-slot MRU composition cache -- revisited orientations are instant; first visit still pays one kernel build |
| Peninsulas (#2), islands (#5), wetland (#7), iceberg (#8), reservoir (#9), lava (#11) missing/unfaithful in generated-terrain preview | ROOT CAUSE FIXED: preview appended CA gensteps after the vanilla list, so every earlier per-cell mutator consumer failed neutral; gensteps now merge by def order -- needs the operator's re-check per mutator |
| Rivers overly angular (#4) | FIXED in the CA diagram: deterministic pinched meander per link; generated-map channel geometry unchanged (real-terrain smoothing is a projection question, still open) |
| Roads woeful (#10, #14) | IMPROVED in the CA diagram (drawn polylines by road class); real-map road projection fidelity still open |
| Mountain ranges legible but not representative (#6) | IMPROVED: relief shading + rock mass in the diagram from the same hill factors generation consumes |
| Islets (#3) | already good (operator) |
| Atoll (and mutators generally) should be dynamically reshapeable/elongatable (#12) | IMPLEMENTED, awaiting runtime evidence (711dfb4, 93d40ca): one canonical authored-shape state (per-family admissible degrees of freedom -- atoll: extent/elongation/lagoon/bearing/variation; fjord: width/bearing/variation; wetland: coverage/variation; twenty-plus families each with their own), deterministic identity-seeded defaults (absent state is byte-identical to the unauthored realization; variation 0 is the identity seed), scribed on the plan and carried through relocation and footprint changes. It folds into every composition identity so authoring regenerates diagram, preview, and map from the same values, and every projection pass resolves authored-over-default. First control surface: the landing inspected-area's 'Shape features...' editor (commit-on-release; untouched rows read 'as found'). Direct spatial editing is implemented as a fourth consumer (6929afe, b209779): preview click selects an area, double-click opens its editor, and while the editor is open each of the area's inland-water basins offers a drag handle at its kernel-recorded resolved center -- dragging beats the pan and releasing commits position through the same canonical seam. Member-carried mutators also tick (739ef50: a member's lava flow schedules eruptions). The forty resolution wirings were sweep-verified fallback-exact (one slider-word inversion found, fixed f8754ee). Handle scope: the inland-water family (coast folds are coast-anchored and offer no position degree); rotation/extent gestures remain possible further consumers. All implemented-awaiting-runtime-evidence. |
| Toxic lake over-represented (#13) | ROOT CAUSE FOUND AND FIXED (fdd35de): CA never rolls mutators (incidence is vanilla's per-tile assignment; verified -- no CA AddMutator call exists), but the radial lake family sizes its basin as a fraction of map width, so the ANCHOR tile's native instance ran against the aggregate map: a toxic lake at 0.6 of the whole stitched width. Any non-anchor member's lake never ran at all. The kernel now re-expresses Lake/ToxicLake/Pond/DryLake/LakeWithIsland per carrier at one-tile scale (native constants, deterministic identity); generation flattens and paints each basin at the native mutator order; the diagram shows inland water by depth and kind; the landing column's new Features row states carried mutators with incidence counts. Operator runtime verdict pending. |
| Geometry user-definable, granular composition, valley maps (#14) | RESOLVED IN THE SHIPPED CONTRACT, acceptance pending: shape and extent are player-authored state under the canonical 2..16 connected-unblocked rule (shift-click add/release with connectivity and blocked-tile refusals); Size and Turn were reclassified as presets over the same geometry, with a consequence-listing confirmation when a preset change would drop settlements or the arrival. A valley, island chain, or coastal strip is composed directly and generates at true vertex-hull proportions. Operator exercised live sculpting 12->13->12->11->10->9->10 on the current build. |
| Wetland (#7) | FIXED (f182c14): per carrying area from its own deterministically seeded ridged field of native character, masked to owned cells; anchor instance suppressed. |
| Coast shapes silently degraded (#2 peninsulas, #5 islands, #8 iceberg) | FIXED (25f85c3, 0c89740): Bay, Fjord, Peninsula, and CoastalIsland fold their native models into the shared coast field per carrier (they were suppressed as Coast subclasses but never re-expressed); Iceberg folds its opening ring and derives the berg mass per carrier -- ice terrain, solid-ice walls, and shallow ring at native thresholds, white mass in the diagram. |
| Reservoir (#9) | No def named Reservoir exists in Core or Odyssey tile mutators; the nearest referents (the lake-family water bodies) are now projected per carrier. If the operator meant something else, name it and it re-opens. |
| Valley | FIXED (725ddec): trough-and-flanks field added per carrying area at one-tile scale, coast-oriented and opened seaward, masked to owned cells, run between the mountain add and the cave fill. |
| Lava (#11) | FIXED (70eee8d, a65550a): LavaLake and LavaCrater join the radial basin projection (deep lava, volcanic ring, raised crater rim); LavaFlow applies its vein-preserving flatten and cooled-lava terrain per carrying area. Two named deviations: the native largest-island flood-fill cleanup is not mirrored (its touches-map-edge test has no meaning at carrier scale), and eruption Ticks stay root-only, so a member-carried flow is physically projected but schedules no eruptions. |
| Geometry-mutator projection arc | CLOSED to two named remainders (commits fdd35de..edbff40): Basin (own kind: anchor-pinned .3 basin + rim/entrance elevation pass; the earlier lake slice had silently mis-projected it as a generic wandering lake -- self-found and fixed), Hollow, Chasm, Plateau, Cliffs, Crevasse, HotSprings, TerraformingScar, Dunes, IceDunes, Oasis, LakeWithIslands, and ObsidianDeposits all projected per carrier from exact native graphs with deterministic seeds. Lakeshore: traced end-to-end -- the water painter already branches lake-biome boundary water to fresh terrain and the groundwater salt test is ocean-only (vanilla CoastDirectionAt), so the one genuine gap was the shore band; non-ocean shores now take the native lakeshore treatment region-wide (8416f43; the one-tile CoastOffset has no carrier-scale analog, named deviation). Harbor: carrier-local re-expression shipped (967fdd3) -- outpost near the carrier's own land, owner prefers the settlement holding the area, dock walks to the carrier's real nearest boundary water through the native builders re-expressed verbatim; the native perpendicular center-alignment leg is dropped, named deviation. No named remainders. Cross-cutting notes: member-carried mutator Tick hooks (LavaFlow eruptions) do not fire -- physical projection only; the Map Preview generator's tolerance of thing-spawning passes (iceberg walls, ice spurs, obsidian lumps) rides the same gensteps native mutators already use there, unverified statically. |

## Standing process constraints (operator, 2026-08-19):

- Semantic owner first, exercise instrumentation after. The harness must not
  invent product semantics or substitute fixture state for authoring/runtime
  behavior.
- Habitation target: represented society -> operators/programs -> spaces ->
  functional assets -> material stocks/use. Never room label -> preset.
- Viability audit distinguishes required capability, represented capability,
  physical infrastructure, current stocks, and legitimate deficits. No
  arbitrary starter bundles.
- The border fix is not softening/noising: fabric terminates differently in
  different places because the things occupying them terminate differently.
  A visible boundary exists only where a represented cause (wall, road,
  property, land-use, terrain treatment) produces one.
- Read the B-series regional contract before changing world-map behavior.
- For every consequential fact ask: who causes, operates, owns, accesses,
  benefits, is excluded, knows how; what it consumes and produces; which
  pawns interact with it; what happens to them when it changes and to it
  when they change. Where actors are represented, receipts trace
  actor -> need/capability/authority -> program/institution -> physical
  asset -> use/outcome -> changed state. Aggregate-only modeling is a
  failure wherever an actual actor is represented; equally, pawn properties
  are never derived from settlement averages -- individuals stay individual.

## Opening-flow arc (session of 2026-08-20, builds 9b5847c -> da76321)

| Item | State |
| --- | --- |
| Opaque workspace canvas (9b5847c) | REJECTED by operator: replaced vivid native world map + generated preview with a sparse hex diagram; wrong abstraction, not restyled - removed |
| Two-view model (e844055) | SHIPPED: real globe augmented in place (compose/arrival pills projected onto actual tiles) + generated Map Preview docked as CA's preview panel; no third geographic abstraction on the landing page |
| Stitch proportionality | VERIFIED in code + live receipts: backing frame = angular hull of member tile vertices at constant cells/degree; interior removal leaves frame identical (1526x1099), hull growth adds exactly the new tile's extent; per-cell facts from owning member |
| Choose arrival broken in every prior UI | ROOT CAUSE: armed poller branch had no selection-change gate - instantly consumed the pinned start tile and disarmed. FIXED (da76321); operator verdict pending (signed off before testing) |
| Preview regen on same-composition selection | FIXED: tile-selected prefix short-circuits when resolved composition matches the generated texture at exact backing; operator verdict pending |
| Arrival folded into geography composition identity | REMOVED (last arrival-preview coupling remnant); arrival is descriptive data only |
| Pending drafts refused over unauthored founding-culture shell (a66d199) | FIXED and verified live: the previously-refused draft restored cleanly next launch |
| Compose add/remove on new build | EXERCISED live by operator: 12->13->12->11->10->9->10 sculpting, frame reframed correctly each step, no errors |
| Unexplained diamonds / DIAGRAM badge / black rectangle | REMOVED (marks are now semantic pins/squares/rings; badge deleted; black box was the blanked WorldInspectPane - restored) |

## Opening-flow depth pass (2026-08-20 continuation, commits c5e2751..de78c87)

| Item | State |
| --- | --- |
| All eight tendency variables carry kernel-computed consequences | DONE: urban thresholds, spacing point-split, frontier counts/realization, distant budgets, frequency roll, span range, origins target-distinct (arithmetic moved into the shared kernel; runtime + dialog consume one function) |
| Tendencies presets | DONE: write-tables computed from the presets themselves; live match-state chips |
| Starting Region inspector controls | DONE by function: 37 ghost conversions + gated-disabled state; text fields native by choice |
| Founding technology consequence | DONE: translation-boundary answer at draft ranks (supported count + concrete gaps), revision-cached |
| Founding custom-society statement | DONE: states its three actual components |
| Settlement placement comparison | DONE: per-faction holdings + characterization in choices |
| Choice dialog comparison/density | DONE: replaces-context in details; group counts in navigation |
| Landing member inspection | DONE: column answers with the selected area's own ground/settlement/arrival capability |
| Preview compose parity + water depth | DONE: release-on-preview through the same funnel; qualitative depth in cell tooltips |
| Component editors (Culture / Political Order / Technological Knowledge dialogs) | OPEN: unaudited this pass |
| Operator runtime acceptance | OPEN: owner is the operator; no build launched |

| Component editors + remaining flow windows | DONE: all ten flow windows on the CAO surface; Technology editor gains the live rank→construction-consequence loop (shared revision-cached model function); Political editor's live identity verified pre-existing; Culture has no comparable closed-form (distributions are the state; Overview summarizes) - stated, not papered over |

| Input-routing defect (self-review) | FIXED: globe compose/arrival interactions could fire beneath the docked preview or the column (page pass sees events first); they now stand down when the cursor is over any other window |
| Vanilla-control straggler sweep | CLOSED at zero across all flow files, including Culture editor question internals and the flow-reachable causal inspector |

## Queue-boundary corrections and findings (2026-08-20, commits 527e4a6, b545dc1)

| Item | State |
| --- | --- |
| Landing facts recomputed every OnGUI pass | FIXED (527e4a6): the derivation (member sweeps, rock resolution, kernel + geography inspection, claimed-ground loop, groundwater survey) now reruns only when a read input changes (seed, membership, footprint claim, requested extent, arrival tile, settlement count, groundwater tuning); reservation validity is world-state-dependent and stays per-pass |
| Window-wrap retrofit onto BeginWindowSurface | Judged under the restored WHAT/HOW boundary: zero pixel/behavior change, never goal work. The mechanical edits had already been applied when the operator stopped it; they are behavior-identical and were kept as a ride-along (b545dc1) rather than churned a second time by reverting. No further time. |
| Page_CAConfigureFoundingIdeo / FluidIdeo | CLASSIFIED under the operator's vanilla-screen rule: the visible surface is native Page_ConfigureIdeo (RimWorld's own Ideoligion editor); the CAO portion is invisible state capture (CaptureNativeIdeo + session save in CanDoNext/DoBack). Left native. Operator may overturn. |
| Nine runtime-play windows | EVIDENCE: none is opened from any opening-flow surface; openers are DevTestModule, EpistemicInspectionModule, HomeIntentModule, OrganizationModule (3 sites), SurvivalModule (2 sites). Outside the opening-flow census; untouched. |
| Dialog_CARegionalOperationalRoles | FINDING for operator disposition: zero callers anywhere (RegionalOperationalSiteModule.cs:101) - dead window, not deleted on my own authority |

## Adversarial closure audit (operator-ordered, 2026-08-20, commits 6d91b5c, 8f240e1)

| Audit line | Finding and disposition |
| --- | --- |
| Map Preview, statically | RESOLVED from the mod's decompiled generator: previews run exactly the ElevationFertility/Terrain/MutatorPostElevationFertility/MutatorPostTerrain gensteps (plus CA's two merged regional steps via its registered predicate) on a real disposable Map with full thing/edifice/roof infrastructure, under a pushed Rand seed, guarded against concurrent real generation, disposed after each run. Critical/non-critical/final passes never run there, so Harbor and the ancient family are preview-inert by construction. Thing and roof spawning at the terrain pass was dead work (the preview texture reads terrain only) -- now skipped in preview; terrain statements stay. The former "preview tolerance of thing-spawning" runtime question is closed statically. |
| Shape state, threading | DEFECT FOUND AND FIXED: the preview worker thread reads plan.featureShapes (requests capture the reference; the composition contract enumerates it mid-generation) while the editor writes on the main thread. All mutation is now copy-on-write with an atomic reference swap. |
| Shape state, bounds | DEFECT FOUND AND FIXED: stored values were consumed raw; the resolution seam now clamps to the family's admissible band. |
| Shape state, the rest | Persistence: optional scribed node inside plan schema 16 (exact-match validator unaffected; old plans load empty, old builds ignore the node). Relocation: corresponding-area remap with carried-feature filter; footprint changes filter by retained membership. Identity: folded into the geography canonical (preview dedupe), the diagram cache signature, and the landing-facts key; the seam overlay keys on ownership, which shapes do not move. Consumers: registry-to-wiring cross-check is one-to-one across all families; preview and real generation read through the two request factories, the diagram through the same kernel, the editor through the same plan. Family fidelity: cove/valley/archipelago/plateau/scar omit degrees their native models do not possess (documented per family); Basin keeps its pinned center against authored wander by registry omission. |
| "Optional deepenings" test | Every degree of every family is editable through the registry-driven editor rows; position is additionally draggable for the inland-water family. Rotation/extent gestures are optional under the operator's usability test, not by assertion. |
| Ancient-structure family | THE AUDIT'S PRINCIPAL YIELD -- was still anchor-native and aggregate-anchored, including an unpatched non-critical genstep. All six now carrier-local (vents, ancient structure + perimeter scatter, abandoned colonies via the steered native settlement scatterer, quarries with the full native lump/column/roof contract, uplinks); ruins scale their native per-tile fill to the carrying share with positions still map-wide (named deviation -- the native generator has no maskable seam). Ice-dune spawning gains an edifice guard against an iceberg's seam-crossing walls. |
| Lakeshore / Harbor downstream | LittoralTerrainAt has exactly one consumer (the TerrainFrom postfix); GenStep_Outpost registers its own rects in UsedRects, so later placement honors the harbor. |
| Substrate-arc classifier | DEFECT FOUND AND FIXED: six families adapted by the earlier substrate arc still classified tile-local/region-wide, mislabeling the loss receipt. |
| B19 boundary | GOVERNANCE.md carries only the generic batch rule and does NOT itself draw a B18/B19 scope line. The boundary rests on the operator's ratified batch contract in SESSION_STATE.md ("Current B18 contract"): "B18 establishes the REGIONAL SUBSTRATE only... B19's acceptance boundary is Economic and Civil Order PLUS the first integrated runtime of the real CA settlement composition, absorbing integration, behavioral, and performance convergence there; no later batch is predeclared." The settlement-runtime rows above stand under that operator-owned contract, not under a governance file. |
| Stale sweep | Zero TODO/FIXME/HACK in Source; cave and river subfamilies covered by inheritance in the topology exclusions; per-worker private caches bounded and keyed by feature identity. |

## World Tendencies / World Presets product pass (operator-directed, 2026-08-20, commits 29f69cb, 97439a7)

| Requirement | Disposition |
| --- | --- |
| Preset coverage | Nine curated regimes replacing four, audited pairwise against the kernel: every pair differs materially in at least three consumer outcomes (urban thresholds 54-69; frontier counts 1-6 of 8 with cabin/homestead forms; origin targets 2-5 of 6; distant budgets 2-20 of 20; connection mids .10-.75; spans 2-3 to 6-9; placement .2-.85). The original four keep their exact values so existing matching states still match. New: City-states, Wide marches, Crossroads world, Quiet backwater, Imperial marches. |
| One preset interaction | The editor names the active state (preset name, or Custom with scribed provenance of the diverged-from preset); one 'World presets' browser reached by one entry; the preset-chip row and the misnamed 'Preset details' removed; the browser states the current state as its comparison anchor and closes back into the live-updating editor. Same StateName vocabulary on the world-params card, the editor, the browser, and the session summary. |
| Row hierarchy | Human concept -> control with anchor words -> current semantic state word -> one kernel consequence. Symbols, consumers, full causal prose, and secondary calculations behind the Causal-detail toggle and every row's hover. |
| Control semantics | Coupled frequency band = native one-range control with its numeric band label; region span = native integer range 1-10; continuous tendencies = sliders with semantic anchor words; band labels demoted from competing button rows to anchors, state words, and hover descriptions. |
| Authored vs realized | Distinct colors: warm for kernel consequences of authored values, blue solely for what a world rolled -- the roll marked as a tick ON the authored band plus a 'This world rolled X from A-B' line naming the [CA][Tendencies] receipt. Editing the band or applying a preset clears the stale roll. |
| Presets as explanatory objects | Each card answers regional structure, dispersion, urban emergence, frontier weight, origin diversity, and distant activity through the kernel, keeps the tendencies-not-guarantees note, and ends with the exact written values. |
| Preserved | Eight explicit canonical variables, independent editing, coupled band, integer span range, bundles-not-modes, shared-kernel consequences, receipt provenance, Custom-after-divergence, fresh defaults still equal to Balanced world. |
| Adversarial review findings fixed | Slider anchor words collide with the row header in the native layout (rail moved into its own band); consequence and realized readback shared one color (separated). |

Deploy note: the rebuilt DLL waits in .build-flow while RimWorld runs; it deploys the moment the game exits.

## Deployment defect: a session of builds never reached the game (found by operator runtime, 2026-08-20)

The operator's World Tendencies screenshot showed the old four-tab dialog after a launch that should have carried the rebuild. Diagnosis with evidence:

| Question | Finding |
| --- | --- |
| What rendered the screen | The old Dialog_CAWorldGeneration compiled into the stale DLL the game loaded -- one dialog class, one entry point; no second rendering path exists in source (grep: 'Preset details' appears in no source file). |
| Was the new code compiled | Yes: the intended build (SHA1 ac262a95) contains 'World presets' x2, all five new preset names, and zero 'Preset details' (UTF-16 string search of the binary). |
| Did the game load the intended DLL | No. The installed mod junction resolves to the REGIONAL worktree, whose LoadFolders.xml loads '/' for v1.6 -- the game loads <regional worktree>\Assemblies\ColonistAwareness.dll. That file was SHA1 6361849d, dated 12:51 -- the PREVIOUS session's final build. Every deploy this session had copied to the MAIN repo root's Assemblies\, a path the game never reads. |
| Harmony/routing seam | None. Pure wrong-target deployment. |
| Blast radius | Every runtime observation this session -- both operator inspection rounds -- exercised the pre-session build 6361849d. None of this session's work (landing memoization, features row, the twenty-plus mutator projection slices, canonical feature shapes, the tendencies rebuild) had ever been loaded. Every 'deployed' claim this session was false until now. |
| Fix and verification | ac262a95 copied to the worktree Assemblies with the game exited; hash verified at the loaded path; string-verified in place (0 x 'Preset details', all nine presets present). The four-tab + 'Preset details' screen is no longer renderable from the loaded assembly: the only class that drew it no longer contains it. The deploy-target memory is corrected with the resolved junction evidence. |

## World Tendencies / World Presets: representation rebuilt from the simulation (operator-directed, 2026-08-20, commit 634800c, deployed 2cd6daaa at the verified loaded path)

Both runtime screenshots treated as failed acceptance evidence; backend preserved (kernel, owners, nine bundles, provenance, persistence, authored-vs-realized). The operator-facing representation is rebuilt: four world facets on one unscrolled screen (the land / settlement / the frontier / the wider world), each tendency as human concept + anchored control + semantic state + one kernel consequence, facet interplay lines, zero implementation vocabulary in any visible string. One shared language across card, editor, and browser: state names, three semantic tones (authored ink / warm derived / blue realized), and an eight-dimension profile glyph. Causal detail is the comprehension chain (authored -> mechanism -> together with -> derives -> realized) in world language; no pseudo-debug layer is exposed -- a real diagnostic surface needs consumption traces, and symbol dumps are neither teaching nor debugging (deliberate scope statement, not an omission). The preset browser is purpose-built: glyph cards under a legend, no catalog chrome, and a comparison pane answering what would materially change versus the current authored state, kernel deltas only where outcomes move, unchanged dimensions named once, exact values demoted to last. Deployed-DLL string verification: new-surface strings present, failed-surface strings absent.

## Tendencies represented, not narrated (operator-directed medium correction, 2026-08-20, commit 5fd68ac, deployed 6734515e at the verified loaded path)

The prior pass changed vocabulary, not medium -- the operator predicted it before seeing it, and the pattern is memorized (represent-dont-narrate). This pass deletes every consequence sentence, interplay sentence, hover paragraph, and the five-part prose chain; the model is carried by live kernel-driven representations: a tile field joining into regions under the band and reach controls (using the world's actual roll in the realized tone when rolled), settlements gathering under the spacing slider, the urban support scale built from the real contributing facts as tagged fixed segments with the town/city bars moving on it and floors at the bars, an eight-square frontier board the sites tendency occupies while the holdings tendency changes each square's residents and build -- the last square poor land, visibly capped at a cabin, the binding constraint exposed -- six origin pips whose distinct hues are the distinct peoples, and twenty distant pips of which the lit ones advance. Text remaining: names, state words, one intro line, one caveat, one-sentence hovers. Deletion test holds. The Causal-detail toggle is deliberately removed: its chain became the screen, and a true diagnostic tracer is separate future work. The preset browser compares the same representations side by side -- changed pairs bright, unchanged dimmed under a scrim, numbers only as tags. Binary string-verified at the loaded path: representation-era strings present, narration-era strings absent.

## Create World recomposed: one world-generation interface (operator-directed, 2026-08-20, commit acdecc7, deployed 71866301 at the verified loaded path)

The operator rejected both the foreign glyph card the runtime showed ("immediately open to slop") and the vanilla-compatible token row I proposed next -- vanilla's layout is not the authoritative product grammar where CAO functionality is involved. The page is recomposed: vanilla's planet rows keep the top of the left column and factions keep the right, both untouched; the left column's remaining half -- formerly dead space -- becomes the world's character. A "World character" label row in the page's own 200px-label grammar names the authored state (preset name, Custom-with-provenance, or Custom). Below it a world vignette fills the column to its foot: the region diagram as the land, twelve settlement marks gathered by the spacing tendency and hued by the distinct founding peoples, the first marks ringed as towns where the urban threshold allows, frontier holdings filled by their material level, the distant world as a lit horizon strip, and a blue realized edge once a world has rolled -- every mark a kernel call on the eight owners, no decorative bars, no meters. Two doors pinned at the column's foot in vanilla's own button grammar: "World presets..." and "Author tendencies...". The page answers "what world am I about to generate, and where do I author it more precisely" with a named state, a drawn world, and two labeled doors -- not a paragraph, not eight meters, not one beige button.

Flow re-audit (Create World -> tendency state -> preset comparison -> causal authoring -> generation -> realized readback): every segment reachable and consistent. Readback within the opening flow works by construction -- the session adopts the plan's policy instance at plan construction, generation writes realized values into that instance, the world component scribes it, so reopening the page or either dialog after generation shows the rolled state in the realized tone. Post-embark readback has no player entry by design: that is the B19 integrated-runtime boundary, not an opening-flow gap. Deployed-DLL verification at the junction-resolved path with the game exited: hash 71866301, new-surface strings present, old-card strings absent.

Standing: World Tendencies and World Presets remain failed acceptance evidence until the operator's runtime pass; this entry-surface recomposition does not discharge that.

## The regional substrate: the world is actually regional (operator redirection, 2026-08-20, commits 6a18688..11eecb4, B19 development, NOT deployed)

The operator froze the World Tendencies / infographic iteration (runtime screenshots preserved from cao_world_tendencies_runtime_screenshots.zip; UI and palette work retained for later reuse) and redirected to the foundational inversion: CAO presented controls for a regionalized, populated, politically structured world before that world existed as persistent observable state. The from-code audit (no prior ledger dispositions used as evidence) established: regions were created only at authored confirmation or on-demand at first map visit, identity was rooted at the clicked tile, no policy field had a world-generation consumer, the frequency roll was lazy, settlement concentration and source variety never touched the authored path, RunWorldPass's policy parameter was dead, and the in-game globe carried read-only decoration over tile-owned selection.

### What was built, dependency-first per the directive's order

| Priority | State | Substance |
| --- | --- | --- |
| 1. Persistent world-wide topology | IMPLEMENTED + RECEIPTED | CARegionalTopologyKernel (pure partition: deterministic hashed seed order, cohesion growth, land-share feedback controller meeting the authored joined share exactly, merge-to-band fixpoint) + CARegionalTopologyRecord scribed per region (CA_regionalTopology) + WorldGenStep at order 450 + one-time post-load migration with registered footprints pre-owned + authored-footprint carving with connected-component splinter identities. |
| 2. Member lookup + neighbor graph | IMPLEMENTED | O(1) tile-to-region index and region-id index rebuilt from persisted membership; neighboring-region graph derived and cached, never scribed. |
| 3. Region-based globe selection | IMPLEMENTED | Both globes: selecting any land selects its region (footprint layer draws the selected partition region as one object; inspect label/text name the region, membership, settlements, and derived political standing). Setup selection is region-first: clicking new ground adopts the partition region as the candidate; compositions matching a partition region re-adopt its identity; relocation targets the destination's region. |
| 4. One region, one map | IMPLEMENTED | Materialization realizes the partition record (identity, members, realization seed rooted at the record; entry tile = arrival context only); a member of a registered region can never mint a competing plan (fails closed); the member map-open redirect defers to a tile's own MapParent so quest sites keep their encounter maps; settlements standing on a materializing region's members are absorbed where they stand as mandatory sources, consumption before reservation. |
| 5. Viewport / control plane | IMPLEMENTED | The region main tab serves every region: loaded regional map reads actual state (existing); from the globe, registered regions show persisted composition, unrealized regions show the deterministic kernel projection (cached by region id; member click pans to the member's subarea), settlements with faction colors, frontier holdings with resident/material tooltips, arrival mark, political standing in the header. |
| 6. Persistent settlements + political state | IMPLEMENTED | CARegionalWorldSettlementState per world settlement (founder, coarse population, urban class from real support facts vs the propensity threshold, founding date), scribed, rebuilt at materialization, migrated once for older saves, readable in the settlement inspect string. Political geography derived, never implied: unsettled / held-by-one / divided / contested from standing settlements and actual relations, on inspect and viewport. |
| 7. Ownership/jurisdiction overlays | PARTIAL (named) | Derived summary shipped; parcel/jurisdiction/government overlays await the B19 economy/civil layer that owns those facts (per the ratified B19 boundary). No fake overlays were built. |
| 8. Tendency consumers | See classification below | |
| 9. Realized readback | IMPLEMENTED (pre-embark; in-game via viewport/inspect) | Rolled frequency asserted into the persisted policy at generation instead of first-visit lazily. |
| 10. Terminology/infographic polish | FROZEN by operator | UI work preserved untouched. |

### Honest per-tendency classification (A sim-no-representation / B representation-no-consumer / C on-demand-only / D end-to-end / E no-product)

| Tendency | Before (code-verified) | Now |
| --- | --- | --- |
| Joined-region share (band + roll) | C - lazy roll, consumed per visited region only | D - partition consumes the roll at worldgen; receipts prove exact land-share targeting; regions visible/selectable on both globes |
| Region reach (span band) | C - per-visit extent | D - partition sizes inside the band to the merge fixpoint; backing maps and viewport consume membership |
| Settlement gathering | C - and only on the derived path; never authored, never world | D - world settlement scatter scored by concentration at worldgen (0.5 exactly vanilla-neutral); distant founding uses the same scoring; visible as actual placement |
| Urban development | C - realizedScale on plan rows, world-invisible | D-minus - world settlements carry persistent urban class from support facts vs threshold, in inspect; RESIDUAL: initial realization only (no longitudinal development), world class not yet fed into regional records at materialization |
| Frontier sites/holdings | C - per-plan and per-map only, no world representation | C+ - viewport now represents holdings with resident/material facts; RESIDUAL: no world-scale frontier state outside plans, no globe marks (named choice) |
| Settlement origins (variety) | C - derived-path reallocation only | D - worldgen faction distribution honors the distinct-source target; distant founding prefers unrepresented sources; reallocation retained |
| Distant world (rate) | B - real org-pulse consumer, invisible counter | D-minus - distant factions found dated, persistent, globe-visible settlements at an owned deterministic-gated cadence, capped at 1.5x founding count; org pulse retained; RESIDUAL: no growth/decline of existing settlements |
| RunWorldPass policy input | dead API | removed - faction social state derives from faction facts alone |

### Static and deterministic acceptance (tools/B19RegionalSubstrateReceipts, 21/21 PASS, CI-registered)

Coverage/disjointness, member-resolution single-valuedness, connectivity, byte-identical determinism, seed sensitivity, land-share at 0.10/0.40/0.80, span-band fixpoint, share-zero, pre-assignment exclusion, fragmented geography, carve components, unit hash, explicit Region(A)==Region(B)/Region(C)!=Region(A), concentration spread/cluster/neutral, variety monotonicity, urban threshold movement + support floor, frontier count/size separation + poor-land cap, off-map budget bounds, world-settlement fact determinism.

### Adversarial closure audit - what static work does NOT prove (runtime-only evidence, honestly)

Save/load round-trip of topology + states (scribe paths exist; the actual cycle is runtime); preview-to-map congruence on a live materialization; both-globes rendering fidelity; the Factions-prefix behavior against the real modlist's faction defs; migration on the operator's existing campaign; distant-founding cadence in play. Named residuals beyond the classification: (a) the viewport's derived preview uses the first registered region's map-size profile (or 250) until the world's local scale is chosen - preview scale may differ from eventual materialization scale; (b) world settlement population/class and regional record derivation are separate derivations pending unification at materialization; (c) setup-time clicking inside an already-registered region (only reachable on a restarted world) falls back to the legacy bundle path; (d) topology adds roughly 0.5-3MB to saves at large world coverages. Deployment was NOT requested and has not happened; the loaded path still carries 71866301 (the pre-redirection build).

## The opening feature made mechanically truthful (operator redirection back to the world-authoring feature, 2026-08-20, commits a6c1eda + a4c32a2, NOT deployed)

The operator held the acceptance boundary at the start menu: the regional substrate is preserved with its runtime validation queued, and Create World -> World Tendencies -> World Presets is the active work. The substrate audit supplied what the controls lacked - a named persistent consumer per tendency - and this pass rebuilt intelligibility on that ground.

What each control now states and shows, with its real consumer: Joined regions and Region reach draw the ACTUAL partition (the diagram runs CARegionalTopologyKernel.Partition live on a small grid - the land-share controller, band fixpoint, and cohesive growth are executing, not illustrated), and the hover names the world-forming division of all land. Settlement gathering places its marks by the actual WorldPlacementScore sequence, and names worldgen placement plus distant founding. Urban development keeps the true 137-point fact gauge (weights identical to the kernel), gains per-segment tooltips naming each fact, and names inspectable settlement standing; its bar tags anchor apart inside the rect, closing both runtime collisions from the operator's screenshots. Frontier controls name per-region holdings; origins names the three founding paths; the distant strip and vignette use OffMapActivityBudget itself and name the acting share plus founding cadence. The vignette's towns are the real support-versus-threshold comparison (replacing a fake derived count), and a four-word legend names the marks. Concept corrections: "tiles" is "areas" everywhere; the dialog intro no longer claims Starting Region choices replace tendencies wholesale (they take over only inside the authored region); the browser's land row keeps the roll caption inside bounds.

Hide-audit conclusion: no control is hidden - after the substrate, every tendency has a product-real persistent consumer, named in its own hover. Create World's character row carries the state name plus its one meaning line above the kernel-true vignette and the two doors; presets and tendencies remain two views of one authored state through shared drawings, StateName, and the same meaning line in all three places. Palette, banded controls, typography, and panel language untouched per the directive.

NOT deployed (deployment not requested; the loaded path carries 66aa3ca2 from the pre-directive deploy). Awaiting the operator's runtime pass on the opening feature; the substrate's runtime validation remains queued behind it.

## The representational standard redefined: cartography, never encoded primitives (operator directive with reference mockup, 2026-08-20, commits 96cc1e7, NOT deployed)

The operator rejected the rectangle/dot/block system in totality with runtime screenshots (cao_opening_ui_semantic_blocker_screenshots.zip): the rendered objects did not look like the phenomena they represent, and a legend cannot fix wrong form. The standard is memorized (representation-resembles-subject): RimWorld consistency governs window placement, input, frame, and transitions - never cartographic fidelity, terrain quality, settlement depiction, or graphical sophistication. Map Preview is the lower bound. A reference mockup set the clarity bar; its fictional controls (Resources and Riches, Harshness, Technology) were NOT adopted - the real eight tendencies drive everything.

What was built: one controlled representative world. A pure reference kernel builds fixed deterministic geography (continent, corrugated mountain spine with non-regional bare rock mirroring the substrate's impassable exclusion, rivers carved until they reach the sea, woodland masses, coastal shallows) and applies the ACTUAL kernels over its area graph - the topology partition, sequential WorldPlacementScore placement under vanilla's separation rule, urban standing from real support facts against the authored threshold (reference facts authored so the threshold's movement genuinely changes which places cross into towns and cities - verified in the differential renders), per-region frontier holdings, variety-driven founding peoples with per-people roof colors. A pure cartographic renderer paints it at 1280x800: hypsometric tint under hillshade, cased roads seeking low ground, joined regions as organically bordered warm-washed provinces, settlements as roofed building clusters with fields (hamlet, village, town, walled city with keep - visibly different scales), holdings as hut-and-field farmsteads.

The fidelity was iterated against rendered PNGs through five passes (the render tool, tools/B19ReferenceWorldRender, is CI-registered and renders the differential matrix: balanced, join low/high-wide, spread/clustered, urban low/high, frontier dense/sparse, origins repeated/varied). The no-legend perception test at the current state: geography, inhabited places, settlement-scale differences, clustering versus dispersion, frontier habitation, and connected-versus-fragmented regional structure are all recognizable unlabeled; the spread/clustered pair and the join-band pair read as visibly different worlds.

Surfaces: the tendencies editor is one controls column (banded controls preserved) beside ONE shared rendered scene - no per-control mini-charts remain - with the controlled-simulation framing ("Same reference seed, same geography - only the tendencies change"), eight state-word chips, and the representative-projection caveat. Preset cards carry each preset's rendered world; the comparison renders your world beside the preset's under identical reference conditions with changed tendencies named beneath (numbers only where the number is the point). The Create World vignette is the same rendered world. Textures build once per cadence tick (drag surfaces hold their last frame; identity-critical surfaces never show another world's map) and release on page close.

Honest residuals: (a) the post-generation regional viewport/control plane has NOT yet adopted the scene's visual language for its overlay marks - the projection ground texture is already geographic, but settlement/holding marks remain simple; that alignment is the standing next slice, per the directive's requirement that the persistent regional view exceed stock Map Preview; (b) the distant-world tendency currently reads only through its state chip - its non-spatial treatment inside the scene panel is open; (c) scene build cost ~40-80ms per state on the main thread, bounded by the cadence throttle - acceptable in menus, unmeasured on the operator's machine. NOT deployed; the loaded path still carries f243f8f3.

## Lineup completed for the runtime pass (2026-08-20, commits 0b2e42b + deploy f08cc974 at the verified loaded path)

The named residuals are closed: CAPlaceGlyphs carries the rendered scene's built-place vocabulary to GUI scale, and every regional overlay now speaks it - the in-game globe's faction diamonds are replaced by roofed settlement clusters that grow with realized standing (co-holders as smaller neighbors), the regional viewport draws plan settlements and hut-and-field holdings as places in both modes, and the tendencies editor gains the beyond-the-horizon band (twelve distant silhouettes, lit to the actual acting budget - the one deliberately non-spatial treatment). The setup widget keeps its area-anchored badges by the ratified B14 no-coordinate-promise contract, recorded as a named decision, not an omission. Deployed with the game exited: hash f08cc974 identical at build output and loaded path; new-lineup strings verified present; the two flagged absent-strings are live current copy (the realized-share line, the reach hover), not remnants.

The complete opening lineup awaiting the operator's runtime verdict on this build: Create World character zone (name, meaning line, rendered world vignette, two doors) -> World Tendencies (controls column beside the one shared rendered scene, state chips, horizon band, controlled-simulation framing) -> World Presets (rendered-world cards, side-by-side comparison under identical reference conditions, changed tendencies named) -> generation -> in-game globe (region selection, place-glyph overlay, inspect naming with political standing) -> the region viewport (persisted or deterministic-projection ground with place glyphs) -> the regional substrate beneath it all, its own runtime validation still queued behind the opening-feature verdict.

## Startup defect, per-class patch resilience, and the phantom-failure filter (2026-08-20/21, commits 988fd62, c6c93b9; deploys 47bb85ed, 58fdfa09)

The f08cc974 lineup build was dead on arrival at runtime (operator: "it went from create world to ideologion, no tendencies"): CAWorldSceneReleasePatch targeted Page_CreateWorldParams.PostClose by string, the class declares no such method, Harmony threw at mod instantiation, and PatchAll aborted with the entire mod. Fixed by targeting the declared Window.PostClose with an instance filter, AND the failure mode itself was removed: patches now apply per class, so one broken patch class logs loudly and stands down for the session while every other patch applies (988fd62, deployed 47bb85ed by the exit watcher, verified live). The fresh log then showed 62 "patch class failed" errors - all phantoms: the per-class loop had dropped PatchAll's own filter and fed Harmony every type in the assembly, so ordinary classes with methods named Cleanup/Prepare/Prefix (every JobDriver, every LordToil, the manually-installed helpers) were misread as patch directives. No real patch was affected. The filter (class-level HarmonyPatch attribute) was restored while keeping per-class isolation (c6c93b9, deployed 58fdfa09). Lessons banked: verify string-named patch targets against the decompile; a resilience mechanism must reproduce the semantics of the path it replaces.

## The graphics come out; words and the world carry the representation (2026-08-21, commits 1ba0fea, b10d91f, 8cb8b2a, f5ef7d3, 3de78a7; deploys 08ff340c then the standing exit-watcher)

Operator redirection in three steps, each accepted as given. (1) "Remove the graphics, keep everything else": the rendered reference world, its kernel, renderer, scene cache, render tool, vignette, preset thumbnails, side-by-side scenes, and horizon band were deleted (~1,900 lines); palette, panels, banded controls, copy, and start-menu compatibility preserved; the regional setup preview's 8x8 colored settlement squares replaced with the built-place glyphs (the representation belongs on the world surfaces, not the menu). (2) "Too detached and analytical; small type; 'towns are hard-won' describes no mechanic I've witnessed": the front door became World character - nine named worlds each carrying authored prose about the country itself, name at heading weight, description in a bordered block at reading size, differences from the current world demoted beneath, caveat quiet at the foot; the generated spec sentences moved entirely to the advanced surface (Adjust tendencies..., controls left, What-this-world-does panel right); the urban copy now states only the observable mechanic: a standing judged from what a place has, read by inspecting it. (3) The operator's structural challenge - is any of this visible on the globe, or only per-click; where do cross-factional arrangements live; how does this become history; where is the persistent per-region preview - was answered with verified code citations (the honest answer was "mostly not yet") and then closed under explicit permission:

- The globe now draws the WHOLE partition: every multi-area region's border, muted; brighter where the ground carries settlements; single-area regions deliberately undrawn (outlining them would re-grid the map). Mesh keyed to a world-state revision, never selection.
- Political state PERSISTS: one record per region (holder faction set, contested flag, dated events, cap 40) diffed at every mutation point - worldgen, materialization, distant founding - plus a daily pass catching vanilla capture/destruction. Materialized regions continue the same history thread on their plan identity after carve.
- History EXISTS: dated events per holder change ("X settled into <region>; the land is now divided among 2 polities"), read on settlement inspection (region, remembered status, latest event) and in the regional viewport (persisted status in the header; a quiet three-event History panel over the map corner).
- The viewport marks the ground's named features (landmark/historical monument glyphs, from the same facts source as the setup preview), alongside settlements, holdings, arrival, zoom/pan.

Honest residuals, stated as obligations: unrealized regions' viewport plans remain derived-and-cached (deterministic, so stable, but not scribed); no representation distinguishes federation/jurisdiction beyond held/divided/contested - that political structure does not exist yet and was not faked; the partition-layer draw cost on a large world is unmeasured on the operator's machine; ALL of the above is UNACCEPTED pending the operator's runtime pass. The standing exit-watcher deploys the current build and relaunches on the next game exit.

## End-to-end propagation audit of the eight tendencies (2026-08-21 overnight, commits 053f69f..6c27f58, deployed e866efb0)

The operator's question was whether the choices the player can make
actually propagate into the running game. They were traced in current code
rather than from names, comments, prior ledger rows, or receipts. The
governing fact discovered first: **no world has ever been generated with
any of this**. `Player.log` carries zero `[CA][WorldSettlements]`, zero
`[CA][Topology]`, and zero generation markers across every session to date;
every runtime observation so far has been of menus. The whole regional
substrate and its consumers have never executed once, which reframed the
pass from adding behavior to making the first execution survivable and
truthful.

### What each tendency actually does, traced

| Tendency | Real production consumer | Terminates at | Honest status |
|---|---|---|---|
| Joined regions (band) | `WorldGenStep_CARegionalTopology` order 450 → `Partition` over every eligible surface tile | persistent scribed topology; globe partition layer; region-first selection; one-map-per-region | CLOSED to persistent world state, unobserved at runtime |
| Region extent (span) | same partition call | member counts per region → the map you get on entry | CLOSED to persistent state, unobserved |
| Settlement gathering | `FactionGenerator.GenerateFactionsIntoWorldLayer` prefix (order 500) → `PlaceSettlementTile` → `WorldPlacementScore`; also distant founding | actual tile of every generated settlement | CLOSED, and REPAIRED tonight — see below |
| Urban development | `BuildState` → `SettlementScale` → `urbanClass` | a persisted classification, surfaced only in the settlement inspect line; regional plans additionally feed `CulturalExpressionModule` and the population screen | CLASSIFICATION ONLY. Nothing grows over time. No physical, material, or behavioral consequence follows scale for world settlements. Recorded as truth, not repaired — inventing growth was out of scope |
| Frontier sites / holdings | `PopulateDerived` → `RealizeFrontierHoldings` on the materialization path | plan records, persisted with the region; drawn in viewport and preview | REGION-SCOPED BY DESIGN. Holdings never become world objects and exist only for regions that materialize or are being viewed. The world at large has no holdings |
| Settlement origins | `ChooseFaction` inside the worldgen prefix | which factions own the world's settlements | CLOSED, unobserved |
| Distant world | `PulsePoliticalBeliefsUnderBudget` (background org belief pulses) and the 900k-tick distant-founding cadence | real new `Settlement` world objects carrying founding dates; persistent organization belief state | CLOSED and genuinely longitudinal. Evidence reaches the player as a new settlement whose inspect line states its founding date; there is deliberately no letter |

### Defects found and repaired

**Settlement gathering did essentially nothing at real world sizes.** The
scoring kernel discriminates across a 0..15 band; the production caller
passed raw tile distance. Real planets place settlements tens of tiles
apart, so effectively every candidate saturated the cap: spread stopped
separating from neutral entirely, and clustering only bit when a sample
happened to land within fifteen tiles. The caller now scales measured
distance into the band by the world's own mean spacing. The kernel is
untouched. This is the single repair most likely to make two World
Characters produce visibly different worlds.

**The receipt suite could not have caught it.** `concentration-placement`
fed the kernel distances already inside the band, proving the function
while the integration that feeds it was inert. A second receipt now asserts
world-scale behavior in both halves — that raw 25 and 60 tiles score
identically (the defect, kept as evidence) and that band-scaling restores
separation. 22/22.

**Worldgen settlement placement could have produced a world with no
settlements.** The prefix suppresses vanilla placement wholesale and returns
false; any throw inside it had no error path. The body is now guarded and
vanilla completes whatever remains unplaced.

**A failed partition would have refused world creation.** `EnsureTopology`
runs inside generation and was unguarded. Every consumer already gates on a
non-empty topology, so the designed degradation existed but was
unreachable; a failure now empties the partition, logs the cause with an
explicit statement that the world will have no regional behavior, and lets
generation finish.

**The political ledger added tonight bloated saves and swept too eagerly.**
It persisted a record for every joined region including the thousands no
faction holds — unclaimed is the absence of a record — and resolved every
region's name eagerly on a daily pass. Both corrected.

### Unmeasured, and deliberately left to runtime

Partition build time across a real planet, and the partition draw layer's
cost across every joined region on the globe, have never been measured.
Both now report their own elapsed milliseconds and record counts to the
log, so the morning session produces the numbers instead of an estimate.

### Not repaired, and why

Urban development changes a classification and nothing else; growth over
time does not exist anywhere in the code. Frontier holdings are
region-scoped and never enter the world. Both are recorded as they are.
Repairing either means adding simulation the ratified architecture does not
currently establish, which is the operator's decision and not a defect to
fix unattended.

### Second pass: missing consumers implemented, region-identity breaks closed (commits 1290bc4..7a6ae50)

The first pass stopped at describing incomplete chains. This pass traced
them and implemented the missing consumers where intent was already
established.

**Region identity — four breaks, all closed.**

*The pre-landing preview manufactured competing geography.* `EnsurePreviewPlan`
never consulted the partition: it built a stock hex bundle around whichever
tile was clicked under a freshly minted `CA-RG-` id. On every fresh world the
previewed member set, identity and backing size therefore contradicted what
materialization would create. It now realizes the record through the same
`CreateFromTopology` the materialization path uses.

*Arrival changed composition.* A settlement standing on the arrival tile was
skipped when absorbing residents, and arrival defaults to whichever member
the region was entered through — so the same region absorbed a different set
of settlements depending on where the player came in, frozen into the scribed
plan at first materialization. Absorption is now arrival-independent.

*The political ledger double-wrote.* A realized plan adopts the partition
record's id and the carve deliberately leaves that record standing, so both
ledger loops reached the same `regionId` with different holder sets —
flip-flopping the persisted record and appending a false dated event every
daily sweep, forever. The plan is now the single writer wherever one exists.
This defect was introduced earlier the same night.

*The carve guard keyed on identity alone*, so a plan adopting an id while
covering different ground would have left the partition asserting tiles the
plan had taken. It now requires membership equality — the invariant the three
breaks above were leaning on.

**Missing consumers, implemented.**

*Owned settlements could not diverge.* B15 establishes that a settlement
references its faction's state "unless an explicit local divergence is
modeled", eight consumers read that flag, and gated local editors exist for
it — but its only writer detached the site from its owner first, and every
owner-assigning path reset it to false. The owned-and-divergent case had
consumers, editors, and no producer. `EnsureDivergentState` is that producer:
the site keeps its owner and gains its own political, technological and
institutional state seeded from that owner, with one control driving it.

*Absorbed settlements lost their population.* A settlement standing on a
region's members was absorbed with a freshly invented population, so the same
place could read as a town on the globe and a village inside its region.
`absorbedWorldPopulation` (scribed, additive, `-1` on every existing save)
carries it, consulted after operator authoring and before fact-derivation.

*World settlement records were never maintained.* Written at generation,
materialization and distant founding, and never again — so a captured
settlement kept its original owner and standing and the inspect line was
wrong from the first capture. A daily pass now rebuilds them, silent when
nothing differs so the world-state revision does not force a globe mesh
rebuild for nothing.

*Frontier holdings never reached the world map*, so a region worked by
homesteads was indistinguishable from an empty one; they now draw in the
built-place vocabulary, and a holdings-only region is no longer skipped
entirely. *The settlement tooltip never stated scale* although the glyph
collapses a shared tile to its largest place. *The tile inspect pane
recomputed its own political summary* while the settlement pane read the
persisted record, so the two could disagree about one region; both now read
the remembered state and its latest dated event.

*The regional preview used the wrong map scale.* It asked the first
registered region and fell back to a literal 250, while materialization
resolves pending size → this game's chosen size → the world's initial size.
Same chain now.

*The off-map budget throttled on-map subjects.* Frontier holding
organizations exist only for holdings on a LOADED player map, yet were
classified background and throttled by the distant-world rate. They are now
always live.

### Genuinely operator-blocked (product intent absent, not implementation)

- **Urban development has no consequence beyond classification.** Nothing
  grows over time anywhere in the code. Deciding what a "town" should *do*
  differently is a simulation-scope decision.
- **Frontier holdings are region-scoped and never become world objects.**
  Whether the frontier tendencies should populate the whole world is a scope
  decision; the ratified architecture establishes only per-region realization.
- **Off-map settlement organizations never acquire customs**, because
  `SyncRegionalSettlements` skips records with no loaded map. Pulsing them is
  provably a no-op, so the distant world's *political drift* is inert even
  though its *founding* is real. Whether off-map settlements should simulate
  political change is a scope decision with real cost.
- **Jurisdiction and claims have no canonical field at all** — only derived
  holder state. Representing disputed or overlapping claims would be new
  ontology.
- **Settlement-level organizations** are expressible only through owning
  factions; federations spanning settlements directly are not representable.
- **Extent and rotation rebuild a partition-derived region from a bundle**,
  giving it a fresh identity rather than composing from the record. Whether
  those controls should be offered for partition regions at all is a product
  question.

### Known dead state, left in place deliberately

`CASiteLocalSocietyState.institutionalStateIncomplete` is written and scribed
but never read for a site; its faction-level twin gates structure generation,
and no site-level generator exists to gate. `CASiteFactionReferenceKind.WorldFaction`
ownership is validated and resolved but `SetWorldOwner` has no callers.
Removing either is schema churn with no product benefit tonight.

### Construction material and world frontier (commits 4039ec2..3e6a83d)

The operator's correction: the World Tendencies parameterization promises
consequences that have not been built, and this is greenfield - the pieces
exist for one system but were never intertwined. Material is not
`materialLevel = 2 -> stone walls`; it follows from development pressure,
technological knowledge, local resources, trade and access, population and
material capacity, culture where relevant, and what is actually available.

**Material had three uncoordinated answers and one dead input.** The
frontier shell was hard-coded to wood whatever its holding said; the
settlement fit-out kept its own local-rock palette; morphology used a
tech-tier band; and `materialLevel`, written by the frontier tendency and
drawn in glyphs, had no consumer in any physical build. One settlement could
be three materials depending which system placed the thing.

`CAConstructionMaterials` is now the single answer, resolving valid choices
from the facts that decide them and ordering that set by development
pressure rather than letting pressure select a material outright. Its
arithmetic lives in a pure kernel (`CAConstructionMaterialsKernel`) so the
thresholds exist once and can be exercised without an engine. The frontier
shell, the fit-out palette, the morphology palette, and longitudinal
development all consume it; the longitudinal path additionally draws only on
materials the settlement actually holds, because a place adding to itself
cannot build from what it does not have.

Two defects surfaced inside that work and were fixed: the holding context
passed its development level as its economic capacity, counting it twice;
and the capacity thresholds were calibrated for settlements counting people
in hundreds, which pinned every frontier holding to the lowest material
however developed - the same saturation that had made settlement gathering
inert, reproduced inside the fix for it. Three receipts now assert reach
across a cabin, a homestead, a village and a city, that ambition never
exceeds means, and that within reach the more worked material wins while
local ground breaks a tie against an import.

**Culture is named as an absent input rather than implied.** Nothing in the
culture registry bears on what a place builds from - no material, craft,
permanence or display axis - and cultural expression yields a signature
rather than a building preference. The module states where such an axis
would enter.

**Urban standing reached the built world.** The materialization site already
stated that co-siting sets the floor and a settlement's own standing may
raise its internal complexity, and implemented only the floor, so an urban
centre and a hamlet on the same ground built identical complexity. Standing
now raises quarters.

**The frontier exists across the whole world.** Holdings were built only
inside regional plans, which exist only where the player has been, so an
open-frontier world and a heartlands world were identical across nearly the
entire map. Holdings are a deterministic consequence of facts the world
already holds, so they derive on demand from the same kernel calls the
plan-side realization uses, cached per region and flattened once per world
change. The world view screens on land capacity while realization also
screens habitat viability, so a region may show a holding on a tile its
realization declines - counts and character hold, the exact ground is
settled at realization. That difference is stated in the module rather than
claimed away.

**The distant world can now drift.** The off-map budget mostly selects
faction bodies, and faction bodies had never been reconciled against any
structure, so pulsing them could not open a conflict, cost support, or
recover any - the whole pass was a provable no-op however high the tendency
was set. A faction body's structure is that faction's own represented
institutions, which are world state needing no loaded map.

Repository gates held throughout: 26/26 substrate receipts, synthetic-state
census 316 classified / 0 unresolved, persistence census 267 carriers / 0
unrouted, tracked assembly matching a clean production build.
