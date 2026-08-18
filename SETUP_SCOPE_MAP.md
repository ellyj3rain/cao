# Setup scope map — control → field → owner → consumer

**2026-08-05 08:58 UTC / 01:58 PDT.** Commissioned after the live screen was
judged a failed prototype. This is the audit that had to exist before any
redesign, per the operator's instruction: for every visible control, the exact
field it writes, the object that owns that field, and the generation or runtime
consumer that reads it — then the scope the control actually belongs to.

> **Status — RECORD, SUPERSEDED.** This document answered the
> wrong question. It catalogued the controls already inside
> `RegionalSetupModule` and asked which scope each belonged to — never whether
> RimWorld already owned the decision, or whether the screen should contain it
> at all. Because every control had a consumer, it concluded they merely needed
> clearer scopes, which produced four tabs over the same wrong premise
> (DR-83).
>
> **Read `START_SURFACE_MAP.md` instead.** That one starts outside CA, from
> `Scenario.GetFirstConfigPage()`, and maps each CA decision to its proper
> extension point in the real colony-creation flow.
>
> The tables below are a historical snapshot of the removed screen and its
> pre-convergence schema. They preserve the dead-control and flow evidence, but
> their type names and fields are not current implementation references.

Surface audited: `CARegionalSetupDialog.DoWindowContents`,
`Source/RegionalSetupModule.cs:2108-2766`. Scope vocabulary is the operator's:
**world → region → faction group → settlement/site → player start**.

---

## 1. The finding, before the tables

The screen does not merely fail to *label* scope. **It fails to keep scope
contiguous.** Five scopes are interleaved down one scroll column, and two of
them appear in more than one place:

| Order down the screen | Section | Scope |
|---|---|---|
| 1 | Fill quickly / Add place / Remove | region |
| 2 | Settlement rows (×N) | settlement-site |
| 3 | Faction groups (×M) | faction group |
| 4 | Relations between local factions | region (pairwise) |
| 5 | **Founding** | **player start** |
| 6 | Water — survey box | **player start** (again) |
| 7 | Water — aquifer sliders | **world** |
| 8 | World rules | **world** (again) |
| 9 | World policy — later regions | **world** (again, narrowed) |

A player cannot form a mental model from that order because there isn't one to
form. `Founding` and the water survey are the same scope separated by a section
header; the four aquifer sliders are world scope sitting *inside* a section
whose first element is a player-start report.

Three specific consequences, each verified in code rather than inferred:

**The `Founding` block is unowned on screen.** It writes `plan.founding`, a
single `CAFoundingPreset` on `CARegionalPlan`, read only by
`FoundingCompactModule` to build the relations *your own starting pawns* live
under. It is drawn immediately after the faction-group loop ends, so the
nearest visible context is the text field containing `Denisovans`. Nothing on
screen contradicts the reading that it configures them.

**`Water` contains two scopes under one heading.** The survey box calls
`CAGroundwater.SurveyTile(plan.startTileId, …)` — that is the *landing tile*,
player-start scope, and read-only. The four sliders beneath it write
`plan.groundwater`, whose own on-screen text admits they "decide what lies
under the ground everywhere in this world". One heading, two scopes, no
divider.

**`plan.worldPolicy` serves two different scopes and is split by header
alone.** `cabinShare`, `patrolsEngage`, `piersGenerate` and `caLiveliness` are
world-wide and apply to the starting region too. `populatedRegionChance`
through `maximumTechLevel` apply *only* to regions visited later. Same object,
same lifetime, no structural distinction — only which heading they happen to be
drawn under.

---

## 2. Region scope

Composition of the starting region: how many places exist and who is adjacent
to whom.

| Control | Field written | Owner | Consumer |
|---|---|---|---|
| `Fill quickly: N places · M groups` | `bases[]`, `factionGroups[]` (rebuilds both lists) | `CARegionalPlan` | `RegionalWorldModule` |
| `Add place` | appends to `bases[]` | `CARegionalPlan` | `RegionalWorldModule` |
| `Remove` (per row) | removes from `bases[]`, then `RemoveUnusedGroups()` | `CARegionalPlan` | `RegionalWorldModule` |
| Pair relation button | `CARegionalRelationPlan` in `relations[]` | `CARegionalPlan` | `RegionalWorldModule` |
| Map widget click | `bases[slot].memberTileId` via `CARegionMapWidget.awaitingSlot` | `CARegionalBasePlan` | `RegionalWorldModule`, `RegionalWorldPresentationModule`, `RegionMapWidget` |

Note the pair-relation control is region scope but is *rendered* as though it
belonged to the faction-group section it follows.

---

## 3. Faction-group scope

Who the owners are. One group may own several settlements.

