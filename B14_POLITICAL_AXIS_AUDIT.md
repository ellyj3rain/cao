# B14 political-axis audit

Status: historical diagnostic input. The accepted current model is the complete
Political Order composition documented in `ARCHITECTURE.md`,
`AUTHORING_ONTOLOGY_COVERAGE.md`, and `SCHEMA_REGISTRY.md`; references below to
the former flat beliefs/current-order editors describe the defect B14 replaced.

Status: evidence and replacement input for operator review. Political source remains frozen.

## Finding

CAFactionAxes is not one ontology. It is one storage vocabulary used for two different owners:

- B — CAPoliticalBeliefs.positions, owned by a founding population or faction and intended to state what is proper.
- O — CARegionalFactionPlan.factionStructure, later CAFactionState.factionStructure, owned by an established faction and intended to state what is instituted.

Every option below is currently admitted to both owners. The editor permits several options on one axis, except that an option named none excludes other entries on leadership, local order, and defense. Consequently an unscoped set can state single leader plus all members, decree plus consensus, or several incompatible franchises without identifying different bodies, domains, or periods.

No settlement owns an independent current-order record. Settlements inherit factionStructure and separately store a coarse settlement-authority value. Population groups reference a faction's political beliefs; they do not own current order.

## Evidence and notation

Current production paths:

| Code | Current consumer |
|---|---|
| S | Persistence, validation, summaries, shared editor, exact-key belief/order tension |
| C | Generic political cognition, attitude, coalition, and support scoring in CulturalPoliticsStateModule; this does not instantiate an institution |
| F | Founding-arrangement suggestion or comparison in PoliticalBeliefPracticeModule |
| Jb | Belief standard used to judge represented acts in PoliticalBeliefPracticeModule and PoliticalBeliefEffectsModule |
| Jo | Current-order entry projected to an organization custom; only a small subset is projected |
| A | Settlement-authority inference from faction leadership and participation |
| I | Minority Ideoligion projection or suppression |
| G | Aperture selection |
| E | A represented social fact maps to this political option as evidence |

Asset codes:

| Code | Built-in partial asset |
|---|---|
| B-SC / O-SC | Shared council / Standing council |
| B-DF / O-DC | Delegated federation / Delegated councils |
| B-CE / O-SE | Central executive / Single executive |
| B-CS | Customary standing |
| B-CP / O-CW | Cooperative production / Cooperative workplaces |
| B-CM / O-CS | Common provision / Common stores |
| B-PT / O-PM | Private trade / Private market |
| B-PS / O-PD | Public service / Public distribution |
| B-OE | Open and equal membership |
| B-HM | Hereditary membership |
| B-CD / O-W | Community defense / Community watch |
| B-PF / O-PF | Professional security / Professional force |
| O-PA | Public assembly |

Creator notation in every row is P-B (player founders may author the belief), EF-B/O (an existing faction may author either flat record), PG-ref (a population group can reference beliefs), and ES-inherited (a settlement currently inherits faction order). Direct/user means the option has no built-in partial asset but remains available through the editor or a user-saved belief set.

## Complete option classification

### Leadership

