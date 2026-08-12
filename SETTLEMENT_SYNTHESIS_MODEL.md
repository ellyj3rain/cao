# Settlement generation model

This document defines the current regional authoring and
settlement-generation model. The code, saved plan, generated settlement records,
and setup UI use the same terms.

## Current saved facts

| Level | Saved facts |
|---|---|
| Region | selected world areas, arrival area, world tendencies, settlement pattern, principal settlement scale, regional relation pattern, frontier holdings |
| Faction | source faction, substantive inherited Culture, optional visual tradition, native Ideoligion receipt, complete Political Beliefs, faction structure, settlement authority, faction era |
| Settlement | owning faction, world area, population origin, resident population, land capacity, role, form, sparse facility exceptions, realized access, services, civic state, economy, trade, specialization, historical development, scale, population groups, provisions, local Culture |
| Faction relation | left faction, right faction, realized relation, authored/generated provenance |
| Frontier holding | world area, household size, land capacity, material level, form, faction status |

Regions contain factions and settlements directly. Outcomes are saved once after
their causes are resolved; runtime consumers do not reroll authoring tendencies.

## World tendencies causal contract

World tendencies are defaults for generated state. A starting-region choice
replaces the corresponding default at the surface that owns the fact.

| Control | Direct cause and realized result |
|---|---|
| Generated land | resolves single-area or stitched generated regions; the confirmed footprint then owns its member areas |
| Stitched region size | requests a connected member-area count; actual land, impassability, and occupied neighbors constrain it |
| Settlement concentration | changes placement scoring before the saved settlement pattern is classified from actual distance, routes, hierarchy, mixture, and relations |
| Urban growth propensity | changes the threshold applied to actual population, land, access, services, civic state, economy, trade, specialization, regional role, and history |
| Frontier holding frequency | determines how many suitable unoccupied sites receive holdings |
| Frontier holding size | determines household and material form after a site exists |
| Unaffiliated residents | changes the generated population share without faction membership |
| Reallocation source variety | changes generated source selection without authoring final ownership or settlement count |
| Local faction formation | changes whether an eligible generated owner becomes a new local faction |
| Regional conflict | changes generated relations for new faction pairs; authored relations remain fixed |
| Off-map activity rate | changes the saved world-processing budget, not settlement facts |

RimWorld's world-population setting controls the major-settlement pool. Only an
explicit scenario override may add a starting settlement beyond that pool.
Frontier holdings never consume or enlarge it.

## Generation order

Generation consumes one confirmed candidate in this order:

1. Project selected world areas into one visual land shape. Coasts, water depth,
   roads, rivers, caves, and selected features use that projection.
2. Select major settlements from RimWorld's pool, assign generated owners, place
   them, and persist faction relations. Starting-region rows retain authored
   count, positions, owners, and relation overrides.
3. Resolve each established faction's substantive Culture, native Ideoligion,
   Political Beliefs, faction structure, settlement authority, and temporal
   basis. Resolve the player's inherited Culture and Political Beliefs, retain
   the native Ideoligion receipt, and materialize only the exact rules adopted at
   landing. Broader player structure remains unset until play establishes it.
4. Resolve each settlement's population, land, role, form, access, services,
   civic state, economy, trade, specialization, history, facilities, and sparse
   exact facility exceptions. Resolve frontier sites and forms separately.
5. Derive and save settlement scale, regional relation pattern, and settlement
   pattern from those facts.
6. Generate or retain population groups and causal starting provisions.
7. Derive material capability from faction era and local supports.
8. Establish each existing settlement's directly authored local Culture
   baseline: constituents, current meanings, current practices, plurality, and
   disagreement. Do not fabricate transition history.
9. Materialize settlements, holdings, residents, organizations, facilities,
   provisions, faction relations, and map geography from saved results.

Opening, drawing, previewing, or inspecting a screen does not regenerate state or
advance Culture.

## Culture

Culture is durable social meaning and practice, not a native `CultureDef`, visual
style, political profile, or prose summary of settlement variables.

Every Culture saves:

- stable inherited and local identity;
- constituent Cultures and population shares;
- inherited and current cultural meanings;
- inherited and lived practices;
- observations and transition history;
- predecessor and evidence signatures;
- maturity and temporal basis;
- optional native visual tradition, labeled separately.

A cultural meaning concerns one namespaced registered social subject. It stores
approval, normality, prestige, salience, population scope, provenance, source
identity, evidence signature, and historical ticks. A cultural practice records
durable repeated conduct. Selecting a meaning does not create a practice.

The initial open registry contains twelve subjects spanning shared space,
exchange, defense, knowledge, public voice, compelled service, enforced order,
compulsory transfer, shared provision, inherited rank, custody, and conduct in
war. Each registered subject identifies its factual source and real consumers.
Later CA modules and explicit adapters may register more. Unknown valid keys
survive persistence; inert unregistered strings do not enter ordinary editors.

