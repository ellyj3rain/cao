# B12 Acceptance Receipts

These receipts exercise the pure equations used by the runtime wrappers, inspect the represented consumer routes, and parse the current authored fixture. They are static evidence; they do not claim psychometric validity or operator runtime acceptance.

| # | Contract | Result | Evidence |
|---:|---|---|---|
| 1 | question registry validates | **PASS** | no failure |
| 2 | current registry retains the B12 questions | **PASS** | count=48; unique=48 |
| 3 | five ordered anchors per question | **PASS** | all anchors are named and strictly monotonic |
| 4 | every question names a represented consumer | **PASS** | behavior, political, institution, and knowledge routes inspected |
| 5 | explicit new-question defaults validate | **PASS** | 48/48 neutral authoring defaults; no identity-derived facts |
| 6 | materialization is deterministic | **PASS** | same seed=0.353485/0.353485 |
| 7 | pawns vary within one population | **PASS** | pawn17=0.353; pawn18=0.095 |
| 8 | variance floor rejects degenerate input | **PASS** | floor=0.06 |
| 9 | anchor lookup is monotonic | **PASS** | -0.90,-0.45,0,+0.45,+0.90 -> 0..4 |
| 10 | subgroups preserve represented offsets | **PASS** | low=-0.467; high=0.435; shares=100 |
| 11 | one mean changes only its distribution | **PASS** | target 0.120->0.518; unrelated=B33D6446 |
| 12 | spread changes dispersion, not authored center | **PASS** | authored=0.700; means 0.699/0.693; sd 0.041->0.291 |
| 13 | question labels remain descriptive | **PASS** | no moralized good/bad or niceness scale |
| 14 | ordered endpoints share one centered scale | **PASS** | all five-anchor scales are centered and mirror-coherent |
| 15 | outsider inclusion is independent from integration | **PASS** | changing outsider inclusion leaves integration unchanged |
| 16 | hereditary status is independent from rank | **PASS** | inheritance legitimacy does not author durable rank |
| 17 | nominal political forms remain nominal | **PASS** | leader, council, assembly, and decision forms remain option keys |
| 18 | unsupported subjects remain outside Culture | **PASS** | B13 maps represented research to novelty acceptance; taxation and compulsory transfer remain factual evidence |
| 19 | B13 separates salience from conviction | **PASS** | attention 0.360->0.684; conviction stable |
| 20 | B13 separates norm pressure from enforcement | **PASS** | pressure 0.358->0.617; enforcement stable |
| 21 | divergence tolerance reduces norm pressure | **PASS** | pressure 0.358->0.055; enforcement stable |
| 22 | B13 separates inherited confidence from knowledge confidence | **PASS** | prior 0.550->0.950; knowledge stable |
| 23 | doctrine changes injunctive pressure, not Culture | **PASS** | injunctive 0.075->0.300; private stable |
| 24 | B13 separates observation likelihood from expression | **PASS** | observation 0.050->0.950; expression stable |
| 25 | unobserved psychology uses an explicit neutral prior | **PASS** | prior=0.500; identity is not an evidence source |
| 26 | sparse influence is bounded and selective | **PASS** | accepted=-0.165; distant=-0.800 |
| 27 | relationship approach is bounded and monotonic | **PASS** | 0.70..1.15 |
| 28 | institution fit follows belief-practice agreement | **PASS** | agreement produces greater legitimacy fit |
| 29 | novelty changes knowledge transmissibility | **PASS** | receptive > tradition-bound |
| 30 | descriptive and injunctive norms remain separate | **PASS** | descriptive=-0.70; injunctive=0.28 |
| 31 | private and public positions can diverge | **PASS** | gap=0.11 |
| 32 | perceived and actual norms can diverge | **PASS** | represented perceived=-0.60 and actual=+0.40 |
| 33 | pluralistic ignorance is representable | **PASS** | private support can coexist with contrary expression and belief |
| 34 | referent trust and prestige alter influence | **PASS** | positive referent=0.023; negative referent=-0.023 |
| 35 | repeated independent exposure strengthens adoption | **PASS** | one=0.032; three=0.047 |
| 36 | collective reinforcement remains bounded | **PASS** | reinforced=0.047; source=0.500 |
| 37 | network topology changes the observed neighborhood | **PASS** | local=0.032; bridged=0.019 |
| 38 | within-culture political disagreement is possible | **PASS** | same Culture; support 0.06->0.17 |
| 39 | material interests can diverge from Culture | **PASS** | neutral=0.06; adverse interest=-0.04 |
| 40 | political confidence follows represented knowledge | **PASS** | confidence 0.65->0.95; support stable |
| 41 | political propositions can change option support | **PASS** | confidence-only=0.06; directed evidence=0.13 |
| 42 | issue correlation requires cross-pawn variation | **PASS** | aligned=1.00; independent=-0.32 |
| 43 | issue links control the evaluated hypothesis family | **PASS** | family=1246; 64 fixed-seed null families produced 0 links; r=.90 n=16 retained |
| 44 | issue evidence weight survives network normalization | **PASS** | raw=0.90; shrunk=0.69; lone weak link=0.11 |
| 45 | political issue links emerge from observed bundles | **PASS** | qualified faction samples update current links; vanished evidence removes stale links |
| 46 | perceived political majority requires observed exposure | **PASS** | proximity alone and faction-wide means do not reveal political positions |
| 47 | coalitions sort without complete issue constraint | **PASS** | shared issue, grievances, and goals are separate; other positions survive |
| 48 | political belief remains separate from current structure | **PASS** | current organization is evidence of experience, not a copied preference |
| 49 | noncompliance outcomes are reachable from represented inputs | **PASS** | Dissent,Object,Organize,Reform,Exit,Violate,Retaliate |
| 50 | procedure and performance affect legitimacy independently | **PASS** | base=0.500; procedure=0.568; performance=0.554 |
| 51 | public support is separate from outcome performance | **PASS** | base=0.500; support=0.541; performance=0.554 |
| 52 | corruption and coercion reduce legitimacy | **PASS** | base=0.500; corrupt/coercive=0.440 |
| 53 | sanctions have conditional effects | **PASS** | fair deterrence=0.83; arbitrary reactance=0.83 |
| 54 | unfair sanctions can crowd out cooperation | **PASS** | voluntary 0.68->0.19 |
| 55 | institution and asset remain distinct owners | **PASS** | organizations own rules/offices/history; material assets remain program state |
| 56 | institution lifecycle evidence is retained | **PASS** | staffing, succession, maintenance cadence, decisions, and sanctions persist |
| 57 | organization owner assembles legitimacy evidence | **PASS** | organization composes procedure, outcomes, public support, and narrow cultural/political fit projections |
| 58 | source trust changes claim confidence | **PASS** | confidence 0.597->0.642 |
| 59 | independent corroboration changes claim confidence | **PASS** | confidence 0.597->0.651 |
| 60 | distinct sources produce bounded corroboration | **PASS** | one source is not corroboration; independent sources accumulate to the bound |
| 61 | explicit contradiction lowers current confidence | **PASS** | uncontested=0.597; two conflicts=0.577 |
| 62 | novelty changes attention, not discovery | **PASS** | attention 0.625->0.886; confidence stable |
| 63 | access changes eligibility, not truth | **PASS** | restricted testimony can be ineligible; direct observation remains available |
| 64 | access and novelty jointly govern transmission | **PASS** | access and novelty are independent transmission inputs |
| 65 | propositions retain provenance and contradictions | **PASS** | claim, holder, source, channel, chain, evidence, and conflicts persist |
| 66 | observed claims derive live and independent source evidence | **PASS** | mixed direct/report channels=Witnessed/Reported; two reporters retained; holder self-source rejected; corroboration 0.25->0.45 |
| 67 | research uses the complete represented contract | **PASS** | question, actors, authority, method, facility, materials, evidence, preservation, and dissemination |
| 68 | institutions preserve represented knowledge | **PASS** | organization records become custodied propositions |
| 69 | knowledge networks remain sparse and bounded | **PASS** | recent institutional records and prior knowledge are capped |
| 70 | all questions have actual source consumers | **PASS** | 24/24 B12/B13 registry constants remain in their designated consumers; later questions use the registry-driven cognition path |
| 71 | durable owners partition represented cognition | **PASS** | cultural cognition, political cognition, and proposition knowledge persist separately |
| 72 | legitimacy is owned by the represented organization | **PASS** | organization schedules and owns legitimacy; cognition provides read-only evidence; no duplicate institution ledger |
| 73 | Culture changes selection appraisal, not authorization | **PASS** | Allowed remains the causal authorization; CA discretionary job selection consumes the response |
| 74 | direct operator intent bypasses cultural selection | **PASS** | direct, relayed, and restored operator intent remain authoritative |
| 75 | skills are excluded from stable personality | **PASS** | skills occur only in dynamic Disposition projection; stable gaps use neutral priors |
| 76 | psychology records recoverable evidence | **PASS** | each trait, backstory, gene, or missing-evidence prior has a source record |
| 77 | stable psychology and dynamic state are separate | **PASS** | stable profile persists evidence; mood, pain, fatigue, threat, and load update separately |
| 78 | skills enter competence and perceived control | **PASS** | skills affect current efficacy and institutional competence, not personality factors |
| 79 | psychology mappings remain versioned and discriminant | **PASS** | evidence mappings name separate constructs and persist their version |
| 80 | identity hashes do not create Culture or psychology facts | **PASS** | question facts are authored or migrated; psychology changes only from represented evidence |
| 81 | Culture migration is exact and evidence-preserving | **PASS** | only exact approval and salience map; other dimensions survive as legacyEvidence |
| 82 | Culture UI authors questions and inspects practices | **PASS** | question rows active; practice history read-only; obsolete editors absent |
| 83 | player and established surfaces share current Culture | **PASS** | founding carries inherited state; established regional editor uses same model |
| 84 | coalitions use positive support inside one faction boundary | **PASS** | negative support and cross-faction membership cannot manufacture a coalition |
| 85 | preflight validates nested B12 payloads | **PASS** | question, psychology, attitude, coalition, knowledge, and organization appraisal payloads are checked |
| 86 | new owner version tags are always serialized | **PASS** | all catalog-2 world owners force their required inline version tag even when its value equals the current schema |
| 87 | legacy adapter has only exact approval and salience inputs | **PASS** | mean=0.400; salience=0.540; normality and prestige have no adapter parameter |
| 88 | catalog retains the B12 cognition owner | **PASS** | world.cultural-cognition remains a catalog-2 owner and now requires schema 2 |
| 89 | politics and proposition knowledge have separate owners | **PASS** | catalog 2 introduces both schema-1 owners additively |
| 90 | durable Culture catalog admits exact nine-to-ten migration | **PASS** | minimum=9; current=10 |
| 91 | organization schema adds owner-held institutional appraisals | **PASS** | world.organization 1 -> 2 carries legitimacy and sanction appraisals |
| 92 | organization one-to-two migration initializes new owned lists | **PASS** | schema-1 organizations gain empty appraisal histories before schema-2 validation; no prior appraisal history is invented |
| 93 | catalog routes and nested bindings are complete | **PASS** | 87 catalog schemas have executable component, nested-record, or native-class persistence routes |
| 94 | events are persisted before Culture history changes | **PASS** | fact -> pawn reaction -> sustained group pattern -> Culture transition |
| 95 | Culture transitions require duration and coverage | **PASS** | minimum evidence, pawns, participation, and historical duration are explicit |
| 96 | unchanged history suppresses no-op transitions | **PASS** | identical evidence and sub-significance changes do not append history |
| 97 | migration composes exact questions with preserved evidence | **PASS** | exact adapters create distributions; every old record remains evidence |
| 98 | cohort and subgroup state remain represented | **PASS** | regional populations keep scopes and subgroup mixtures |
| 99 | social, institution, and Culture maintenance use separate owners and cadences | **PASS** | cultural social pulse, organization appraisal, and cultural maintenance/history are separate |
| 100 | influence exposure is timestamped per observed subject | **PASS** | new observations renew only their Culture question or political axis |
| 101 | fixture migration validates before atomic replacement | **PASS** | fixture routes through the shared validated pair-commit helper |
| 102 | fixed-seed populations are deterministic at several sizes | **PASS** | n16=8E1FCA36; n256=1BDFF7C8; n4096=AA0F438D |
| 103 | population continuation is order-independent | **PASS** | materialization identity, not traversal order, owns each draw |
| 104 | hot cognition work is bounded | **PASS** | both hot pulses process at most 12 pawns; profile, attitude, edge, organization, and political-knowledge lookups use owner indexes |
| 105 | social and political graphs are sparse | **PASS** | each pawn retains at most eight influence edges; issue links are faction-bounded |
| 106 | history, research, and sanction memory are capped | **PASS** | per-pawn evidence, research inputs, and sanction histories have explicit caps |
| 107 | fixture pair replacement rolls back injected failure | **PASS** | active and mirror originals survive a failure after the first replacement |
| 108 | active and mirror fixtures agree | **PASS** | SHA-256=004C5A0F2505594D36E89CBD0F02A4BBE46C1B044965E6A5F29AFD27AFC521AF; mirror=004C5A0F2505594D36E89CBD0F02A4BBE46C1B044965E6A5F29AFD27AFC521AF |
| 109 | fixture carries the current authoring epoch | **PASS** | authoringDataEpoch=14 |
| 110 | authored identity and composition survive | **PASS** | region/candidate/tile/scale; 3 factions; 4 settlements; 4 current population assignments; 19 program facts |
| 111 | fixture uses current Culture schema | **PASS** | 8 schema-11/registry-3 records; 577 distributions; every represented inherited population scope has 48 questions; obsolete meaning payloads absent |
| 112 | migration evidence survives serialization | **PASS** | questions=577; B12 live=21; quarantined=1; evidence=26; complete roots plus valid represented local facts retained; one orphaned scoped fact and unmapped compulsory transfer preserved as evidence |
| 113 | current fixture round-trips structurally | **PASS** | question identities survive XML readback |

Result: **113/113 PASS**
