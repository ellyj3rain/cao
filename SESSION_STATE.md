| Document | Colonist Awareness Overhaul Session State |
|---|---|
<!-- cao:generated:version BEGIN -->
| Version | `1.3.0.2-alpha` · closed batch tip `B8` · next `B9` |
<!-- cao:generated:version END -->
| Repository | `SESSION_STATE.md` |
| Status | ACTIVE - current operational state. |

# Session state

Updated 2026-08-12 UTC / 2026-08-11 PST.

Read this before claiming where creation or gameplay testing stands. Compile,
receipts, review, deployment, and operator runtime evidence remain separate.

## Version control and governed history

| Surface | State |
|---|---|
| Branch | `mallowfluff/b3-creation-flow-interaction` |
| Exact B8 baseline | `0f520946927b2ab754fa8bdd367a909ec3781538` |
| B8 source closure | `1560c2ed16f27eb3d258fd57962ea2c0fdb3e68a` |
| B8 governance closure | The following governance commit carries this state, the B8 append-only record, version projections, and README twin. |
| Publication | Normal fast-forward publication to the current branch is part of B8 closure. |

The portable chronology contains 110 closed batches: `A1-A102` and `B1-B8`.
Thirty-five contiguous version units cover that chronology exactly once; B8 is
patch unit `VU-035`, deriving `1.3.0.2-alpha`. Twelve `TF-*` families and thirty
series-neutral `T-*` threads classify work without changing chronology. `B9` is
the next ordinary batch.

## Current authoring contract

Creation exposes direct facts and inspects derived consequences. Starting Region
retains the selected region and arrival area, map, factions, settlements,
locations, population groups and sources, ownership, relations, provisions, and
sparse exact starting exceptions. Material scale, infrastructure, access,
services, facilities, research capability, and spatial form are realized from
actual population, land, geography, technology, knowledge, institutions,
economy, trade, material state, relationships, and history.

The founding flow coordinates four distinct concepts:

| Concept | Current meaning |
|---|---|
| Culture | Inherited and local social history with substantive meanings and practices |
| Ideoligion | RimWorld's native religious, ritual, moral, and spiritual system |
| Political Beliefs | Thirteen normative answers about proper authority and social order |
| Rules at landing | The authority, work, voice, and supply arrangement actually adopted at founding |

Culture at T0 is not a name or style. It saves constituent populations,
inherited and current meanings, inherited and lived practices, observations,
transitions, maturity, temporal basis, and provenance. Each meaning concerns one
namespaced registered subject and records approval, normality, prestige, and
salience. The initial registry contains twelve subjects; later CA modules may add
source-and-consumer contracts without changing the Culture schema. Native
`CultureDef` remains a separately labeled optional visual tradition.

The Culture composer presents overview, social meanings, inherited practices,
visual tradition, and causal preview. Saved Culture profiles copy inherited
state without locality, observations, transitions, or a live profile identity.
Established factions and settlements use the same model at their established
temporal boundary. New founders carry inherited Culture and form local history
through play unless the scenario explicitly supplies an established start.

Political authoring is question-first. Five built-in profiles each answer all
thirteen questions, contain no parent or missing field, copy answers as authored
state, and leave every answer editable. World state stores only the vector, not
the profile identity. NPC derivation resolves an axis from same-axis realized
structure, an explicit observed fact, or scored cultural meaning, records stable
causal evidence, and leaves unsupported axes unset.

Informed pawn responses preserve contributions from Culture, Political Beliefs,
Ideoligion where relevant, personal state, institutions, and relationships.
Actual reactions aggregate by subject into participation, mean response,
dispersion, polarization, influential minorities, alignment, and cross-group
dissonance. Qualified evidence across historical periods may change later
Culture. One event, unchanged evidence, or opening an editor cannot.

Culture consumers use the generic meaning resolver. They may rank or interpret
otherwise valid spatial, social, institutional, political, and development
choices, but Culture grants no knowledge, permission, authority, office,
technology, labor, land, material, treasury, or execution capability. Abstract
stock symbols do not represent Culture, Political Beliefs, their profiles,
founding rules, or cultural expression. Native Ideoligion retains its actual
symbol; concrete object and action icons remain.

## Persistence boundary

