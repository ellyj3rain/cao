# Settlement generation model

This document defines the current regional authoring and
settlement-generation model. The code, saved plan, generated settlement records,
and setup UI use the same terms.

## Current saved facts

| Level | Saved facts |
|---|---|
| Region | selected world areas, arrival area, world tendencies, settlement pattern, principal settlement scale, regional relation pattern, frontier holdings |
| Faction | source faction, substantive inherited Culture, optional visual tradition, native Ideoligion receipt, normative Political Beliefs, instituted current order, settlement authority, faction era |
| Settlement | owning faction, world area, population origin, resident population, land capacity, role, form, realized access, services, civic state, economy, trade, specialization, historical development, scale, population groups, settlement program, provision arrangements, local Culture |
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
   Political Beliefs, current order, settlement authority, and temporal
   basis. Resolve the player's inherited Culture and Political Beliefs, retain
   the native Ideoligion receipt, and materialize only the exact rules adopted at
   landing. Broader player structure remains unset until play establishes it.
4. Resolve each settlement's population, land, role, form, access, services,
   civic state, economy, trade, specialization, and history. Derive one open
   settlement program from those facts and loaded functional asset contracts.
   Resolve frontier sites and forms separately.
5. Derive and save settlement scale, regional relation pattern, and settlement
   pattern from those facts.
6. Generate or retain population groups and causal provision arrangements with
   real household, communal, or authority operators.
7. Derive material capability from faction era and local supports.
8. Establish each existing settlement's directly authored local Culture
   baseline: constituents, inherited/local question distributions, current
   practices, plurality, legacy evidence, and disagreement. Do not fabricate
   transition history.
9. Materialize settlements, holdings, residents, organizations, program assets,
   provisions, faction relations, and map geography from saved results.

Opening, drawing, previewing, or inspecting a screen does not regenerate state or
advance Culture.

## Culture

Culture is a durable population distribution over explicit social questions plus
concrete practice and history. It is not a native `CultureDef`, visual style,
political profile, factual-subject list, or prose summary of settlement variables.

Every Culture saves:

- stable inherited and local identity;
- constituent Cultures and population shares;
- inherited and local question distributions;
- exact legacy evidence from compatible former meaning records;
- inherited and lived practices;
- observations and transition history;
- predecessor and evidence signatures;
- maturity and temporal basis;
- optional native visual tradition, labeled separately.

A question distribution stores question and population scope, center, spread,
descriptive-norm prior, prestige signal, salience, norm strength, visibility,
source confidence, divergence tolerance, optional subgroups, provenance, source
identity, evidence signature, and historical ticks. A cultural practice records
durable repeated conduct with actors, target, trigger or cadence, operator,
authority, setting, material conditions, evidence, consumer, provenance, and
implicated subjects. A factual subject does not become a question or practice.

The production vocabulary contains 24 Culture questions in eight categories,
45 factual social referents, and 30 concrete practices derived from the active mechanics inventory.
Every Culture question identifies a coherent construct, direct authoring fact,
constraints, migration rule, and real consumer. Every referent and practice has a
factual source and consumer. An open registration contract remains
representational capacity, not evidence that unsupported future content exists.

The same Culture composer serves player founding, established factions, and
settlements at their different temporal scopes. It exposes constituents, eight
question categories, five descriptive anchors per question, one global
diversity control, advanced per-question spread, complete historical/social
presets, deterministic randomization, inherited and current concrete practices,
legacy evidence, disagreements, continuity, transitions, optional visual
tradition, and causal preview. All authoring paths write the same Culture object.
A founding Culture
must contain at least one substantive question distribution or inherited
practice. Saved Culture profiles copy inherited distributions, practices, and
optional visual tradition without locality,
observations, transitions, evidence, or a live profile pointer.

## Political Beliefs and realized order

Political Beliefs and current order use the same thirteen comparison subjects:
leadership, decisions, participation, dissent, ownership, economy, work,
support, membership, status, local order, defense, and conduct in war. Beliefs
record what a population considers proper. Current order records instituted
mechanisms. Agreement and contradiction are both valid state.

Each subject is independently composable and may carry several compatible
mechanisms. Only explicit absence on leadership, local order, or defense excludes
standing mechanisms on that same subject. Twelve belief sets and ten current-order
sets are transparent partial accelerators. Each declares its exact mechanisms,
adds those mechanisms to a copy, and preserves unlisted and already-authored
compatible facts. Set identity never enters world identity, later saved-set edits
cannot mutate a world, and missing player positions are never filled randomly.

NPC generation derives one axis only from:

- the same subject in realized current order;
- an explicit axis-specific observed fact; or
- the corresponding non-neutral Culture question distribution where one exists.

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

Pawn cultural state persists private position, public expression, perceived
descriptive and injunctive norms, confidence, moral conviction, identity
centrality, enforcement expectation, visibility, prestige, uncertainty, and
provenance. Actual social structure supplies sparse influence edges weighted by
contact, trust, prestige, conformity, visibility, and repeated exposure. Culture
transition considers prior distribution and practice, pawn and group response,
institutional response, population composition, material and spatial conditions,
and elapsed historical time.

