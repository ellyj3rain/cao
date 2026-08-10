# Settlement generation model

This document defines the current regional authoring and settlement-generation
model. The code, saved plan, generated settlement records, and setup UI use the
same terms.

## Current state

A regional plan contains:

| Level | Saved facts |
|---|---|
| Region | selected world areas, arrival area, world tendencies, settlement pattern, principal settlement scale, regional relation pattern, frontier holdings |
| Faction | source faction, culture, Ideoligion, political beliefs, faction structure, settlement authority, faction era |
| Settlement | owning faction, world area, population origin, resident population, land capacity, regional role, form, starting facilities, access, services, civic development, economic capacity, trade connectivity, specialization, historical development, urban support, realized scale, population groups, starting provisions |
| Faction relation | left faction, right faction, relation, whether the player set it |
| Frontier holding | world area, household size, land capacity, material level, form, faction status |

Regions contain factions and settlements directly.

## World tendencies causal contract

World tendencies are defaults for generated state. Each control owns one direct
cause. The result is saved before downstream generation reads it. A
starting-region choice replaces the corresponding default at the surface that
owns the fact.

| UI control | Saved policy field | Direct effect | Constraints | Saved result and consumers | Starting-region override |
|---|---|---|---|---|---|
| Generated land | `stitchedRegionFrequencyMin`, `stitchedRegionFrequencyMax`, `realizedStitchedRegionFrequency` | Resolves the world frequency used to choose single-area or stitched generated regions. | World seed; connected eligible land. | The world policy stores the realized frequency; each region stores its requested and realized extent. The automatic region builder consumes both. | The selected starting-region footprint owns its member areas. |
| Stitched region size | `stitchedRegionSizeMin`, `stitchedRegionSizeMax` | Sets the requested member-area count after a generated region is selected as stitched. | Connected land, impassable terrain, and occupied neighbors cap the result. | `requestedRegionTileCount`, `memberTileIds`, and backing dimensions are saved and consumed by projection and map generation. | The starting-region extent selector owns the request. |
| Settlement concentration | `settlementConcentration` | Changes the placement score for pool-authorized generated settlements before classification: low rewards distance, high rewards proximity. | Land capacity, access, route links, available member areas, and the settlement pool. | Each settlement's `memberTileId` and physical grouping are saved; `settlementPattern` is then classified from the realized placement, distance, routes, hierarchy, frontier, faction mixture, and relations. | Authored settlement positions own `memberTileId`; the tendency never moves them. |
| Urban growth propensity | `urbanGrowthPropensity` | Raises or lowers the urban-support threshold. | Resident population and the saved land, access, services, civic development, economic capacity, trade connectivity, specialization, regional role, and history remain required. | Each settlement stores `urbanSupport` and `realizedScale`; the region stores the maximum as `settlementScale`. Layout, pawn-group strength, provisions, and UI read the saved scale. | Authored infrastructure, facilities, roles, and other settlement facts replace their generated inputs before confirmation. |
| Frontier holding frequency | `frontierHoldingFrequency` | Determines how many suitable, unoccupied non-arrival areas receive holdings. | Suitable land, major-settlement occupancy, and arrival land. | One `frontierHoldings` row is saved per realized site. Frontier materialization and settlement-pattern classification consume those rows. | A starting-region footprint constrains the available holding sites; it does not alter major-settlement count. |
| Frontier holding size | `frontierHoldingSize` | Determines household size and material form after a holding site exists. | The holding's land capacity caps household and material levels. | `householdSize`, `materialLevel`, and `form` are saved on each holding and consumed directly by pawn and site generation. | A saved holding row owns its realized size and form. |
| Unaffiliated residents | `unaffiliatedPopulationShare` | Changes the generated share of residents without faction membership. | Settlement-specific deterministic variation and the remaining 100-percent population share. | `populationGroups` stores the realized shares. Pawn assignment, political identity, quarters, and provisions consume them. | Authored population groups own their shares and affiliations. |
| Reallocation source variety | `reallocationSourceVariety` | Changes whether source settlements selected from RimWorld's authorized pool repeat an owner or introduce another eligible owner. | Available pool settlements and eligible factions; it cannot add settlements or set final ownership. | Population-origin receipts and source faction keys are saved before local-faction formation and settlement materialization. | Local faction formation independently decides whether a generated owner retains its source faction. Authored settlement ownership replaces generation. |
| Local faction formation | `localFactionChance` | Changes whether a generated settlement owner becomes a new local faction or retains the source settlement's world faction. | Eligible settlement-capable faction definitions. | Faction `source`, faction definition or load ID, and settlement `factionKey` are saved; all faction consumers use that identity. | Authored factions own their source and type. |
| Regional conflict | `regionalConflictChance` | Changes the hostile chance only for newly generated faction pairs. | Existing RimWorld faction relations remain factual inputs. | Every pair stores one `relation`; `regionalRelationPattern` and `settlementPattern` derive from those rows. Defense, population mixing, trade, organizations, and map generation consume the saved relation. | A relation set in Starting Region has `authorRelation` and is never rerolled. |
| Off-map activity rate | `offMapActivityRate` | Sets the share of unloaded organizations processed per world pulse. | Current unloaded organization count; loaded and player-facing organizations remain active. | The saved rate and persistent activity cursor produce the per-pulse budget. Organization updates consume that budget; settlement facts remain resident regardless of the rate. | No starting-region field changes this world processing budget. |

