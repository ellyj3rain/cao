| Document | Colonist Awareness Political Architecture — superseded design record |
|---|---|
| Version | `0.2.2` |
| Historical batch | `A85` |
| Design authority | ellyj3rain |
| Repository | `POLITICAL_ARCHITECTURE_RATIFICATION.md` |
| Status | **RECORD — SUPERSEDED.** Preserved as the design history that preceded the current faction and settlement model. It is not an implementation contract. |
| Substrate baseline | The six-test assessment was made against source **through `7f63b2f`**. Implementation begins after that commit and does **not** retroactively alter the baseline: the scorecard records what the substrate could express before the ratified work started. |
| Current implementation | Factions own culture, Ideoligion, political beliefs, faction structure, and settlement authority. Federation membership has one relation representation. The projection caches and pre-release migration described below were removed. See `ARCHITECTURE.md`, `SETTLEMENT_SYNTHESIS_MODEL.md`, and `SESSION_STATE.md`. |

# Political architecture — superseded design record

The remainder of this file records the earlier proposal in its original terms.
Current terminology and contracts live in the canonical documents linked above.
References below to the retired object model, projection caches, and migration
steps are historical statements, not current requirements.

> **Historical status.** These contracts were ratified for the former design.
> The convergence pass replaced that design with the current faction and
> settlement model.
>
> The six-test scorecard is a **historical measurement of the pre-work
> substrate**, taken against source through `7f63b2f`. Step 0 changes what the
> substrate can express; it does not change what the scorecard recorded.


**What rev 5 left broken.** Federation membership had two truths, because
`memberOrgKeys` remains persisted and defines `IsFederation` while `CARelation`
can express the same fact. Rule revisions were called immutable and then mutated
via `effectiveTo`. Provenance and revisioning were specified separately and never
composed, so nothing said whether removing a seed could alter enacted history. A
`Complete` Offices surface was treated as proof of no executive, though a body
may hold one. `SequenceUnspecified` injected an unknown into a total order.
And the practicability formula still accepted *access to a written repository* as
equivalent to a knowledgeable practitioner.

---

## 1. Federation membership and delegation

### The contradiction, run

| State | |
|---|---|
| `Confederation.memberOrgKeys` | `[ ProvinceA ]` |
| `CARelation` | `{ party=Org:ProvinceB, org=Org:Confederation, roleDef=Constituent }` |
| ProvinceA | no constituent relation |

**Who are the members?** Today, both answers are defensible and they are
*disjoint*. `IsFederation` is true because `memberOrgKeys` is non-empty
(`OrganizationModule.cs:874-877`). The federation obligation logic at
`OrganizationModule.cs:1324-1336` iterates `memberOrgKeys` and would process
**ProvinceA only**. A relation-based query returns **ProvinceB only**. Nothing
reconciles them, and no consumer is aware the other exists.

### Chosen contract: the typed constituent relation is canonical

```
CARoleDef: Constituent                              // Def-backed, extensible

membership(F) ≝ { M : ∃ CARelation { party = Org:M, org = Org:F,
                                     roleDef = Constituent,
                                     not expired at T } }

memberOrgKeys(F)  ≝ derived from membership(F)      // migration cache only
IsFederation(F)   ≝ membership(F) ≠ ∅               // same query, not a second one
```

**Why membership is a `CARelation` fact and not a string list.** Membership
already carries terms — `entry`, `exit`, `sunsetTick`, `cededDomains`,
`heldDomains`, `voice`, `protection`, `origin`. A province that joined under
duress, on a 15-day sunset, ceding only Defence, is a relation. A bare string in
`memberOrgKeys` can carry none of that, so the moment any of it matters the
relation must exist anyway — and then two records assert membership.

### Corrected `delegatedDomains` semantics

- The union means **"delegated by at least one member."**
- It does **not** grant federation-wide authority over every member.
- **Authorization is scoped to the specific member → federation relation.**
  `relation.Cedes(domain)` on *that* relation is the only authorization test.
  `CARelation.Cedes` already exists (`PoliticalPrimitivesModule.cs:245`) and is
  already used this way by `TaxationModule.cs:70` and
  `ConvictionPracticeModule.cs:189,264`.
