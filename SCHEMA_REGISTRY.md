# Campaign schema registry

Status: canonical for `1.4.1.0-alpha` / B13
Boundary version: `1`
First durable CAO campaign baseline: B11
Current catalog version: `3`

This registry versions semantic state families, not filenames or C# layouts.
Moving a type without changing its saved meaning does not create a schema.
Changing saved meaning, identity, validation, or lifecycle does.

`CACampaignSchemaCatalog` is the executable catalog. On every successful save,
`CACampaignCompatibilityWorldComponent` persists the boundary, one record per
catalog key, and truthful migration/bootstrap receipts. Before SafeSaver swaps
the candidate into place, CAO streams the emitted component tree, validates
every catalog-backed component owner at its exact game/world/map scope, checks
required nested payloads for every repeated parent, and appends a terminal
SHA-256 seal. A current save must pass the same validation and seal check before
Scribe loads any owner. The profiler and its counters are runtime-only and are
never part of this registry.

`PERSISTENCE_CENSUS.md` is the independent source-side ledger: it discovers
actual production writers/native carriers and maps every one to this catalog or
to a stated non-campaign exclusion. The executable reverse check in
`ValidateCatalogCoverage` requires every row here to have a real validation
route. Neither list is accepted as evidence for itself.

## Common compatibility contract

For all registry rows unless an exception is stated:

- current and minimum compatible versions are the values shown below;
- a B13 save must preflight at campaign boundary `1`, catalog `3`, and carry a nonempty
  manifest;
- unmanifested B10 state is accepted only when every discovered legacy
  `CA_authoringDataEpoch` is `10`;
- the B10-to-B11 operation preserves represented fields and stable identities,
  adds the boundary/manifest on the next successful save, and records
  `initialized at B11 upgrade from represented B10 state; no earlier history
  inferred`;
- a newly introduced family may initialize only from facts represented at the
  upgrade tick and records that upgrade-time provenance;
- a catalog-1 B11 save may omit only families introduced in catalog 2; those
  families initialize from facts represented at the B12 upgrade tick and record
  that provenance before catalog 2 is published;
- catalog 3 requires cultural-cognition schema 2 and Culture question registry
  2. Experimental catalog-2 cultural-cognition schema 1 is below the current
  compatible floor and fails visibly rather than acquiring invented separated
  causal state;
- every current save has exactly one instance of each world-level compatibility
  owner, while the map Culture owner may occur once per represented map;
- every catalog-backed component occurs at its declared game/world/map scope,
  each map component occurs once per represented map, and every required root,
  repeated nested record and parallel-list invariant validates before save swap
  and before load;
- each inline owner version must agree with its manifest record;
- owner migration validates semantic state before and after a version change,
  and publishes the new owner/catalog version only after validation succeeds;
- an unknown key, future boundary, incompatible version, conflicting B10 epoch,
  or current boundary without a manifest blocks load visibly and guards save;
- there is no automatic destructive live-state fallback.

Pending creation data follows the separate authoring contract below and is not
evidence of realized campaign history.

## Component and engine state families

