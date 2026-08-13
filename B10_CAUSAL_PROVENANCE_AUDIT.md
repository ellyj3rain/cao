# B10 Causal-Provenance Audit

| Field | Evidence |
|---|---|
| Batch | `B10` - Causal Closure and Elimination of Synthetic Social State |
| Baseline | `f372c6b50294f631a370d111b1641536bade7876` |
| Source closure | `3d93e4405ddbcdd5e842f9dd0245de8fe157b974` |
| Audit time | 2026-08-13 01:29 UTC / 2026-08-12 18:29 PDT |
| Scope | Active source, persistence, generation, authoring, fixture tooling, and runtime consumers |
| Result | The repeated repository-wide sweep has no unresolved Critical or High synthetic-social-state finding. Optional breadth remains absent rather than represented by a proxy. |
| Executable evidence | [`B10_ACCEPTANCE_RECEIPTS.md`](B10_ACCEPTANCE_RECEIPTS.md), 75/75; [`B10_SYNTHETIC_STATE_SWEEP.md`](B10_SYNTHETIC_STATE_SWEEP.md), 152 classified occurrences and 0 unresolved Critical/High |

## Ownership ledger

The `Facts` column states what makes the concept exist. A read model may explain
or compare those facts; it cannot be their sole cause.

| Concept | Owner and persistence | Constitutive facts | Formation and mutation | Consumers and read models | B10 proxy result | Receipts |
|---|---|---|---|---|---|---|
| Faction | `CAFactionRecord` in the regional plan and realized faction component | Authored or native faction identity, definition, members, territory, and relations | Authored at world setup or retained from native faction generation; changed by represented faction events | Settlements, population, relations, authority, world projection | Random local-faction creation and random relations removed | 52, 55, 58 |
| Settlement | `CARegionalSettlementRecord` and world object | Stable settlement key, faction relation, area, population, placed assets, and authored initial facts | Authored or generated from an eligible site; changed by migration, work, construction, damage, policy, and history | Programs, capabilities, provisions, regional projection | Tendency values no longer establish settlement institutions | 21-35, 52, 58 |
| Population group | `CARegionalPopulationGroupRecord` under its settlement | Saved share, affiliation, source, Ideoligion, beliefs, Culture, and separate-community status | Authored composition; materialization binds actual residents; later migration and demographic events mutate it | Pawn assignment, Culture, beliefs, provision demand | No default group or hashed relationship is synthesized | 38, 52, 58 |
| Kinship | Native pawn relations, read by `CADomesticUnitFormation` | Actual partner, parent, child, and blood relations | Native relationship events | Domestic formation, social knowledge | Pawn ordering and adjacency are not kinship | 1-4 |
| Domestic unit | `CADomesticUnitWorldComponent` | Persistent unit key, settlement, explicit memberships, residence/provision scope, formation provenance | Actual kin/partner or shared-bed evidence; represented join, exit, split, merger, death, and dissolution transitions | Domestic provision, stores, holdings, access, rebuilding, knowledge | Hash-selected four-pawn household deleted; unresolved residents self-provision individually | 1-10 |
| Domestic membership | `CADomesticUnitMembership` within the unit | Pawn identity plus source kind and source identity | Only a represented formation or transition event | Unit membership and provision access; membership list is the read model | UI, save ordering, tick, and hashes cannot add members | 2-8 |
| Organization | `CAOrganizationWorldComponent` and organization records | Stable organization, members, purpose, structure, and represented activity | Authored established fact or represented founding/participation event | Authority, work, commerce, civic and security capability, programs | Labels and settlement fallback keys do not create organizations | 15-17, 24, 44-47 |
| Office | Organization/public-support records | Office definition, jurisdiction, occupied holder, and authority basis | Appointment, election, succession, vacancy, or other represented decision | Civic assessment, behavior authorization, policy execution | Empty title or room does not establish an office | 16, 24, 41 |
| Authority | `CAAuthorityContext` and faction/settlement structure | Actual leader, office, jurisdiction, decision rule, delegation, and accepted order | Authored established order or represented institutional decision | Behavior gate, governance, security, taxation, provision | Political belief and building presence never substitute | 16, 17, 40-47 |
| Status | Pawn/group standing and structure records | Membership, office, rank, ownership, or other represented standing source | Participation and institutional transitions | Voice, access, authority, interpretation | No arbitrary category creates standing | 3, 38, 52 |
| Policy | Settlement/faction policy records | Issuing authority, jurisdiction, adopted rule, and decision provenance | Actual decision; amendment or repeal is another represented transition | Tax, access, work, provision, behavior | Tax UI no longer rolls an approval result or invents policy | 41, 46, 47, 52 |
| Agreement | Institutional/economic relation records | Parties, terms, authority, acceptance, and effective state | Offer and acceptance; represented revision, completion, breach, or termination | Exchange, credit, access, work, provision | Labels and relation scores cannot fabricate an agreement | 15, 44-51 |
| Ownership | Holdings, program assets, and faction/settlement records | Owner identity, owned object/scope, acquisition basis | Authored initial fact or represented transfer, construction, inheritance, seizure, or loss | Access, provision, commerce, repair | Provider fallback does not rewrite ownership | 9, 27, 44, 48 |
| Work organization | Organization and behavior/work records | Workers, assignment, authority/standing, knowledge, inputs, active work | Authored established operation or represented assignment and execution | Capability, program operation, production, maintenance | Organization name alone cannot create work | 13-17, 24-30 |
| Provision arrangement | `CAProvisionArrangement`, schema 5 | Exact operator, eligible population, nodes, access, stock, work, funding, policy where needed | Direct authored or observed facts admit a domestic, communal, or authority arrangement; materialization preflights every node and atomically records exact holdings and stock | Food/stores/access/distribution behavior and inspection | `Support` category switch removed; vendor and religious proxies remain absent | 43-51, 60, 65 |
| Taxation and funding | Policy, authority, organization, transaction/collection records | Legitimate authority, adopted tax policy, taxpayers/base, collection path, expenditure | Actual decision and collection events | Authority provision, maintenance, treasury summaries | Random consensus/majority simulation removed; unresolved authority remains unresolved | 41, 46, 47, 52 |
| Commerce | Transaction ledger plus exchange organization | Exchange actors, goods/services, stock, counterparty, rule, route, and actual transactions | Represented exchange operation and transaction history | Commerce capability, trade program, wealth evidence | Trade tendency and asset presence do not create commerce | 15, 18, 21, 35 |
| Practiced capability | `CASettlementCapabilityEvidence` and persisted assessment, schema 2 | Domain-specific actors, organizations, active operations, knowledge, materials, history, and blockers | Recomputed from direct evidence after relevant activity; evidence signature persists | Read-only inspection and explanation | Shared-basis score, random jitter, and capability-to-operation feedback deleted | 11-20, 61 |
| Technology and knowledge | Faction research plus pawn/organization knowledge | Completed research, actor-held facts, communication, and provenance | Native research and actual observation/communication | Capability, behavior gate, program knowledge requirements | Population, era, Culture, and hashes do not fabricate knowledge | 14, 25, 29, 36 |
| Research | Saved Research program, its exact placed bench, qualified typed resident, and research work receipt | Question/project, operator, worker, knowledge, funding, exact material node, and saved operating contract | A complete program precedes work; native job completion records milestones and history | Capability evidence and knowledge growth summarize completed work | Bench, faction era, or a research job cannot establish its own premise | 14, 25, 27, 29, 35, 63 |
| Culture | `CACulture`, schema 8 under authoring epoch 10 | Constituent meanings, practices, observations, transitions, and provenance by exact subject/population | Inherited authored state; later qualified historical transition from actual response evidence | Demand, legitimacy, participation, prestige, stigma, form and interpretation; summaries remain read models | Culture never supplies authority, labor, material, office, knowledge, or capability | 36-39, 61 |
| Political Beliefs | `CAPoliticalBeliefs`, schema 8 under authoring epoch 10 | Normative answers with source and confidence | Authored, observed, or causally inferred normative state; changes through represented belief evidence | Legitimacy, compliance, dissent, reform pressure, interpretation | Beliefs are no longer copied into current structure or provision | 40-42 |
| Ideoligion integration | Native `Ideo` plus saved exact native identity | Native Ideo identity, memes, precepts, roles, rituals, and membership | Native creation/selection and native ideological change | Pawn belief, Culture overlap, social interpretation | Independent Ideoligion uses exact identity; hash-selected Ideo removed | 39, 52, 58 |
| Social interpretation | Social-meaning and interpretation records | Actor-held fact, exact Culture subject/scope, beliefs, Ideoligion, personal and institutional context | Evaluation of known fact; recorded response may enter later historical aggregation | Pawn response, group pattern, Culture transition | Culture flag is not execution permission | 36-42, 55 |
| Behavior intent | `CAIntentRecord` and typed behavior contract | Actor, known trigger, initiative, permission, authority, capability, material state, native execution lane | Authorized decision creates intent; completion/stand-down/player intervention changes it | Native jobs, duties, Lords, diagnostics | Summaries and social meaning cannot bypass the gate | 36, 52-55 |
| Settlement program | `CASettlementProgram`, schema 4 | Need, exact operator where institutional, standing, knowledge, labor, material inputs/nodes, activity, target, access, funding/maintenance, failure state | Explicit authoring or complete observed operational evidence persists one entry per program and operator; removal of a required fact suspends it | Materialization, runtime work, maintenance, repair, inspection | Threshold-created, asset-created, and same-kind entry collapse removed | 21-35, 59, 63-71, 75 |
| Spatial materialization | Program placement/materialization records | Persisted program, valid functional candidates, map feasibility, placed identity | Select equivalent valid forms only after eligibility; native placement creates nodes | Runtime work, access, maintenance, repair | Asset selection never creates a program or institution | 27, 31, 32 |
| Repair and rebuilding | Program materializer and actual placed-asset records | Damaged/missing placed identity, owning program/operator, materials, labor, access | Damage/destruction creates need; authorized work restores actual nodes | Program continuity and historical evidence | No abstract facility score produces repair | 31, 32 |
| World tendencies | World policy plus realized summaries | Explicit world-generation pressures or read-only values calculated from actual facts | Authored pressure affects its owning generation threshold/placement; realization persists once | Explanation and comparison | No-consumer controls removed; summaries cannot instantiate programs | 18, 21, 22, 55 |
| Fixtures | Active pending plan and keyed mirror, plan schema 10 / authoring epoch 10 | Intentional world, region, candidate, arrival, scale, faction, settlement, and population facts | B10 tool strips obsolete derived state; runtime calls the production realization path | Creation-flow load and acceptance receipts | Parallel fixture-only causal implementation deleted | 33, 56, 58 |
| Save/load | CA-owned world/plan components | Serialized authoritative records and schema epochs | Scribe/XML round-trip; incompatible pre-release derived state is discarded | All runtime consumers | Ordering does not create identity; signatures prove unchanged evidence | 6, 20, 30, 50, 58 |