- The aggregate is **display and diagnostic only**, and must never be consulted
  for authorization.

### Evidence that a hand-written aggregate drifts

`OrganizationModule.cs:5865` writes the literal `"defense"`. `CADomains.Defence`
is `"defence"` (`PoliticalPrimitivesModule.cs:124`). Everywhere else the
codebase correctly uses the constant — `FoundingCompactModule.cs:398,579,609`.

So the one hand-written delegation aggregate in the repository **has already
drifted from the constant catalog**, and no `CADomains`-based query can ever
match it. It is currently **latent, not live**: only the display line at
`OrganizationModule.cs:4610` reads `delegatedDomains`, and it prints the string
rather than resolving it. The moment authorization consulted the aggregate, a
federation founded through that path would silently hold no delegated defence at
all.

This is the failure mode a derived projection removes by construction, and it is
concrete rather than hypothetical. *(Recorded as **F-132** and closed for the
source path by the Step-0 implementation: creation now uses `CADomains.Defence`,
the aggregate has no production writer, and a validator asserts every projected
domain is in `CADomains.All`. Never run in game.)*

### Migration cost — "one line" withdrawn

| | `delegatedDomains` | `memberOrgKeys` / `IsFederation` |
|---|---|---|
| **Read consumers** | 1 — display (`4610`) | 6 sites — federation obligations (`1324, 1326, 1334, 1336`), display (`4609`), query (`5023`) · **plus `IsFederation`, 12 consumers** (`758, 1321, 1389, 1514, 1538, 4497, 4508, 4606, 4615, 5022, 5836`) |
| **Writers to remove** | 1 (`5865`) | 2 (`5862, 5864`) |
| **Legacy load** | scribed (`958-959`) + null guard (`982-983`); keep reading, synthesize constituent relations on `PostLoadInit` | scribed (`956`) + null guard (`980-981`); same |
| **Recomputation** | on any write to a constituent relation | same query, same trigger |
| **Validation** | assert no writer outside the projector; assert every element is a `CADomains` constant — this check alone would have caught `"defense"` | assert no writer outside the projector |
| **Cache policy** | scribe as cache; rebuild on load; never trust across a schema change | same |

**Total: 7 read sites, 3 writers, 12 `IsFederation` consumers, plus a
load-time synthesis path.** The earlier "one line" counted only the display
consumer of `delegatedDomains` and ignored membership entirely.

---

## 2. Immutable revision content, append-only activation

`effectiveTo` was mutable state on a record called immutable. Removed.

```
CARuleRevision  { revisionKey, lineageKey, contentHash, …content… }   // IMMUTABLE
                                                                      // never edited, never deleted
CARuleEnactment { id, lineageKey, revisionKey, effectiveFrom, byActId }   // append-only
CARuleRepeal    { id, lineageKey, effectiveFrom, byActId }                // append-only
```

```
operative(lineage, T):
    E = latest CARuleEnactment for lineage with effectiveFrom ≤ T
    R = latest CARuleRepeal    for lineage with effectiveFrom ≤ T
    if E is null                      → none
    if R exists and R.effectiveFrom > E.effectiveFrom → none (repealed)
    return E.revisionKey
```

Supersession is no longer written *into* the superseded record — it is the
existence of a later enactment. Nothing mutates.

**Which revision a new act gets.** At **proposal**, resolve
`operative(lineage, proposedAt)` and **store the `revisionKey` on the act**. The
stated contract: an act is judged under the rules in force *when it was
introduced*, so an amendment mid-passage does not retroactively change the
procedure it is passing under. (Resolving at enactment instead is a legitimate
constitutional variant; it would be a per-lineage flag, and is not proposed now.)

**Historical acts** retain the stored `revisionKey` permanently, and revision
content is immutable, so the historical query is exact rather than reconstructed.

**Deletion is prohibited** for any revision referenced by an act, mandate or
action.

### `CAActStageOutcome.bodyKey` removed