| Schema key | Current / minimum | Semantic and persistence owner | Saved state and validation |
|---|---:|---|---|
| `campaign.boundary` | 1 / 1 | `CACampaignCompatibilityWorldComponent` | boundary, schema records, migration receipts; full catalog validation |
| `world.act-ledger` | 2 / 1 | `CAActLedger` | acts, evidence, distinct reporter-to-holder source routes, per-route acquisition channels and proposition delivery, response bookkeeping and retention; schema-1 rows gain explicit source-route collections without invented reporters |
| `map.arrangements` | 1 / 1 | `CAArrangementMapComponent` | arrangement identities and membership; live-pawn pruning |
| `map.assault-awareness` | 1 / 1 | optional repeated `JobDriver_CASearchKnownContact` state | hostile identity, source tick, completed survey state; absent when no search job is active |
| `map.autonomous-home` | 1 / 1 | `AutonomousHomeMapComponent` | pending plan, vetoes, material/construction evidence, completed CA buildings |
| `game.autonomy` | 1 / 1 | `AutonomyComponent` | initiative schema and per-pawn tiers |
| `map.parley` | 1 / 1 | `CAParleyMapComponent` | parley and envoy missions |
| `map.behavior-intent` | 1 / 1 | `CABehaviorIntentMapComponent` | owned job intents and bounded decision observations |
| `map.combat-aftermath` | 1 / 1 | `CAAftermathAccountabilityMapComponent` | execution accountability records |
| `game.combat-spatial-log` | 1 / 1 | `CACombatSpatialLogComponent` | concern snapshots and battle-log-linked records |
| `game.combat-topology` | 1 / 1 | combat-topology receipt owner | serialized topology incidents/reference artifacts used by governed diagnostic receipts; no gameplay mutation on readback |
| `map.culture-longitudinal` | 2 / 1 | `CACultureLongitudinalMapComponent` | local Culture question distributions, practices, evidence, history and evaluation schedule; Culture model validates revision/history |
| `map.equipment-transition` | 1 / 1 | `EquipTransitionMapComponent` | stowed/transition weapon identity and authority episodes |
| `game.hidden-things` | 1 / 1 | `HiddenThingsComponent` | hidden stash identities and quality |
| `world.faction-state` | 2 / 1 | `CAFactionStateWorldComponent` | faction state list; Culture, normative beliefs, and current-order nested validation |
| `world.player-founding` | 2 / 1 | `CAPlayerFoundingWorldComponent` | confirmed founding plan, application tick and source identity |
| `world.organization` | 2 / 1 | `CAOrganizationWorldComponent` | organizations, offices, membership, customs, agreements, cases, offers, gatherings, frontier plans, legitimacy and sanction appraisals |
| `world.organization-relations` | 1 / 1 | `CAOrganizationRelationsWorldComponent` | typed relations and facility holdings; unique IDs/signatures and next-ID continuity |
| `world.regional` | 2 / 1 | `CARegionalWorldComponent` | realized regions, settlements, world policy and groundwater tuning; stable region/settlement identities and nested schema validation |
| `world.cultural-cognition` | 2 / 2 | `CACulturalCognitionWorldComponent` | persistent psychology evidence, dynamic condition, private/public attitudes, attention, inherited-prior strength, perceived social pressure, observation likelihood, confidence, conviction, uncertainty, sparse influence edges, cursors and cadence state; introduced in catalog 2, current separated semantics published in catalog 3 |
| `world.political-cognition` | 1 / 1 | `CAPoliticalCognitionWorldComponent` | pawn political attitudes, issue links, faction-bounded coalitions, cursors and cadence state; introduced in catalog 2 |
| `world.proposition-knowledge` | 1 / 1 | `CAPropositionKnowledgeWorldComponent` | propositions, holders/sources/access/confidence/transmission/custody/decay and research receipts; introduced in catalog 2 |
| `world.social-reactions` | 1 / 1 | `CASocialReactionWorldComponent` | persisted reactions by fact/pawn/population/organization |
| `world.transaction-ledger` | 1 / 1 | `CATransactionLedger` | parties, transactions, credit terms and debts |
| `map.home-space-program` | 1 / 1 | `PlannedUseMapComponent` | player space programs, resident roster intent and authoring state |
| `map.home-prerequisite` | 1 / 1 | `CAHomePrerequisiteMapComponent` | exact retained material demand and owned source IDs |
| `map.mission-triage` | 1 / 1 | `MissionCasualtyKnowledgeMapComponent` | mission casualty facts |
| `game.offhand` | 1 / 1 | `OffhandComponent` | pawn-to-offhand thing identity |
| `game.operational-access` | 1 / 1 | `OperationalAccessComponent` | policy schema, projections and retry state |
| `map.patrol` | 1 / 1 | `CAPatrolSystemMapComponent` | patrol circuits, assignments and scheduling state |
| `map.raid-response` | 1 / 1 | `RaidResponseMapComponent` | shelter cells and behavior/authority episodes |
| `map.contingency-plan` | 1 / 1 | `CAPlanMapComponent` | named plans, legs and stable plan IDs |
| `map.road-expansion` | 1 / 1 | `CARoadExpansionMapComponent` | road projects and restoration validation state |
| `map.settlement-planning-context` | 1 / 1 | `CASettlementPlanningContextMapComponent` | context snapshot, revision and source signature |
| `map.settlement-work` | 1 / 1 | `CASettlementWorksMapComponent` | research, repair and rebuild work receipts and restore-validation state |
| `map.spatial-initiative` | 1 / 1 | `CASpatialInitiativeMapComponent` | active spatial objective and built storage/room receipts |
| `game.squad` | 1 / 1 | `SquadComponent` | squad membership, roles and orders |
| `map.squad-support` | 1 / 1 | `SquadSupportMapComponent` | owned support taskings |
| `map.storage-program` | 1 / 1 | `CAStorageProgramMapComponent` | storage projections, authority and audit state |
| `map.inventory-storage` | 1 / 1 | inventory-storage subordinate state in `CAStorageProgramMapComponent` | inventory projections, vetoed objectives and placement policy |
| `game.sustenance` | 1 / 1 | `SustenanceComponent` | body-weight, fullness and original-body facts |
| `map.task-force` | 1 / 1 | `CATaskForceMapComponent` | regional task-force identity and composition |
| `map.toxic-waste` | 1 / 1 | `CAToxicWasteLifecycleMapComponent` | inventory, staging, capacity, freezing, source, relocation, transport, destination, consequence and native-response records |
| `map.trap-memory` | 1 / 1 | `TrapMemoryMapComponent` | sprung trap cells and observed ticks |
| `map.welfare-support` | 1 / 1 | `CAWelfareThresholdSupportMapComponent` | owned support requests and lifecycle evidence |
| `map.welfare-knowledge` | 1 / 1 | welfare subordinate state in `KnowledgeMapComponent` | expected-person facts and accountability records |
| `map.withdrawal` | 1 / 1 | `WithdrawalMapComponent` | withdrawal plans and native job continuity |
| `map.immediate-combat-recovery` | 1 / 1 | `CACombatRecoveryMapComponent` | combat recovery episodes and interruption evidence |
| `map.drafted-combat-initiative` | 1 / 1 | `CADraftedCombatInitiativeMapComponent` | actor-local defensive initiative and projectile facts |
| `map.tactical-overlay` | 1 / 1 | `CAOverlayMapComponent` | authored objective and line cells |
| `pawn.political-belief-memory` | 1 / 1 | `Thought_CAPoliticalBelief` in native pawn memory | political-belief identity and originating event identity for every persisted memory occurrence |
| `pawn.hygiene-needs` | 1 / 1 | `CANeed_Thirst`, `CANeed_Hygiene`, and `CANeed_Bladder` in native pawn needs | need level, last state and body-owner continuity |
| `thing.water-state` | 1 / 1 | `CompWell`, `CompRainCatch`, and `CompWaterVessel` in native thing comps | groundwater, catchment, stored water and source continuity |
| `scenario.established-player-settlement` | 1 / 1 | `ScenPart_CAEstablishedPlayerSettlement` in the native scenario | founding scenario identity and established-settlement selection |
| `embedded.camping-state` | 1 / 1 | embedded Camping Stuff game, thing, spec, sketch and job owners | tent layout usage, packed/spawned parts, damage, roof sketches and in-flight camping jobs |
| `native.tactical-lord` | 3 / 3 | `TacticalLord` | native Lord order schema and job continuity |
| `native.stack-lord` | 1 / 1 | `StackLord` | native stack lifecycle state |
| `native.regional-settlement-lord` | 1 / 1 | regional settlement Lord/jobs | native settlement duty continuity |
| `native.ca-job-drivers` | 1 / 1 | individual CA JobDrivers other than the separately classified assault-search driver | native job targets, phase and completion continuity |
| `world.regional-reservation` | 1 / 1 | `CARegionalPlan` under `CARegionalWorldComponent` | member/reserved tile identities are authoritative; subordinate reservation WorldObjects are reconciled exactly on load and never become a second owner |