| Stable key | Current label — description | Stored | Current consumers | Actual type, dependencies, contradictions, and substrate | Asset / creator | Disposition and migration |
|---|---|---|---|---|---|---|
| leadership.single | single leader — one leader has final authority | B/O | S,C,F,Jb,Jo,A | B: preference for personal final authority. O: incomplete office/mandate claim. Requires an office, holder or selection rule, jurisdiction, term, succession, removal, and domains of final authority. Contradicts whole or none in the same scope; can coexist with a council only if powers are divided. CAOffice supplies holder, seniority, grants, and succession but no jurisdiction or appointment rule. | B-CE/O-SE; P-B, EF-B/O, PG-ref, ES-inherited | Retain a scoped belief proposition. Replace O with typed office plus mandate and selection records. Convert the old O entry only when its associated office and domain are recoverable; otherwise mark current authority incomplete. |
| leadership.council | council — a standing council leads | B/O | S,C,F,A | B: preference for collective authority. O: incomplete collective body claim. Requires membership, selection, term, jurisdiction, agenda control, and decision rules. May share power with an executive; contradicts single only when both claim the same final power. CAOrganizationGroup is only a named pawn set and lacks mandate. | B-SC/O-SC; P-B, EF-B/O, PG-ref, ES-inherited | Retain as belief. Replace O with a typed decision body and mandates. A council preset must instantiate membership and procedure together. |
| leadership.whole | all members — all members share authority | B/O | S,C,F,Jb,Jo,A | B: preference for direct member authority. O: assembly institution plus boundary and aggregation rules, not leadership by itself. Requires the membership set, assembly jurisdiction, quorum, and decision rule. Contradicts unqualified single authority; can delegate bounded powers. | O-PA; direct/user B; P-B, EF-B/O, PG-ref, ES-inherited | Rename the belief to member assembly or direct member rule. Replace O with an assembly body and delegated mandates. |
| leadership.federated | delegated leaders — local leaders grant limited authority upward | B/O | S,C,A | B: preference for subsidiarity or delegated authority. O: relation among settlement bodies, mandates, and revocation rules. Cannot exist without local authorities and a recipient body. Contradicts central final authority only when delegation is neither limited nor revocable. CAFederation and settlement-membership relations already store delegated responsibilities. | B-DF/O-DC; P-B, EF-B/O, PG-ref, ES-inherited | Retain a belief about delegated authority. Migrate O to existing federation or faction-membership relations with explicit delegated responsibilities; remove it as a leader type. |
| leadership.none | no permanent leader — leadership exists only when needed | B/O | S,C,F,Jb,Jo,A | B: preference against a standing executive. O: absence of a standing office plus an ad hoc authorization procedure. It does not identify who may act in emergencies. Contradicts a permanent single leader in one scope, but not temporary or task-specific command. | direct/user; P-B, EF-B/O, PG-ref, ES-inherited | Retain as an absence belief. Replace O with no standing executive plus a specified temporary-decision rule; unresolved authority must remain visibly incomplete. |

### Decisions

| Stable key | Current label — description | Stored | Current consumers | Actual type, dependencies, contradictions, and substrate | Asset / creator | Disposition and migration |
|---|---|---|---|---|---|---|
| decisions.decree | decree — the leader decides | B/O | S,C | B: preference for executive discretion. O: aggregation/authority rule. Requires an authorized office, policy domain, promulgation channel, and review or override conditions. Contradicts majority or consensus only within the same body and domain. | B-CE/O-SE; P-B, EF-B/O, PG-ref, ES-inherited | Retain a scoped belief. Migrate O to a decision rule attached to an office and domain. |
| decisions.majority | majority vote — the eligible vote; the majority binds all | B/O | S,C | B: procedural preference. O: aggregation rule. Requires a body, franchise, quorum, threshold, tie rule, and decision domain. Can coexist with consensus in different bodies or decision classes. | B-SC/O-SC; P-B, EF-B/O, PG-ref, ES-inherited | Retain as a belief about procedure. Replace O with a typed aggregation rule; never store it without eligibility and body links. |
| decisions.consensus | consensus — the eligible must consent for it to bind | B/O | S,C | B: procedural preference. O: aggregation/veto rule. Requires a defined eligible set, abstention rule, blocking actors, and scope. Unscoped coexistence with majority is incoherent; scoped coexistence is ordinary. | B-DF/O-DC/O-PA; P-B, EF-B/O, PG-ref, ES-inherited | Retain as belief. Replace O with a body-linked aggregation rule and explicit veto/consent threshold. |
| decisions.custom | custom and standing — custom and personal standing decide | B/O | S,C | Incoherent mixture of rule source, informal practice, and unequal decision weight. It names neither the custom nor the positions that receive weight. CAOrganizationCustom and group standing are available but do not supply a decision procedure. | B-CS; direct/user O; P-B, EF-B/O, PG-ref, ES-inherited | Decompose into named custom, position eligibility, decision weight, and adjudication practice. Remove this compound option after one-time fixture conversion. |

