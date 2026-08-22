# B18 - Authoring-State Convergence and Runtime Performance

| Field | Record |
|---|---|
| Batch | `B18` |
| Date | 2026-08-18 to 2026-08-19 UTC / 2026-08-18 to 2026-08-19 PDT |
| Name | Authoring-State Convergence and Runtime Performance |
| Status | Closed append-only batch |
| Threads | [`T-001`](THREADS.md#t-001), [`T-002`](THREADS.md#t-002), [`T-009`](THREADS.md#t-009), [`T-013`](THREADS.md#t-013), [`T-019`](THREADS.md#t-019), [`T-020`](THREADS.md#t-020), [`T-021`](THREADS.md#t-021), [`T-022`](THREADS.md#t-022), [`T-027`](THREADS.md#t-027), [`T-030`](THREADS.md#t-030) |
| Local Git commits | The B18 source, tools, governance, and receipt commits on `mallowfluff/b18-authoring-state-convergence` carry this record and its evidence. |
| Build | Two clean Release rebuilds complete with zero warnings and zero errors and are byte-identical; identity is recorded in `Receipts/B18/B18_REPRODUCIBLE_BUILD_RECEIPT.md`. |
| Receipts and verification | `Receipts/B18` records the static authoring-state convergence contracts, the fixed-seed performance evidence at 200x4, 300x6, and 400x6 scales, the passive-play matrix runs with their defect-repair lineage, the retained B10-B17 suites, and deployment identity. |
| Corrections | Repairs the settlement-creation reachability contract (program rooms, program assets, provision stock, and generated residents all join the settlement's map-edge-connected pawn network); bounds the combat spatial log's save footprint with a named recency horizon; repairs the emitted-save seal's latent refusal queue (default-omission fields, missing schema stamps, death-reachable null references, and three kernel contracts corrected to shipped or designed state). |
| Provenance | Begins from exact B17 closure `6b1bf1316e793f2409a7f13cf8b5288db8b85b5b`. The B18 performance-convergence directive, the fixed-seed convergence exercise, the decompiled engine ground truth, and the operator's governed fixture establish the implemented boundary. |
| Runtime fixture | The active keyed plan and governed mirror remain one byte-identical current-schema pair (SHA-256 `0381E19688C708DA9AA20E800155F56DD421F64D0E22AB0EBD7B856CA5C6832A`); the convergence exercise runs its own disposable fixed-seed candidate and never enters durable world state. |
| Deployment | The installed mod path is a junction to this worktree; the verified reproducible candidate is promoted to `Assemblies/ColonistAwareness.dll` and compared by SHA-256 with RimWorld closed. |
| Source relation | Follows `B17` and forms kohai capability unit `VU-045`, deriving `1.7.1.0-alpha`; no former-ledger label applies. |

## Record

B18 turns the complete start-game path into a measured performance-convergence
loop and closes it correct. The retained generation optimizations - deferred
native recent-memory refresh for starting cargo, cached frozen plant terms,
exact animal locality masks, the nested RegionGrid rebuild bypass, district
mutation fast paths, and native terrain-generation progress reporting - hold
their demonstrated gain: complete 400x6 generation fell from 257.3 s
pre-optimization to 121.8 s across four fresh-process self-terminating
runs, under the preferred three-minute target, with 300x6 at ~142 s and
200x4 at ~51 s and run-to-run variance near one percent. The shipped
assembly differs from the last measured build only in combat-topology
capture code outside the generation path; it carries no separate measured
claim because the operator ended the benchmark sessions.

The batch's correctness repair closes the one remaining creation defect: a
deterministic 9x9 program room could satisfy the static siting validator while
its single unconditional south door abutted a neighboring wall, leaving a
completely materialized communal provision unreachable by every resident. The
repair is one coherent boundary - program placement joins the settlement's
map-edge-connected pawn network, the same `CanReachMapEdge`-through-doors
network native settlement generation guarantees its inhabitants. The siting
validator counts only connected rooms and ground; the program room selects its
door side among all four cardinals by a walkable connected approach, verifies
its built interior joins the network after region rebuild, and rolls back
cleanly to the next site otherwise; program assets and provision stock accept
only connected cells; and CA's direct BaseGen population push applies the
exact native settlement spawn predicate it had dropped.
`CAProvisionAccessService.HasAccess` was not weakened. Repeated fixed-seed
runs at every scale materialize 4/4 settlements with all creation gates true,
all programs materialized, all provisions operational, and faction-state
receipts passing.

The passive-play matrix extends the same convergence exercise through paused,
camera-movement, Normal, Fast, and Superfast phases, an open CA surface, a
raid, a fire, a full 60,000-tick in-game day, a sealed disposable save, and
post-day verification, with per-phase frame percentiles, hitches, achieved
TPS, process CPU, managed heap, GC counts, and the bounded module profiler,
persisted after every phase. Its disposable save produced the first real
current-boundary campaign save CA's own emitted-save seal ever validated end
to end, and that seal surfaced a queue of latent refusal defects that would
have blocked the operator's first post-B18 save; each was repaired at its
owner and the retained suites were re-verified after every kernel change. CA
subsystem cost stays near two percent of frame time in every measured phase;
map things stay flat across the in-game day; and the post-day map receipt
holds 4/4 provisions operational, with one honest causal exception - kitchens
whose stock was eaten or burned during a day of chronic siege report their
incomplete material contract truthfully.

## Boundary

B18 establishes the performance and correctness of the REGIONAL SUBSTRATE.
The exercised settlements are still substantially generic RimWorld
settlements; the measured FPS/TPS does not predict the completed CAO
simulation, whose first integrated runtime - the real CA settlement
composition with its economic and civil order - is B19's acceptance
boundary. B18 closes at the verified assembly, the governed performance
receipt, and current governed records. The measured native costs that remain - the
intermittent ntdll heap fault under sustained synthetic combat (three
occurrences, dumps preserved) and the native large-save load wall (a 108 MB
regional save loads past 110 minutes while CA's streaming preflight is 3.6 s
of it) - are named receipt debt with preserved evidence, not closed claims.
Static, fixture, and build receipts do not establish visual or gameplay
acceptance. The next development batch is `B19`; the next runtime action is
the operator's test.