RimWorld's world-population setting controls the major-settlement pool. CA may
reallocate a nearby authorized settlement into a generated region and records
the consumed source. Only an explicit `ScenarioOverride` may add a starting
settlement beyond that pool. Frontier holdings never consume or enlarge the
major-settlement pool.

Standard maps use the same frontier kernel. Their world component saves one
`CAFrontierMapPlan` per map with the map identity, causal hash, and realized
holding rows. Runtime materialization reads those rows; map area constrains the
number of suitable sites but does not act as a second frequency policy.

## Generation order

Generation resolves one confirmed candidate in this order:

1. Project the selected world areas into one visual land shape. Coasts, water
   depth, roads, rivers, caves, and selected geographic features use that same
   projection.
2. Select major settlements from RimWorld's settlement pool, assign generated
   owners, place them, and persist faction relations. Starting-region rows
   retain their authored settlement count, positions, owners, and relation
   overrides.
3. Resolve each faction's culture, Ideoligion, political beliefs, faction
   structure, and settlement authority.
4. Resolve and save each settlement's population, land capacity, access,
   services, civic development, economic capacity, trade connectivity,
   specialization, regional role, and
   historical development. Resolve frontier sites, households, and material
   forms separately.
5. Derive and save each settlement's scale, the regional relation pattern, and
   the realized settlement pattern from those facts.
6. Generate or retain settlement population groups.
7. Generate starting provisions from population, faction structure, facilities,
   infrastructure, settlement scale, and role. A player override changes one
   provision's distribution without replacing its generated cause or operator.
8. Derive material capability from faction era and local supports.
9. Materialize settlements, frontier holdings, residents, organizations,
   facilities, provisions,
   faction relations, and map geography from those saved results.

Generation consumes only a confirmed plan with a candidate identity. Opening a
screen, previewing, or drawing the UI does not regenerate saved choices.

## Faction state

Culture, Ideoligion, political beliefs, and faction structure are distinct.

- Culture supplies names, style, and ordinary customs.
- Ideoligion uses RimWorld's native `Ideo` state.
- Political beliefs describe what a population believes about leadership,
  decisions, participation, dissent, ownership, economy, work, support,
  membership, status, local order, defense, and war conduct.
- Faction structure answers the same concrete questions for the arrangement now
  in force. Beliefs and structure may disagree.
- Settlement authority records what several settlements of the same faction
  decide and provide together.

Each political-belief and faction-structure answer records its source as unset,
generated, authored, or preset. Presets fill answers; they are not additional
political entities.

## Settlement state

Settlement form, starting facilities, infrastructure, provisions, and capability
are separate facts.

Starting facilities are individually generated or set. The current facility set
is hearth, stores, infirmary, workshop, jail, dining hall, and laboratory.

Infrastructure has three independent dimensions:

| Dimension | Meaning |
|---|---|
| Access | roads, coast, and regional reach |
| Services | local capacity to support residents |
| Civic development | shared works and administration |

Population is stored as population groups. Each group records its share, faction
affiliation, Ideoligion source and certainty, political-belief source, and whether
it lives in a separate quarter. Materialization assigns real pawns to those saved
groups.

Economic capacity is a saved settlement fact derived from resident population,
civic development, and explicitly authored workshop and stores facilities.
Urban support then combines that capacity with saved land, access, services,
trade connectivity, specialization, regional role, and historical development.
The urban-growth tendency changes only the threshold applied to that support;
it does not author city status directly.

Starting provisions are generated causal arrangements, not stacks of starting
items. Their causes include everyday food, reserves, and a population group with
different needs. Distribution may be household, neighborhood, or centralized.
Facility and infrastructure changes reconcile the same saved causes instead of
adding duplicates.

## Capability

Capability is a derived assessment, never an authored cause. Faction era is the
upper bound. Facilities and infrastructure determine what the settlement can
practice locally. Medicine, communications, production, logistics,
fortification, weapons, training, and organization remain separate derived
results.

The current implementation takes faction era from the selected RimWorld
`FactionDef`. `researchStock` and `researchMilestones` describe work at a staffed
laboratory and its material rewards; they do not define faction knowledge or
unlock technology.

When the map is loaded, real pawns, buildings, stocks, roads, water, and
organizations remain authoritative. Derived capability summarizes those supports;
it does not override them.

## Persistence

The current pending-plan schema is `2`. It writes `factions`, `settlements`,
`populationGroups`, `startingProvisions`, `factionStructure`, `politicalBeliefs`,
`settlementAuthority`, `relations`, `frontierHoldings`, `settlementPattern`,
`settlementScale`, `regionalRelationPattern`, and
`settlementRealizationComplete` directly. Each settlement row carries the
realized facts used to derive its scale. A confirmed plan consumes those facts;
generation does not consult its tendencies again.

This project is pre-1.0. Superseded experimental plan and save schemas are not a
runtime compatibility target. The authored regional fixture is converted to the
current schema rather than supported through permanent aliases or migrations.
