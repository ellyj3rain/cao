| Document | Colonist Awareness Overhaul Session State |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.7.1.0-alpha` · closed batch tip `B18` · next `B19` |
<!-- cao:generated:version END -->
| Repository | `SESSION_STATE.md` |
| Status | CLOSED record; ACTIVE opening-flow product pass on the branch tip awaiting operator runtime acceptance. |

# Session State

Updated 2026-08-20 19:25 UTC / 12:25 PDT.

Read this before claiming where creation or gameplay testing stands. Compile,
receipts, deployment, and operator runtime evidence remain separate.

## Opening-flow product pass (post-B18 tip, unaccepted)

The operator rejected two presentation models (opaque workspace canvas;
shell-only theming) and holds the acceptance boundary. Current tip carries,
across commits `05122ea..HEAD`, all UNACCEPTED until operator runtime
verdict:

| Surface | State on tip |
|---|---|
| World tendencies | Rebuilt three times under operator direction, current tip = representation-first (commits 634800c, 5fd68ac, acdecc7; deployed 71866301 at the verified loaded path): the Create World page recomposed so the left column's lower half carries the world's character (state name in the page's label grammar, a kernel-driven world vignette filling the column, two foot-pinned doors: World presets... / Author tendencies...); the editor is four world facets whose model is carried by live drawn representations (region tile field, gather field, tagged urban support gauge with moving thresholds, eight-square frontier board with the poor-land cap exposed, origin pips, distant pips) with prose deleted to names/state words/one caveat; the preset browser compares the same representations side by side, changed pairs bright, unchanged dimmed. Backend preserved throughout: eight canonical owners, coupled band rolled once per world, kernel as the one shared function, nine audited presets, provenance + Custom-after-divergence, scribed persistence, `[CA][Tendencies]` receipt. Realized readback pre-embark works by construction (session adopts the plan's policy instance; generation writes into it); post-embark readback is B19 boundary. All UNACCEPTED pending operator runtime. |
| Landing page | Hover-outline compose/arrival on the real globe (pill grids removed); vanilla chrome/gizmos/inspect pane withheld while CA owns the page; column carries progression + per-area inspection of the vanilla-selected member. |
| Arrival | Single owner `TrySetArrival` (+ query `ArrivalRefusalFor`); selection-change gate fixed the never-worked armed flow; footprint layer draws the arrival hex (warm fill+rim) and regenerates on arrival change; arrival removed from geography composition identity; same-composition preview regeneration deduped. |
| Regional preview | Map Preview presentation owned by CA when a plan is bound: lane-filling region aspect, zoom/pan/reset, per-cell terrain/area/water tooltips from the generated map, member seams, settlement marks + names, landmark pins, arrival ring, arrival-click and compose-release parity with the globe. |
| Starting Region page | CAO shell + nav; rail rows state population/area/role, orphan rows carry warn-bar + full states/hover-emphasis parity. Inspector panels verified cause-authoring by read; their 37 value-chooser/action controls converted by function to kit ghosts with a gated-disabled state (text fields deliberately native). |
| Player Founding page | CAO shell + nav + themed card chrome; Technology card computes the translation boundary's actual answer at the draft's ranks (supported constructions + concrete gaps, cached by knowledge revision); custom society states its three actual components; arrangement belief-vs-landing comparisons and validation pre-existing. |
| Component editors + flow windows | Census closed: Culture, Political Order, and Technology editors plus the founding-terms, political-mixture, population-group, settlement-programs, culture-rename, and both profile windows all draw the CAO surface; the Technology editor recomputes its construction consequence live as ranks change (shared, revision-cached model function). Runtime-play windows deliberately untouched. Self-review found and fixed a real input-routing defect (globe compose/arrival interactions could fire under the docked preview or column; they now stand down when the cursor is over any other window), and the straggler sweep closed to zero vanilla buttons, menu sections, alt-rects, and light-highlights across every flow file, Culture editor internals and the flow-reachable causal inspector included. Cache-lifecycle audit fixed a cross-world staleness bug (seam-overlay identity now folds the world seed) and hardened the seam builder; the window shell is now a named kit primitive (BeginWindowSurface/EndWindowSurface) for future flow windows. Landing-column facts are derivation-memoized behind an input key (reservation validity stays per-pass), and the column states the region's carried special features with incidence counts. The radial lake mutator family (Lake/ToxicLake/Pond/DryLake/LakeWithIsland) is now projected per carrier at one-tile scale instead of running aggregate-scaled on the anchor and lost on members -- kernel re-expression, generation flatten+terrain at native order, diagram water by depth and kind. Wetland, Bay, Fjord, Peninsula, CoastalIsland, Iceberg, Valley, and all three lava workers are likewise projected per carrier (each with its native model; deviations named in the ledger); every operator-numbered punch-list item has a shipped disposition, and the geometry-mutator projection arc is fully closed (Lakeshore via the fresh-shore model, Harbor via carrier-local re-expression), and #12 is implemented as one canonical authored-shape state with per-family degrees of freedom, identity-seeded defaults, composition-identity folding, and the 'Shape features...' editor as its first control surface -- direct spatial editing remains an optional consumer the state enables. The shape capability's consumers: landing column ghost, preview click-select/double-click-shape (inspect mode), region-page feature inspector; member-carried mutators now tick (eruptions schedule for members); the forty resolution wirings verified fallback-exact (one slider-word inversion found and fixed). The operator-ordered adversarial closure audit then ran: Map Preview was resolved statically from its decompiled generator (the thing-spawning question is closed -- spawning is now preview-skipped as dead work), shape state gained copy-on-write thread safety and band clamping, the still-anchor-native ancient-structure family (vents, structures, colonies, quarries, uplinks, ruins) went carrier-local including the previously unpatched non-critical genstep, and the scope classifier now names the substrate-arc adaptations. Remaining runtime-only evidence is experiential: visual fidelity of the projections, the authoring loop's feel, and the slices in play. DEPLOYMENT CORRECTION 2026-08-20: the installed mod junction resolves to THIS regional worktree (LoadFolders '/'), so the game loads <worktree>\Assemblies\ColonistAwareness.dll; a full session of builds had been copied to the main repo root's Assemblies instead and never loaded -- every runtime observation until the correction exercised the pre-session build 6361849d. The current build ac262a95 (all session work included) is now verified at the loaded path; the next launch is the FIRST runtime exposure of this session's entire stack. |
| Choice dialogs | Shared kit themed; inspecting an alternative shows what it replaces; groups state counts; world presets state exact writes; settlement placement states each faction's holdings + characterization; society presets verified appropriate as built. |
| Save-killer fix | Pending drafts with unauthored founding-culture shells load (verified live on the previously-refused draft). |

Commit trailer rule honored from `GOVERNANCE.md` reading onward (no
Co-Authored-By for automated tools); earlier tip commits carry the trailer
and stay as-is (ref rewriting is operator-owned).

## Version control and governed history

| Surface | State |
|---|---|
| Active checkout | `C:\Users\jleyv\Peanut Butter\AI Assisted Software Engineering Mass Repository\Projects\colonist-awareness\.claude\worktrees\rimworld-regional-multithreading-47e9ec` |
| Branch | `mallowfluff/b18-authoring-state-convergence` |
| B18 starting point | `6b1bf1316e793f2409a7f13cf8b5288db8b85b5b` (B17 closure) |
| Closed chronology | `A1-A102` and `B1-B18`; B18 is kohai capability unit `VU-045`; `B19` is next. |
| Version | Semantic replay derives `1.7.1.0-alpha` from 45 contiguous capability units. |
| Publication route | The current branch publishes to protected `main` through the repository's hosted PR checks and squash-merge policy. |

## Current B18 contract

B18 establishes the REGIONAL SUBSTRATE only. The exercised settlements are
still substantially generic RimWorld settlements; the measured FPS/TPS does
not predict the completed CAO simulation. B19's acceptance boundary is
Economic and Civil Order PLUS the first integrated runtime of the real CA
settlement composition, absorbing integration, behavioral, and performance
convergence there; no later batch is predeclared.

| Surface | Current state |
|---|---|
| Generation performance | Complete 400x6 generation 121.8 s across four fresh-process self-terminating runs (257.3 s pre-optimization); 300x6 ~142 s; 200x4 ~51 s; run variance ~1%. The shipped assembly differs from the last measured build only in combat-topology capture code outside the generation path and carries no separate measured claim (the operator ended the benchmark sessions). Retained optimizations: deferred cargo recent-memory refresh, cached frozen plant terms, exact animal locality masks, nested RegionGrid rebuild bypass, district mutation fast paths, native terrain progress reporting. |
| Creation reachability | Program rooms, program assets, provision stock, and generated residents all join the settlement's map-edge-connected pawn network (the native `CanReachMapEdge` PassDoors contract). The program room selects its door side by connected approach and rolls back cleanly; `CAProvisionAccessService.HasAccess` is unweakened. Repeated fixed-seed runs at 200x4, 300x6, and 400x6 hold 4/4 settlements created, all programs materialized, all provisions operational. |
| Passive-play matrix | Marker `SIZExCOUNT-passive` runs paused/static, paused/camera-pan, Normal, Fast, Superfast, open CA surface, raid, fire, a full 60,000-tick day, a sealed disposable save, size-gated reload, and post-day verification, with per-phase frames/hitches/TPS/CPU/heap/GC and the bounded module profiler, persisted per phase. `-notrace` suppresses the behavior trace for one session for cost attribution. |
| Save seal | The emitted-save seal validated its first real current-boundary campaign save; the latent refusal queue it exposed is closed (topology recency horizon, force-serialized default fields, schema stamps on eight record classes, death-tolerant reference contracts, three kernel corrections). Two sealed ~108 MB saves prove the save side. |
| Named native debt | Intermittent ntdll 0xc0000005 at offset 0x70f32 under sustained synthetic combat (3 occurrences, WER dumps preserved); native load of a 108 MB regional save measured past 110 minutes (CA streaming preflight is 3.6 s of it) - the matrix's reload is size-gated with an explicit no-claim above 40 MB. |

## Governed runtime fixture

| Fact | Value |
|---|---|
| Runtime keyed plan | `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml` |
| Governed mirror | `%USERPROFILE%\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml` |
| Pair identity | 679,624 byte-identical bytes; SHA-256 `0381E19688C708DA9AA20E800155F56DD421F64D0E22AB0EBD7B856CA5C6832A` |
| Current schemas | pending authoring epoch 15; regional plan 16; settlement population 2; frontier holding 2; frontier map plan 3; settlement record 10; campaign catalog 6; proposition knowledge 2 |
| Benchmark identity | The convergence exercise runs a disposable fixed-seed candidate (world seed `CA-B18-PERFORMANCE-CONVERGENCE`, tile seed 181806, run seed 181814) that never enters durable world state; cross-process runs reproduce the same structural outcomes with in-map name/±1-rect jitter. |

## Verification and deployment

| Gate | Result |
|---|---|
| B18 static convergence | **PASS** - 10/10 authoring-state contracts against the B18 failed-run fixture |
| Creation and provisioning | **PASS** - repeated 200x4, 300x6, and 400x6 fixed-seed runs: 4/4 creation, programs materialized, provisions operational, faction-state receipt PASS in every completed run |
| Passive matrix | **PASS** - full in-game-day soaks at 200x4 (three completions) and the completed 400x6 matrix (3600 s capped soak, 51,321 ticks); CA subsystem cost ~2% of frame time and CA draw ~3 ms/frame; map things flat across the day; post-day receipts causal |
| Save seal | **PASS** - two full current-boundary saves sealed by the complete streaming preflight at 200x4; the 400x6 artifact-census overflow was corrected and is verified by B11 (78/78) and the static contracts, not by a rerun; reload size-gated as named native debt |
| Retained suites | **PASS** - B10 75/75 + sweep 189/0; B11 78/78; B12 113/113; B13 67/67; B14 24/24, 12/12, 12/12, 21/21, 22/22; B15 full; B16 54/54; B17 full; behavior convergence 98 verified / 107 |
| Reproducible Release build | **PASS** - .NET SDK 8.0.423; two clean byte-identical builds; 0 warnings and 0 errors |
| Deployment | **PASS** - the installed mod path is a junction to this worktree; installed assembly SHA-256 matches the verified build |
| Repository gates | **PASS** - version replay `1.7.1.0-alpha` (45 units, B19 next), version tests 4/4, B10 audit classification current |

Current executable receipts in `Receipts/B18` establish source, fixture,
performance, matrix, build, and deployment evidence. They do not establish how
the flow looks or plays.

## Operator runtime boundary

After final deployment, a fresh operator launch is the next evidence boundary.
Claude does not choose values, advance creation, start the game, alter saves,
or claim visual or gameplay acceptance for the operator.

| Runtime focus | Operator check |
|---|---|
| Regional start | Confirm the fixture region loads and a 400-scale start reaches a playable map within the measured envelope with visible progress throughout. |
| Settlement provisioning | Visit generated settlements; confirm kitchens, tables, and stock are physically reachable and residents use them. |
| Passive play | Play an ordinary day; confirm responsiveness matches the measured profile and nothing degrades over the session. |
| Save/reload | Save and reload the real campaign; note wall time (large regional saves carry a measured native load cost). |
| Crash watch | The intermittent native fault fired only under sustained synthetic combat in unattended benchmarks; note any occurrence in ordinary play. |

## Environment and preserved evidence

- RimWorld target: 1.6.4871 rev591.
- RimWorld was operated only through the disposable convergence exercise;
  operator saves, autosaves, and checkpoints were never modified, and the
  exercise deletes its own disposable save. Benchmark sessions the operator
  force-quit are preserved under Receipts/B18/Performance but excluded from
  every claim.
- `Receipts/B18/B18_PERFORMANCE_RECEIPT.md` is the governed performance
  record; `Receipts/B18/Performance/` preserves every run's logs, receipts,
  and the crash/reload evidence trail.
- WER dumps for the intermittent native fault are preserved in
  `%LOCALAPPDATA%\CrashDumps` (`RimWorldWin64.exe.179968.dmp`,
  `RimWorldWin64.exe.83040.dmp`) with a full process dump of the reload
  grind under the session scratch `ca-b18tools\dumps`.
- Operator control of time, pawn orders, windows, saves, and autosave remains
  unchanged unless explicitly requested.
