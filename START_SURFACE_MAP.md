# Colony-creation surface map

Updated 2026-08-12. This is the current onboarding contract.

## Current flow

| Stage | Owner | Current decision |
|---|---|---|
| Scenario | RimWorld | scenario, arrival method, starting pawn count, starting items, forced map |
| Storyteller | RimWorld | storyteller, difficulty, permadeath |
| World | RimWorld + CA | seed, coverage, climate, population, faction roster, and CA world tendencies |
| Landing | RimWorld + CA | landing tile, selected regional land, visual geography, and arrival area |
| Starting Region | CA | existing factions, settlements, relations, substantive Culture, Ideoligion, Political Beliefs, current order, population groups, placement, settlement programs, and provision arrangements |
| Founding society | CA + RimWorld | founders' cultural background, native Ideoligion, Political Beliefs, and the Founding Arrangement adopted at landing |
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

### Starting Region

The Starting Region page authors who already lives on the selected land. Objects
and Map remain its primary navigation. Object selection opens Details; map
selection remains spatial and offers `View Details`. Compact settlement Details
keeps the selected settlement visible and supports previous/next comparison in
the same order as the object rail without regenerating state.

Factions own Culture, Ideoligion, Political Beliefs, realized structure,
Settlement Authority, relations, and technology. Settlements own identity,
population, local Culture, location, realized material state, explicitly
established operational facts, the programs derived from those facts, and
causal provision arrangements. Settlement Details can establish or remove one
operation for one exact population-group operator. Unsupported operations remain
absent and explain the missing contract. Ordinary Details shows
only meaningful authoring choices, essential inspection, blocking warnings, and
object-specific secondary inspection. One valid choice is a fact, not a button;
generation and schema provenance remain diagnostic state.

The page does not add another political identity tier. Organization details are
generated from these saved facts and continue in play as offices, groups, customs,
security, agreements, policies, claims, relations, and decisions.

### Established societies

Starting Region describes societies whose histories precede the player's
arrival. Their cultural background, contextual expression, Ideoligion, Political Beliefs, social order,
institutions, faction relations, and any conflict between belief and practice
are current facts. The faction and settlement editors write those realized
facts directly.

### Player founding

`Page_CAPlayerFounding` follows RimWorld's native Ideoligion chooser. It uses the
same cultural-background and Political Beliefs models and editors as existing
factions while retaining the founders' draft through native preset, load, fixed,
fluid, customize, Back, and Continue paths. RimWorld's native `Ideo` remains the
authority for Ideoligion.

The player authors a founding moment, not an established society. Cultural background,
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

One keyed pending file per world identity stores the current regional candidate;
the active mirror carries the same plan. Regional schema 11 writes factions,
settlements, relations, population groups, explicit operational facts,
settlement-program schema 4 entries, program-asset identities, provision
arrangements, substantive local Culture, and the regional copy of
`playerFounding`. Culture and Political Beliefs schema 9 stores substantive
inherited and current state. A world component owns the same confirmed founding state for
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

The next operator test must confirm that both active plan surfaces restore the
same three-faction/four-settlement composition and 19 explicit established
operations without losing the arrival area or options; that Objects, Map,
Details, selection, program establishment/removal, and settlement comparison
feel natural; that Culture semantic editing and contextual exact-value fine
tuning reopen without loss; that factual programs, exact operators, assets,
provisions, and security assignments survive generation and save/load; and that
the selected map generates. Visual hierarchy, copy fit, Faction Relations,
geography, feature placement, settlement identifiers, cultural readings,
program composition, residents, and provisions remain runtime judgments.