## Nested semantic model contracts

| Schema key | Current / minimum | Owner | Identity and validation |
|---|---:|---|---|
| `model.culture` | 10 / 9 | faction, settlement or local Culture owner | stable Culture ID/locality, constituents, inherited/local question distributions, legacy evidence, concrete repeated practices, direct-question observations with measured position/spread/population/continuity/source identity, and transition history; a subject key is neither a question nor a practice identity |
| `model.political-beliefs` | 9 / 9 | faction/founding owner | independently composable normative mechanisms and derivation receipts; beliefs do not rewrite current order |
| `model.current-order` | 1 / 1 | faction/founding/region owner | independently composable instituted authority, labour, voice, property and related mechanisms; self-identification and normative belief remain separate |
| `model.founding-arrangement` | 1 / 1 | player founding owner | adopted founding order and agreement/tension with professed beliefs |
| `model.player-founding-plan` | 3 / 3 | player founding owner | plan identity, Culture, Ideoligion reference, beliefs and arrangement |
| `model.regional-plan` | 11 / 11 | regional world owner | region ID, candidate/arrival/member tiles, faction/settlement/relation plans and authored fixture state |
| `model.regional-settlement-record` | 8 / 8 | regional world owner | stable `regionalId + slot`, faction, local rect, population, program, provision, capability and history-bearing fields |
| `model.settlement-population-group` | 1 / 1 | settlement composition owner | stable group key, kind, share, faction and Ideoligion relationships |
| `model.domestic-unit` | 1 / 1 | domestic state owner | settlement-scoped stable unit identity, continuity pawn, memberships and transitions |
| `model.domestic-provision-demand` | 1 / 1 | domestic state owner | exact unit/operator need and source evidence |
| `model.frontier-map-plan` | 1 / 1 | organization owner | stable map/tile/form holding plan; derived realization signature validates inputs |
| `model.groundwater-tuning` | 1 / 1 | regional world owner | fixed world-generation tuning saved with the world |
| `model.settlement-residence` | 1 / 1 | settlement residence owner | stable pawn assignment, population group, entry and typed exit |
| `model.settlement-capability` | 2 / 2 | settlement capability owner | domain, level, evidence source/signature and assessment tick |
| `model.settlement-program` | 4 / 4 | settlement program owner | program collection and registry contract |
| `model.settlement-program-entry` | 4 / 4 | settlement program owner | stable causal signature, operator/labour/standing/material/access/maintenance facts and runtime status |
| `model.settlement-operational-fact` | 3 / 3 | settlement program operational owner | exact represented need, operator, labour, knowledge, funding, stock and policy facts |
| `model.settlement-program-asset` | 1 / 1 | settlement program asset owner | `programAssets` is the only persisted placed-asset authority: exact thing/zone ID, cell, role, program signature and provision binding; entry ID lists are derived compatibility views only |
| `model.settlement-provision` | 5 / 5 | provision arrangement owner | exact operator identity, material nodes, funding, access and operating state |
| `model.starting-stock` | 1 / 1 | regional settlement/material owner | exact thing/provision-node identity and quantity |
| `model.organization-relation` | 1 / 1 | organization relations owner | typed party keys, formation/change/dissolution and terms |
| `model.organization-holding` | 1 / 1 | organization relations owner | stable facility holding and responsible organization |
| `model.settlement-layout` | 1 / 1 | settlement layout owner | stable graph nodes, gates and source revision |
| `model.settlement-research-work` | 1 / 1 | settlement works owner | settlement/program/operator/job/bench and authority receipt |
| `model.settlement-repair-work` | 1 / 1 | settlement works owner | settlement/program/operator/job/thing and authority receipt |
| `model.settlement-rebuild-work` | 1 / 1 | settlement works owner | program asset identity, old thing, blueprint, location and authority receipt |

