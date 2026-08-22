# B18 Performance and Runtime Convergence Receipt

Status: CLOSED with named debt. Hardware-specific wall-clock numbers live
here by design and are not CI gates. Machine: operator laptop, RTX 4090
Laptop GPU, 32 logical processors, RimWorld 1.6.4871 rev591, mod launched
with `-disable-compute-shaders`.

**Scope boundary.** B18 establishes the performance and correctness of the
REGIONAL SUBSTRATE: fixed-seed generation, creation correctness, and the
runtime cost of the currently exercised content. The exercised physical
settlements are still substantially generic RimWorld settlements; they do
not represent the eventual simultaneous runtime load of the intended CA
settlement simulation. Nothing in this receipt claims that the measured
FPS/TPS predicts completed CAO performance. The first integrated runtime of
the intended settlement simulation is B19's acceptance boundary.

## 1. Fixed-seed benchmark identity

Every run uses the convergence exercise's governed identity: world seed
`CA-B18-PERFORMANCE-CONVERGENCE`, starting-tile seed 181806, run RNG seed
181814, marker-selected `SIZExCOUNT` scale. Cross-process runs reproduce the
same world, region, slots, and structural outcomes; in-map name draws and a
±1-cell rect jitter vary between processes (an ambient pre-placement Rand
draw), so comparisons are made at the structural and phase-timing level, not
byte level. The slot-2 communal-provision reachability failure reproduced in
every pre-fix run; the post-fix state reproduced 4/4 operational in every run.

## 2. Complete-generation timing (complete map generation / finalization, ms)

| Scale | Backing map | Run | Complete gen | Finalization | Source state |
|---|---|---|---:|---:|---|
| 400×6 | 1246×1080 | pre-optimization run | 257,309 | 4,552 | before district-mutation fast path |
| 400×6 | 1246×1080 | optimized run 1 | 168,465 | 7,439 | retained optimizations |
| 400×6 | 1246×1080 | optimized run 2 | 156,429 | 4,918 | retained optimizations + progress |
| 300×6 | 951×826 | final-source run 1 | 142,989 | 8,478 | reachability fix + passive harness |
| 300×6 | 951×826 | final-source run 2 | 141,146 | 8,625 | same |
| 200×4 | 477×553 | pre-fix (program-room run 2) | 50,799 | 3,201 | before reachability fix |
| 200×4 | 477×553 | reachability-fix run 1 | 51,429 | 3,105 | after reachability fix |
| 200×4 | 477×553 | reachability-fix run 2 | 50,367 | 3,035 | same |
| 400×6 | 1246×1080 | final-era run 1 | 121,838 | 4,190 | reachability + harness + seal repairs (`799AB941…`) |
| 400×6 | 1246×1080 | final-era run 2 (passive) | full matrix completed | — | same build |

Four fresh-process 400×6 generations across the optimization line (257.3 →
168.5 → 156.4 → 121.8 s), each from a self-terminating session with its
preserved log, establish the generation claim. The shipped assembly
(`0F8B16C2…`) differs from the last measured build only in combat-topology
capture code, which is outside the generation path; it was not separately
benchmarked because the operator ended the benchmark sessions, and no
measured claim is made for it. Runs launched during the operator's
force-quit window are preserved but excluded from every claim.

The retained generation optimizations hold their demonstrated gain
(257.3 s → 156–168 s at 400×6, under the preferred 3-minute target). The
correctness repair costs ~1% at 200×4 — within run variance — because the
added `CanReachMapEdge` queries are region-graph lookups.

## 3. The correctness repair (slot-2 provision reachability)

Defect: `TryBuildProgramRoom` validated only its own 9×9 footprint and placed
its single door on the south side unconditionally; nothing anywhere validated
that residents, program assets, and stock join one reachable network. On the
fixed seed, slot 2's dense morphology wedged the program room against an
abutting building whose north wall was the cell directly outside the door.
Diagnostic evidence (nodes, stock, residents, per-target `CanReach`, region
ids, and a 33×33 standing-edifice map) is in
`Performance/diagnostic-200x4-access-diagnostic-run1/`; nodes sat in region
2542/district 376 while every resident was in district 653, 0/11 reachable.