`stageKey` plus the act's `procedureRevisionRef` resolves the stage, and the
stage carries `bodyKey`. Keeping a copy on the outcome would be a second truth
able to disagree with the first — the same defect as `delegatedDomains` in §1.
Removed rather than reclassified as a cache, since no query needs it.

### Exact revision references carried

| Record | References |
|---|---|
| `CAAct` | `procedureRevisionRef` |
| `CAMandate` | `selectionRevisionRef`, `tenureRevisionRef` |
| `CARemovalAction` | `removalRevisionRef` + `procedureRevisionRef` of the procedure that carried the removal |
| `CAReviewAction` | `jurisdictionRevisionRef` (+ `actId`, already required) |
| `CAAmendmentAction` | `constraintRevisionRef` + `procedureRevisionRef` of the amendment procedure |

---

## 3. Provenance and revisioning composed

**The boundary, stated:**

> **Projection composes DRAFTS — the current authoring state.
> Enactment MATERIALIZES immutable historical revisions.
> Provenance operates only on drafts.**

An enacted revision is a **snapshot**, not a view. It has no contribution rows,
and nothing in the provenance layer references it.

### The frozen-history test, run

| Step | Effect |
|---|---|
| Preset A and archetype B contribute to `draft(Statute)` | contribution rows in the draft layer |
| Enacted as v1 | draft is **materialized**: `CARuleRevision v1` (content copied, `contentHash` computed) + `CARuleEnactment{ v1, effectiveFrom }` |
| Act X references v1 | `Act X.procedureRevisionRef = v1` |
| B removed | B's contribution rows dropped **from the draft layer only** |
| Regeneration | the draft re-projects without B's stage |

| Required result | Held? |
|---|---|
| v1 remains immutable and queryable for Act X | **Yes** — v1 is materialized content; no projection touches it |
| The projected current draft changes | **Yes** — B's contribution is gone from the draft |
| A newly enacted form becomes v2 | **Yes** — the next enactment materializes a new revision; v1 is never overwritten |
| Removing B cannot alter or delete v1 | **Yes** — v1 has no contribution rows, and deletion of a referenced revision is prohibited |

### Deterministic ordering on equal ranks

Order elements by:

```
(rank ASC, contributorPrecedence ASC, elementKey ASC)
```

Element keys are unique within a field, so the comparator is total and ties are
impossible. Two seeds contributing the same rank resolve by precedence; two
contributions from one seed at one rank resolve by element key. The result is
stable across regenerations and independent of enumeration order.

---

## 4. Query-aware completeness

A `Complete` Offices surface cannot prove "no executive exists" if a `CABody`
may also hold executive authority.

**The asymmetry, stated:** *existence needs one witness; non-existence needs
closure over every surface through which the fact could hold.*

```
CADerivedFactSpec { factKey, dependencySurfaces[] }

noExecutive(polity)  dependencies = { Offices, Bodies, AuthorityEdges }
```

```
answer(fact, polity):
    if a witness exists in ANY dependency surface   → Yes, it exists
                                                       (completeness irrelevant)
    else if every dependency surface is Complete    → No
    else                                            → Unknown
```

### The test, run

| State | Answer |
|---|---|
| Offices `Complete`, zero offices · Bodies `Unknown` · a body may hold `Decision` | **Unknown** — Bodies is a dependency and is not Complete |
| Then Bodies `Complete`, AuthorityEdges `Complete`, no executive holder in any surface | **No** |

A semantic surface (`AuthorityPositions`, spanning offices, bodies and their
authority edges) is a legitimate optimization for frequent queries, but the
dependency set is the general mechanism and is what makes the rule checkable.

### Enumerated versus asserted closure

```
CACompleteness { …, certification: Enumerated | Asserted }
```

- **`Enumerated`** — produced by a transaction that actually walked the surface.
  Provable; queries may answer from it silently.
- **`Asserted`** — an operator claim with no enumeration. Stored **separately in
  kind** and **reported separately**: a query answering from an asserted closure
  must say so — *"No — asserted, not enumerated."* It is never silently
  equivalent to enumeration.

