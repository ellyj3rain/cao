# Player base layout corpus

| Field | Value |
|---|---|
| Batch | B14 |
| Purpose | Native layout evidence for autonomous construction and settlement materialization |
| Status | Active; 4,238 complete structured snapshots and nine operator saves extracted; capability and quality curation remains open |
| Extractor | `tools/PlayerBaseLayoutExtractor` |
| Normalized data | `Corpus/PlayerBaseLayouts/operator/`; broad clean manifest and profile under `Corpus/PlayerBaseLayouts/external/` |
| Selection metadata | `Corpus/PlayerBaseLayouts/corpus-manifest.json` |

The learned object is the layout serialized in a RimWorld save: built and planned
entities, zones, occupied ground, spatial extent, population context, biome
evidence, scenario, and mod context. Screenshots and discussion posts can identify
candidates or explain a decision, but they are not substitutes for native layout
data.

The extractor is read-only. It accepts full RimWorld `.rws` saves and Real Ruins
`.bp` snapshots, hashes each source, omits source paths and pawn names, and emits
deterministic schema-2 JSON. The normalized records retain entity definitions and
coordinates because those relationships are the data the builder needs to study.
The source saves and external snapshots themselves are not copied into the
repository.

## Evidence strata

Corpus membership is based on the layout's actual value, stage coverage, biome
adaptation, and meaningful difference from other examples. It is not an author
quota. One builder may supply many examples when those bases genuinely express
different useful solutions.

The operator corpus is strong evidence for early establishment under combat
precarity. It records high-military and high-intellect colonists building,
furnishing, protecting, and extending settlements while food, power, storage,
medical access, fields of fire, fallback positions, and unfinished reserves all
compete. It is not claimed as a representative corpus of mature mid-game and
end-game development. Complementary saves from builders with demonstrated mature
colonies supply that missing range; they do not replace the operator stratum.
The current local save directory contains 62 parseable `.rws` candidates. All 62
extract without failure, while the nine governed rows below remain the admitted
operator evidence. The other checkpoints stay in local screening until lineage,
meaningful difference, and evidence value are established; quantity alone does
not promote them.

### Original four-save operator corpus

| ID | Native save and SHA-256 | Extracted context | Evidence admitted | Coverage boundary |
|---|---|---|---|---|
| OP-001 | `autostart-DISABLED.rws` — `4C094DC9D5721FBFA68A8AAC3001355BCCDD4511F160EFF1C1108C017F73EA14` | Temperate forest; 775 built/planned entities; 125×81 built-anchor span; seven ideology-bearing player pawns | Infrastructure-first controlled playground; perimeter, energy, shelter, security, incomplete reserves, and intended facility placement under the operator's controlled high-difficulty test | Controlled early-establishment fixture, not mature progression evidence |
| OP-002 | `Autosave-5.rws` — `822D5B09493BCEBCB56A240C0B57F018AC2A784AB38611293E7289A96D412BE1` | Tropical rainforest; 421 built/planned entities; five growing zones; five stockpiles; 77×55 full evidence span; four ideology-bearing player pawns | Compact river settlement; communal barracks, walled crops, separate climate-controlled storage, formal research, and short survival/service chains | Early compact settlement and social-layout evidence |
| OP-003 | `The Sixth-Seventh Reich (Permadeath).rws` — `ED13B5EBEFE9B645CB67E67FCCF8169AB9267F628519EA82DE5ACC5940C0FB8B` | Tropical rainforest; 1,750 built/planned entities; ten growing zones; twelve stockpiles; 132×211 completed-anchor span; seven ideology-bearing player pawns | Dense core, work and storage bands, terrain-following frontier growth, the barn/hydroponic habitation as a forward defense area, and fallback to the central bird base | Historically called the mature bird colony; admitted for evolved defensive topology and growth under precarity, not as general mid/end-game coverage |
| OP-004 | `The Sixth-Seventh Reich 2.rws` — `FB79390F8FA949253B95F941AB2A1A1A67DFD6108589DB78042CF9D2B5146390` | Tropical rainforest; 214 built/planned entities; nine growing zones; five stockpiles; 147×123 evidence span; nine ideology-bearing player pawns | Mountain accretion around existing rock: irregular corridors, small rooms, common halls, fields, utilities, and a distinct high-status room | Terrain-led and plural-population layout evidence; not a late-game capability sample |