| Control | Field written | Owner | Consumer |
|---|---|---|---|
| `New faction (template)` / `Existing faction` | `source` | `CARegionalFactionGroupPlan` | realised in `RegionalSetupModule:3551` region, then `RegionalWorldModule` |
| Faction picker (`group.Summary`) | template / existing-faction selection | `CARegionalFactionGroupPlan` | `RegionalWorldModule` |
| `Player: <relation>` | player relation | `CARegionalFactionGroupPlan` | `RegionalWorldModule` |
| `World faction` checkbox | `visibleInWorld` | `CARegionalFactionGroupPlan` | `RegionalWorldPresentationModule` |
| `Name` text field | `customName` | `CARegionalFactionGroupPlan` | `RegionalSetupModule:3551` → `faction.Name` |
| `Technology: … (from faction)` | *none — derived label* | — | — |

---

## 4. Settlement/site scope

One row per place. Every field here is per-settlement and none of it is
currently distinguishable from the group block above it.

| Control | Field written | Owner | Consumer |
|---|---|---|---|
| `Land: <tile>` | `memberTileId` | `CARegionalBasePlan` | `RegionalWorldModule`, `RegionalWorldPresentationModule`, `RegionMapWidget` |
| `Group N · <tech>` | `factionGroupKey` | `CARegionalBasePlan` | `RegionalWorldModule`, `RegionalWorldPresentationModule`, `RegionMapWidget` |
| `Stays on the map` | `persistent` | `CARegionalBasePlan` | `RegionalWorldModule` |
| `Shares a town` | `siteClusterKey` (`PhysicalClusterKey` derives) | `CARegionalBasePlan` | `RegionalWorldModule`, `RegionalWorldPresentationModule`, `RegionMapWidget` |
| `Starts with` | `institutionMask` (−1 = derive from tech) | `CARegionalBasePlan` | `InstitutionalBirthModule`, `RegionalWorldModule` |
| *(not drawn)* | `operationalRoleMask` | `CARegionalBasePlan` | `RegionalOperationalSiteModule`, `RegionalOverviewModule` |

`operationalRoleMask` is deliberately not authored here — the in-code comment
says roles belong to the in-play surface where sites, people and equipment
actually exist. That decision is correct and should survive the redesign; it is
listed so the field is not mistaken for an omission.

---

## 5. Player-start scope

**Currently split across two non-adjacent sections.**

| Control | Field written | Owner | Consumer |
|---|---|---|---|
| 4 preset buttons | replaces `founding`, sets `foundingAuthored` | `CARegionalPlan` | `FoundingCompactModule` |
| `Who may give binding orders?` | `commandRule` | `CAFoundingPreset` | `FoundingCompactModule`, `IdeoConventionsModule` |
| `May founders be ordered to work?` | `labourCompulsory` | `CAFoundingPreset` | `FoundingCompactModule`, `IdeoConventionsModule` |
| `Do founders have a voice from day one?` | `foundersVote` | `CAFoundingPreset` | `FoundingCompactModule`, `IdeoConventionsModule` |
| `Are supplies held in common?` | `suppliesInCommon` | `CAFoundingPreset` | `FoundingCompactModule`, `IdeoConventionsModule` |
| `Expires after N days` | `termDays` | `CAFoundingPreset` | `FoundingCompactModule` |
| Consequence card | *none — rendered from the fields* | — | — |
| Water survey box | *none — reads* `startTileId` + `groundwater` | — | `CAGroundwater.SurveyTile` |

Two things this table makes visible that the screen does not.

`IdeoConventionsModule` reads four of these fields, which is the
**ideology → charter** link: an *unauthored* founding is shaped by the CA
precepts chosen in the vanilla Ideology creator. The four questions here are
the same four axes as the precepts — authority, labour, voice, property — but
one layer down: the precepts are what the people hold *proper*, the founding is
what they have actually *adopted*. Nothing on screen says so, which is a large
part of why the block reads as arbitrary.

B2 carries that recovered relationship into the current faction ontology. The
normative side is now `CAPoliticalBeliefs`, not removed CA political precepts;
the adopted side is `CAPlayerFoundingPlan.arrangement`. The coordinated
Founding Society page shows both before play while RimWorld's native `Ideo`
continues to own Ideoligion. This paragraph records the current implementation;
the table above remains the historical evidence from which the relationship was
recovered.

The confirmed draft is stored by `CAPlayerFoundingWorldComponent`, independently
of regional geography. The exact arrangement is materialized once as founding
relations and retains its duration. It does not populate broader
`factionStructure` answers. Those fields describe institutions that exist only
after the colony has developed them through play.

`plan.startTileId` — the subject of the water survey and the place the founding
applies to — **is not editable in this dialog at all.** It is set by the
`Landing tile` control on the outer setup panel. So player-start scope is
currently spread across two separate surfaces with no cross-reference.

---

## 6. World scope