The nested types inside each row share that row's owner, lifecycle and migration.
For example, offices, memberships, customs, security practices, decisions,
claims, policies, agreements and breach cases are children of
`world.organization`; they are not independent secondary ledgers.

## Pending authoring state

The following surfaces exist before a world is confirmed and are explicitly
outside realized campaign history:

| Surface | Owner / version | Reset or conversion rule |
|---|---|---|
| Active and mirror Starting Region plan XML | `CARegionalPlan` schema 11 plus `CAPendingAuthoringDataEpoch` 12 | May be rejected/discarded when its pending schema is incompatible; on confirmation it is converted into `world.regional` facts once |
| Culture profiles and political-belief sets | `CAUserCultureProfile` wrapper 9 containing `CACulture` 10, and `CAUserPoliticalBeliefSet` 9 | Invalid entries may be pruned; applying one copies Culture distributions or an explicit partial normative patch into an authored/realized owner |
| Unconfirmed player founding draft | `CAPlayerFoundingPlan` 3 in session state | May be replaced until confirmation; confirmed state belongs to `world.player-founding` |
| Creation preview/projection caches | creation-page/session owners | Recomputed freely; never treated as campaign history |

`CAPendingAuthoringDataEpoch` is named and scoped accordingly. Its current
pending-authoring value is `12`. Live owners read legacy epoch `10` only to
recognize the controlled B10 envelope; they no longer write the epoch and never
use it to erase live state. B12 Culture migration converts schema 9 meaning
records only through exact ordered-question adapters. It retains every former
dimension and unmatched subject as `CACultureLegacyEvidence`; it never invents
a question. The prior B10-to-B11 practice conversion remains the compatibility
floor: a subject-shaped practice survives only when represented longitudinal
evidence identifies the corresponding concrete repeated conduct.
Ownership and economy `mixed` records expand only where the B10 description
named every mechanism. The ambiguous B10 `mixed support` record fails streaming
preflight before owner load; it is not silently dropped or assigned an invented
pair. The governed pending fixture already stores explicit current mechanisms.

