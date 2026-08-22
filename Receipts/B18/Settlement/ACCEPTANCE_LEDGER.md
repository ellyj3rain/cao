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
