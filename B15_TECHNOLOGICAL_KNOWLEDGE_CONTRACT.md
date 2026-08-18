# B15 Technological Knowledge Contract

Status: canonical for B15

## Ownership

The authored faction owns three sibling components:

`Culture + Political Order + Technological Knowledge`

`CAFactionState` is the realized campaign owner. Society is the existing
authoring surface that composes those components together. A Society preset is
a reusable snapshot of their values. Neither the Society surface nor a preset
becomes a campaign object, and neither owns state after application.

Founding and Starting Region plans may stage all three components before a
faction exists. Confirmation validates the complete staged composition and
copies it once into the realized faction. Settlements reference faction-owned
state unless an explicit local divergence is modeled. Saved settlement
technology fields are historical realization receipts, not effective
capability authorities.

## Authoring and presets

The Society surface edits Culture, Political Order, and Technological Knowledge
as one faction composition. Culture and Political Order retain their own direct
editors. Technological Knowledge has one direct domain editor. Each component
may be replaced independently from a component preset when that is the player's
task.

A Society preset contains an independent key, name, metadata, Culture snapshot,
complete Political Order snapshot, and complete Technological Knowledge
snapshot. Applying one:

1. deep-copies and validates all three candidate components;
2. replaces the three faction-owned values in one operation;
3. restores all three prior values if validation or commit fails; and
4. stores no preset key or shared mutable preset value in the faction or its
   settlements.

User-saved Society profiles use the same contract. Existing Culture-only and
Political-Order-only profiles keep their independent identities and are not
discarded merely because the Society profile schema advances.

## Knowledge model

Technological Knowledge records practical capability in nine domains:

| Domain | Scope |
|---|---|
| Medicine | diagnosis, treatment, surgery, and medical production |
| Agriculture | cultivation, husbandry, and dependable food production |
| Construction | shelter, buildings, civil works, and structural repair |
| Manufacturing | worktables, tooling, repeatable production, and fabrication |
| Metallurgy and materials | smithing, machining, fabrication, and material processing |
| Electrical systems | power, electronics, batteries, and powered machinery |
| Chemistry | medicines, fuels, drugs, and chemical processing |
| Logistics and preservation | storage, refrigeration, transport, and supply handling |
| Weapons and defense | weapons, armor, fortification, and defensive systems |

Each domain separately records the ability to understand, construct, operate,
and maintain at ranks zero through five. Known native research projects remain
explicit evidence. Provenance records whether a value was authored, generated,
learned through research, or initialized from a native faction baseline.

`FactionDef.techLevel` is not effective authored capability. It may seed a new,
otherwise unauthored faction once; serve native code before a world or faction
owner exists; and contribute native research metadata that the translation
layer converts into explicit requirements. Runtime consumers query the
faction's Technological Knowledge.

## Availability models

Standard mode reads the faction-owned domain state directly. Knowledge remains
a social capability when an individual pawn dies, departs, becomes incapable,
or changes faction. Standard mode does not create pawn custody and does not
transfer canonical ranks through a pawn.

Experimental Distributed Knowledge changes where that same knowledge is
available. It does not create another technology ontology or authority.
Domain-and-competency records may be carried by pawns, institutions, or durable
records. Effective capability is the highest accessible rank at the requesting
map or settlement scope. Overlapping carriers provide redundancy; losing the
only accessible carrier removes that practical capability until an accessible
carrier, institution, or record supplies it again. Pawn recruitment, faction
change, death, and incapacity update custody without regenerating lost carriers.

Initial pawn distribution occurs at a stable faction-population lifecycle
boundary and only once. After that boundary, a usable humanlike pawn who joins
or spawns in the faction while Distributed Knowledge is enabled receives an
idempotent, skill-bounded custody projection. This is not teaching,
apprenticeship, or a simulation of how the pawn learned it. Queries never
initialize or reroll distribution. Pawn custody is checked against the living
carrier at the requesting map; institutional and recorded custody is checked
against its persisted map or world-tile scope. Custody identities, rank bounds,
availability state, and receipts validate on save and load.

## Translation and consumers

`CATechnologyRequirementResolver` is the one translation boundary between
RimWorld definitions and Technological Knowledge. It maps native research,
buildables, manufactured items, recipes, plants, habitat requirements, and compatibility checks to
explicit domain, competency, and rank requirements. The native research map is
exact and audited; unknown projects fail closed until mapped. Requirements for
ordinary work that has no research prerequisite are still explicit, so rank
zero does not silently grant production, agriculture, medical work,
maintenance, construction, or weapon operation.

Research completion remains a native fact. CA never marks a project complete
merely because a broad domain rank is high. Research availability, cost,
progress, and native tech-level compatibility query the faction-owned
knowledge projection. Completing research records the exact native project and
raises only its translated domains.

Settlement viability separates three questions:

- what the environment requires;
- whether the faction knows how to satisfy those requirements; and
- whether the settlement has the materials, labor, programs, and access to do
  so.

Current execution coverage is explicit:

| Capability | Current consumer boundary |
|---|---|
| Understand research | availability, cost, progress, speed, and research presentation |
| Construct buildables | build visibility, work selection, active construction, starting structures, frontier, morphology, settlement programs, security, autonomous homes, and furnishing |
| Operate recipes | recipe visibility, bill work selection, and active bill jobs |
| Operate agriculture | plant selection, sowing work selection, and active sowing jobs |
| Operate medicine | tending work selection and active tending jobs |
| Maintain buildables | repair and breakdown work selection and active jobs |
| Construct and operate weapons | exact milestone manufacture plus equipped-weapon availability |
| Establish habitat functions | placement potential plus separate confirmation of material programs, labor, supply, and access |

The full nine-domain by four-competency grid is inspectable forward capacity.
A cell not represented in this table is not claimed as shipped behavior merely
because it exists in the schema. Future consumers must use the same translator
and add executable receipt evidence. A stored compatibility tier may explain a
past realization, but current behavior resolves current faction knowledge.

## Persistence and compatibility

B15 publishes campaign catalog `4`. `model.technological-knowledge` schema `1`
is nested in `world.faction-state` schema `3`. `world.player-founding` and
`world.regional` advance to owner schema `3`; the staged player-founding plan is
schema `4`, regional plan is schema `14`, and regional settlement record is
schema `9`. Pending authoring epoch `13` distinguishes three-component Society
profiles and plans from the former two-component form.

Supported B14-to-B15 migration initializes Technological Knowledge from the
represented faction source and current authored plan facts at the upgrade
boundary, validates the complete result, then advances the owner or plan
version. It does not invent earlier custody, loss, teaching, institutional
retention, or research history. Existing compatible Culture and Political Order
profiles survive; former Society profiles without the third component are
discarded visibly rather than interpreted as complete snapshots.

## Acceptance boundary

B15 closes only when executable receipts demonstrate three-component Society
application and rollback, selected-society viability, exact definition mapping,
standard-mode stability, deterministic distributed custody, redundancy, loss
of an isolated carrier, recovery through represented custody, migration,
serialization/readback, current-fixture preservation, retained suites, two
byte-identical clean builds, and byte-identical deployment while RimWorld is
closed. Those receipts establish technical readiness; the operator's fresh
runtime test determines how the feature looks and plays.
