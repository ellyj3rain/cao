# Colony-creation surface map

Updated 2026-08-09. This is the current onboarding contract.

## Current flow

| Stage | Owner | Current decision |
|---|---|---|
| Scenario | RimWorld | scenario, arrival method, starting pawn count, starting items, forced map |
| Storyteller | RimWorld | storyteller, difficulty, permadeath |
| World | RimWorld + CA | seed, coverage, climate, population, faction roster, and CA world tendencies |
| Landing | RimWorld + CA | landing tile, selected regional land, visual geography, and arrival area |
| Starting region | CA | factions, settlements, relations, culture, Ideoligion, political beliefs, faction structure, settlement authority, population groups, facilities, and provisions |
| Ideology | RimWorld | the player faction's Ideoligion |
| Starting pawns | RimWorld | founders, skills, traits, relationships, xenotypes, and backstories |
| Game start | RimWorld + CA | map generation, scenario arrival, and the colony's generated or authored founding arrangement |

`Page_CAStartingRegion` is inserted after `Page_SelectStartingSite`. The landing
page retains the globe and exact tile selection. The inserted page owns faction
and settlement authoring and must be confirmed before generation can begin.

## Surface ownership

### World tendencies

`Page_CreateWorldParams` exposes World tendencies beside the world settings they
affect. These are defaults for generated regions and settlements. Starting-region choices may
override them. The control does not describe the player colony.

### Landing and geography

The landing page owns the selected world tile. CA adds the connected regional
footprint, arrival area, and projected visual geography. Coasts, water, roads,
rivers, caves, and selected geographic features use the same projection. A valid
footprint is enough to proceed to the Starting region page; incomplete faction or
settlement work is repaired there.

### Starting region

The Starting region page authors who already lives on the selected land. It uses
the current faction and settlement model directly. Factions own culture,
Ideoligion, political beliefs, faction structure, settlement authority, and era.
Settlements own form, role, facilities, access, services, civic development,
population groups, and starting provisions.

The page does not add another political identity tier. Organization details are
generated from these saved facts and continue in play as offices, groups, customs,
security, agreements, policies, claims, relations, and decisions.

### Player Ideoligion and founding arrangement

RimWorld's Ideology pages remain authoritative for the player faction's
Ideoligion. CA does not add custom political precepts or a pre-landing
belief-comparison screen. At game start, after the scenario, founders,
Ideoligion, and site exist, the founding arrangement records current ownership,
work, decision, and membership terms. Authored terms win; open terms are generated
from the scenario, current faction structure, and political beliefs.

## Persistence and validation

One keyed pending file per world identity stores the current regional candidate.
It writes factions, settlements, relations, population groups, facilities,
infrastructure, and provisions under their current names. There is no load alias
or migration path for abandoned pre-release schemas.

The landing page validates the selected footprint. The Starting region page
validates the complete regional bundle, arrival area, factions, settlements, and
confirmation. Generation uses that exact confirmed candidate.

## Runtime gate

The next operator test must confirm that the keyed authored composition restores
without losing its arrival area or options, passes both page gates, and generates
the same selected map and preset. Visual geography, feature placement, settlement
identifiers, facilities, residents, and provisions remain runtime judgments.