A complete creator transaction (§rev 5) certifies `Enumerated`. A bare assertion
is representable but self-identifying.

---

## 5. Coarse temporal bounds

`SequenceUnspecified` is **withdrawn**. It inserted an unknown into a total order
by sorting before real events, which is a manufactured answer.

```
CATemporalBound = Exact( CAHistoricalTime )
                | CoarseYear( phase, yearsBeforeArrival )
```

Comparison rules:

| Comparison | Result |
|---|---|
| `CoarseYear(Y)` vs an exact event in a **different** year | determinate |
| `CoarseYear(Y)` vs an exact event in the **same** year | **Indeterminate** |

`CAHistoricalTime` remains a total order over *events*. Bounds are a separate
type and are not inserted into it.

### The test, run

Mandate begins `(Pregame, year −10, sequence 3)`; duration `{ years: 1 }`.
Resolved end: `CoarseYear(Pregame, −9)`.

| Event | Mandate active? |
|---|---|
| year −9, sequence 0 | **Indeterminate** |
| year −9, sequence 5 | **Indeterminate** |
| year −10, sequence 7 | **Yes** — determinate, before the bound's year |
| year −8, any sequence | **No** — determinate, after the bound's year |

Both year −9 events fall in the bound's own year, and calendar precision cannot
order them against it. The answer becomes determinate when an **actual expiry
event** — a succession, a dissolution, a deposition — supplies a real sequence,
at which point `CAMandate.end` becomes `Exact`.

**Explicitly rejected:** a start-of-year or end-of-year convention. It would make
every such query answerable and silently wrong at the boundary, which is exactly
the class of false precision this revision removes. Indeterminacy is confined to
the boundary year, which is the honest extent of the uncertainty — and in
practice most mandates end with a real event anyway.

---

## 6. Repository media and the practitioner lifecycle

**Withdrawn:** the formula term accepting *access to a written repository* as
equivalent to a knowledgeable practitioner. Reading is not knowing.

### Medium capabilities

| Medium | Consult | Copy | Teach | Practice |
|---|---|---|---|---|
| **Written** | ✔ | ✔ | ✔ | only under an explicit consultative-execution rule for that project |
| **Oral** | — | ✔ (recitation) | ✔ | — |
| **Embodied** | — | ✔ (by teaching) | ✔ | **✔** |
| **Artifact** | ✔ | ✔ (reverse-engineering, per project rule) | — | only under an explicit consultative-execution rule |

**Teaching from an accessible repository creates an embodied repository in a
pawn** — a `CAHistoricalEvent{ kind = teaching }` whose `consequences[]` name the
new `CAKnowledgeRepository{ medium = Embodied, pawnId }`. Vanilla `Pawn.skills`
then gate practical ability. Copying creates a new repository of the target
medium; neither creates a new holding.

### Four separated derived reads

```
extant(project)           ⇐ ≥1 surviving repository anywhere in the world
possessed(polity, proj)   ⇐ ≥1 surviving repository under this polity's custody
accessible(site, proj)    ⇐ a possessed repository reachable from site
                             under a CAAccessGrant
practicable(site, proj)   ⇐ ∃ pawn at site holding an EMBODIED repository for proj
                             (or Written/Artifact plus a consultative-execution rule)
                          ∧ that pawn's vanilla skills meet the project requirement
                          ∧ an operational CAFacilityHolding of the required kind
                          ∧ CASupplyAccess covers the materials
```

### The lifecycle, re-run

| Stage | extant | possessed | accessible | practicable |
|---|---|---|---|---|
| Archive survives, specialists die | ✔ | ✔ | ✔ | **✘** |
| A skilled but untaught pawn can read the archive | ✔ | ✔ | ✔ | **✘** — reading is not knowing |
| Teaching occurs → embodied repository created | ✔ | ✔ | ✔ | ✘ while facility or supply are absent |
| Facility and supply become available | ✔ | ✔ | ✔ | **✔** |