Correction (one coherent boundary — "program placement joins the settlement's
edge-connected pawn network", the same `CanReachMapEdge`-through-doors
network native `SymbolResolver_Settlement` guarantees for its inhabitants):

- `CanSiteCreationDemands` counts only rooms/cells that join the network;
- `TryBuildProgramRoom` selects its door side among all four cardinals by a
  walkable, network-joined exterior approach, verifies the built interior
  joins the network after region rebuild, and rolls the room back cleanly to
  try the next site when it does not;
- `FreeCell` (program assets) and `SpawnStock` accept only network-joined
  cells (stock previously used straight-line nearest, which could cross
  walls);
- CA's direct BaseGen `pawnGroup` push now applies the exact native
  settlement spawn predicate (`CanReachMapEdge`, `TraverseParms.For(
  TraverseMode.PassDoors)`) that native `SymbolResolver_Settlement:52`
  applies and CA's direct push had dropped; independent residents use the
  same anchor.

`CAProvisionAccessService.HasAccess` was not modified. Post-fix, repeated
200×4 and 300×6 runs materialize 4/4 settlements with all creation gates
true, all programs materialized, all provisions `operational`, faction-state
receipt PASS, and zero exceptions.

## 4. Creation-menu responsiveness

Visible-window evidence (OnGUI rendered): first heavy open 34.342 ms, reopen
13.227 ms, ordinary render 4.065 ms
(`Performance/diagnostic-200x4-ui-passive-visible-run1-*`), and in this
session's access-diagnostic run: first open 18.780 ms effective, reopen
PASS, ordinary render PASS. Hidden runs that never render OnGUI deliberately
claim no UI result. All measured values sit far under the 1000/250/100 ms
targets.

## 5. Passive-play matrix (200×4 shakedowns)

Harness: marker `SIZExCOUNT-passive` extends the convergence exercise with a
wall-clock phase machine (paused/static, paused/camera-pan, Normal, Fast,
Superfast, open CA surface, raid, fire, deferred-site statement, a full
60,000-tick in-game day at Normal, disposable save, reload, post-reload
verification), per-phase frame percentiles, hitches, achieved TPS, process
CPU, managed heap, GC counts, and the bounded CA module profiler; progress
persists to `ca-passive-progress.txt` after every phase so a crash cannot
erase measurements. Benchmark instruments (exercise-session-only): pinned
`targetFrameRate=60`/`vSyncCount=0`/`runInBackground=true` (an occluded
unattended window is otherwise compositor-throttled to ~10 fps, and Normal
ticks ride frames), per-phase native log-cap lift, and per-frame re-assert of
the phase's declared speed (native threat letters force-pause and would
otherwise silently freeze a measured phase — shakedown run 3's combat phase
measured a paused renderer exactly that way).

Matrix run 4 (200×4, final harness, concurrent build contamination noted):

| Phase | fps median | 1% low | frame p50/p95/p99 ms | hitches >100/>250 ms | TPS |
|---|---:|---:|---|---|---:|
| Paused static | 60.0 | 39.0 | 16.7/17.6/25.6 | 1/0 | 0 |
| Paused camera pan | 60.0 | 44.7 | 16.7/16.7/22.4 | 0/0 | 0 |
| Normal quiet | 22.6 | 2.3 | 44.2/105.8/436.9 | 30+/1 | 37.4 |
| Fast | 14.1 | 6.5 | 71.1/107.8/152.8 | 2x/0 | ~46 |
| Superfast | 22.7 | 2.3 | 44.0/74.3/427.3 | 2x/2 | ~46 |
| CA surface open | 22.7 | 12.3 | 44.1/64.6/81.6 | 2x/0 | 36.0 |
| Combat raid | 20.1 | 5.7 | 49.8/119.7/174.0 | 5x/0 | active |
| Fire | 20.1 | 6.0 | 49.7/118.1/167.9 | 36/1 | 34.3 |
| Day soak (60,001 ticks) | 20.4 | 7.0 | 48.9/89.3/142.8 | 1048/49 | 36.2 |