## Corrected Critical and High findings

Every item below was corrected. The renewed sweep has no unresolved item at
these severities.

| Finding | Severity | Correction |
|---|---|---|
| Pawn-ID ordering and stable hash selected four residents as a household | Critical | Replaced with persistent domestic units formed from represented partner, kin, or shared-bed evidence; otherwise individual self-provision |
| Shared capability basis plus random jitter asserted practiced capability | Critical | Replaced with eight domain-specific evidence assessments and blockers |
| World-tendency thresholds instantiated settlement programs | Critical | Removed tendency ownership; programs consume direct operational evidence |
| `CAProvisionCausalFacts.Support` category selected provision form | Critical | Removed the category and switched to exact domestic, communal, and authority contracts |
| Political Beliefs initialized current social order | Critical | Missing established order remains explicitly incomplete; normative and instituted state stay separate |
| Stable hash selected an independent Ideoligion | Critical | Persist exact native Ideo identity or leave unresolved |
| RNG created local factions and regional relations | Critical | Removed random semantic creation; only authored/native factions and generated persisted relations are consumed |
| Broad controls without independent consumers advertised causal authority | High | Removed the controls and persisted fields |
| Stable hash contributed to specialization existence | High | Specialization derives from explicit operational roles; hashes may only select equivalent form afterward |
| Building and room presence stood in for institution and capability | Critical | Objects are typed material/spatial nodes; operator, activity, authority, labor, knowledge, and maintenance are separate requirements |
| Fixture tools implemented parallel program/provision derivation | High | Deleted obsolete generators and receipt shims; B10 fixture contains intentional facts and enters production realization |
| Tax editing rolled consensus/majority and fabricated adopted policy | Critical | Removed RNG decision; absent real policy/decision leaves tax collection unresolved and authority provision ineligible |
| Active filenames and tooling encoded obsolete batch identities | High | Renamed active source by concept and deleted superseded executable scaffolding |
| Provision material validation required holdings and stock receipts that no producer created | High | Provision materialization now preflights every arrangement, produces exact per-node holding and starting-stock receipts, commits only after all nodes succeed, and rolls every mutation back on failure |
| Multiple same-kind provision arrangements collapsed into one program/operator | High | Program identity is now `(program key, operator identity)` throughout derivation, materialization, persistence, and runtime readback |
| Derived security capability authorized patrol form and engagement, whose history then raised the capability | High | Operative security reads exact placed Defense assets, assigned guard identities, and explicit security policy; capability remains a read-only assessment |
| NPC research work created the research activity later cited as its own demand | Critical | Saved operational Research program, exact placed bench, typed qualified resident, and exact Research demand must all exist before a job starts |
| Encountered hostile factions received synthesized doctrine and a manual hash decided whether to use it | Critical | Encounters resolve an already persisted organization only; advanced assault behavior requires a practiced `ambush` or `line` custom with no hash/adherence gate |
| Road projects selected generic faction pawns and lost their owning commitment on revalidation | High | Road work selects typed settlement residents and revalidates against the saved Transport program through an explicit road-project target |
| The first synthetic-state audit could pass without inspecting materialization, cardinality, capability feedback, or manual doctrine gates | High | The audit now executes source-owner invariants for every corrected path in addition to classifying all 152 active RNG/hash occurrences |
| Population and program UI described unowned spatial effects or pending state as realized | High | Separate-community copy states its actual Ideoligion/cultural effect; the program inspector distinguishes saved contracts, selected asset types, and placed assets and resolves player-facing operator/Thing labels |
| No production authoring surface owned established-program formation | High | The settlement editor now establishes or removes one explicit program fact for an exact population-group operator through the same pure formation kernel used by the governed fixture |
| Generic placed-asset IDs could not prove the exact program, operator, signature, or role later work relied upon | High | Typed program-asset receipts now persist exact identity for every Thing and zone role, and repair/rebuilding atomically rebinds those receipts |
| Provision observations could reuse remote, unreachable, or already-claimed stock | High | Observed stock must be a unique live item on settlement ground reachable by its actual consumers; runtime revalidates the same exact receipt |
| Security work could read a broad armed-pawn fallback instead of produced assignments | High | Live Defense program, exact placed assets, exact operator, typed resident labor, and armed guard assignment now produce persisted security practices; patrols consume only their live assigned guards |