**Before teaching:** knowledge is extant, possessed and accessible — and not
practicable. **After teaching:** an embodied repository exists and practice
becomes possible once the material conditions are met. High Intellectual does not
substitute for having been taught; it gates whether the teaching *takes* and
whether the practice succeeds.

---

## 7. Scorecard

**No verdict changes.** Rev 6 corrects the *proposal*, not the substrate; no
verified fact about existing records changed.

| Test | Rev 5 | **Rev 6** |
|---|---|---|
| 1 Bicameral republic | FAIL | **FAIL** |
| 2 Parliamentary monarchy | FAIL | **FAIL** |
| 3 Absolute monarchy | FAIL | **FAIL** |
| 4 One-party state | PARTIAL | **PARTIAL** |
| 5 Confederation | PARTIAL | **PARTIAL** |
| 6 Market socialism | PARTIAL | **PARTIAL** |

One clarification on **Test 5**. §1 shows federation membership has two
competing truths today. That is a **duplicate-truth defect, not a
representability failure**: a per-member cession *is* expressible now, because
`CARelation.partyOrgKey` admits an organization as a party. The confederation
facts that passed still pass; what §1 adds is that a second system can contradict
them. Test 5 stays PARTIAL.

---

## 8. Remaining implementation falsifiers

- **Constituent relations cost more than they save.** If synthesizing relations
  for every legacy `memberOrgKeys` entry on load, plus repointing 12
  `IsFederation` consumers, exceeds the cost of simply forbidding one of the two
  systems by convention, the derivation is over-engineered. *Test at step 0 by
  prototyping the load-time synthesis against a saved federation.*
- **Enactment snapshots duplicate storage.** Materializing every revision copies
  content that a draft projection could reproduce. If revisions are large and
  numerous, store a content hash plus a contribution-set reference instead — but
  only if replay is provably deterministic, which the §3 tie-break is designed to
  make true. *Test at step 3.*
- **Proposal-time revision binding is the wrong constitutional rule.** If acts
  are commonly expected to be judged under the rules at *enactment*, the default
  is backwards. *Test by encoding a real amendment-during-passage case.*
- **Dependency sets are unmaintainable.** If every new derived fact needs a
  hand-authored surface list that goes stale, completeness queries will quietly
  answer from incomplete dependency sets. *Test at step 6 by counting derived
  facts and checking whether the list can be generated rather than authored.*
- **Indeterminate is too common.** If most mandates end without an expiry event,
  boundary-year queries will routinely answer Indeterminate and consumers will be
  tempted to invent a convention. *Test at step 1 by measuring how many generated
  mandates terminate with a real event.*
- **Consultative execution is the common case.** If most projects can in fact be
  performed from a written reference by a sufficiently skilled pawn, the
  embodied-repository requirement inverts the default and should be opt-out
  rather than opt-in. *Test at step 7 against a sample of `ResearchProjectDef`s.*
- **Option A's inert fields**, **revision explosion**,
  **`CASelection`/`CATenure`/`CARemoval` merge**, **projection cost**,
  **qualification records**, **research seeding**, and **recognizer
  distinguishability** — all carried unchanged from rev 5 §8.

---

## 9. Unchanged and still proposed

Position-mediated authority as the central diagnosis (rev 4 §1) · Option A,
generalizing `CARelation.partyRef` to Pawn | Organization | Office | Body
(rev 4 §2, rev 5 §1) · `CAMandate` and `CASeat` with `CAOffice.holderId` demoted
to a derived cache (rev 4 §3) · representation and selection as separate axes,
with the hereditary territorial chamber proof (rev 4 §4) · the repaired
act/review graph and its bicameral and parliamentary encodings (rev 4 §5) ·
Test 4's restricted-nomination graph (rev 4 §10) · the duration precision
contract (rev 5 §5) · the sequence in rev 5 §8, with rule-revision infrastructure
before any rule type is consumed · the horizontal band creator with mechanisms
rather than dropdowns · three non-agreeing outputs · procgen through the same
substrate · four surviving precept beliefs · external reuse narrow, with the
author-utilization record written only when a donor and a specific mechanic are
actually selected.