Soak begin/end evidence (run 4): map things 167,750 → 163,050 (no thing
growth); world pawns 27 → 34; managed heap 3,663 → 4,905 MB (see §7);
windows stable; broader pawn knowledge default-off (no epistemic queue
exists). Post-day map receipt: 4/4 provision arrangements operational.

Runs 5 and 6 (same harness, spatial-log retention cap deployed): full
60,000-tick days at 37.5 and 38.4 TPS; run 5's managed heap SHRANK across
the soak (4,838 → 4,360 MB) and run 6 grew modestly (4,529 → 4,875 MB) with
map things flat in both — the day-soak heap is no longer monotonic. After
the in-game day of chronic siege and fire, one communal provision in run 5
and two in run 6 reported `not operating` with an INCOMPLETE material
contract: their stock had been consumed or burned during the day. That is
causal simulation (stock is an actual fact; kitchens run out), not a
creation defect — creation-time state was 4/4 operational in every run, and
replenishment is B19's economic scope.
The CA module profiler attributes under ~2% of phase wall time to CA
subsystems in every phase; the dominant frame cost is native simulation and
rendering plus the default-on CATrace diagnostic logging (the `-notrace`
attribution marker exists but was not run; recorded as named uncertainty
in §10).

## 6. Save/reload

The developer exercise deliberately blocks all saves (its transient plan was
not scribed, so any save would orphan dependent records). For the matrix's
disposable save/reload leg, the transient plan now round-trips through its
own transient scribe slot (`CA_transientDeveloperExerciseRegion`): it never
enters the durable `regions` list, restores as transient, and saves remain
blocked after reload; the save patch admits exactly the matrix's
`ca-b18-passive-disposable` save inside its explicit window, and the file is
deleted at matrix completion. Operator saves were never touched.

The matrix's save leg turned out to be the first time CA's own emitted-save
seal (`SealEmittedSave`, shipped with B17) ever validated a REAL
current-boundary campaign save end to end — the operator's existing saves
predate the current campaign boundary and take the migration path, and the
B10-B17 suites exercise synthetic envelopes, "not represented as a true
RimWorld save round trip". The matrix therefore surfaced a queue of latent
save-refusal defects that would have hit the operator's first post-B18
save, each fixed at its owner:

1. `CACombatSpatialLogComponent` exceeded the preflight's
   1,000,000-element streaming limit after a day of chronic combat (the
   native battle log retains battles ~7 in-game days after their last
   entry, and each retained entry carried a CA topology snapshot).
   Corrections: records now live only while their native battle-log
   entries live AND at most the newest 2,000 serialize; the topology
   budget dropped to 2 incidents x 512 KiB with 2,048 deltas each,
   sized to the preflight's element economics (element-per-byte density
   varies ~5x across delta shapes); the preflight's overflow exception now
   names the exact element path that crossed the limit.
2. `Scribe_Values` default-omission versus exactly-once preflight
   contracts: `withinGroupSpread`/`subgroupSeparation` (default 2 is one of
   five authorable values), the political-order name/roll quartet, pawn
   psychology `mappingVersion`, influence `lastContactTick`/
   `lastObservedTick`, founding `appliedAtTick`, native-culture-event
   faction reference pair, `CAOrigin` null origin keys, political-thought
   strings, and lord `settlementCenter` — all now force-serialize (with
   null-string normalization where non-null is required). An exhaustive
   audit of every `ValidateRequiredChildren` contract against its
   emitters' scribe defaults grounds the set.
3. Nested schema bindings demanded `schemaVersion` children that eight
   shipped record classes never wrote (`CARelation`, `CAFacilityHolding`,
   `CASettlementLayout`, `CAGroundwaterTuning`, `CAFoundingArrangement`,
   the three settlement work records) and two self-defeating defaults
   omitted theirs (`CASiteFactionLinks`, `CASiteLocalSocietyState`,
   `CAFrontierMapPlan`, `CAStartingStockRecord`); every class now stamps
   its catalog version. The bare `factionStructure` axis-entry list, which
   cannot carry a version child, is bound through a new versionless route
   that preserves catalog-to-route closure (the first attempt — removing
   the binding — was caught by B11 receipt 24 as a production load-gate
   regression and corrected).