## Program-by-program operational result

`Direct` means the current runtime has a complete direct fact contract.
`Explicit` means the program is admitted only when a saved operational fact
supplies the complete contract. `Absent` is the correct result when that fact is
not present. All material programs require a valid candidate contract after
their semantic eligibility is established.

| Program | Need | Operator/authority | Knowledge/labor | Material/access/funding | Runtime and failure contract | Result |
|---|---|---|---|---|---|---|
| Housing | Actual resident shelter demand | Residents/domestic identities | Construction labor and known valid form | Residential space, materials, access | Materialize homes; missing land/material/labor blocks | Direct |
| Food preparation | Actual resident food demand | Individual/domestic/represented provision operator | Cook/work assignment | Kitchen/hearth, stock, access | Food work and rebuilding; missing operator/stock blocks | Direct when factual |
| Storage | Actual stock requiring protected storage | Saved operator/owner | Hauling and storage work | Storage nodes and access | Stock movement/maintenance; no operational fact means absent | Explicit |
| Medicine | Actual care demand | Medical actors/service operator | Active doctor work and medical knowledge | Treatment space, medicine, access/funding | Care work; removing actor or operation suspends | Explicit |
| Production | Represented production demand | Production organization or workers | Recipe knowledge and active production labor | Work nodes, inputs, logistics | Production work/history; missing input/operator blocks | Explicit |
| Specialized industry | Represented specialized output demand | Exact specialist operation | Specific knowledge and workers | Specialist work nodes/inputs | Production history; no explicit role means absent | Explicit |
| Trade | Represented exchange demand | Exchange organization and counterparties | Commercial operation and labor | Goods/stock, route, transaction rule/funding | Actual transactions; missing organization/history blocks | Explicit |
| Governance | Instituted decision/administration demand | Actual authority and occupied office/organization | Administrative work and records | Meeting/record nodes where required, jurisdiction/funding | Decisions/policy execution; belief or room alone blocks | Direct only from instituted structure |
| Custody | Actual custody/security demand | Authorized custody operator | Assigned wardens/security labor | Secure space and access rule | Custody work; no operator/demand means absent | Explicit |
| Defense | Actual defense demand | Authorized defense organization | Trained assigned actors | Defensive assets/equipment/access | Response/readiness/repair; missing authority or actors blocks | Explicit |
| Research | Active question/project | Supporting organization and assigned researcher | Research knowledge and active contract | Bench or valid non-bench contract, inputs/funding | Research work/milestones; any required fact missing blocks | Explicit |
| Religion | Represented Ideoligion practice demand | Actual religious operator/participants | Practice knowledge and labor | Ritual/social site, inputs/access/funding | Repeated practice; native Ideo alone does not create program | Explicit |
| Gathering | Actual recurring gathering demand | Participants or operating organization | Known practice and coordination work | Place and access | Recurring gathering; label/site alone blocks | Explicit |
| Recreation | Actual resident recreation demand | Residents/eligible operator | Participation | Valid recreation node and access | Native recreation use/repair; missing access/form blocks | Direct when factual |
| Art and memory | Actual expression/memorial demand | Artists, participants, or operator | Relevant skill/practice | Site/work node/material/funding | Production or recurring remembrance; History score alone cannot create | Explicit |
| Agriculture | Actual food/material demand | Farm workers or organization | Growing knowledge and labor | Land, tools, inputs, access | Growing/harvest history; no operational fact means absent | Explicit |
| Animals | Actual animal-use/care demand | Handlers or organization | Handling knowledge and labor | Pens/barns/feed/access | Animal work/care; asset alone blocks | Explicit |
| Communications | Actual coordination demand | Assigned operator/organization | Communication knowledge/labor | Functioning medium, reach, access/funding | Communication history; object alone blocks | Explicit |
| Transport | Actual route/movement demand | Route users or operator | Hauling/transport work | Exact road/river/vehicle/access contract | Movement and maintenance; valid route evidence required | Direct from exact route facts |
| Communal provision | Actual shared provision demand | Population organization with members | Workers and distribution rule | Kitchen, stores, stock, access, shared work/funding | Distribution behavior; any missing fact means absent | Explicit |
| Authority provision | Jurisdictional reserve/distribution demand | Actual authority and distribution organization | Authorized workers | Treasury/tax/requisition, stock, nodes, access | Collection/expenditure/distribution; missing policy or collection blocks | Explicit |
| Domestic provision | Actual individual/unit demand | Exact pawn or domestic-unit identity | Domestic work/self-provision | Provision node, stock/production, access | Domestic preparation/access; unresolved groups never become households | Direct after resident reconciliation |