The same Culture composer serves player founding, established factions, and
settlements. It exposes overview, social meanings, inherited practices, optional
visual tradition, and causal preview. A founding Culture must contain at least
one substantive meaning or inherited practice. Saved profiles copy inherited
meanings, practices, and optional visual tradition without locality,
observations, transitions, evidence, or a live profile pointer.

## Political Beliefs and realized order

Political Beliefs and faction structure answer the same thirteen concrete
questions about leadership, decisions, participation, dissent, ownership,
economy, work, support, membership, status, local order, defense, and conduct in
war. Beliefs record what a population considers proper. Structure records what
is in force. Agreement and contradiction are both valid state.

Player authoring is question-first. Each answer is independently authored. Five
complete built-in profiles provide transparent accelerators; each answers all
thirteen questions, has no parent or missing value, and copies the vector as
authored state. The profile key and name do not enter world identity. Missing
player positions are never filled randomly.

NPC generation derives one axis only from:

- the same axis in realized faction structure;
- an explicit axis-specific observed fact; or
- a non-neutral cultural meaning that scores that axis.

Every resolved axis records the causal evidence and stable tie-break. Unsupported
axes remain unset. Technology, hostility, raids, trade reach, leader presence,
and permanent-enemy status do not stand in for political beliefs.

Native Ideoligion remains first-class and separate. Its religious, ritual,
moral, and spiritual semantics may contribute when they overlap a known social
fact, but Ideoligion does not bound Culture. Founding rules remain the actual
authority, work, voice, and supply arrangement adopted at landing.

## Social interpretation and cultural change

Social facts do not create map-global knowledge. A pawn responds only when a
real observation or communication route supplies the fact. Interpretation may
retain separate contributions from Culture, Political Beliefs, native
Ideoligion where relevant, personal state, institutions, and relationships.
Contradictions remain visible.

Recorded pawn reactions aggregate per subject into weighted response,
dispersion, polarization, participation, influential minority presence, group
alignment, and cross-group dissonance. Actual social structure determines
influence. Culture transition considers prior meaning and practice, pawn and
group response, institutional response, population composition, material and
spatial conditions, and elapsed historical time.

Qualified evidence across at least two historical periods may change meaning or
practice and records predecessor, evidence, changed subjects and dimensions,
population scope, successor signature, tick, and cause. One isolated event,
unchanged evidence, or a broken evidence run does not rewrite Culture. Plural
constituent meanings and unresolved conflict may persist.

## Generic consumption

The generic meaning resolver returns aggregate meaning, constituent
contributions, dissonance, provenance, and confidence for one subject and scope.
Spatial, social, institutional, political, and settlement-development consumers
use that result to rank or interpret otherwise available possibilities. Culture
does not create permission, authority, knowledge, technology, labor, material,
land, treasury, office, or execution capability.

## Settlement state and capability

Population groups record share, affiliation, Ideoligion source and certainty,
political-belief source, and separate-quarter status. Materialization assigns
real pawns to those saved groups.

Facilities are derived from current settlement facts. Sparse exact exception
bits may preserve or forbid a named starting facility; there is no general
development profile or per-facility Generated/Include/Omit authoring matrix.
Access, services, civic state, economic capacity, trade connectivity,
specialization, role, history, and urban support remain independent realized
facts. Urban-growth tendency changes only the threshold, not city status itself.

Starting provisions are causal arrangements rather than arbitrary stacks. Their
operator, access, funding, and cause derive from population, faction structure,
facilities, infrastructure, settlement scale, and role. A player override changes
distribution only.

Capability is a derived assessment. Faction era is the upper bound; actual
facilities, infrastructure, knowledge, pawns, buildings, stocks, roads, water,
and organizations determine what a settlement can practice. A capability cache
never overrides those supports.

## Persistence

The current authoring data epoch is `8`. The pending-plan schema is `6`; Culture
and Political Beliefs schema is `8`; player founding schema is `3`. The plan
writes factions, settlements, population groups, provisions, local Culture,
Political Beliefs, faction structure, settlement authority, relations, holdings,
patterns, scales, realization state, and the player-founding object directly.

This project is pre-release. An epoch mismatch clears incompatible CA-owned
Culture, Political Beliefs, profile, founding-draft, social-interpretation, and
regional-authoring state with one diagnostic. The current implementation does
not preserve abandoned fields, aliases, partial profiles, old preset identities,
or field-by-field migration machinery. The governed B8 fixture is generated
directly from its intentional world, region, candidate, arrival, scale, faction,
settlement, and population inputs.
