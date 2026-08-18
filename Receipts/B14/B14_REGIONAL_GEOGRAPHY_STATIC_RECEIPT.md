# B14 regional geography static receipt

This executable receipt verifies the region-selection composition contract, the current Map Preview 1.6 API, the shared preview/generation boundary, the docked interaction surface, the live base-game TileMutatorDef catalog, and an exact compiled assembly. Operator visual and generated-map acceptance remain runtime evidence.

| Contract | Result | Evidence |
|---|---|---|
| fixed authoring axes are fully enumerated | **PASS** | 9 scales x 6 orientations x (4+6+8+10+12 arrivals) = 2160 |
| selected composition enumerates every geographic axis | **PASS** | scale, shape, arrival, biome, relief, water, links, stone, landmarks, and mutators feed one signature |
| selection preview and generation share one contract | **PASS** | confirmation validates; preview and generation emit the same world-derived composition identity; projection retains every selected boundary-water source; generation fails closed |
| projection verifies constituent and continuous-feature fidelity | **PASS** | every selected member must retain land; coast, atoll, cove, and archipelago carriers retain their realized shape obligations |
| Map Preview binds the current 1.6 size API | **PASS** | installed API and source both use GameInitMapSizeOverride plus World/PlanetTile/MapParent; exact texture size is plan-owned |
| Map Preview compatibility waits for safe initialization | **PASS** | the play-load worker returns before external type reflection; the existing main-thread completion callback retries after DefOf binding |
| preview ownership follows the selected candidate | **PASS** | the current click binds its derived composition before Map Preview asks for size or generation steps; unrelated clicks and page boundaries clear the transient authority |
| preview layout is one docked interaction surface | **PASS** | details remain fixed; preview and toolbar dock together; image has no synthetic land-letter or diamond overlay; the exact sorted area set owns its cache; recreated settings windows are recaptured and saved positions are restored |
| starting-region map authoring wiring is closed | **PASS** | static wiring: visible labels resolve in reverse paint order; map actions reopen the draft and write startTileId or memberTileId; arrival and settlement occupancy remain mutually exclusive; compact placement returns to map |
| live base-game tile-mutator catalog is enumerated | **PASS** | 87 TileMutatorDefs discovered: AbandonedColonyOutlander, AbandonedColonyTribal, AncientChemfuelRefinery, AncientGarrison, AncientHeatVent, AncientInfestedSettlement, AncientLaunchSite, AncientQuarry, AncientRuins, AncientRuins_Frozen, AncientSmokeVent, AncientToxVent, AncientUplink, AncientWarehouse, AnimalHabitat, AnimalLife_Decreased, AnimalLife_Increased, ArcheanTrees, Archipelago, Basin, Bay, CaveLakes, Cavern, Caves, Chasm, Cliffs, Coast, CoastalAtoll, CoastalIsland, Cove, Crevasse, DryGround, DryLake, Dunes, Fertile, Fish_Decreased, Fish_Increased, Fjord, FoggyMutator, Harbor, Headwater, Hollow, HotSprings, IceCaves, IceDunes, Iceberg, InsectMegahive, Junkyard, Lake, LakeWithIsland, LakeWithIslands, Lakeshore, LavaCaves, LavaCrater, LavaFlow, LavaLake, Marshy, MineralRich, MixedBiome, Mountain, Muddy, Oasis, ObsidianDeposits, Peninsula, PlantGrove, PlantLife_Decreased, PlantLife_Increased, Plateau, Pollution_Increased, Pond, River, RiverConfluence, RiverDelta, RiverIsland, Sandy, SteamGeysers_Increased, Stockpile, SunnyMutator, TerraformingScar, ToxicLake, UndergroundCave, Valley, WetClimate, Wetland, WildPlants, WildTropicalPlants, WindyMutator |
| open modded feature space fails closed | **PASS** | every realized selected mutator is classified by obligations; unknown modded peers cannot bypass durable confirmation |
| verified source build is current and cleanly emitted | **PASS** | bytes=4149248; SHA256=A5A3D89A516B08156D0163E1F49D8DEC97FCFFED799B60531715DB9C9D495FC9; built 2026-08-17T23:43:24.7190877Z; newest source 2026-08-17T23:14:26.2898919Z |

## Enumerated fixed axes

- Local source scales: `200`, `225`, `250`, `275`, `300`, `325`, `350`, `400`, `500`.
- Requested extents: `4`, `6`, `8`, `10`, `12`.
- Orientations: six world-grid headings.
- Arrival: any realized member not occupied by an authored settlement; every member is available before occupant authoring.

## Enumerated live base-game tile mutators

87 defs: `AbandonedColonyOutlander`, `AbandonedColonyTribal`, `AncientChemfuelRefinery`, `AncientGarrison`, `AncientHeatVent`, `AncientInfestedSettlement`, `AncientLaunchSite`, `AncientQuarry`, `AncientRuins`, `AncientRuins_Frozen`, `AncientSmokeVent`, `AncientToxVent`, `AncientUplink`, `AncientWarehouse`, `AnimalHabitat`, `AnimalLife_Decreased`, `AnimalLife_Increased`, `ArcheanTrees`, `Archipelago`, `Basin`, `Bay`, `CaveLakes`, `Cavern`, `Caves`, `Chasm`, `Cliffs`, `Coast`, `CoastalAtoll`, `CoastalIsland`, `Cove`, `Crevasse`, `DryGround`, `DryLake`, `Dunes`, `Fertile`, `Fish_Decreased`, `Fish_Increased`, `Fjord`, `FoggyMutator`, `Harbor`, `Headwater`, `Hollow`, `HotSprings`, `IceCaves`, `IceDunes`, `Iceberg`, `InsectMegahive`, `Junkyard`, `Lake`, `LakeWithIsland`, `LakeWithIslands`, `Lakeshore`, `LavaCaves`, `LavaCrater`, `LavaFlow`, `LavaLake`, `Marshy`, `MineralRich`, `MixedBiome`, `Mountain`, `Muddy`, `Oasis`, `ObsidianDeposits`, `Peninsula`, `PlantGrove`, `PlantLife_Decreased`, `PlantLife_Increased`, `Plateau`, `Pollution_Increased`, `Pond`, `River`, `RiverConfluence`, `RiverDelta`, `RiverIsland`, `Sandy`, `SteamGeysers_Increased`, `Stockpile`, `SunnyMutator`, `TerraformingScar`, `ToxicLake`, `UndergroundCave`, `Valley`, `WetClimate`, `Wetland`, `WildPlants`, `WildTropicalPlants`, `WindyMutator`.

Verified compile: `Assemblies/ColonistAwareness.dll`; SHA-256 `A5A3D89A516B08156D0163E1F49D8DEC97FCFFED799B60531715DB9C9D495FC9`.

Overall: **PASS**
