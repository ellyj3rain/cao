# B14 political-authoring replacement proposal

Status: design evidence, superseded where implementation has converged. The
current authority is the complete Political Order composition documented in
`ARCHITECTURE.md`, `AUTHORING_ONTOLOGY_COVERAGE.md`, and `SCHEMA_REGISTRY.md`.
This proposal remains as provenance for the normative-versus-instituted split.

## Outcome

Replace the shared flat axis set with separate owners for:

| Layer | Owner | Stored content | Derives or consumes |
|---|---|---|---|
| Political beliefs | represented population or faction | scoped claims about what ought to be proper | founding suggestions, appraisal, political cognition, belief/practice tension |
| Current institutions | faction, settlement, or organization | offices, bodies, organizations, memberships, and mandates | authority, decisions, agreements, work, provision, security |
| Current rules and policies | adopting institution or organization | eligibility, procedure, obligations, protections, distribution, conflict rules | authorization, compliance, generation, runtime behavior |
| Practice and history | acting organization/population and event ledger | dated acts, decisions, repeated evidenced practices | culture, reputation, belief response, legitimacy |
| Legitimacy and support | population appraisal of a named institution or act | derived evidence and current aggregate | cooperation, compliance, agreements, political change |

Culture and Ideoligion remain separate. Culture supplies inherited social meaning and practice; Ideoligion remains RimWorld's native religious and moral system; political beliefs state what political conduct and order the population considers proper.

## Proposed model

### 1. Political beliefs

CAPoliticalBeliefProfile owns a collection of scoped political claims. Each claim records:

- stable subject and proposition;
- population owner;
- object or affected class;
- scope and conditions;
- strength or salience where represented;
- source and provenance.

The player does not edit this schema directly. Belief profiles and a compact composer project it into clear statements such as:

- A standing council should decide common affairs.
- Every resident should have a voice in settlement decisions.
- Productive land should be held in common.
- Surrendered enemies should be spared.

Claims may genuinely coexist when their scopes differ. A population may also hold tensions. The model records those tensions rather than using unscoped additive entries.

### 2. Institutions and organizations

Current order is an index over represented objects, not a second belief list.

| Object | Required facts | Existing substrate to retain or extend |
|---|---|---|
| Office | holder/eligibility, selection, term, succession, removal, jurisdiction, mandates | CAOffice |
| Decision body | members or selection rule, jurisdiction, agenda owner, procedure, quorum, threshold, tie/veto rule | CAOrganizationGroup plus a new typed body record |
| Organization | kind, members/operators, resources, authority, records | CAOrganization |
| Mandate | grantor, recipient, responsibility, scope, start/end, revocation | delegated responsibilities and relation origin/sunset |
| Membership rule | eligible population, admission authority, conditions, rights, duties, exit | organization membership relations and population facts |
| Property rule | asset class, title holder, use/exclusion/transfer/inheritance rights | CAClaim, relations, organization ownership |
| Work rule | worker class, assigning authority, task scope, consent, term, compensation, exemptions, enforcement | CARelation responsibilities and compensation |
| Provision program | operator, stock/funding, eligibility, service level, rationing, failure behavior | CAProvisionArrangement, facilities, stores, operators |
| Security body | members, command, jurisdiction, powers, duties, equipment, accountability | CASecurityPractice, guard pawn IDs, offices, policies |
| Conflict rule | protected/target class, triggering condition, command, treatment, exception, sanction | political standards, custody/protection relations, CAActRecord |

Rules use typed functions equivalent to position, boundary, authority, aggregation, information, payoff, and scope. Those internal names need not appear in player copy.

### 3. Practice, events, legitimacy, and support

- A policy states what is adopted.
- An operator and mandate identify who can carry it out.
- CAActRecord and decision history state what happened.
- Repeated evidence may establish a practice.
- Pawns and populations appraise the named office, body, policy, act, or organization.
- Legitimacy and support are derived from known procedure, belief fit, treatment, outcomes, identity, and history.

No layer backfills a missing upstream fact. Belief does not create an office; a policy label does not create an operator; an event does not prove a durable practice.

## Creator-specific asset system

The common pipeline is:

creator context → eligible authored assets → instantiated facts → bounded variation → dependency validation → saved result → direct editing