The governed composition intentionally establishes only the operations supported
by its recovery evidence: four Food preparation, four Storage, four Medicine, two
Production, four Gathering, and one Custody fact. These 19 facts retain exact
population-group operators. The remaining program kinds stay absent until an
operator establishes them or complete observed evidence exists.

## Institution-asset classification

| Native or CA object | Classification | Institution link required |
|---|---|---|
| Bed or bedroom | Material node and residential spatial scope | Actual resident assignment or domestic membership |
| Kitchen/hearth/worktable | Work node | Operator, worker, knowledge, inputs, outputs, access, maintenance |
| Storage object | Storage node | Owner/operator, stock rule, hauling, access, maintenance |
| Research bench | Work node | Research contract, qualified worker, organization, active project, knowledge |
| Medical bed | Material/service node | Medical actors, care work, medicine, access, service organization where applicable |
| Prison bed/secure room | Access and spatial node | Custody authority, operator, wardens, target population, custody work |
| Ritual site | Symbolic and spatial node | Actual participants/operator, practice, access, repeated activity |
| Gathering spot | Spatial node | Actual participants, recurring activity, access, history |
| Communication object | Communication node | Assigned operator/organization where required, functioning reach, access, communication history |
| Road, river, landing link | Access node and spatial constraint | Actual movement/route use and maintenance contract |
| Defensive structure | Material node | Authorized security actors, command structure, readiness, equipment, maintenance |
| Art or memorial object | Symbolic/material node | Artists or participants, expression/remembrance practice, access, history |

