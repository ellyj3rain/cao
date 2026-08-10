# Settlement generation model

This document defines the current regional authoring and settlement-generation
model. The code, saved plan, generated settlement records, and setup UI use the
same terms.

## Current state

A regional plan contains:

| Level | Saved facts |
|---|---|
| Region | selected world areas, arrival area, world tendencies, settlement pattern, settlement scale |
| Faction | source faction, culture, Ideoligion, political beliefs, faction structure, settlement authority, faction era |
| Settlement | owning faction, world area, regional role, form, starting facilities, access, services, civic development, population groups, starting provisions |
| Faction relation | left faction, right faction, relation, whether the player set it |

Regions contain factions and settlements directly.

## Generation order

Generation resolves one confirmed candidate in this order:

1. Project the selected world areas into one visual land shape. Coasts, water
   depth, roads, rivers, caves, and selected geographic features use that same
   projection.
2. Resolve settlement pattern, scale, and each settlement's regional role.
3. Resolve each faction's culture, Ideoligion, political beliefs, faction
   structure, and settlement authority.
4. Resolve each settlement's form, facilities, access, services, and civic
   development.
5. Generate or retain its population groups.
6. Generate starting provisions from population, faction structure, facilities,
   infrastructure, settlement scale, and role. A player override changes one
   provision's distribution without replacing its generated cause or operator.
7. Derive material capability from faction era and local supports.
8. Materialize settlements, residents, organizations, facilities, provisions,
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

The current pending-plan schema writes `factions`, `settlements`,
`populationGroups`, `startingProvisions`, `factionStructure`, `politicalBeliefs`,
`settlementAuthority`, `settlementPattern`, and `settlementScale` directly.

This project is pre-1.0. Superseded experimental plan and save schemas are not a
runtime compatibility target. The authored regional fixture is converted to the
current schema rather than supported through permanent aliases or migrations.
