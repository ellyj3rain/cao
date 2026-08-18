# Onboarding information architecture — historical record

Status: **RECORD, SUPERSEDED**

Current contract: DR-91 and `SETTLEMENT_SYNTHESIS_MODEL.md`.

This document records an earlier design. Its terminology, scopes, and control
model are not implementation requirements.

Historical screen-level IA for the former pre-landing surfaces. The corrections
below were binding for that design. They no longer govern implementation.

## Former binding corrections

1. **No duplicate expanse/extent axis.** The geographic control is the
   **constituent-tile expanse** (Exact / Range / FitAvailable). **Map resolution**
   (cells per source tile) is independent. **Backing-map dimensions are derived** —
   never a third player axis.

2. **Level 2 is not a literal generator call order.** History is **upstream** of
   present material state. The ratified causal relations, preserved exactly:
   - clustering **distributes** authorized major settlements, never creates them;
   - **frontier density** produces holdings over *suitable opportunities*, and
     **composition follows** once holdings exist;
   - **metropolitan development is conditional** on population, clustering,
     geography, infrastructure, economy and history.

3. **Population authority is settled.** Vanilla `OverallPopulation` remains
   authoritative for world-level major-settlement abundance. CA may **spatially
   distribute and concentrate** that authorized pool — a region's count may vary
   substantially — but clustering **does not create** settlements. Manual
   major-settlement edits in the starting region are therefore either
   **reallocations from that pool** or an **explicit scenario override**.

4. **The population screen consumes the stable candidate regional projection**, not a
   landing-bound preview. Changing `startTileId` within the same footprint changes
   **entry state only**: move marker/focus; do not rebuild or reroll regional
   geography. The candidate projection is what both the landing page and the
   population screen visualize.

5. **Research is not per settlement.** Persist research on the **polity/institution
   that owns the knowledge**. What a settlement materially has access to is **derived**
   from that state plus its real infrastructure. Do not recreate technology as a
   settlement slider.

## Screen 1 — World generation

**Level 1 — region incidence and scale (always visible)**
- stitching incidence: exact / world-seeded range
- realized frequency: *displayed*, persisted per world once rolled
- constituent-tile expanse: exact / range / fit-available
- starting-region override: world rule / force stitched / force isolated
- map resolution (cells per source tile), independent of expanse
- backing-map dimensions: derived readout only

**Level 2 — inhabitation (expander).** Presented as ratified causal relations, with
history upstream of present material state:
- historical-turnover prior (upstream)
- cluster incidence and weighted cluster-scale distribution — *distributes* the
  authorized pool
- frontier density over suitable opportunities → frontier composition once holdings
  exist
- conditional metropolitan development (population, clustering, geography,
  infrastructure, economy, history)

**Level 3 — budget and projections (expander)**
- World Liveliness = **off-map computation budget**
- independent projections: regional cell count, off-map simulation load, persisted
  object count. **No combined formula.**

## Screen 2 — Starting-region population

The **region is the ontology**, not a record table. Opens on the *same candidate
regional projection the landing page uses* — members, seams, landmarks, roads,
historical sites, water — with inhabitation drawn on it. Add / remove / move / reroll
are operations on objects in that geography.

Zero settlements is **not** an empty form: geography, biome mix, landmarks, historical
sites, roads, water and surrounding population context remain legible.

Selecting an inhabited place opens, in place: faction identity and name, settlement
name, form, districts vs independent settlement, relationships, generated history,
material state, institutions, observable physical consequences. Research access is
**derived** from the owning polity's state and real infrastructure (correction 5).

## Removed — do not reintroduce under new labels

| removed | why |
|---|---|
| `population × liveliness × extent` cost formula | rejected performance model |
| World Liveliness as "% of societies evolving" | it is a computation budget |
| `cabinShare` as a global frontier-incidence slider | composition is conditional on holdings existing |
| "0–2 additional bases when a later region is populated" | clustering never creates settlements |
| `Later-faction technology cap` as a world axis | research belongs to the owning polity |
| `Patrols on contact` / inherited security posture | tactical doctrine, not world generation |
| "Generate waterfront piers" toggle | derived from waterfront/infrastructure/economy |
| `persistent` (places retained after unload) | implementation knob |
| accumulating "advanced" drawer | contents redistributed by decision level |

## Implementation order

1. **Engine-root decoupling** — `Find.GameInitData.startingTile` is currently set to
   the landing tile (`RegionalSetupModule.cs:1760`), making `map.Tile == startTileId`
   and every root-bound socket landing-bound. Correction 4 depends on this.
2. **Landing-swap harness** — same candidate + footprint + different landing tile must
   produce identical regional fact and terrain hashes.
3. Screen 1 rebuild against Level 1/2/3.
4. Screen 2 rebuild against the candidate projection.

Mountain spatial magnitude (footprint-relative spread) is tracked **separately** and is
not part of the landing invariant.