## Repeated synthetic-state sweep

The active repository was searched for RNG, range selection, stable hashes,
thresholds, fixed group sizes, count inference, boolean and string switches,
generated owners/providers, fallbacks, default membership, fabricated
relationships, current-tick identity, UI regeneration, read-model feedback,
fixture-only derivation, asset-as-institution, belief-to-order, and
Culture-to-authority paths.

Remaining `Rand`, hash, and threshold uses are confined to causally equivalent
map morphology, visual or material form, native pawn/form generation, bounded
scheduling and staggering, layout, names, receipt signatures, and deterministic
tie-breaking after eligibility. They do not establish semantic social state.
Population assignment materializes a saved authored composition onto actual
pawns; its ordering is a binding operation, not a source of membership,
relationship, or identity. Missing optional capability and program breadth is
reported as absent, blocked, or low-confidence evidence.

## Persistence and fixture

| Surface | B10 state |
|---|---|
| Authoring data epoch | `10` |
| Regional plan | schema `10` |
| Materialized settlement record | schema `8` |
| Domestic units | schema `1` |
| Settlement residence | schema `1` |
| Capability assessment | schema `2` |
| Settlement programs and operational facts | program/entry schema `4`; operational-fact schema `3` |
| Program asset receipts | schema `1` |
| Provision arrangements | schema `5` |
| Culture / Political Beliefs | schema `8` under authoring epoch `10`; valid intentional B8 state preserved, incompatible derived state discarded |
| Active fixture | 3 factions, 4 settlements, 9 population groups; world `alysaliu|1|Algorab Markab`; region `CA-RG-EB596A12`; candidate `613b1fe44104`; arrival `389638`; map scale `350` |
| Fixture identity | Active and keyed mirror are 65,717-byte-identical files at SHA-256 `F539239672875C668F750A09CCC370B4B6AFD8165B0F6FA7D37543AFE8391440` |
| Fixture established operations | 19 explicit facts: 4 Food preparation, 4 Storage, 4 Medicine, 2 Production, 4 Gathering, and 1 Custody; each names its actual population-group operator |
| Fixture derivation | Historical recovery masks are evidence only; the fixture generator translates the supported composition through `CASettlementOperationalFactAuthoringKernel`, while production `RefreshDraftRealization` owns program derivation |

