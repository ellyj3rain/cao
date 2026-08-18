# B14 - Regional World and Society Creation Convergence

| Field | Record |
|---|---|
| Batch | `B14` |
| Date | 2026-08-14 to 2026-08-17 UTC / 2026-08-13 to 2026-08-17 PDT |
| Name | Regional World and Society Creation Convergence |
| Status | Closed append-only batch |
| Threads | [`T-001`](THREADS.md#t-001), [`T-002`](THREADS.md#t-002), [`T-013`](THREADS.md#t-013), [`T-014`](THREADS.md#t-014), [`T-015`](THREADS.md#t-015), [`T-019`](THREADS.md#t-019), [`T-020`](THREADS.md#t-020), [`T-021`](THREADS.md#t-021), [`T-022`](THREADS.md#t-022), [`T-023`](THREADS.md#t-023), [`T-024`](THREADS.md#t-024), [`T-025`](THREADS.md#t-025), [`T-026`](THREADS.md#t-026), [`T-027`](THREADS.md#t-027), [`T-028`](THREADS.md#t-028), [`T-029`](THREADS.md#t-029), [`T-030`](THREADS.md#t-030) |
| Local Git commits | The B14 audit, source, bundled integration, governance, and receipt commits on `mallowfluff/b14-generation-political-audit` carry this record and its evidence. |
| Build | Two clean, no-incremental Release builds completed with 0 warnings and 0 errors and emitted byte-identical 4,149,248-byte assemblies at SHA-256 `A5A3D89A516B08156D0163E1F49D8DEC97FCFFED799B60531715DB9C9D495FC9`. |
| Receipts and verification | Society preset execution passes all 22 built-ins, atomic rejection and rollback, component isolation, and Scribe readback. Authoring convergence passes 24/24; regional geography 12/12; generation 12/12; settlement environment 21/21; retained Culture completion 67/67; retained acceptance 78/78; persistence census 253 carriers, 86 schemas, 5 explicit exclusions, and 0 invalid routes. Neo floor and ceiling pass and classify the aggregate as a minor capability. |
| Corrections | Removes orphan preview glyphs and clipped, center-glued Map Preview behavior; unifies selection, preview, confirmation, and generation around one regional composition; restores complete startup dependencies; makes Culture identity propagation owner-aware; replaces flat political/current-order duplication with complete Political Order plus separate represented institutions; separates Society recipes from Culture presets and applies both canonical components atomically. |
| Provenance | Begins from exact B13 closure `20ed6a3bf079fb4fc93f6735bed4cd926d7b552d`. `REGIONAL_GEOGRAPHY_CONTRACT.md`, the political-axis audit and proposal, research corpora, live RimWorld definitions, decompiled engine flow, Map Preview integration evidence, operator runtime reports, and current-schema fixture establish the implemented boundary. |
| Runtime fixture | Pending authoring epoch 12; regional plan schema 13; Culture schema 10 / registry 2; Political Order schema 10; represented institutions schema 1. Active and mirror plans are byte-identical at 397,275 bytes and SHA-256 `6AE604BB8157FC5F12C58932A2476700A336F3C057F11D85441D09BFF151EF04`, preserving the world, region, candidate, arrival, map scale, 3 factions, 4 settlements, 4 current population assignments, and 19 established-program facts. |
| Deployment | RimWorld was closed. The installed mod junction targets the active worktree, and the worktree and installed assemblies are byte-identical at the verified build identity. Static and deployment evidence do not claim operator visual or gameplay acceptance. |
| Source relation | Follows `B13` and forms minor capability unit `VU-041`, deriving `1.5.0.0-alpha`; no former-ledger label applies. |

## Record

B14 closes the gap between regional authoring and the world that is actually
generated. A candidate now carries one deterministic composition identity over
scale, connected extent, orientation, root, arrival, backing frame, member
biomes and relief, stone, routes, features, mutators, and boundary water.
Selection, preview, confirmation, and generation validate and consume that same
state. Installed feature obligations are enumerated from world facts; unknown or
incomplete obligations fail closed instead of silently becoming a generic map.
Map Preview remains directly controllable inside the creation flow, and the
former synthetic letter, number, and diamond overlays are removed.

Starting Region's Region, Map, and Details views now reference one selected
object graph. Arrival and settlement placement operate on actual region members,
prevent occupied collisions, and use the same operation from list and map
surfaces. The schema-13 fixture retains its authored geography, factions,
settlements, populations, ownership, relations, programs, Culture, Political
Order, and represented institutions without rerolling realized state.

Environment and settlement generation use the actual biome, terrain, growing
period, ecology, temperature, water, light, pollution, hazards, and vacuum to
derive concrete habitat requirements. Established settlements must satisfy
those requirements through their saved programs. Frontier residents appear
only after a materially viable site exists. Autonomous construction ranks
candidate placements through task-owned survival, adjacency, throughput,
circulation, expansion, buffering, defense, material, and visual-order evidence.
The player-layout corpus records reusable spatial evidence without copying a
base, imposing a style mode, or treating one operator's saves as the whole
quality distribution.

Political Order replaces the former flat beliefs/current-order duplication with
one complete normative composition of twenty-six questions and twelve
independently variable ownership domains. Blendable questions normalize to 100;
exclusive questions remain singular. Names, short summaries, and accounts are
generated from those causal variables. Represented offices, rules, ownership,
security, and other institutions remain separate realized facts rather than a
second political editor.

Society is a coordinated initializer, not another persistent world object. Each
built-in or user-saved Society recipe owns an independent identity, one Culture
reference or snapshot, and one complete frozen Political Order snapshot. One
validated operation deep-copies both components into the faction's canonical
owners and rolls both back if validation or commit fails. Culture-only and
Political-Order-only presets remain available as explicit component
substitutions. Applying or placing a Society leaves no preset ownership in the
faction or settlement and does not rewrite Ideoligion, founding terms,
institutions, practice, or history.

The offline creator and receipt tools exercise the same current schemas and
application paths used by the game. Bundled Vehicle Framework dependencies,
Bad Hygiene integration, and startup content resolve through the complete
installed mod tree. Fixed-cause receipts, retained regressions, the governed
fixture, two reproducible builds, and closed-process byte verification establish
the deployable test boundary. The next evidence is the operator's fresh
RimWorld visual and gameplay test; `B15` is the next ordinary batch.