### Participation

| Stable key | Current label — description | Stored | Current consumers | Actual type, dependencies, contradictions, and substrate | Asset / creator | Disposition and migration |
|---|---|---|---|---|---|---|
| participation.universal | all residents — all residents take part | B/O | S,C,F,Jb,Jo,A,E | B: inclusive-franchise preference. O: boundary rule for a particular body or decision. Requires resident definition, body, domain, and vote/voice power. Contradicts other franchises only for the same body. | B-SC/O-PA; P-B, EF-B/O, PG-ref, ES-inherited | Retain as belief. Replace O with a body-linked eligibility rule. PublicVoice evidence may support the belief but cannot instantiate the franchise. |
| participation.members | members only — enrolled members take part; others do not | B/O | S,C,F,A | B: membership-bound franchise preference. O: boundary rule requiring a membership registry and decision body. Can coexist with resident participation in another body. | B-DF/O-SC; P-B, EF-B/O, PG-ref, ES-inherited | Retain as belief. Migrate O to explicit membership eligibility on each body. |
| participation.standing | earned standing — those with earned standing take part | B/O | S,C,F,A | B: merit or standing-based franchise preference. O: boundary and payoff rule. Requires the qualifying deeds, assessor, threshold, loss conditions, and body. Group standing alone is not an eligibility rule. | direct/user; P-B, EF-B/O, PG-ref, ES-inherited | Retain as belief only when qualification is specified. Replace O with a standing criterion linked to a body. |
| participation.heads | household heads — one voice per household | B/O | S,C,F,A | B: household-representation preference. O: position, boundary, and aggregation rules. Requires represented households, selection of a head, replacement, and voting body. Domestic-unit records can supply households but not political representation. | B-CS; direct/user O; P-B, EF-B/O, PG-ref, ES-inherited | Retain as belief. Replace O with household delegate positions and a body-linked rule. |

### Dissent

| Stable key | Current label — description | Stored | Current consumers | Actual type, dependencies, contradictions, and substrate | Asset / creator | Disposition and migration |
|---|---|---|---|---|---|---|
| dissent.plural | protected — open dissent is protected | B/O | S,C,I,G | Mixture of a rights belief and unspecified protection policy. It does not say whether speech, petition, assembly, association, refusal, or exit is protected, by whom, against what sanction, or with what remedy. Current O suppresses no minority Ideoligion and also weakly changes glazing. | B-SC; direct/user O; P-B, EF-B/O, PG-ref, ES-inherited | Replace with specific protected-conduct beliefs and instituted protection/remedy rules. Remove aperture use. Convert the old value to an unresolved broad protection claim, not invented detailed rights. |
| dissent.majoritarian | majority rule — the majority decides; minorities are tolerated | B/O | S,C,I | Incoherent mixture of aggregation procedure and an undefined tolerance practice. Majority rule belongs under decisions; tolerance requires named protected conduct and enforcement. Current Ideoligion projection treats it differently without an explicit rule. | direct/user; P-B, EF-B/O, PG-ref, ES-inherited | Split majority procedure from minority protections. Remove this dissent option; preserve the old phrase as migration evidence when no exact split can be recovered. |
| dissent.orthodoxy | one doctrine — open dissent is suppressed | B/O | S,C,I | Mixture of normative intolerance, doctrine policy, prohibited conduct, and enforcement practice. Requires the doctrine, covered conduct, sanction, enforcing body, jurisdiction, and exemptions. It currently suppresses unprotected minority Ideoligion. | direct/user; P-B, EF-B/O, PG-ref, ES-inherited | Replace with doctrine and explicit prohibition/sanction rules. Convert only the demonstrated Ideoligion restriction; leave other conduct unset. |
| dissent.customary | custom and rank — treatment depends on custom and status | B/O | S,C,I | Undefined rule source plus status-contingent enforcement. Requires named custom, statuses, conduct, decision maker, sanction, and remedy. Current projection protects only marked groups. | B-CS; direct/user O; P-B, EF-B/O, PG-ref, ES-inherited | Decompose into named custom, status category, and enforcement rule. Do not preserve it as a catch-all option. |

