# Culture Research Corpus

Date: 2026-08-13 PDT

## Purpose

This corpus governs the Culture questions and built-in historical presets used
by the authoring system. It records why each variable exists, what its ordered
scale means, which represented facts can change it, and which simulation owner
consumes it. It is a design and provenance record, not a claim of psychometric
calibration.

Culture records population distributions over questions. Ideoligion owns
doctrine, Political Beliefs own claims about proper political arrangements,
institutions own adopted rules, and practices own observed conduct. A source
listed here is evidence that may update Culture; it is not a substitute for the
fact owner and does not assert that an event occurred.

## Primary source families

| Key | Source | Use |
|---|---|---|
| `WVS7` | [World Values Survey wave 7 documentation and questionnaire](https://www.worldvaluessurvey.org/WVSContents.jsp?CMSID=Documentation) | Cross-national items on family, gender, work, authority, immigration, social organization, values, and change. The official joint questionnaire supplies question wording and ordering evidence. |
| `ISSP22` | [ISSP 2022 Family and Changing Gender Roles V source questionnaire](https://issp.org/wp-content/uploads/2022/02/issp_2022_final_source_questionnaire.pdf) | Family obligation, gendered work, household authority, care, and changing role expectations. |
| `ESS` | [European Social Survey data and rotating modules](https://www.europeansocialsurvey.org/data-portal) | Immigration, justice, democracy, institutional trust, welfare attitudes, human values, and social participation. |
| `GSS` | [General Social Survey social-change reports](https://gss.norc.org/get-documentation/social-change-reports.html) | Longitudinal evidence on family and gender roles, free expression, punishment, political participation, and social tolerance. |
| `Schwartz` | Schwartz, S. H. (1992), universal content and structure of values | Broad value structure and the distinction between population value tendencies and concrete institutions. |
| `Norms` | Cialdini, Reno, and Kallgren (1990); Bicchieri (2006) | Descriptive and injunctive norms, conditional preference, visibility, and sanctions. |
| `Tightness` | Gelfand et al. (2011), DOI `10.1126/science.1197754` | Norm strength and tolerance for divergence as distinct population properties. |
| `Legitimacy` | Tyler (2003), DOI `10.1111/1540-5893.3703002` | Procedural legitimacy and the distinction between an outcome, the rule producing it, and acceptance of that rule. |
| `Punishment` | Fehr and Gächter (2002), DOI `10.1038/415137a`; Boyd et al. (2003), DOI `10.1073/pnas.0630443100` | Punishment, cooperation, sanction response, and the need to keep observed sanctions separate from norm pressure. |
| `Knowledge` | Sperber et al. (2010), DOI `10.1111/j.1468-0017.2010.01394.x` | Epistemic vigilance, source evaluation, expertise, corroboration, and confidence. |

The survey families show that the questions are intelligible and repeatedly
measured. They do not provide ready-made RimWorld distributions. Built-in
preset centers below are research-informed authoring priors, not fitted
population estimates. No demographic group is assigned a value by identity.

## Question registry

Every category contains three questions. Every row has five named anchors in
the code registry, one or more represented evidence routes, and at least one
substantive downstream consumer.

| Category | Stable key | Ordered variable | Historical evidence route | Substantive consumers | Basis and limitation |
|---|---|---|---|---|---|
| Relationships, family, sexuality | `relationships.sameSexAcceptance` | condemnation to affirmation of same-sex romantic relationships | known romance outcomes and unions | romance approach, relationship legitimacy, membership conflict | `WVS7`, `GSS`, `Norms`; native orientation and attraction remain authoritative |
| Relationships, family, sexuality | `relationships.pluralityAcceptance` | exclusive unions to preferred plural unions | actual plural relationship outcomes and household composition | household formation, jealousy appraisal, relationship rules | `WVS7`, `Norms`; native Ideoligion relationship rules remain separate |
| Relationships, family, sexuality | `relationships.kinObligation` | individual discretion to binding extended-kin duty | household membership, kin aid, and household provision | care appraisal, kin-support politics, household support rules | `WVS7`, `ISSP22`; represented kin and aid are required before history changes |
| Gender and social authority | `authority.genderDistribution` | male dominance through symmetry to female dominance | gender of represented officeholders and binding decision-makers | command legitimacy, office support, office selection | `WVS7`, `ISSP22`, `GSS`; descriptive office composition is not itself an eligibility rule |
| Gender and social authority | `authority.genderedWork` | open work to rigid gender division | represented work assignments by gender and role | work assignment appraisal, labor conflict, work eligibility | `WVS7`, `ISSP22`; pawn competence and operator work settings remain authoritative |
| Gender and social authority | `authority.officeAccess` | gender-restricted to equal office access | appointments, elections, and office tenure by gender | appointment appraisal, office-access support, eligibility rules | `WVS7`, `ISSP22`, `GSS`; actual office rules remain institutional state |
| Status and hierarchy | `status.hereditaryLegitimacy` | inherited status illegitimate to naturalized | inherited rank and kin succession | succession appraisal, status support, office legitimacy | `Schwartz`, `Norms`, `Legitimacy`; kin relation alone does not create succession evidence |
| Status and hierarchy | `status.rankDifferentiation` | rank rejected to entrenched rank | represented office privilege and durable status assignment | deference, resource legitimacy, office privilege | `Schwartz`, `Norms`; transient skill or job priority is not durable rank |
| Status and hierarchy | `status.mobility` | fixed-at-birth status to open mobility | represented promotion, demotion, appointment, and standing changes | mobility appraisal, status politics, appointment rules | `WVS7`, `Legitimacy`; mobility records require an actual before and after state |
| Membership and outsiders | `groups.outsiderInclusion` | exclusionary to socially integrative | hospitality, rescue, recruitment, and outsider contact | hospitality, recruitment, intermarriage, access conflict | `WVS7`, `ESS`; contact does not imply approval without response evidence |
| Membership and outsiders | `groups.integrationPreference` | required separation to expected integration | mixed-population residence, work, and social ties | mixed-group interaction, integration politics, residence policy | `ESS`, `Norms`; spatial proximity alone is weak evidence |
| Membership and outsiders | `groups.membershipAccess` | closed descent to open membership | admissions, exclusions, and faction membership changes | recruitment, naturalization, membership procedure | `WVS7`, `ESS`, `Legitimacy`; faction membership remains a separate factual owner |
| Public authority and social order | `voice.inclusionExpectation` | reserved voice to universal voice | participation in binding decisions and public gatherings | meeting participation, participation conflict, decision procedure | `WVS7`, `ESS`, `GSS`; voice does not itself authorize a decision |
| Public authority and social order | `voice.dissentTolerance` | suppressed to protected public dissent | objections, protests, sanctions, and tolerated criticism | objection response, dissent politics, speech and meeting rules | `WVS7`, `ESS`, `GSS`; private disagreement is not public dissent evidence |
| Public authority and social order | `authority.enforcementLegitimacy` | force rejected to routine enforcement | orders, resistance, enforcement, and actual sanctions | order appraisal, public-order support, sanction rules | `WVS7`, `ESS`, `Legitimacy`; expected enforcement is learned from represented enforcement, not copied from norm strength |
| Property, labor, provision | `labor.coercionLegitimacy` | compelled labor never legitimate to institutionally expected | compelled work, refusal, and enforcement | work refusal, labor conflict, work rules | `WVS7`, `Norms`; legality and operator orders remain separate |
| Property, labor, provision | `provision.mutualObligation` | household responsibility to collective guarantee | provision, reserves, unmet need, and care | aid, support politics, provision systems | `WVS7`, `ISSP22`; facilities and supplies remain material facts |
| Property, labor, provision | `property.control` | concentrated private control to common control | represented ownership, common stores, transfers, and disputes | ownership disputes, property politics, transfer rules | `WVS7`, `Schwartz`; it does not infer title from item location |
| Violence, captivity, punishment | `war.captiveProtection` | no restraint to strong duty of care | surrender, custody, execution, rescue, and treatment outcomes | custody appraisal, war legitimacy, custody rules | `Norms`, `Legitimacy`; custody authority and medical capacity remain separate |
| Violence, captivity, punishment | `war.punishmentSeverity` | restorative restraint to exemplary severity | represented sanctions, punishment, clemency, and recidivism | punishment appraisal, punishment politics, sanction schedules | `GSS`, `Punishment`; severity and enforcement probability are independent |
| Violence, captivity, punishment | `war.retaliatoryViolence` | restraint to obligatory retaliation | attacks with represented prior harm, reprisals, and peace agreements | revenge appraisal, feud support, reprisal restraint | `WVS7`, `Norms`; ordinary combat without a represented prior harm is not retaliation |
| Knowledge and tradition | `knowledge.access` | esoteric to open access | teaching, publication, archives, and proposition access | proposition access, transmission, archive and school policy | `WVS7`, `Knowledge`; access does not establish truth |
| Knowledge and tradition | `knowledge.noveltyAcceptance` | tradition-bound to experimental | research attempts, corroboration, adoption, and observed payoff | claim evaluation, research adoption, method rules | `WVS7`, `Knowledge`; novelty does not increase discovery or truth directly |
| Knowledge and tradition | `knowledge.expertiseDeference` | status-indifferent to expert-led judgment | advice, demonstrated skill, source accuracy, and task outcomes | source weighting, expert-role support, credential rules | `ESS`, `Knowledge`; expertise is domain-specific and must be represented |

## Built-in historical and social presets

Presets are complete editable Culture objects. A preset writes all 24 means,
salience values, and one global diversity setting into the same distributions
used by manual editing and randomization. It creates no mode flag and has no
blend operation. Editing one question after applying a preset changes only that
question.

| Preset | Historical/social organizing evidence | Deliberate profile | Limitation |
|---|---|---|---|
| Mobile kin band | Small mobile groups with strong kin reciprocity, little durable office, and knowledge carried through social transmission | strong kin duty, low durable rank, limited formal enforcement, moderate outsider caution, shared hardship provision, broad practical knowledge | not assigned to any ethnicity or time period; mobility and kin organization are authored facts |
| Ranked agrarian households | Landed households, inherited standing, gendered labor, local customary authority, and restricted office | high hereditary legitimacy and rank, stronger gender division, kin provision, restrained membership and knowledge access | abstracts a wide family of agrarian orders and does not claim a universal historical type |
| Civic market town | Mixed households, voluntary trade, public gathering, guild-like expertise, and negotiated local office | broad public voice and membership, mixed property control, moderate rank, high expertise weight, open exchange and knowledge access | a town may deviate on any row; the preset is an authoring prior |
| Central court society | Central office, formal rank, concentrated authority, taxation, specialized expertise, and managed public order | high rank and enforcement legitimacy, restricted voice and dissent, concentrated property control, selective knowledge access | courtly organization does not imply a particular doctrine or Ideoligion |
| Frontier mutual-aid settlement | Weak mature institutions, high household interdependence, practical openness, and defense under uncertain conditions | high provision and mobility, moderate voice, low hereditary rank, cautious outsider inclusion, high novelty and expertise | frontier danger does not itself create hostility or punitive norms |
| Industrial civic association | Broad membership, wage and specialist work, formal public procedure, institutional provision, and open technical knowledge | high voice, office access, mobility, knowledge access and expertise; mixed property control and moderate enforcement | not a modern-national template and does not supply technology, buildings, or institutions |
| United States - postwar mid-century (1946-1964) | GSS longitudinal social-change reports, WVS United States samples, and ISSP family and gender-role modules support the period comparison | strong conventional family norms, gendered work, private property, civic participation, and confidence in enforcement | research-informed design prior; it is not a fitted national distribution and does not assign any pawn a value by identity |
| United States - turn of the millennium (1995-2005) | The same longitudinal source families support a later comparison point within one society | broader relationship and office acceptance, open membership, strong mobility, private property, and accessible technical knowledge | central positions abstract internal regional and population disagreement into the separately authored diversity setting |
| United States - contemporary (2017-2024) | Recent GSS, WVS, and ISSP waves support a third period comparison while retaining the same question registry | broad relationship and office acceptance, open work, civic voice, accessible knowledge, and substantial internal disagreement | current-period label bounds the source window; empirical calibration and measurement-invariance review remain pending |

Preset values are specified and tested in `CACulturePresetLibrary`. The nine
complete presets include three periods of the same society, proving that a
society name is not a timeless Culture identity. Their rationale is transparent
here; the exact numeric centers are intentionally kept in source so fixed-seed
receipts can detect drift.

## Causal separation contract

| Authored field | Direct meaning | Explicitly separate downstream state |
|---|---|---|
| Position | population center on the named ordered scale | a pawn samples an individual private position; psychology may produce a small question-specific deviation |
| Global diversity | default within-population spread for all questions | it does not move any question center |
| Per-question spread | advanced override of that question's population spread | it does not alter global diversity or other questions |
| Salience | likelihood and intensity of attention to the question | moral conviction also requires position extremity, identity, doctrine, or lived evidence |
| Norm pressure | perceived pressure toward an injunctive population position | expected enforcement requires represented rules, sanctions, or enforcement history |
| Divergence tolerated | how much disagreement is accepted without social pressure | it does not determine punishment severity or legal permission |
| Public visibility | likelihood that an expression is observed | it does not compress the expression toward zero |
| Source confidence | strength of the inherited population prior | pawn knowledge confidence also requires direct evidence, source evaluation, and epistemic vigilance |

## Calibration status

All B13 scale anchors are semantic anchors. Preset centers are documented design
priors. Distribution widths are controlled by the player or deterministic world
generation. Empirical calibration remains pending until a dataset, selection
rule, item transformation, uncertainty model, measurement-invariance review,
fit receipt, and held-out validation are committed together.