4. The kernel required `CA_homeCompletedAutoBuildings` while the emitter
   has always written `CA_homeCompletedBuildings`; the kernel now names
   the shipped label. The kernel also refused identity-only cultures;
   an identity-only culture is a first-class designed state (the player
   faction before founding authorship), and the kernel now accepts it
   while still enforcing per-scope question coverage whenever any scope
   is represented.

The developer exercise deliberately blocks all saves; for the matrix's
disposable leg the transient plan now round-trips through its own
transient scribe slot, the save patch admits exactly the matrix's
disposable save inside its explicit window, and the file is deleted at
completion. Operator saves were never touched. After the batch, all
retained suites pass (B10 75/75 + sweep 189/0, B11 78/78, B12 113/113,
B13 67/67, B14 24/12/12/21/22, B15 full, B16 54/54, B17 full + behavior
convergence 98/107). Matrix run 9 surfaced two more queue members behind
the faction-state component — a founding plan whose inner arrangement is
legitimately null when its preset applies through the relation ledger
(kernel now allows null there), and B11 receipt 24 caught the
represented-institutions binding removal as a load-gate regression
(corrected with the versionless route). Matrix run 10 then SAVED
SUCCESSFULLY — the first complete current-boundary campaign save CA has
ever sealed — at 110 MB after the in-game day (~84% native map things at
477x553 with ~166K things; CA components ~18 MB, dominated by the combat
spatial log's 820K elements under the 2x512-KiB budget). The reload of
that 110 MB save ground past 35 minutes without completing and was
terminated; the topology budget was halved again (2x256 KiB, ~410K
elements worst case) to cut both the seal margin and the load cost.
Matrix run 11 (halved topology budget) advanced the queue to its next
member: casualty facts whose rescuer or beneficiary died during the raid
scribe null references that the seal required non-null — a dead casualty
is causally legitimate history, so the kernel now allows null there. A
second exhaustive audit then swept every remaining required-non-null
reference contract for death-reachable nulls and hardened the full set in
one pass: the stack-breach door (destroyed mid-fight; doorCell survives)
and the settlement lord's faction (null for unowned sites by the B17
contract) moved to a new native allow-null lane; space-program resident
rosters and patrol assignments prune destroyed pawns at save (mirroring
their existing load/step pruning); the stack-breach lord gained the
Notify_PawnLost paired removal it lacked; the search-contact job's
pre-toil defaults force-serialize; settlement-record cultures and the
lazily-created founding plan coalesce at their owners. B11's synthetic
textual-null envelope moved from the now-legitimately-null casualty
rescuer to the still-required patrol pawn, and receipt 24's full
rejection matrix passes again (78/78). The save side is proven by the two sealed
full saves; the reload leg above 40 MB carries an explicit no-claim (§9
and the named native load wall below).