### Ownership

| Stable key | Current label — description | Stored | Current consumers | Actual type, dependencies, contradictions, and substrate | Asset / creator | Disposition and migration |
|---|---|---|---|---|---|---|
| ownership.private | private owners — individuals own farms and workshops | B/O | S,C,F,Jb,Jo | B: private-title preference. O: property-rights regime. Requires asset class, title holder, use/exclusion/transfer rights, inheritance, and taking rules. It may coexist with other regimes across asset classes, not as an unscoped contradiction. Claims and relations provide partial title/obligation substrate. | B-PT/O-PM; P-B, EF-B/O, PG-ref, ES-inherited | Retain as scoped belief. Replace O with asset-class property rules and represented claims. |
| ownership.cooperative | cooperatives — workers and communities own them together | B/O | S,C,F,Jb,Jo | B: cooperative-title preference. O: organization, membership, governance, surplus, exit, and asset rights. Current option creates only a generic property-in-common custom. | B-CP/O-CW; P-B, EF-B/O, PG-ref, ES-inherited | Retain belief. Replace O with a cooperative organization plus asset claims and member decision rules. |
| ownership.common | shared ownership — productive property is held in common | B/O | S,C,F,Jb,Jo | B: common-title preference. O: common-property regime requiring the owning community, access, exclusion, allocation, stewardship, and transfer constraints. It can coexist with private personal goods. | B-CM/O-CS; P-B, EF-B/O, PG-ref, ES-inherited | Retain belief. Replace O with scoped common title and access/allocation rules. |
| ownership.state | faction ownership — the faction owns farms and workshops | B/O | S,C,F | B: public/faction-title preference. O: property regime requiring the owning organization, delegated operator, use rights, allocation, and disposal rules. The current organization custom bridge ignores it. | O-PD; direct/user B; P-B, EF-B/O, PG-ref, ES-inherited | Retain a scoped belief. Replace O with organization-owned claims and operator mandates. |

### Economy

| Stable key | Current label — description | Stored | Current consumers | Actual type, dependencies, contradictions, and substrate | Asset / creator | Disposition and migration |
|---|---|---|---|---|---|---|
| economy.market | trade — prices and bargains distribute goods | B/O | S,C | Mixture of exchange permission, price formation, contract, and distribution. Markets can coexist with rationing, public provision, or common property. Agreements and trade relations are partial substrate. | B-PT/O-PM; P-B, EF-B/O, PG-ref, ES-inherited | Remove economy as one axis. Retain beliefs about voluntary exchange and contract; migrate O to exchange rules and actual markets/trade relations. |
| economy.planned | planned distribution — leaders allocate goods and work | B/O | S,C | Mixture of authority, allocation procedure, labor assignment, and provision policy. Requires allocator office, goods/work scope, information, priority, appeal, and enforcement. | O-PD; direct/user B; P-B, EF-B/O, PG-ref, ES-inherited | Decompose into allocation mandates, work rules, and provision programs. Preserve old O only as unresolved plan evidence if owners and domains are absent. |
| economy.communal | shared stores — goods are pooled and shared | B/O | S,C | Mixture of a storage institution, contribution rule, access rule, and distribution practice. CA provisions, stores, facilities, and operator assignments are the correct substrate. | B-CM/O-CS; P-B, EF-B/O, PG-ref, ES-inherited | Move belief content to common provision and contribution standards. Replace O with represented stores, operators, stocks, access, and rationing rules. |

### Work