The selection itself is recorded as generation provenance. It is not presented as an inference that a FactionDef, Culture, or material condition proves a population's beliefs.

| Creator | Eligible assets | Result |
|---|---|---|
| Player founding population | belief profiles and founding-arrangement presets | carried Culture, Ideoligion, political beliefs, and only the immediate rules adopted at landing |
| Existing faction | belief profiles, established-order assets, institutional modules, and explicit scenario overrides | a complete faction-level belief profile and represented authority/rule graph appropriate to its authored history |
| Existing settlement | local-order assets constrained by faction mandates, settlement population, technology, geography, programs, and history | local offices, bodies, operators, rules, and delegated responsibilities |
| Population group | existing belief profile references or explicitly authored population beliefs | beliefs only; no current-order ownership |
| Scenario author | explicit assets and owner-scoped overrides | deterministic authored facts that replace the corresponding generated owner |

Bounded variation may replace a council's selection rule, term, franchise, or decision threshold only when the asset declares the permitted alternatives and all linked dependencies remain valid. Generation never rerolls the saved realized state at runtime.

## Existing assets retained

### Belief assets

The twelve current belief-set names remain useful. Their additive entries become scoped claims:

| Existing asset | New use |
|---|---|
| Shared council | representative collective-authority and broad-participation profile |
| Delegated federation | delegated, locally rooted authority profile |
| Central executive | personal executive-authority profile |
| Customary standing | custom- and status-based authority profile, after its compound claims are made explicit |
| Cooperative production | cooperative property and worker-governance profile |
| Common provision | common property and shared-provision profile |
| Private trade | private title, voluntary exchange, and contract-work profile |
| Public service | public obligation and faction-provision profile |
| Open and equal membership | open membership and no fixed legal rank profile |
| Hereditary membership | descent membership and inherited-rank profile |
| Community defense | resident defense and quarter profile |
| Professional security | professional security and combatant-protection profile |

User-saved belief sets remain supported as current-profile authoring. New saves are versioned profiles or explicitly scoped profile fragments. They no longer copy untyped axis entries.

### Current-order assets

The ten current order sets become typed institutional modules:

| Existing asset | Required instantiated result |
|---|---|
| Standing council | council body, membership rule, jurisdiction, majority procedure, quorum/tie rule |
| Single executive | executive office, selection/term/succession, jurisdiction, decree authority, review limits |
| Delegated councils | local bodies, recipient faction body, named delegated responsibilities, revocation and term |
| Public assembly | assembly body, resident boundary rule, jurisdiction, consensus rule |
| Cooperative workplaces | cooperative organization, members, asset claims, work-assignment and surplus rules |
| Common stores | store/program operator, stock source, contribution, access, rationing, and accountability |
| Private market | exchange permission, contract rules, represented traders or market relations, scoped private claims |
| Public distribution | owning organization, allocator mandate, goods/work scope, provision program, obligations and exemptions |
| Community watch | watch organization, roster, command, schedule, powers, jurisdiction, accountability |
| Professional force | guard/defense organization, staff, command, funding, deployment authority, jurisdiction |

An institutional module may be composed into an established order only after its required links resolve. It cannot leave a label standing in for a missing body or operator.

## Player founding surface

Retain the current four-card Founding society hierarchy:

1. Culture
2. Ideoligion
3. Political beliefs
4. Rules at landing

The Political beliefs card shows the selected profile name, a short list of concrete commitments, source, and any intentional tensions. Choose set and Edit open one composer, not thirteen independent menus. The composer groups related claims but edits complete statements with their subject and scope.

The Rules at landing card remains temporally narrow. Its presets continue as:

- Shared survival
- Emergency command
- Ancestral commons
- Single founder

Each preset materializes a dependency-complete initial arrangement:

| Founding fact | Required representation |
|---|---|
| Binding decisions | founders' assembly or a chosen lead, with decision scope |
| Chosen lead | selection among founders, temporary mandate, term/end condition |
| Required work | covered founders, work/defense scope, exemptions, duration |
| Starting supplies | initial title, common stock or separate holdings, rationing operator |
| Term | no fixed end or represented expiry and succession path |

The card reports direct play consequences and belief tension. It does not ask for a parliament, constabulary, bureaucracy, mature property code, or military formation unless a scenario explicitly begins with one.