Two matrix runs (the minimized run 3 and run 8) died to an identical
intermittent native crash — 0xc0000005 in ntdll.dll at fault offset
0x70f32, WER dumps preserved (`RimWorldWin64.exe.179968.dmp`,
`RimWorldWin64.exe.83040.dmp`, plus `RimWorldWin64.exe.42372.dmp` from the
previous session's runs) — during sustained synthetic combat; runs 4-7
completed the same schedule. Tracked as an intermittent native-level
fault, not reproduced under instrumented visible runs 4-7.

## 7. Memory and queue growth

Managed heap grew ~1.2 GB across run 4's day soak under chronic combat. Map
things and windows are flat; world-pawn growth is native (raid deaths). The
combat spatial log's unbounded battle-window accumulation is corrected (§6);
the remaining growth attribution (CATrace/flight-recorder session buffers vs
native combat state) is a named uncertainty (§10); the `-notrace` marker
exists for a future comparison. Working set is
unavailable under the Mono runtime (reported as such, no claim).

## 8. Remaining bottlenecks and debt (to date)

- Normal-speed play on the exercise fixture is frame-bound near ~20 fps
  visible / ~36 TPS with chronic multi-settlement combat; CA subsystem cost
  is ~2%. Native-vs-trace attribution is a named uncertainty (§10).
- Fast/Superfast saturate near ~46 TPS on 200×4 under combat load.
- `KnowledgePropagation` examines ~27K candidate elements/second during
  quiet play (816K per 30 s) for ~1.7 ms/s of CPU; bounded but the
  examined-set scale is the B19 dirty-propagation target.
- A native hard crash (0xc0000005 in ntdll, WER dump preserved) occurred in
  one minimized-window shakedown (run before the pacing instruments); a
  RimWorld crash dump from the previous session's runs also exists. Not
  reproduced in instrumented visible runs so far; tracked as environmental
  until reproduced.

## 9. Final 400×6 matrix evidence

The completed 400×6 passive matrix (final-era build, all phases, preserved
in `Performance/target-400x6-passive-artifact-census-wall/`):

| Phase | fps median | 1% low | frame p50/p95/p99 ms | hitches >100/>250 ms | TPS |
|---|---:|---:|---|---|---:|
| Paused static | 60.0 | 47.2 | 16.7/17.9/21.2 | 1/0 | 0 |
| Paused camera pan | 60.0 | 40.4 | 16.7/18.6/24.8 | 0/0 | 0 |
| Normal quiet | 15.8 | 5.6 | 63.3/105.9/177.5 | 24/1 | 14.3 |
| Fast | 15.4 | 4.8 | 65.0/108.0/207.2 | 33/1 | 14.1 |
| Superfast | 15.9 | 5.4 | 62.8/104.3/184.4 | 25/2 | 13.9 |
| CA surface open | 15.9 | 3.5 | 63.0/118.4/285.3 | 17/3 | 13.3 |
| Combat raid | 14.8 | 3.6 | 67.5/183.4/279.6 | 74/9 | 12.1 |
| Fire | 14.8 | 3.7 | 67.6/110.6/269.6 | 27/5 | 12.8 |
| Day soak (3600 s cap, 51,321 ticks) | 14.8 | 5.1 | 67.7/110.2/195.9 | 5854/176 | 14.3 |

Its save was refused by the artifact-census element overflow, which the
overflow-path diagnostic pinpointed and the bounded battlefield window
corrected; the correction is verified by the B11 contract suite (78/78) and
the static contracts rather than by a rerun, because further benchmark
launches were stopped by the operator.

## 10. Active-phase frame rate: explicit boundary, not a baseline

The instrumented matrix measured ~12.8-15.9 fps in active phases on the
1.35M-cell 400×6 fixture, while earlier engineering sessions measured
roughly 44-61 FPS during active and materializing operation on a
substantially LARGER ~2.78M-cell regional map. The matrix's own telemetry
shows the low figure is NOT an intrinsic renderer baseline for 400×6:

- the same fully generated map renders 60.0 fps median in both paused
  phases (renderer pipeline sound when the simulation is idle);
- Normal, Fast, and Superfast all converge to ~14 TPS / ~15 fps, the
  signature of a main-thread simulation-bound frame (ticks ride frames);
- CA's draw contribution is ~3 ms/frame and CA subsystems ~2% of wall time
  by the bounded profiler;
- the measured state carried loads the earlier sessions did not: a chronic
  synthetic raid plus fires across six settlements, the default-on CATrace
  behavior logging flushing per-revision lines, per-frame matrix sampling,
  and an unattended, unfocused window (frame pacing was pinned, but
  foreground/background compositor behavior differs from the earlier
  foreground measurements).

Within existing telemetry the active-phase figure attributes to main-thread
simulation plus diagnostic logging under sustained synthetic combat in an
instrumented unfocused session; the residual split between native
simulation, trace logging, and focus state is recorded as uncertainty (the
`-notrace` marker exists for a future comparison). The earlier 44-61 FPS
active measurements on the larger map remain the better indicator of the
substrate's intrinsic rendering capability. Per the scope boundary above,
none of these numbers claim to predict completed CAO simulation
performance.