The current authoring epoch is 8. Regional plan schema is 6; Culture and
Political Beliefs schema is 8; player founding schema is 3. An epoch mismatch
clears incompatible CA-owned Culture, Political Beliefs, profile,
founding-draft, social-interpretation, and regional-authoring state and emits one
diagnostic. No field conversion, alias, partial preset inheritance, or abandoned
profile identity remains. This is an intentional pre-release reset.

## Build, receipts, and review

| Evidence | Result |
|---|---|
| B8 acceptance | PASS - exact 57/57 receipts |
| World tendencies | PASS - 185 assertions |
| Player founding | PASS - 48 assertions |
| Creation-flow interaction | PASS - 58 assertions |
| Behavior convergence | PASS - 107 numbered cases: 98 statically verified; 9 operator-runtime observations pending |
| Frozen-tree reviews | Ontology, causality, UI, structural, and playability: no unresolved Critical or High finding |
| Release build | Clean Release rebuild: 0 errors; the same 12 inherited warnings |
| Built assembly | 3,321,344 bytes; SHA-256 `995C8123DA81EB083C311C2C792223077B0A13B11FA7FB583531F704F563A6BF` |
| Deployment | RimWorld was closed. The active project DLL is byte-identical to the verified build at the same SHA-256. |

The twelve warnings are the existing member-hiding and DefOf assignment
warnings. `git diff --check` passes with only line-ending notices.

## Authored runtime fixture

| Field | Current authority |
|---|---|
| World | `alysaliu|1|Algorab Markab` |
| Region | `CA-RG-EB596A12` |
| Candidate | `613b1fe44104`, confirmed |
| Arrival/root tile | `389638` |
| Map scale | 350 |
| Composition | 3 factions, 4 settlements, 9 population groups |
| Schema | Authoring epoch 8; regional plan 6; Culture and Political Beliefs 8; player founding 3 |
| Temporal basis | Existing settlements carry direct established baselines with no fabricated transition history |
| Compatibility | `developerExercise=true`; transient runtime materialization only; durable saving disabled |

Active and keyed plan:

- `C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlan.xml`
- `C:\Users\jleyv\AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config\CARegionalPendingPlans\regional-plan-9bcbba2fdcd1e7f334711a69635242a2.xml`
- 45,518 bytes each
- SHA-256 `4BEF806DADF9A40F83E4E8684B55BBC6335C8BC078119132D66BCA3769BC9093`

Both files are byte-identical and parse the same current-schema composition,
relations, population assignments, founding state, substantive faction and
settlement Cultures, and complete political vectors. The selected region's
unsupported world mutators remain an explicitly stamped transient developer
exercise. It can materialize for this disposable run but is not scribed.

## Operator runtime boundary

The B8 build is deployed and the static gate is complete. Test the following in
RimWorld:

| Case | Runtime observation |
|---|---|
| Authoring | Compose a substantive founding Culture; edit meanings and inherited practices; keep visual tradition separate. |
| Politics | Answer all thirteen political questions, apply a complete profile, then independently edit an answer and inspect belief-versus-rule tensions. |
| Native Ideoligion | Enter, edit, return, and navigate Back/forward without losing Culture, Political Beliefs, or rules at landing. |
| Starting Region | Inspect faction and settlement Culture, constituents, local disagreement, political state, institutions, and the retained map. |
| 97 | Complete creation and reach map generation. |
| 98 | Confirm player map generation completes and behavior components initialize. |
| 99 | Confirm missing optional assets remain nonfatal and expose their fallback receipt. |
| 100 | Observe ordinary native work at Standard. |
| 101 | Observe one bounded Proactive response. |
| 102 | Observe one persistent Autonomous objective. |
| 103 | Issue direct work around every initiative tier and confirm player authority. |
| 104 | Not runnable on this transient fixture. Use a compatible-region save/reload run to compare registered intent identity and ownership. |
| 105 | Run through the first in-game hour and inspect `Player.log` for repeating exceptions. |

Static evidence does not select options, advance the game, change saves, or
substitute for how the creation flow and generated colony look and play.

## Environment and preserved evidence

- RimWorld target: 1.6.4871 rev590.
- Odyssey is installed; active runtime state is not asserted without a launch.
- `PARALLEL_ONTOLOGY_AUDIT.md` remains a damaged consolidation; its recovered
  full copy remains in the preservation package.
- Preservation package:
  `Projects\colonist-awareness-preservation-20260806-0614Z-2314PDT`, with a
  verified mirror on `D:`.
- The RS-008 source save and recovered documents remain out-of-band evidence.