| Stable key | Current label — description | Stored | Current consumers | Actual type, dependencies, contradictions, and substrate | Asset / creator | Disposition and migration |
|---|---|---|---|---|---|---|
| work.contract | hired work — workers choose paid jobs | B/O | S,C,F,Jb,Jo | B: preference for voluntary compensated labor. O: employment relation requiring employer, worker, task, term, compensation, consent, exit, and breach rules. It can coexist with household or public duties for other work. | B-PT/O-PM; P-B, EF-B/O, PG-ref, ES-inherited | Retain belief. Replace O with represented work relations and compensation terms. |
| work.organized | organized workers — worker groups organize jobs | B/O | S,C,F,Jb,Jo | B: worker self-organization preference. O: worker organization plus authority over assignment, membership, and bargaining. Current projection treats it merely as voluntary work. | B-CP/O-CW; P-B, EF-B/O, PG-ref, ES-inherited | Retain belief. Replace O with worker group, mandate, and assignment/bargaining rules. |
| work.duty | required service — members owe work to the faction | B/O | S,C,F,Jb,Jo,E | B: service-obligation preference. O: obligation rule requiring subject population, work domains, duration, exemptions, compensation, authority, and enforcement. CompelledService is evidence, not the rule itself. | B-PS/O-PD; P-B, EF-B/O, PG-ref, ES-inherited | Retain a scoped belief. Replace O with explicit obligations and enforcement; connect actual assignments through relations. |
| work.household | household work — families and households assign work | B/O | S,C,F | B: household-production preference. O: household authority and work practice. Requires domestic unit, allocator, member scope, property relation, and exit. | direct/user; P-B, EF-B/O, PG-ref, ES-inherited | Retain belief. Replace O with domestic-unit work relations; do not map it automatically to voluntary labor. |

### Support

| Stable key | Current label — description | Stored | Current consumers | Actual type, dependencies, contradictions, and substrate | Asset / creator | Disposition and migration |
|---|---|---|---|---|---|---|
| support.private | self-provided — each household provides for itself | B/O | S,C | B: household-responsibility preference. O: allocation/access rule requiring household boundaries, resource ownership, emergency fallback, and dependants. | B-PT; direct/user O; P-B, EF-B/O, PG-ref, ES-inherited | Retain belief. Replace O with household provision responsibilities and actual stocks/access. |
| support.public | faction support — the faction supplies basic needs | B/O | S,C,E | B: public-obligation preference. O: provision policy and program requiring operator, funding, stocks, eligibility, service level, and failure behavior. SharedProvision evidence does not instantiate the program. | B-PS/O-PD; P-B, EF-B/O, PG-ref, ES-inherited | Retain belief. Replace O with CA provision arrangements and organization obligations. |
| support.communal | shared stores — common stores supply basic needs | B/O | S,C | B: communal provision preference. O: a concrete store/access institution. It overlaps economy.communal because both point to the same missing object. | B-CM/O-CS; P-B, EF-B/O, PG-ref, ES-inherited | Merge with the shared-store institution; retain the normative duty separately from the material store. |
| support.charitable | charity — religious and voluntary groups provide support | B/O | S,C | B: voluntary/intermediate-group responsibility preference. O: provider organization, eligibility, funding, discretion, and service practice. Ideoligion alone does not instantiate a charity. | direct/user; P-B, EF-B/O, PG-ref, ES-inherited | Retain belief. Replace O with an organization-owned provision program. |

### Membership