## Initialization and migration paths

| Situation | Path | Provenance / mutation rule |
|---|---|---|
| New B13 campaign | owners form state through ordinary creation/materialization; compatibility component writes boundary 1 and catalog 3 | `new campaign initialization`; no invented prior history |
| B10 controlled state | streaming preflight validates uniform epoch 10 before Scribe load; current fields are read unchanged; seven former blanket-reset owners adopt owner version 1 | one idempotent B10-to-B11 receipt per owner plus boundary/catalog receipts |
| B11 catalog-1 save | preflight validates every catalog-1 owner; upgraded Culture and catalog-2 owners initialize from represented upgrade-time facts; owner-specific validation precedes catalog-2 publication | exact B11-to-B12 receipts; no backdated psychology, politics, coalition, institution or proposition history |
| B12 catalog-2 save with cultural-cognition schema 1 | preflight rejects the incompatible owner version before load mutation | no synthetic conversion into B13's separated causal fields; source save remains unchanged |
| Current B13 save | preflight validates boundary, catalog 3, Culture registry 2, every manifest entry and each owner payload before load | no migration; current schema validation only |
| New additive subsystem | owner initializes from facts represented at the upgrade tick after preflight | receipt explicitly says initialized at upgrade; no backdated event/history |
| Supported future model revision | owner-specific pure/idempotent migration, registered compatible range, validation, then receipt | stable semantic IDs preserved; changed algorithms apply to future formation/transitions |
| Unsupported state | preflight blocks before load mutation and save guard refuses writes | operator-visible reason; source save remains unchanged |

## Validation and destructive policy

Preflight is streaming and read-only. It checks campaign boundary, catalog,
legacy epoch consistency, manifest uniqueness/completeness, known keys and
version ranges, required owner cardinality, and agreement between inline owner
versions and the manifest. Owner normalization and semantic validation run only
after that decision. Validation is part of the migration transaction: no owner
version or receipt is committed for invalid state. A successful migration
records a receipt once; rerunning the same from/to operation is a no-op.

There is no destructive live-state migration in B13. Any future destructive
exception requires an exact incompatibility finding, affected keys, a backup,
governance record, operator-visible warning/authorization, and a rollback pair
of the prior DLL and prior save. Until then the correct result is a visible
load failure, not partial loading or regenerated history.