The dimensions and counts above are regenerated from the normalized records and
match the earlier governed F-78 corpus measurements at their owning boundary.
Where a historical receipt measured completed building anchors and the normalized
record also includes zones, blueprints, or frames, both bounds remain separate in
the data rather than forcing one number to serve both meanings.

### Same-builder Academy variation set

| ID | Native save | Extracted distinction |
|---|---|---|
| OP-005 | `The SixSeventh Reich (Permadeath).rws` | Tropical rainforest mountain service core; 215 built/planned entities; ten growing zones; two stockpiles; 72×79 evidence span |
| OP-006 | `The Fifth Reich (Permadeath).rws` | Tropical rainforest; southern residential/work core, central crops, northern animal infrastructure; 352 entities; 49×124 evidence span |
| OP-007 | `Koeron (Permadeath).rws` | Arid shrubland; seven completed structures plus 115 planned entities; sparse operating core beside an aspirational expansion rather than a falsely completed base |
| OP-008 | `The Fourth Reich (Permadeath).rws` | Temperate forest; compact domestic block, detached project ground, retained water and trees, and limited excavation; 198 entities |
| OP-009 | `Coalition of Erewhon (Permadeath).rws` | Boreal forest; radial modules around growing ground, detached functional nodes, a southern perimeter, and retained forest; exact source hash `0DBB30070B57D9066E0C9AB5BB3F4B90BFE4410361821C8CEBEC0FC39583CA5F` |

This set is useful because the builder and operator are held constant while the
terrain, settlement state, intended expansion, and built grammar vary. That does
not make every checkpoint independent evidence: byte-identical copies and nearby
states are grouped by colony lineage before training or evaluation so the same
layout cannot leak across both sides of a receipt.

## Normalized layout contract

| Data family | Serialized evidence | Use |
|---|---|---|
| Source identity | File name, byte length, SHA-256, game version, scenario, ordered mod list | Provenance, exact deduplication, and compatibility limits |
| Map identity | Map ID, generator, dimensions, parent settlement, world tile | Keeps multiple maps and settlements distinct |
| Environment | Same-tile biome evidence for full saves; exact biome, terrain cells, roof cells, capture origin, and capture size for structured snapshots | Conditions the layout on the ground that made it viable |
| Population | Player-faction pawn things, ideology-bearing pawn count, pawn-def and Ideoligion distributions | Distinguishes population and social context without exporting pawn identity |
| Built state | Full saves: completed buildings, blueprints, and frames with def, position, rotation, material, and quality. Structured snapshots: placed things with exact cell, rotation, material, stack, wall, and door evidence | Preserves realized layout and, where the source contains it, intentional expansion |
| Programs | Native growing, stockpile, fishing, bathing, and other zones with exact cells | Connects buildings to production, storage, food, and service ground |
| Derived geometry | Completed, all-built, and full-evidence bounds; density; def and zone counts | Supports comparison without erasing the underlying coordinates |

Biome inference for a full save currently uses same-tile tale surroundings when
that evidence exists. A blank value remains blank; the extractor does not guess.
Real Ruins snapshots carry exact biome, terrain, and roof cells directly. Full
save terrain and roof grid decoding remains open because those compressed grids
are required before `.rws` records have the same ground resolution.

## Broad structured layout archive

Real Ruins is an opt-in archive of native player-base snapshots. Its `.bp` files
are GZip XML containing exact placed-item, terrain, roof, capture-bound, biome,
map, year, and world-location data. The thirteen-file 2026-08-16 set remains the
extractor smoke test. On 2026-08-17 the acquisition path then selected 10,000
additional candidates, cached 9,998 source snapshots, and admitted 4,238
structurally complete, byte-unique layouts from 4,238 distinct lineage keys.
Those clean layouts span 80 exact biome definitions and 16 source version
strings. Another 5,760 files end mid-XML and remain a separate fragment pool; two
downloads failed. Neither fragments nor failures enter complete-base evidence.

