# B13 B12 Fidelity Audit

Date: 2026-08-13 UTC / 2026-08-13 PDT

Frozen source: `e9ad9377186b79598a9ac15a21252d0dd2b132cd`

This report was completed before B13 corrective implementation. It compares the
supplied integrated cultural-cognition specification and the B13 completion
contract with the actual B12 source. The classifications below describe the
frozen source; they are not retroactive claims about the corrected B13 state.

## Classification summary

| Requirement | Classification | Frozen-source evidence |
|---|---|---|
| Culture owns distributions over explicit questions, not activities | Exact | `CACultureQuestionRegistry`, `CACultureQuestionDistribution`, and the read-only practice ledger keep questions and practices separate. `ResearchWork` remains `ca.practice.organized_research`. |
| Five ordered anchors and nonzero population variance | Exact | All thirteen definitions have monotonic centers and five anchors; validation enforces the variance floor and deterministic materialization. |
| Descriptive and injunctive norms remain separate | Equivalent | The pawn attitude persists both estimates. The injunctive prior combines the population mean with native doctrine; represented exposure can subsequently update the estimates separately. |
| Salience remains distinct from moral conviction | Missing | `Materialize` directly computes `MoralConviction` from `Salience` and `NormStrength`. B12 receipt 23 explicitly asserts that salience changes conviction. |
| Norm strength remains distinct from expected enforcement | Missing | `ExpectedEnforcement = NormStrength * (1 - DivergenceTolerance)`. No represented sanction, institution, detection, consistency, or personal experience is consulted at materialization. |
| Source confidence remains distinct from pawn knowledge confidence | Missing | `KnowledgeConfidence` and uncertainty are directly calculated from `SourceConfidence` and epistemic vigilance. The inherited-prior strength has no separate pawn field. |
| Visibility remains distinct from public expression | Missing | Visibility multiplies the selected expression toward silence. It is not an observation probability and therefore controls expression rather than whether represented conduct is seen. |
| Public expression is a separately mediated choice | Simplified | Private position, injunctive estimate, conformity, reactance, enforcement, and visibility produce one deterministic scalar. Audience, role cost, relationship cost, expected influence, social support, and represented sanctions are not separate inputs. |
| Pawn psychology mediates Culture | Simplified | Agreeableness, group identification, reactance, epistemic vigilance, openness, and question-specific uncertainty affect conformity, confidence, influence, or appraisal. Psychology does not yet create question-specific private-position deviation from the sampled cultural prior, and several declared mediators are absent. |
| Political belief is pawn-level emergent state | Equivalent | `CulturalPoliticsStateModule` persists pawn/axis attitudes and derives support from Culture, psychology, Ideoligion, material interest, institutional experience, threat, prior belief, knowledge, and represented network evidence. Faction-bounded issue links and coalitions are derived later; current order remains separate. |
| Culture changes appraisal rather than permission or capability | Exact | `AppraiseBehavior` runs after catalog, knowledge, capability, material, authority, operator-intent, and native gates. Relationship and knowledge adapters preserve native zero/observation authority. |
| Historical change passes through represented evidence | Equivalent | Exact-adapter questions can change only after sustained `CASocialGroupPattern` evidence with duration, participation, population, and significance gates. Practices are separately accumulated. Coverage is incomplete for five questions, as detailed below. |
| Every question has a substantive consumer | Exact | Each of the thirteen reaches romance selection, contact topology, discretionary behavior appraisal, pawn political formation, organization legitimacy, or proposition access/transmission. Registry strings alone were not counted. |
| Every question has a historical feedback route | Missing | Eight exact-adapter questions can enter `EvaluateMeaningTransition`. Same-sex acceptance, plurality acceptance, gender authority, intergroup integration, and novelty acceptance have no admitted factual event-to-pattern adapter and therefore cannot update Culture through that route. |
| Empirical calibration and measurement invariance are established | Pending empirical calibration | B12 provides deterministic and monotonic receipts, not fitted distributions, validated loading matrices, cross-cultural invariance, or predictive validation. |

## Cause separation audit

| Cause | Intended owner and meaning | Frozen B12 route | Verdict |
|---|---|---|---|
| Salience | Culture prior for probability and intensity of attention | Direct factor in moral conviction and identity centrality | Causally compressed |
| Moral conviction | Pawn-level strength of moral commitment, mediated by identity, doctrine, experience, and psychology | Derived from salience and norm strength | Causally compressed |
| Norm strength | Population prior for perceived social pressure | Directly converted to expected enforcement | Causally compressed |
| Expected enforcement | Pawn expectation based on represented rules, sanctions, detection, consistency, legitimacy, and experience | `normStrength * (1 - tolerance)` | Causally compressed |
| Source confidence | Strength of the inherited Culture prior | Directly converted to pawn knowledge confidence and uncertainty | Causally compressed |
| Knowledge confidence | Pawn confidence in what is known from observation, testimony, evidence, and custody | Derived from Culture source confidence and vigilance | Causally compressed |
| Visibility | Likelihood that the represented attitude or conduct is observable | Scales public expression toward zero | Causally compressed |
| Public expression | Pawn choice of what to say or perform before an audience | Deterministic blend of private position and injunctive estimate | Simplified |

