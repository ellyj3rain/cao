# Colony-creation surface map

Updated 2026-08-10. This is the current onboarding contract.

## Current flow

| Stage | Owner | Current decision |
|---|---|---|
| Scenario | RimWorld | scenario, arrival method, starting pawn count, starting items, forced map |
| Storyteller | RimWorld | storyteller, difficulty, permadeath |
| World | RimWorld + CA | seed, coverage, climate, population, faction roster, and CA world tendencies |
| Landing | RimWorld + CA | landing tile, selected regional land, visual geography, and arrival area |
| Starting region | CA | factions, settlements, relations, culture, Ideoligion, political beliefs, faction structure, settlement authority, population groups, facilities, and provisions |
| Founding society | CA + RimWorld | founding Culture, native Ideoligion, Political Beliefs, and the Founding Arrangement adopted at landing |
| Starting pawns | RimWorld | founders, skills, traits, relationships, xenotypes, and backstories |
| Game start | RimWorld + CA | map generation, scenario arrival, materialization of the saved Founding Arrangement, and the beginning of the colony's institutional history |

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

### Established societies

Starting Region describes societies whose histories precede the player's
arrival. Their Culture, Ideoligion, Political Beliefs, social order,
institutions, faction relations, and any conflict between belief and practice
are current facts. The faction and settlement editors write those realized
facts directly.

### Player founding

`Page_CAPlayerFounding` replaces the vanilla preset page as the top-level
player creator. It uses the same Culture and Political Beliefs models and
editors as existing factions. RimWorld's native `Ideo` remains the authority for
Ideoligion; CA exposes native presets, load, fixed editing, and fluid editing
from the coordinated founding page rather than presenting vanilla Ideology as a
separate creator.

The player authors a founding moment, not an established society. Culture,
Ideoligion, and Political Beliefs are what the founders bring. Political
Beliefs describe what they consider proper. The Founding Arrangement records
the authority, work, voice, supplies, and duration rules they institute at
landing. Agreement or disagreement between belief and the adopted arrangement
is saved state.

Only the initial order is materialized before play. The player faction does not
receive generated mature institutions or invented pre-game history. Its later
offices, organizations, practices, and institutional changes develop through
simulation. Existing factions continue to receive the realized structures
appropriate to societies with prior histories.

## Persistence and validation

One keyed pending file per world identity stores the current regional candidate.
Schema 3 writes factions, settlements, relations, population groups,
facilities, infrastructure, provisions, and the regional copy of
`playerFounding`. A world component owns the same confirmed founding state for
regional, ordinary, and forced-map starts. It records Culture, Political
Beliefs, the exact Founding Arrangement and its provenance, the native player
Ideoligion content-and-revision receipt, and the tick at which the arrangement
was applied. Same-ID native edits invalidate the prior notification. There is
no load alias or migration path for abandoned pre-release schemas.

The landing page validates the selected footprint. The Starting Region page
validates the complete regional bundle, arrival area, factions, settlements,
and confirmation. The Founding Society page validates all player layers before
Starting Pawns, including RimWorld's native Ideoligion checks for names,
incompatible precepts, ritual targets, and consumable-building rituals.
Generation uses the confirmed regional candidate and saved founding state; it
does not reroll the player's chosen arrangement.

## Runtime gate

The next operator test must confirm that the keyed authored composition restores
without losing its arrival area or options, reaches Founding Society instead of
the vanilla preset page, preserves edits across native Ideoligion configuration
and Back/Next navigation, passes every page gate, and generates the same selected
map. Visual hierarchy, copy fit, native Ideoligion editing, belief-versus-order
clarity, geography, feature placement, settlement identifiers, facilities,
residents, and provisions remain runtime judgments.