`Corpus/PlayerBaseLayouts/external/real-ruins-broad-clean-manifest.json` is the
portable clean cohort. It records exact external identity, source and lineage
hashes, environment, capture geometry, structural counts, a cohort-relative
size quartile, and a deterministic lineage-safe partition for every clean row.
The corresponding aggregate evidence is in
`real-ruins-broad-profile.json`. Generation of both files is byte-deterministic.
Raw `.bp` files and the 46 MB acquisition index remain in the ignored local
cache; the repository carries the exact re-downloadable cohort definition rather
than copying the archive.

This archive supplies broad, real spatial data. It does not identify the builder,
prove quality, carry a full research state, or establish progression stage from
calendar year, object count, or size. Its rows therefore remain screening
candidates until their layouts are inspected and their capability evidence is
classified. Full external saves remain necessary for the mature progression
stratum.

## Autonomous-building contract

The builder consumes factual owners in this direction:

`biome and terrain -> habitat requirements -> population and authored programs
-> native placement validity -> learned spatial relationships -> candidate
ranking -> blueprints/materialization -> readback`

The corpus may teach recurrent relationships: kitchen/freezer/crop trip chains,
hospital access from defensive positions, hazard separation, protected power and
food, usable corridors, terrain-following envelopes, expansion reserves, and
deliberate visual rhythm. It does not supply copied floor plans or a single
quality gradient. Every learned relationship is re-evaluated against the current
map, available materials, population, technology, threats, Culture, Political
Order, and represented institutions.

## Complementary mature corpus

The missing stratum is native-save evidence from builders with demonstrated
mid-game and end-game colonies across materially different biomes and planning
grammars. Admission requires:

1. a parseable native save or an author-supplied normalized export;
2. a layout meaningfully different from already admitted examples;
3. enough progression and material state to establish what stage it represents;
4. biome, terrain, population, technology, and mod context preserved with it;
5. a source locator, hash, author, and evidence limit;
6. no duplicate or near-checkpoint leakage between training and evaluation.

Authorship count is not an admission criterion. Quality and coverage are.

## Visual discovery references

The following sources remain discovery surfaces only. They do not count as
machine-learning rows until a native save or a sufficiently complete structured
export is available:

| ID | Discovery source | Candidate relationships to verify against native data |
|---|---|---|
| VD-001 | [Efficient colony and room layouts](https://www.reddit.com/r/RimWorld/comments/j4rml2/) | Freezer, kitchen, farm, dining, workshop, and hospital trip chains |
| VD-002 | [13x13 mountain plan](https://www.reddit.com/r/RimWorld/comments/wk0yh5/) | Repeated modules, chokepoints, district legibility, armory separation |
| VD-003 | [Colony layout discussion](https://www.reddit.com/r/RimWorld/comments/g9uazm/) | Passage width, freezer envelope, airlocks, local meal storage |
| VD-004 | [Village-style base discussion](https://www.reddit.com/r/RimWorld/comments/1rp164z/what_are_the_good_or_at_least_better_village/) | Distributed service clusters, alleys, travel cost, rebuilding behavior |
| VD-005 | [Three-year ice-sheet base](https://www.reddit.com/r/RimWorld/comments/16308iv/) | Enclosure order, thermal mass, airlocks, fungus/hydroponics, heat control |
| VD-006 | [Sea-ice base after six years](https://www.reddit.com/r/RimWorld/comments/hdwyk7/) | Imported supply, compact heat, redundancy, nonlocal production |
| VD-007 | [Detailed tribal-to-spacer base](https://www.reddit.com/r/RimWorld/comments/1r0vetn/) | Historical accretion and retrofit across capability stages |
| VD-008 | [City-style base](https://www.reddit.com/r/RimWorld/comments/1efq737/) | Streets, blocks, distributed facilities, tactical access |
| VD-009 | [No-killbox defense discussion](https://www.reddit.com/r/RimWorld/comments/1fmm3eh/) | Terrain-led perimeter, retreat paths, side doors, interior cover |
| VD-010 | [RimWorld Gallery](https://rimworld.gallery/m/rimworldporn/default/default/%E2%88%9E/local?type=photos) | Candidate discovery across mountain, town, organic, compact, and sprawling bases |

Popularity and appearance do not establish causality. These references can guide
where to seek a corresponding save and which spatial question to test; they do
not stand in for the data.