Qualified evidence across at least two historical periods may change a question
distribution or practice and records predecessor, evidence, changed questions,
population scope, successor signature, tick, and cause. One isolated event,
unchanged evidence, or a broken evidence run does not rewrite Culture. Plural
constituent meanings and unresolved conflict may persist.

## Generic consumption

The generic Culture resolver returns a population distribution, constituent
contributions, provenance, and confidence for one admitted question and scope.
Social, institutional, political, relationship, knowledge, and discretionary
behavior consumers use that state to appraise or rank otherwise valid
possibilities. Culture does not create permission, authority, knowledge,
technology, labor, material, land, treasury, office, relationship, institution,
research result, or execution capability.

## Technological knowledge

The faction owns Technological Knowledge as a sibling of Culture and Political
Order. Society authoring composes the three values together, and a Society
preset snapshots them together, but neither becomes a runtime owner. Founding
and regional plans stage the value only until faction realization. A settlement
reads current faction knowledge unless an explicit local divergence is modeled;
its saved knowledge tier and revision explain the conditions under which it was
realized and never replace the faction owner.

Nine practical domains separately record understanding, construction,
operation, and maintenance. One requirement resolver translates native
research, construction, production, agriculture, medicine, logistics, and
defense definitions into those domains. `FactionDef.techLevel` may seed a new
otherwise unauthored faction and provide native metadata, but it is not runtime
authority. Standard mode reads the faction state directly. Experimental
Distributed Knowledge derives the same requirements from accessible pawn,
institution, and record custody at the requesting map or settlement scope.

Settlement viability first derives environmental requirements, then asks
whether the faction's current effective knowledge satisfies them, and finally
asks whether represented labor, materials, access, and programs can act on that
knowledge. These remain separate facts: knowing how to build a greenhouse does
not supply its materials, and a hostile biome does not author knowledge.

## Settlement state and capability

Population groups record share, affiliation, Ideoligion source and certainty,
political-belief source, and separate-quarter status. Materialization assigns
real pawns to those saved groups.

Established settlement composition is an open operational program, never a
fixed facility mask, intensity profile, or axis-threshold product. Every active
entry records the concrete need, actual operator where required, standing,
knowledge, labor, material inputs and nodes, service/activity, target population,
access, funding or maintenance, failure condition, and causal signature. Loaded
functional candidates are resolved only after that contract is complete. A room,
worktable, storage object, bed, ritual site, communication object, or other asset
is a node; it cannot establish its own organization or institution.

The registry contains 22 possible program contracts across housing, food,
storage, medicine, production, trade, governance, security, defense, research,
religion, social life, culture, agriculture, communications, and transport.
Unsupported contracts stay absent. Housing, food preparation, recreation,
instituted governance, exact routes, and post-materialization domestic provision
can arise from their direct facts. The other programs require explicit saved
operational evidence or a complete provision arrangement. Loaded native,
expansion, and ported assets participate through functional contracts;
visual-only props do not establish a program.

Provision arrangements are causal records rather than arbitrary stacks. An
individual or persistent domestic unit, communal organization, or authority must
exist socially, materially, and behaviorally before its arrangement can be
generated. Domestic units form from actual partner, kin, shared-residence, or
authored co-residential evidence and preserve source-scoped membership and
transitions. Operator, labor, knowledge, access, funding, stock, material nodes,
policy where required, program support, cause, and exact counts are saved once
and consumed without rerolling. Unsupported vendor, religious, dues, and
abstract-distribution branches are absent.

Capability is a derived domain assessment. Medicine, production, logistics,
civic administration, research, security, commerce, and communications each
retain their exact actors, organizations, active work, knowledge, material
nodes, historical observations, blockers, confidence, assessment tick, and
source signature. Missing evidence yields an absent, weak, or uncertain result.
There is no random jitter, and a capability cache never owns or creates its
supports.

## Persistence

The current pending-authoring data epoch is `13`. The pending-plan schema is `14`; the
materialized settlement record uses schema `9`; settlement programs and entries
use schema `4`; operational facts use schema `3`; program-asset receipts use
schema `1`; provision arrangements use schema `5`; domestic units and residence
use schema `1`; capability assessments use schema `2`; Culture uses schema `10`;
Political Order uses schema `10`; Technological Knowledge uses schema `1`;
player founding uses schema `4`. The plan writes
factions, settlements, population groups, programs, provision arrangements,
local Culture, Political Order, Technological Knowledge, represented institutions, settlement authority, relations, holdings, patterns, scales,
realization state, and the player-founding object directly.

Pending authoring remains pre-release and an epoch mismatch may reject an
unconfirmed regional draft, founding draft, or reusable set with one diagnostic.
It cannot clear realized Culture, Political Order, Technological Knowledge,
represented institutions, social interpretation, or campaign history. Live B15
state follows the durable campaign manifest and explicit compatibility contract.
The governed B15 fixture contains
intentional world, region, candidate, arrival, scale, faction, settlement,
population, and 19 explicitly established operational facts. On load it enters
the same production realization path as an operator-authored draft; the fixture
generator calls the production authoring kernel and contains no parallel causal
model. Its active and keyed surfaces preserve three factions, four settlements,
nine population groups, nineteen established operations, 192 complete root
Culture question distributions, 2 authored local distributions, and 26
preserved evidence records under epoch 13, regional-plan schema 14, and Culture
registry 2.