| Stable key | Current label — description | Stored | Current consumers | Actual type, dependencies, contradictions, and substrate | Asset / creator | Disposition and migration |
|---|---|---|---|---|---|---|
| membership.open | open — whoever comes and stays may belong | B/O | S,C | B: inclusive-membership preference. O: boundary/admission rule requiring residence threshold, registrar, rights, obligations, and exit. Current faction affiliation is a fact but not an admission rule. | B-OE; direct/user O; P-B, EF-B/O, PG-ref, ES-inherited | Retain belief. Replace O with typed admission and membership records. |
| membership.vetted | approved — joining requires service, sponsorship, or an oath | B/O | S,C | Compound boundary rule with three alternative conditions and no decision owner. Requires explicit condition, reviewer, evidence, decision, appeal, and effect. | direct/user; P-B, EF-B/O, PG-ref, ES-inherited | Decompose into admission requirements; do not preserve the ambiguous or. |
| membership.hereditary | inherited — membership passes through families | B/O | S,C | B: descent-membership preference. O: boundary/succession rule requiring parent relation, birth/adoption conditions, loss, and dual-membership handling. | B-HM; direct/user O; P-B, EF-B/O, PG-ref, ES-inherited | Retain belief. Replace O with descent admission rules tied to represented kinship. |
| membership.closed | closed — outsiders are rarely admitted | B/O | S,C | A rate description, not a rule. It lacks categorical exclusions, approving authority, exceptional paths, and observed admissions. | direct/user; P-B, EF-B/O, PG-ref, ES-inherited | Replace with explicit exclusion and exception rules. Preserve rarity only as a derived historical outcome. |

### Status

| Stable key | Current label — description | Stored | Current consumers | Actual type, dependencies, contradictions, and substrate | Asset / creator | Disposition and migration |
|---|---|---|---|---|---|---|
| status.equal | broadly equal — members have no fixed rank | B/O | S,C | B: preference against fixed legal rank. O: absence of status categories, not absence of reputation or office. Requires the legal privileges being denied. | B-OE; direct/user O; P-B, EF-B/O, PG-ref, ES-inherited | Retain belief. Represent O as absence of fixed status privileges while preserving offices and earned standing. |
| status.earned | earned ranks — standing follows deeds and service | B/O | S,C | B: merit-status preference. O: position/payoff system requiring ranks, qualifying acts, assessor, privileges, promotion, loss, and appeal. Existing group standing is a score, not a rank system. | direct/user; P-B, EF-B/O, PG-ref, ES-inherited | Retain belief. Replace O with typed ranks and advancement rules. |
| status.hereditary | hereditary ranks — standing is inherited and persists | B/O | S,C | B: hereditary-rank preference. O: positions, succession, privileges, and kin rules. CAOffice succession supports offices only, not general rank. | B-CS/B-HM; direct/user O; P-B, EF-B/O, PG-ref, ES-inherited | Retain belief. Replace O with rank categories and inheritance rules linked to kinship. |
| status.castes | castes — each member belongs to a fixed caste | B/O | S,C | B: fixed-category preference. O: membership categories with assignment, endogamy or mobility rules, rights, duties, and sanctions. A bare label cannot instantiate caste. | direct/user; P-B, EF-B/O, PG-ref, ES-inherited | Replace the broad option with explicit status categories and consequences; retain a normative claim only if those categories are named. |

### Local order

| Stable key | Current label — description | Stored | Current consumers | Actual type, dependencies, contradictions, and substrate | Asset / creator | Disposition and migration |
|---|---|---|---|---|---|---|
| localOrder.none | no standing watch — no permanent watch exists | B/O | S,C | B: preference against a standing enforcement body. O: absence fact plus an unanswered incident-response procedure. Contradicts standing watch/guards in the same settlement. | direct/user; P-B, EF-B/O, PG-ref, ES-inherited | Retain an absence belief. Represent O as no standing enforcement organization plus an explicit ad hoc response rule. |
| localOrder.watch | community watch — residents keep watch in turns | B/O | S,C | B: resident enforcement preference. O: organization/practice requiring roster, jurisdiction, command, schedule, powers, equipment, and accountability. CASecurityPractice and guard pawn IDs are partial substrate. | B-CD/O-W; P-B, EF-B/O, PG-ref, ES-inherited | Retain belief. Replace O with a security organization and duty roster. |
| localOrder.constabulary | guards — professional guards keep order | B/O | S,C,E | B: professional local-enforcement preference. O: organization requiring staff, command, jurisdiction, powers, custody rules, funding, and accountability. EnforcedOrder evidence is not a constabulary. | B-PF/O-PF; P-B, EF-B/O, PG-ref, ES-inherited | Retain belief. Replace O with an operator-backed security organization. |
| localOrder.rulers | ruler's guard — order answers to the ruler, not the whole | B/O | S,C,G | Mixture of security organization, command relation, and lack of public accountability. Requires ruler office, guard body, jurisdiction, mandate, and review. Current code also favors firing slits near chief structures. | direct/user; P-B, EF-B/O, PG-ref, ES-inherited | Decompose into guard organization and command mandate. Remove aperture use; built form should follow represented security works and site function. |