The recovered composition supports the 19 established facts above and no others.
B10 converts those facts into the current schema without restoring obsolete
facility masks or inventing operators, funding, or history. Unsupported programs
remain visibly absent; the registry and unavailable-state inspection explain
what additional fact would be required.

## Module-impact ledger

The ledger treats a module as an ownership boundary. No B10 subordinate module
adds an independent tick loop; existing regional/materialization cadences invoke
bounded services, and event patches act only at their native lifecycle seam.

| Module or group | Responsibility and authoritative state | Persistence owner and mutation lifecycle | Cadence and dependencies | Consumers; tests/receipts | B11 status |
|---|---|---|---|---|---|
| `DomesticUnitModule` + `DomesticUnitStateModule` | Domestic demand, unit, membership, transition, and formation contracts | Lists on `CARegionalSettlementRecord`; formation mutates through normalization and represented relation/residence events | Regional settlement reconciliation; native relations, beds, typed residence | Provision operator/access, composition, knowledge; receipts 1-8 | Review split between DTOs and formation, but ownership is coherent |
| `DomesticResidenceAdapterModule` | Narrow native-pawn and typed-residence adapter | No state; reads settlement residence and domestic membership | Called on demand; no loop | Domestic formation and provision; receipts 3, 4, 9 | Keep subordinate |
| `DomesticProvisionAdapterModule` | Converts authored population into unresolved domestic demand without inventing members | Demand list on settlement plan/record; composition refresh only | Parent-owned composition refresh | Provision authoring/runtime; receipts 5, 9, 10 | Keep subordinate |
| `SettlementResidenceModule` | Persistent residency identity, source, transition, and compatibility readback | `residenceAssignments` on settlement record; assignment, departure, and native faction-change events | Regional reconciliation plus native faction-change patch | Population projection, domestic units, institutional labor, access; receipts 3, 9, 64 | Review patch/state split in B11 |
| `SettlementCapabilityModule` + `SettlementCapabilityEvidenceModule` | Persisted assessment read model and transient domain evidence contract | Assessment list on settlement record; only `Reconcile` replaces it from direct evidence | Existing regional reconciliation; no independent loop | Inspection only; receipts 11, 18-20, 61 | Coordinator remains narrow |
| `SettlementCapabilityAdaptersModule` | Eight domain-specific evidence readers | No state and no mutation | Called by capability coordinator; organization, work, assets, transactions, history | Pure assessment; receipts 12-18 | Consider one file per domain in B11 |
| `SettlementCapabilityAssessmentKernel` | Pure completeness, blocker, confidence, level, and signature assessment | No state; returns a value | Pure call | Persistence and inspection; receipts 11, 19, 20 | Keep pure |
| `SettlementCapabilityInspectionModule` | Read-only capability summaries and detail | No state or mutation | UI/request driven | Settlement inspection; receipt 61 | Keep subordinate |
| `SettlementProgramOperationalModule` | Converts complete direct facts into one program contract per key/operator | `operationalFacts` and `settlementProgram` on plan/record; derivation replaces matching entries only | Draft realization and materialization boundary | Registry, materializer, axes; receipts 21-30, 59, 63 | Keep subordinate |
| `SettlementOperationalFactAuthoringKernel` | Pure formation of one complete established-program fact for an exact operator | No state and no mutation; returns a stable fact spec from explicit inputs | UI and fixture authoring only | Plan authoring and fixture normalization; receipts 66, 67 | Keep pure and subordinate |
| `SettlementProgramAuthoringModule` | Production lifecycle for establishing and removing initial program facts | Mutates `operationalFacts` on the pending plan, regenerates derived programs, and saves the pending plan after an explicit operator action | Settlement editor action only; registry and pure authoring kernel | Creation flow and runtime realization; receipt 68 | Keep subordinate |
| `SettlementProgramRuntimeModule` | Exact fact, operator, labor, typed-resident, and placed-asset gate before materialization or work | Entry schema 4 owns runtime state/failure and validation tick; reconciliation mutates only those validation fields | Existing regional reconciliation and each materialization/work action | Materializer, provision, research, cultivation, repair, rebuilding, roads; receipts 24, 27, 31, 59, 69, 75 | Keep subordinate |
| `SettlementProgramAssetModule` | Typed identity and liveness for every placed Thing or zone role owned by one exact program/operator/signature | `programAssets` on settlement record, receipt schema 1; materialization records, destruction invalidates, and successful rebuild atomically rebinds | Initial materialization, runtime inspection, repair/rebuild completion | Program runtime, provision, security, research, cultivation, repair; receipts 65, 70, 71, 75 | Keep subordinate; review indexing only after measured need |
| `ProvisionOperatorResolverModule` | Resolves saved operator to an organization, domestic unit, or pawn | No state and never creates/falls back | Called by materialization and runtime | Funding, access, material receipts; receipts 44, 48, 59, 65 | Keep subordinate |
| `ProvisionFundingResolverModule` | Validates actual shared work, domestic work, or taxation/resource flow | No state; reads policy, collection, treasury, and work facts | Called per plan/runtime reconciliation | Provision materialization/runtime; receipts 45-47, 60 | Keep subordinate |
| `ProvisionMaterializationAdapterModule` | Semantic preflight and exact facility/stock readback | Holdings remain in organization-relations ledger; stock receipts remain on settlement record; producer commit/rollback stays in materializer | Initial materialization and runtime validation only | Program materializer, access, runtime; receipts 45, 46, 60, 65 | Revisit transaction boundary with materializer in B11 |
| `ProvisionAccessServiceModule` | Resolves eligible consumers from typed residence and membership | No state and never enrolls | Called per runtime reconciliation | Provision runtime; receipts 9, 44-48, 65 | Keep subordinate |
| `ProvisionRuntimeModule` | Ordered operator, funding, material, and access validation | Mutates only arrangement/program operating status and blockers | Existing regional/materialization reconciliation; bounded by arrangement count | Program inspection and runtime consumers; receipts 43-51, 59, 65 | Keep subordinate |
| `SettlementSecurityAssignmentModule` | Produces exact security practice from a live Defense program, operator, placed assets, and armed typed-resident labor | `securityPractices` on the owning organization; reconciliation adds, updates, or removes practices as their constitutive facts change | Existing regional reconciliation; program runtime, residence, equipment, program assets | Patrols and direct security evidence; receipt 74 | Keep subordinate |
| `SettlementSecurityFactsModule` | Read adapter over live exact defense assets and assigned guards | No state or mutation | Called on demand | Protection/diplomacy decisions and capability assessment; receipts 61, 74 | Keep subordinate |
| `B10SyntheticStateAudit` | Repository-wide RNG/hash classification and causal-owner invariants | Generated Markdown receipt only | Explicit verification run | B10 closure; receipts 52, 57, 59-75 | Keep as external verifier |
| `B10AcceptanceReceipts` + `B10FixtureGenerator` | Executable acceptance and intentional-source fixture normalization through the production authoring kernel | Generated reports and two external fixture files; no runtime state owner | Explicit verification run | 75/75 receipts and production-boundary fixture | Keep tools concept-named by current batch only until superseded |

