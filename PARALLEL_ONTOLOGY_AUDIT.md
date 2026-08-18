# Parallel-ontology audit — current verdicts

Which CA records duplicate an ontology RimWorld already owns, and which are
legitimate extensions. Governed by DR-86: when the game already represents the
thing, extend that system rather than creating a parallel abstraction.

Two tests apply to every candidate:

1. **Is it authoritative or derived?** A derived cache read back from live
   engine state is an extension. A frozen value read for decisions is a parallel
   ontology.
2. **Does it only DESCRIBE the past, or does it PRESCRIBE what the world should
   become?** A record that commands reconstruction is not merely history.

## Verdicts

| Record | Classification | Verdict |
|---|---|---|
| `CAOffice` | derived cache | reads native `Precept_Role` — correct extension |
| `populationCurrent` / `residentIds` | derived cache | counted from real Pawns — correct |
| colony↔neighbour `stance` | derived cache | refreshed from the live engine relation — correct |
| building census | derived cache | correct |
| `relationAtMaterialization` / `goodwillAtMaterialization` | historical record | legitimate; the game keeps no such history |
| `CAClaim` | derived display projection | technically clean; **terminology hazard** |
| **neighbour↔neighbour `stance`** | **authoritative** | **genuine parallel ontology — a frozen snapshot read for decisions** |
| `researchStock` / `researchMilestones` | isolated | bypasses the research ontology; contaminates nothing |
| `techLevelResolved` / `authoredTechLevel` | authoritative | must be derived (DR-86) |
| capability vector (8 ints) | authoritative | must be derived from material supports (DR-85) |
| `CAFacilityHolding` | isolated | latent hazard |
| `treasury` | CA-native ledger | **partially** conserved — initialization and levy unresolved |
| `seededAssets` | **live-validated reconstruction plan** | prescriptive, not historical |

## The findings that carry consequences

**neighbour↔neighbour `stance`.** The only outright parallel ontology found. A
frozen snapshot is read when deciding, while the engine holds the live relation.
Must be repointed at the live relation or justified as a knowledge limit — an
institution acting on what it last knew is defensible, an institution acting on
a stale value by accident is not.

**`seededAssets`.** Every consumer validates against the live map, which
prevents ghost duplication, but validation does not make the record passive:
`TryRebuild` reads the birth record, finds the object absent, and recreates it.
That gives the record causal authority over the settlement's future composition
— a persistent reconstruction template. Unanswered by the code: whether the
asset was destroyed accidentally or demolished deliberately; whether it was
moved or replaced by something better; whether it was captured; whether
rebuilding consumes real resources and labour; whether the settlement still
holds the research, personnel, policy and need for it; whether a founding asset
should remain obligatory fifty years later; and whether institutional change can
amend or retire the template. Unanswered, it preserves a settlement's birth
state indefinitely and fights that settlement's own later development.

**`treasury`.** Legitimate as a CA-native ledger; conservation is only partial
because initialization and levy are unresolved. Repointing `record.production *
150` at whatever derived production score replaces the capability vector would
not fix it — a summary generating wealth is the defect. A starting endowment is
legitimate at world generation, but it must come from a persisted economic fact:
stored silver, prior trade, taxation history, productive surplus, or a material
aggregate.

**`CAClaim`.** The word is the hazard. "Claim" reads as territorial or legal
ownership; the underlying facts are RimWorld's designated **home area** and
**growing zones**. That gap must stay explicit before any political or ownership
consumer is attached.

**`researchStock` / `researchMilestones`.** Declared in `RegionalWorldModule`,
written only in `InstitutionalBirthModule` — incremented when a settlement has a
live `SimpleResearchBench` and one of its own pawns stands within five cells; at
25, 75 and 150 it spawns one hardcoded Thing. Proximity to a bench counts as
research; the rewards are unrelated to any unlock graph; knowledge exists only
as accumulated stock until it becomes an item; it cannot express prerequisites,
partial progress, techprints, divergent paths or persistent societal knowledge;
and possessing or losing the spawned object says nothing about whether the
society can reproduce it. Absorb when per-society research state lands under
DR-86a. Not urgent, not systemic.

**`techLevelResolved` / capability vector.** Both must become derived —
technology from persistent research state (DR-86), capability from material
supports (DR-85). Capability is a computed summary or cache, never an
independent cause.

## Not yet audited

- `CAConvention` against `PreceptDef` — distinguishing individual ideology,
  institutional doctrine, learned custom and tactical convention
- knowledge against native memory and knowledge-bearing objects
- policy and security practice
- `legitimacy` under the separate unsupported-causality test