The frozen persistence model keeps these fields in separate serialized members,
but separate storage does not establish separate causes. B13 must correct the
equations, source records, and receipts while retaining valid B12 owner
boundaries.

## Question-by-question causal trace

Every row begins at the shared `Dialog_CACultureEditor` question control, writes
one `CACultureQuestionDistribution`, and materializes one durable
`CAPawnCulturalAttitude` per pawn, Culture, subgroup, question, and epoch.
The common B12 norm and expression stage is the compressed path described above.

| Question | Authoring and stored fact | Substantive pawn/runtime consumer | Historical feedback in frozen B12 | Classification |
|---|---|---|---|---|
| Same-sex relationship acceptance | Five-anchor mean plus distribution fields | Native-positive romance attempt weight is modified through the initiator's public approach | None. Native relationship conduct is not admitted as an exact question observation | Simplified |
| Relationship plurality acceptance | Five-anchor mean plus distribution fields | Native-positive additional-partner romance attempt weight is modified through the initiator's public approach | None. Existing relationship conduct is not admitted as an exact question observation | Simplified |
| Gender distribution of authority | Five-anchor mean plus distribution fields | Organization cultural fit compares member attitudes with the represented gender distribution of actual office holders; political formation receives the resulting institutional experience | None. Office-holder composition is consumed for legitimacy but does not produce a sustained question pattern | Simplified |
| Hereditary status legitimacy | Five-anchor mean plus distribution fields | Organization fit compares attitudes with represented hereditary succession and kin succession; pawn political support also consumes the question | Exact social-subject adapters for inherited rank and kin succession enter sustained pattern history | Equivalent |
| Social rank differentiation | Five-anchor mean plus distribution fields | Organization fit compares attitudes with represented office seniority; status and authority political options consume the question | Exact office-holding adapter enters sustained pattern history | Equivalent |
| Outsider social inclusion | Five-anchor mean plus distribution fields | Outsider rescue appraisal and membership politics consume the question | Exact outsider-contact and faction-membership adapters enter sustained pattern history | Equivalent |
| Intergroup integration | Five-anchor mean plus distribution fields | Public attitude changes cross-faction, nonhostile contact-edge formation; leadership, decisions, dissent, membership, and other political options consume it | None. The generated contact itself is not an admitted sustained question pattern | Simplified |
| Coercive labor legitimacy | Five-anchor mean plus distribution fields | Authority relay appraisal and work/local-order political formation consume it; institutions retain actual rules and acts | Exact compelled-service and enforced-order adapters enter sustained pattern history | Equivalent |
| Inclusion in public voice | Five-anchor mean plus distribution fields | Communication and organization behavior appraisal, political participation/dissent, and represented decision procedures consume it | Exact public-voice, gathering, delegation, and office-governance adapters enter sustained pattern history | Equivalent |
| Protection owed to defeated people | Five-anchor mean plus distribution fields | Aftermath restraint, custody, captive stabilization, war politics, and custody rules consume it without changing native combat authority | Exact humane-custody, quarter, and punishment adapters enter sustained pattern history with punishment polarity reversed | Equivalent |
| Mutual provision obligation | Five-anchor mean plus distribution fields | Rescue, treatment, triage, provision programs, support politics, and institution appraisal consume it after material and operator gates | Exact represented provision, care, reserve, and authority-provision adapters enter sustained pattern history | Equivalent |
| Access to established knowledge | Five-anchor mean plus distribution fields | Proposition access eligibility, communication appraisal, and transmission consume it; direct observation remains knowledge | Exact knowledge-transmission and long-range-communication adapters enter sustained pattern history | Equivalent |
| Acceptance of novel claims | Five-anchor mean plus distribution fields | Proposition attention/transmissibility, research appraisal, innovation politics, and method rules consume it without creating research completion | None. Research stays correctly classified as practice, but no distinct represented claim-evaluation evidence updates this question | Simplified |

## Corrective obligations established by the audit

1. Add distinct pawn causes for inherited-prior strength, moral conviction,
   knowledge confidence, expected enforcement, observation likelihood, and
   expression choice; do not merely rename the compressed equations.
2. Make salience govern issue attention and effect intensity. It may weight how
   strongly represented evidence is processed, but it cannot itself assert moral
   conviction.
3. Build expected enforcement from represented institutional rules and sanction
   history plus pawn experience. Norm strength remains perceived social pressure.
4. Treat source confidence as a prior weight. Pawn knowledge confidence must come
   from represented observation, testimony, evidence, contradiction, and
   epistemic appraisal.
5. Use visibility to gate observation records. Public expression remains a pawn
   outcome even when nobody observes it.
6. Give psychology question-specific position and conviction mediation with
   versioned, inspectable mappings rather than using psychology only for
   uncertainty and conformity.
7. Close the five missing historical paths only through facts the game already
   represents: relationship conduct, office-holder composition, intergroup
   contact, and proposition/research evidence. No behavior is invented to make a
   question appear complete.
8. Replace B12 receipts that canonize the compression with counterfactual receipts
   for the intended separations.

## Evidence boundary

This audit establishes source fidelity, not empirical validity or gameplay
quality. Literature can justify construct admission and directional hypotheses;
only future calibration and operator runtime evidence can establish parameter
quality and how the system looks and plays.