| Control | Field written | Owner | Consumer |
|---|---|---|---|
| `Salt reaches this far inland` | `saltIntrusion` | `CAGroundwaterTuning` | `GroundwaterModule` |
| `Brackish ground reaches…` | `brackishReach` | `CAGroundwaterTuning` | `GroundwaterModule` |
| `Fresh surface water…` | `highTableReach` | `CAGroundwaterTuning` | `GroundwaterModule` |
| `Rain catchment efficiency` | `catchmentEfficiency` | `CAGroundwaterTuning` | `GroundwaterModule` |
| `Frontier holdings born as lone cabins` | `cabinShare` | `CARegionalWorldPolicy` | `FrontierModule` via `CAWorldRules.CabinShare` |
| `Patrols on contact` | `patrolsEngage` | `CARegionalWorldPolicy` | `PatrolSystemModule` via `CAWorldRules.PatrolsEngage` |
| `Waterfront piers` | `piersGenerate` | `CARegionalWorldPolicy` | `MorphologyAdapterModule`, `NavalBerthModule` via `CAWorldRules.PiersGenerate` |
| `World liveliness` | `caLiveliness` | `CARegionalWorldPolicy` | `OrganizationModule` |
| Estimated world cost | *none — derived* | — | reads world population, `caLiveliness`, `regionTileCount` |

### World scope, narrowed to later-visited regions

Same owner object, different applicability. The narrowing is asserted by a
heading and one sentence of body text, not by structure.

| Control | Field written | Owner | Consumer |
|---|---|---|---|
| `Regions with additional physical bases` | `populatedRegionChance` | `CARegionalWorldPolicy` | `RegionalWorldModule` |
| `Bases with ownership distinct from neighbour` | `disparateFactionChance` | `CARegionalWorldPolicy` | `RegionalWorldModule` |
| `Ownership beyond existing world factions` | `newFactionChance` | `CARegionalWorldPolicy` | `RegionalWorldModule` |
| `New neighbouring factions in conflict` | `conflictRegionChance` | `CARegionalWorldPolicy` | `RegionalWorldModule` |
| `Generated places retained after unload` | `persistentPlaceChance` | `CARegionalWorldPolicy` | `RegionalWorldModule` |
| `Minimum` / `Maximum additional bases` | `minimumAdditionalBases`, `maximumAdditionalBases` | `CARegionalWorldPolicy` | `RegionalWorldModule` |
| `Later-faction technology cap` | `maximumTechLevel` | `CARegionalWorldPolicy` | `RegionalWorldModule` |

---

## 7. Dead controls

**None.** Every visible control writes a field that some consumer reads. An
earlier pass suspected `patrolsEngage` and `piersGenerate` were orphaned; they
are not — they are read through the `CAWorldRules` static facade, which the
first grep excluded because the facade lives in the same file as the toggles.
Recorded because the redesign must not delete them as dead.

The failure here is **not** controls without consumers. It is controls without
*visible subjects* — which is the harder problem, because nothing in the code
is wrong.

---

## 8. Naming and history — the gap

Requested alongside this audit. Current state, precisely:

**Faction names.** `group.customName` is a bare `Widgets.TextField` applied as
`faction.Name`. When left empty, native generation names the faction and the
player never sees the result. There is no generator and no reroll, even though
`FactionDef.factionNameMaker` is exactly what vanilla uses for this.

**Settlement names.** These *are* generated — `GenerateSettlementName`
(`RegionalWorldModule:1027`) uses `faction.def.settlementNameMaker` with the
already-used names passed in for uniqueness, falling back to
`"<faction> <n>"`. But this happens at world generation, after the dialog
closes. The player places settlements they cannot name, cannot see the names
of, and cannot reroll.

**History and backstory.** Nothing exists at any scope. There is no faction
history, no settlement founding history, and no equivalent of vanilla's pawn
backstory for a place. This is the same gap the roadmap already names as *NPC
settlement synthesis* (ideology + technology + geography + economy + history →
mature orders), so it should be designed there rather than bolted onto the
setup screen.

Where each belongs under the scope model: faction name and its reroll in the
**faction-group** inspector; settlement name and its reroll in the
**settlement-site** inspector; colony name in **player start**; generated
history surfaced read-only in whichever inspector owns the object.

---

## 9. What the map implies for the rebuild

Stated as constraints the redesign must satisfy, not as a layout.

1. Every scope must be **contiguous and named**. Player start currently appears
   in two places and world in three.
2. Every section states its subject explicitly — an "Applies to" line — and the
   subject must be an object the player can point at.
3. Selecting an object on the map opens the inspector for that object and only
   that object's decisions; selecting nothing shows region-level composition.
4. `plan.startTileId` must be reachable from the player-start scope, not
   stranded on the outer panel.
5. The `worldPolicy` split between "everywhere" and "later regions only" needs
   to be structural, since one object currently carries both.
6. The founding block must state that it is the **adopted charter** and that an
   unauthored one is shaped by the ideology precepts — the `IdeoConventionsModule`
   dependency is real and currently invisible.
7. `operationalRoleMask` stays unauthored at setup. That was a deliberate
   decision and the audit confirms it holds.

Open question for the operator, not resolved here: whether the four founding
questions remain one block, or are read against the matching precept on each
axis so the profess-versus-practice gap is visible at setup. That is a design
decision about the four-layer architecture, not a layout choice, and it changes
what the inspector is for.