### Copy register

Use object-and-effect language:

- Political beliefs
- Rules at landing
- A council decides common affairs
- Every resident may vote in settlement decisions
- One founder may issue work and defense orders for 30 days
- Starting supplies are held in common and rationed

Avoid conversational framing, academic category names, and fragments such as Your people / How they are organized.

## Existing-faction and settlement surface

An established faction editor keeps two visibly separate summaries:

- Political beliefs — what its populations consider proper.
- Current order — the offices, bodies, rules, organizations, and practices already present.

Current order is shown as a hierarchy of instantiated objects. A faction card may read:

Faction council
→ five member settlements select delegates
→ majority vote for trade and defense policy
→ local settlements retain work and provision rules

Property, work, provision, membership, security, and conflict appear as linked institution cards beneath the responsible body or organization. Presets instantiate a coherent graph. Direct editing opens the selected object and its dependencies; it does not return to a flat option grid.

A settlement editor shows local instances and the faction mandates that bind them. It may change a local body, operator, or rule only where faction authority and scenario scope permit. Population groups remain belief carriers, not institutional owners.

## Validation contract

1. Every office, body, organization, rule, and policy has an owner and stable key.
2. Every authority names actor, action/domain, scope, and conditions.
3. Every decision procedure names body, eligible participants, quorum, threshold, and tie/veto behavior.
4. Every property, work, provision, membership, security, and conflict rule names its affected object or population and its operator/enforcer where required.
5. References resolve before confirmation or generation.
6. Conflicts are evaluated only at the same owner, body, domain, population/asset class, scope, and time.
7. Unusual but structurally complete arrangements are valid. Missing dependencies and impossible same-scope claims are not.
8. Belief tensions are retained as meaningful state and do not invalidate a complete institution.
9. Starting founders receive only represented landing institutions; mature state develops through play.
10. Every visible control writes one owning fact with a real consumer.
11. Saved realized state survives round-trip and generation consumes it without rerolling.
12. Scenario and starting-region overrides replace defaults only at the owning surface.

## Migration proposal

This is a pre-1.0 convergence migration, not permanent backward-compatibility architecture.

1. Preserve the current schema-9 political records, built-in assets, current authored fixture, and user-profile evidence in a B14 migration receipt.
2. Introduce the reviewed new types and executable validation before changing the UI.
3. Convert each old B entry to an exact scoped normative claim where the words establish one. Compound entries such as custom and standing or majority rule under dissent remain unresolved migration evidence until explicitly split.
4. Convert an old O entry only when asset provenance or associated represented objects supply its required dependencies. For example, an O-SC asset can create a council, member boundary, and majority procedure together; a lone council entry cannot.
5. Convert current founding arrangements directly into typed landing facts; their present fields are sufficiently bounded.
6. Convert the active authored fixture and governed test fixtures with an offline repository tool. Report every unresolved value instead of inventing an owner, scope, office, body, or policy.
7. Replace the built-in templates and update creator-specific assets.
8. Update persistence, generation, summaries, consumers, and receipts together.
9. Remove the flat factionStructure serializer, shared-axis editor, and temporary converter from production once current fixtures and development settings have been converted. Retain only the crosswalk and receipt as project history.
10. Run fixed-seed one-variable receipts, full B10–B14 regression, round-trip tests, clean build, byte-verified deploy, and operator runtime test.

## Operator review gate

Political implementation begins only after operator acceptance or correction of:

| Decision | Proposed answer |
|---|---|
| Belief/current-order split | Beliefs are scoped normative claims; current order is represented institutions, rules, policies, practices, and events. |
| Creator assets | Belief profiles, founding arrangements, established-order assets, and local institutional modules have distinct eligible creators. |
| Player scope | Founders author carried beliefs and immediate landing rules; mature institutions develop through play unless the scenario supplies them. |
| Established-society scope | Factions and settlements instantiate typed bodies, offices, mandates, rules, organizations, and operators. |
| Migration posture | Offline one-time conversion of current fixtures/settings; unresolved evidence is surfaced; no permanent pre-release legacy layer. |
| UI posture | One concise projection with coherent presets and object-level editing; no shared 13-axis grid and no menu per variable. |