### Defense

| Stable key | Current label — description | Stored | Current consumers | Actual type, dependencies, contradictions, and substrate | Asset / creator | Disposition and migration |
|---|---|---|---|---|---|---|
| defense.none | no standing defense — defenders assemble only when needed | B/O | S,C | B: preference against a standing force. O: absence of standing formations plus an ad hoc mobilization rule. It can coexist with emergency defenders, not a permanent force in the same scope. | direct/user; P-B, EF-B/O, PG-ref, ES-inherited | Retain absence belief. Represent O as no standing force plus an explicit emergency call-up rule. |
| defense.levy | general levy — every able member owes war service | B/O | S,C | B: universal-service preference. O: service obligation and mobilization institution requiring eligible population, exemptions, term, command, equipment, and enforcement. | direct/user; P-B, EF-B/O, PG-ref, ES-inherited | Retain belief. Replace O with defense obligation, roster, and command records. |
| defense.militia | trained militia — part-time trained; mustered at need | B/O | S,C | B: militia preference. O: formation, roster, training schedule, command, equipment, mobilization, and jurisdiction. Security assignments are partial substrate. | B-CD/O-W; P-B, EF-B/O, PG-ref, ES-inherited | Retain belief. Replace O with a militia organization and mobilization rules. |
| defense.professional | soldiers — professional soldiers defend the faction | B/O | S,C,G | B: professional-force preference. O: staffed formation requiring command, funding, service relation, deployment authority, and jurisdiction. Current code infers militarized apertures from the label. | B-PF/O-PF; P-B, EF-B/O, PG-ref, ES-inherited | Retain belief. Replace O with a professional defense organization. Remove direct aperture inference; consume realized fortifications and assignments. |
| defense.caste | warrior caste — war belongs to a hereditary warrior caste | B/O | S,C,G | Mixture of status system, inherited membership, defense obligation, and force organization. Requires caste definition, descent, privileges, command, service, and equipment. | direct/user; P-B, EF-B/O, PG-ref, ES-inherited | Decompose into hereditary status plus defense organization and obligation. Do not preserve as one option. |

### Treatment in war

| Stable key | Current label — description | Stored | Current consumers | Actual type, dependencies, contradictions, and substrate | Asset / creator | Disposition and migration |
|---|---|---|---|---|---|---|
| warConduct.quarter | surrender accepted — defeated enemies are spared | B/O | S,C,Jb,E | B: conduct norm. O: policy/rule requiring surrender condition, protected persons, custodian, treatment, exceptions, and sanction. QuarterGiven is represented evidence; actual violence remains separate history. It can coexist with combatant protection. | B-CD; direct/user O; P-B, EF-B/O, PG-ref, ES-inherited | Retain the belief as a specific norm. Replace O with a conflict rule and keep events/practice separate. |
| warConduct.strength | victors decide — defeat offers no protection | B/O | S,C,Jb | Mixture of permissive belief and absence of protection. O cannot be proven by a single affirmative institution; it needs explicit permissions, missing protections, and observed conduct. | direct/user; P-B, EF-B/O, PG-ref, ES-inherited | Replace with specific permitted/prohibited conduct. Preserve the old belief only as a broad norm; do not turn it into an instituted fact automatically. |
| warConduct.combatants | combatants only — only those who bear arms may be struck | B/O | S,C,Jb | B: target-discrimination norm. O: conflict rule requiring combatant definition, status changes, command, exceptions, and sanction. Current effect code correctly distinguishes downed/unresisting evidence from merely unarmed. It can coexist with quarter. | B-PF; direct/user O; P-B, EF-B/O, PG-ref, ES-inherited | Retain a precise belief norm. Replace O with an explicit target/protection rule and separate event history. |

