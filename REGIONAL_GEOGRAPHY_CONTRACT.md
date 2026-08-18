# Regional Geography Contract

Starting Region selects one connected composition of world geography. The
composition is not a map preset and no named feature defines a separate flow.
An atoll, cove, mountain, road, river, biome boundary, ruin, or modded tile
mutator is one fact carried by one or more selected areas.

## Selection space

| Axis | Available values | Direct effect |
|---|---|---|
| Local source scale | `200`, `225`, `250`, `275`, `300`, `325`, `350`, `400`, `500` cells per selected world area | Sets the spatial resolution contributed by each source area. |
| Requested extent | `4`, `6`, `8`, `10`, `12` connected areas | Sets how many usable neighboring areas the shape seeks. The realized extent may be smaller where connected usable land ends. |
| Orientation | Six world-grid headings | Changes which connected neighbors enter the shape before geography is projected. |
| Arrival area | Any realized member not already occupied by an authored settlement | Sets the colony's landing area without changing the region's geography. Before occupant authoring, every realized member is available. |
| Core biome | Every installed, realized, passable land biome carried by the selected members | Supplies per-cell biome provenance. Water and impassable biomes cannot be core members. |
| Relief | Flat, gentle hills, large hills, and mountains as carried by each member | Supplies the per-cell elevation blend and mountain carriers. Impassable land cannot be selected. |
| Water context | Ocean, lake, and installed water biomes sampled from the selected land's geographic boundary | Supplies coastline, water depth, beaches, lagoons, coves, islands, and river mouths. Water is geographic context rather than a selectable core member. |
| Routes | Every realized road and river link touching the geographic sources | Supplies clipped road and river paths through the generated map. |
| Stone | Every natural rock type carried by the selected members | Supplies the constituent-local stone palette. |
| Features and old sites | Every realized world feature, landmark, and tile mutator attached to a selected member | Supplies the feature's classified generation and runtime obligations. Unknown or incomplete obligations fail closed. |
| Existing surroundings | Map-bearing world objects near the selected frame | Supplies authoring context. These objects are not silently inserted into the region. |

For a root with enough connected land, the fixed authoring axes provide 240
shape-and-arrival combinations per local scale: six orientations multiplied by
the `4 + 6 + 8 + 10 + 12` possible arrival members. Across the nine local
scales this is 2,160 base combinations per root before the world's biomes,
relief, water, routes, stone, landmarks, and installed mutators are considered.
This count describes the geography-first state before settlements occupy any
member.
Those geographic values are open to the loaded game and mod set, so the code
enumerates them from the selected world facts instead of maintaining a closed
feature-name list.

## One composition, one result

Every candidate receives a deterministic composition signature derived from:

`scale + requested/realized extent + orientation + root + arrival + backing frame + each member's biome/relief/stone/routes/features + boundary water`

The signature is logged at all three execution boundaries:

| Boundary | Required behavior |
|---|---|
| Selection | Validate unique, passable, connected members; the root and arrival must both belong to the region. |
| Preview | Build the shared projection from the saved candidate, verify visible land for every core member, preserve feature-carrier counts, and bind Map Preview to the exact regional backing frame. |
| Generation | Rebuild the shared projection from the confirmed plan, require the same composition contract, then generate terrain, water, relief, routes, and selected features from that realized state. |

The compatibility registry classifies every obligation carried by every
selected tile mutator. Durable confirmation admits only production-supported
obligations. A new or modded feature is therefore included accurately or the
candidate is stopped with the unresolved obligation named; it is never treated
as harmless because it was absent from a hard-coded list.

## Interaction flow

The world remains the primary selection surface.

1. Clicking outside the current footprint moves the whole candidate and keeps
   the chosen extent and orientation.
2. **Size** changes the requested connected extent.
3. **Turn shape** changes which neighbors enter the candidate.
4. **Choose arrival area** changes only the landing member.
5. The details panel reports the realized landscape, relief, stone, routes,
   features, and generation readiness before the player continues.
6. Map Preview is docked in the free map lane with its toolbar attached above
   it. Toolbar settings and reroll controls retain their own behavior and saved
   layout. Compact on-land markers identify the arrival area and settlements;
   explanatory prose remains in tooltips and the details panel rather than
   covering the generated image.
7. Factions, settlements, populations, and their relationships are authored on
   the following Starting Region page because they describe occupants, not
   geography.

This flow preserves direct manipulation: the player changes one owning fact,
sees the resulting composition, and proceeds only when that composition has a
complete generation contract.
