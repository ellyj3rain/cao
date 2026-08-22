# Settlement causal-consumer map

For every authoritative settlement variable that can legitimately affect
physical presentation: the concrete physical decision that consumes it after
commit `644fbbc`, what consumed it before this work (2026-08-19), and what
remains unconsumed with the reason. The physical settlement is the phenotype
of the represented state; no scalar stands between the record and the ground.
The requirements intermediate is the ontology's own: every
`CASettlementProgramEntry` persists `count` and `extent`, computed per plan
and settlement by its program definition.

| Variable | Physical consumer now | Before | Open |
|---|---|---|---|
| program entries (count x extent) | body-cell floor: functional footprints only (sum of count x max(9, extent x 9), x5 circulation) -- run 5 proved these are UNIT counts, so residential ground enters as the resident population's own term (residents x 35), never inside the entries; reserved-rect sizing (same sum in `TryFindSettlementRect`); functional interiors (`CASettlementFitOut`: hall/kitchen/infirmary/granary/workshop/study/prison/commons exist only from their program keys); program assets (`SettlementProgramMaterializer`, pre-existing) | assets only; morphology never read the program | none |
| residentPopulation / population groups | pawn spawn points (pre-existing, sqrt-scaled, capped); dwelling beds sized to expected residents in fit-out; runtime housing-pressure fact (residents vs sleeping slots) deriving the Development demand in the institutional proposal; STARTING COMPOSITION: >= 140 composes Production, >= 280 composes Trade + Storage (gated by CanMaterialize); rect sizing, physical requirements, and fit-out read it first across the model's own 18..1200 range | pawn spawn only; consumers truncated anything past 200 to a fallback 10 | none -- the authored cause survives realization (`authoredPopulation`, run-9 correction 04fd3aa) |
| landCapacity | body-cell ceiling (`0.30 + 0.13 x band` of rect area) and reserved-rect ceiling (`96 + band x 16`); food-route composition split (>= 2 cultivates, < 2 forages, pre-existing) | none | authorable via `authoredLandCapacity`; tile fact remains the default |
| economicCapacity | infrastructure tier: paving verge width (1 or 2) in `GroundCulture`; wall affordability (full vs partial when a defense cause exists) | none | lamp density, floor-def quality are candidate consumers, not yet wired |
| tradeConnectivity | via the Trade program: market plaza held open beside the core (6 x extent cells); placement seeks moderate waterfront; harbor works at a standing pier | none | direct axis unused by design -- the program is the causal owner |
| specialization | via the SpecializedIndustry/Production programs: industry quarter exists; workshop interiors and benches by manufacturing knowledge rank | none | upstream facts derivation from the axis not re-verified this pass |
| historicalDevelopment | accretion 0..1: pack irregularity (skip-chance producing odd gaps and mismatched frontages); STARTING COMPOSITION: >= 2 keeps Storage | none | authorable via `authoredHistoricalDevelopment`; legacy-core layering (older-denser center) is a stated candidate, not yet wired |
| urbanSupport | none direct | my deleted scalar | intentionally unconsumed until a defensible direct consumer exists; services expression belongs to the programs it feeds upstream |
| operationalRoleMask | security bit -> wall cause (with Defense program) | none | other role bits express only through their programs |
| Technological Knowledge (domains) | form default (pre-existing axis rule); equipment tiers per domain in fit-out (manufacturing/medicine/electrical construct ranks); pier and wall material gates (`CanConstructCanonical`, pre-existing) | form default + pier gate | none |
| Culture / cultural expression | apertures (`CACulturalExpressionModel` -> `CutApertures`, pre-existing) | same | no new consumer added; anything further needs a represented cultural consumer, not a style selector |
| Political Order / institutions | provision arrangements -> hearths, dining, operator orgs (pre-existing); institutional authorization gates all works verbs including Development | same | civic-space expression has no causal path defined yet |
| geography (coast, littoral, hill) | pier gate (waterfront + bridgeable run, pre-existing); placement water band for port programs; port-quarter side (watered edge pulls the quarter); site-read defence orientation (fit-out era, superseded by wall-by-cause) | pier gate only | river-adjacency consumers unbuilt |
| materials (local rock) | one stuff palette per settlement from `NaturalRockTypesIn` (adapter palette pre-existing; fit-out palette added) | adapter only | quarry/economy interaction unmodelled |
| form (authored axis; defaults from tech) | pattern family and idioms: hut shapes, pack style, palisade/wall/grid habits, fence idiom -- unchanged ownership | same, PLUS an illegitimate scale fraction | scale removed from form; a represented defense cause overrides the wall idiom in either direction |
| districts | count of differentiated quarters: core + industry (production program) + port (trade/transport with real waterfront); co-sited cluster floor retained | `region.settlements.Count` -- neighbours, not structure | further quarter kinds (administrative, sacred) unclaimed until their programs warrant ground |

Authored causes (run-9 correction, 04fd3aa): `authoredPopulation`,
`authoredLandCapacity`, and `authoredHistoricalDevelopment` are scribed
composition causes on the settlement plan, following the `authoredForm`
precedent. Realization and the saved-causes validator resolve the three
facts through the same authored-first rule, and the realization source
hash carries them. Economy, trade, specialization, and urban support are
never authorable: they are summaries of the composed programs, so an
author differentiates them by authoring the facts that compose different
programs. Run 9 proved the previous fixture's summary-writing was exactly
the writing derivation must overwrite -- four authored settlements
realized identical (pop 605 everywhere).

Runtime development consumes the same map: the institutional proposal derives
the `Development` demand from measured housing pressure; `TryDevelop` infills
beds inside real interiors and extends with a walled annex when they are
full, authorized through the same gate as every other works verb.

Removed by this correction: the single "development standing" scalar as a
morphology input (`CASettlementDevelopment` retains no physical consumer);
the "form is texture, standing is scale" redefinition; the tier-named demo
vocabulary.