## Asset and use-path inventory

| Surface or asset | Current behavior | Audit result |
|---|---|---|
| CAFactionAxes | One 13-axis, 52-option vocabulary for B and O | Primary ontology defect. Normative propositions, bodies, procedures, policies, organizations, practices, and absences share one unscoped set. |
| Dialog_CAAxisEditor | Same questions and choices on both sides; additive mechanisms; exact-key tension | Useful comparison frame, wrong semantic unit. It cannot express dependencies, scope, body ownership, or valid multi-institution coexistence. |
| Twelve built-in belief sets | Partial additive patches; listed options are added and all others preserved | Useful thematic seeds. They should become coherent normative profiles with explicit coverage and allowed internal tension, not arbitrary additive bundles. |
| Ten built-in current-order sets | Partial additive patches over the same options | Useful names and intent, but must instantiate typed offices, bodies, rules, organizations, claims, programs, and mandates. |
| User-saved belief sets | Copies a partial list of axis entries | Preserve user authoring capability. New saves should store a complete versioned normative profile or an explicitly scoped patch, not an untyped bag. |
| Player founding arrangements | Four presets over leaderRule, workRequired, foundersDecide, sharedSupplies, and duration | Correct temporal surface: these are immediate rules at landing. They need typed founding terms and dependency validation, not mature faction institutions. |
| Political derivation kernel | Selects only from discriminating evidence; ties and absent evidence remain unset | Sound causal posture. Both CAPoliticalContext entry points currently return empty evidence, so it does not generate creator-specific political state. |
| Faction/settlement creator | Directly edits beliefs and the flat current-order set | Existing factions need creator-specific institutional assets based on represented history, population, technology, settlement topology, and scenario facts. Settlements need local instances, not another faction-level axis set. |
| Population-group creator | References faction political beliefs independently of affiliation and Ideoligion | Correct ownership boundary. Population groups may carry beliefs; they do not author institutional order. |
| Organization runtime | Offices, groups, customs, security practices, claims, policies, decisions, agreements, faction/settlement membership, and delegated responsibilities | Strong reusable substrate, but current offices lack jurisdiction/selection/removal and generic customs cannot stand in for institutions. |
| Belief/practice bridge | Four founding comparisons; a subset of B becomes act standards; a subset of O becomes organization customs | Demonstrates meaningful effects but exposes sparse coverage. Unmapped options currently advertise more causal reach than they have. |
| Settlement authority | Derived from unscoped leadership plus participation | Useful distinct owner, but inference from contradictory flat entries is not reliable. It should derive from represented faction/settlement mandates. |
| Ideoligion projection | Dissent option controls protection/suppression of minority Ideoligions | The effect belongs to explicit doctrine/protection rules, not a generic dissent label. |
| Aperture generator | plural dissent, ruler guard, professional force, and warrior caste influence opening types | Synthetic social state. Remove these inputs; construction should consume actual security works, facility use, technology, climate, wealth, and recorded historical building choices. |
| Cultural/political cognition | All 52 option keys can influence support and political response | Valid as appraisal of professed or perceived positions, not evidence that institutions exist. Belief and observed institution inputs must remain distinct. |
| Cultural expression summaries | May fall back from missing current order to beliefs | Misleading presentation. An unrecorded institution must remain unrecorded rather than being narrated from belief. |

## Causal conclusion

The current implementation has useful facts but the flat axis set is not their common type. A coherent replacement must preserve six separate lanes:

1. normative political commitments;
2. offices, bodies, organizations, and their mandates;
3. boundary, authority, aggregation, information, payoff, and scope rules;
4. substantive policies and obligations;
5. realized practices and dated events;
6. perceived legitimacy, public support, and social norms derived from the first five.

The replacement proposal is recorded in B14_POLITICAL_AUTHORING_PROPOSAL.md. No political source change is authorized by this audit.