### Existing modules that grew

| Module | B10 impact |
|---|---|
| `RegionalWorldModule` | Persists residence, domestic, capability, program, typed program assets, provision, holding, stock, runtime failure, and security readback and orders existing reconciliation calls. It gained no independent loop. |
| `SettlementProgramMaterializerModule` | Adds exact operator gating, typed per-role asset receipts, provision preflight/atomic receipt commit, repair/rebuild rebinding, and saved-program-led research, cultivation, and maintenance work. |
| `SettlementPlanningContextModule` | Replaces generic institutional demand with typed residents, exact operational programs, represented assets, and demand-specific authorization. |
| `OrganizationModule` | Persists and consumes exact settlement-development proposals and direct security facts; capability summaries no longer authorize action. |
| `RegionalSetupModule` / `RegionalSettlementModelModule` | Advances epoch/schema, discards incompatible derived state, and routes one production realization path. |
| `SettlementProgramModule` | Advances program and entry schema 4, preserves key-plus-operator cardinality, persists runtime blockers, and makes inspection distinguish contracts, selected types, and placed assets. |

### Existing modules that shrank or narrowed

| Module | B10 impact |
|---|---|
| `AxisMaterializationModule` | Remains a coordinator/facade; owns no domestic, capability, provision, funding, or program state. |
| `SettlementCompositionModule` | Provision persistence/query facade remains, while operator, funding, material, access, and runtime ownership moved to subordinate services. |
| `CulturalExpressionModule` | No longer receives fortification or organization capability values as cultural inputs. |
| `PatrolSystemModule` | Uses assigned guards and explicit security policy; capability feedback and provisional support/training doctrine are absent. |
| `HostileFactionOrganizationModule`, `TaskForceModule`, `AssaultApproachModule` | Encounter-time organization/doctrine synthesis and manual adherence hash are absent; only already practiced doctrine can alter assault approach. |
| Concept-named authoring files | Active `B7`/`B8`/`B9` filenames were replaced by `CreationDiagnostics`, `AuthoringDataEpoch`, `CultureInitialState`, `PoliticalContext`, `PoliticalDerivationKernel`, `SocialMeaningKernel`, `SocialInterpretationRuntime`, and `StartingRegionLayoutKernel`; obsolete executable receipt projects were removed. |

### Deferred B11 decomposition candidates

`RegionalWorldModule`, `OrganizationModule`, `SettlementProgramModule`, and
`SettlementProgramMaterializerModule` remain large multi-surface files. B11
should profile and split their already distinct persistence, orchestration, UI,
runtime-work, and Harmony-patch responsibilities where dependency evidence
supports a move. It should also adjudicate the dormant
`AxisMaterializationModule` conflict mutation, per-domain capability adapter
files, and residence state versus faction-change patch. These are structural
hardening candidates, not unresolved B10 causal proxies.

## Closure

The audit corrected every discovered Critical and High proxy. The 75 receipts
cover domestic identity, capability evidence, tendencies and programs, Culture
and political order, provisioning, persistence, UI non-authority, fixture/runtime
equivalence, and the repeated sweep. Aggregate operator runtime validation is the
next action and remains the authority on how the resulting game looks and plays.
