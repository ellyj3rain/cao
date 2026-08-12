# Prior-art traces

One current record of every external implementation CA has investigated. It
replaces the scattered state where an audit's absence from the repository was
mistaken for the audit not having happened.

**Evidence basis is stated per entry and never inflated.** Where a fact was
established by reading source or an assembly, it is marked *verified*. Where it
rests on an investigation whose report was not consolidated here, it is marked
*operator-supplied trace* and the unverified boundary is named. Nothing in this
file is reconstructed from inference.

**Levels** — audit completed · source/assembly traced · adaptation designed ·
code ported · code integrated · runtime verified. **No entry below has reached
"code ported."**

---

## Simple Warrants

`pb3n.SimpleWarrants` — pb3n and Taranchuk. Installed at Steam workshop
`2676828755`; the workshop copy ships **32 `.cs` source files** across 1.3/1.4/
1.5/1.6 trees, so this trace is against real source.

**Status: implementation audited; adaptation architecture ratified; zero CA port
implementation.** `grep -rl "Warrant" Source/ Defs/` returns nothing.

*Verified structure* (read from the shipped source):

- `public abstract class Warrant : IExposable, ILoadReferenceable` (`Warrant.cs:13`)
- Target subtypes: `Warrant_Pawn`, `Warrant_Artifact`, `Warrant_TameAnimal`
- `WarrantsManager : GameComponent` — manager collections
- `WarrantRequestComp : WorldObjectComp` — per-world-object request state
- Target-site generation: `SitePartWorker_Pawn`, `SitePartWorker_ArtifactStash`,
  `GenStep_Camp`, `GenStep_Animal`
- Raid integration: `IncidentWorker_Raid_TryGenerateRaidInfo_Patch`,
  `RaidStrategyWorker_MakeLords_Patch`,
  `IncidentWorker_RaidEnemy_GetLetterText_Patch`
- Pursuit / downed / kidnap behaviour: `JobGiver_AIFightEnemy_Patch`,
  `Pawn_Kill_Patch`, `Faction_Notify_MemberTookDamage_Patch`
- Delivery and exit: `TransportersArrivalAction_ReturnWarrant`,
  `Settlement_GetTransportersFloatMenuOptions_Patch`,
  `FormCaravanComp_CompTick_Patch`
- Failure: `QuestNode_WarrantFailed`
- Player-direction surfaces: `MainTabWindow_Warrants`, `WarrantsTab`,
  `Dialog_SelectPawn` / `Dialog_SelectArtifact` / `Dialog_SelectAnimal`

*Traced lifecycle*: acceptance, expiry, postponement, delivery, completion and
failure; player-relative reward, goodwill, issuer-selection and map assumptions.

*Ratified adaptation boundary* — **preserve** the pursuit, downed-target
kidnapping, exit behaviour, target-site generation and delivery machinery;
**parameterize** the player-relative reward, goodwill and issuer-selection
assumptions; **replace** the incident-based dispatch with CA's own mandate
registry, same-map inter-settlement dispatch and jurisdiction. That replacement
architecture is ratified and unbuilt.

*Unresolved*: **licence position.** No `LICENSE` file is present in the workshop
copy. No port may proceed until the terms are established.

---

## Law and Order

*Evidence basis: operator-supplied trace. Not installed on this machine; no
consolidated report in the repository; class-level facts were not independently
verified here.*

**Status: source-traced; obligation-lifecycle patterns extracted; zero CA
implementation.**

Traced surface: pawn-attached debt records; owed versus overdue as distinct
states; debt state before and after a proceeding; continuing payments from a
stated source; satisfaction as an event distinct from the debt reaching zero;
enforcement retries that preserve the failure reason.

*What it established*: a working obligation lifecycle worth adopting — in
particular that satisfaction is an act with a record, not a balance condition,
and that a failed enforcement attempt must keep its reason.

*What it did NOT establish*: a complete civil court, legal representation,
jurisdiction, or collection system. CA's requirements in those four areas have
no prior art behind them and are original design.

---

## Economic stack

Audited under DR-87, which bars CA from inventing further treasury, business,
payment or market machinery until this audit closes. Organised by economic
function, not by mod. First pass: `ECONOMIC_FUNCTION_AUDIT.md` (13 functions).
Second pass: `ECONOMIC_IMPLEMENTATION_AUDIT.md`.

### RimBank
`github.com/emipa606/RimBank` — user19990313, continued by emipa606 and Bar0th.
**MIT.** *Evidence basis: second-pass implementation audit in the repository.*
Traced across native state and lifecycle, exact mod classes and data flow, CA
state and data flow, overlap, semantic incompatibilities, on-map behaviour,
off-map representation, save/load reconciliation and permission scope.
**Verdict: split by layer** — RimBank is the medium (banknotes, denominations,
ATM exchange, mixed settlement, trader acceptance, change); CA is the ledger
(obligation and account). Adaptation designed; not ported.

### Empire (Empire Refactored / Empire-1_6-Continued)
`github.com/matathias/Empire-1_6-Continued` — Saakra; refactored by Matathias,
Epistatic and others. **GPL-3.0**, and the reason CA is GPL-3.0.
*Evidence basis: first-pass function audit plus the CREDITS trace.*
`FactionFC` is a `WorldComponent` carrying a tax ledger and settlement comps —
the only located implementation that runs a settlement economy while its map is
unloaded. **Adaptation ratified, not begun**: take the off-map settlement
economy machinery, replace the player-centric political shape with CA's
organizations, customs, and political-belief effects, so a levy becomes an act
judged as taxation or requisition.

### Hospitality / Storefront / Gastronomy
Orion (OrionFive); Storefront continued by tomvd. Hospitality is GPLv3 code with
CC BY-SA 4.0 art. *Evidence basis: first-pass function audit.* Traced for the
demand side CA has no model of (visitors
and spending), the point-of-sale surface (staffed shop, sale area, register,
item transfer, silver payment), and restaurant operation (menus, prices, hours,
waiters, paid meals). Division of labour designed: they run the business, CA
records who owns it, who operates it, on what terms, and who the proceeds are
for. Not ported.
**Unresolved**: Gastronomy's repository carries conflicting licence signals — a
no-derivatives README statement alongside a GPL marker. Must be resolved before
any use beyond reference.

### Yayo's Bank
*Evidence basis: operator-supplied trace; no consolidated report in the
repository.* Investigated as banking prior art alongside RimBank and VTE. No
adaptation designed, nothing ported. The specific findings were not consolidated
and are not reproduced here rather than guessed at.

### Vanilla Trading Expanded
**CC BY-NC-ND.** *Evidence basis: first-pass function audit.* Fluctuating
supply/demand prices, banks, loans, contracts, news shocks, a stock market;
self-described as arcade rather than simulation. **Reference or compatibility
target only** — no porting or derivative incorporation without separate
permission. CA influences price through the game's own virtual
`Tradeable.GetPriceFor`.

---

## What the audits concluded

Genuinely CA's, with no prior art located: sub-faction ownership; the firm as an
entity that owns, employs and bears liability; organization demand; regionally
grounded prices; and the link from economic acts to political-belief effects
that already judge taxation against requisition.

CA should stop building point-of-sale, customer demand, currency media, price
fluctuation, and shop/restaurant premises. CA should stop and study before
building treasury and taxation flows — Empire is prior art for both. CA should
adopt the native quest machinery rather than grow an obligation engine.

## Open consolidation

`ECONOMIC_FUNCTION_AUDIT.md` records its own remaining scope as unfinished, and
`ECONOMIC_IMPLEMENTATION_AUDIT.md` closes with "Remaining, not started". The
Law and Order and Yayo's Bank traces have no consolidated report. Those gaps are
recorded rather than filled.

---

## Wall apertures and window mods (audited 2026-08-08)

*Evidence basis for every entry below: verified — the public repositories and
workshop pages were fetched and read by an in-session audit agent on
2026-08-08, with file-level citations recorded in the session. Nothing here is
reconstructed from memory of the mods.*

The audit ran AFTER CA's native aperture pass existed (`6abba3c`): five
def-driven wall-opening kinds, vent-rate airflow, and awareness transparency
were already implemented against vanilla facts. The audit's role was to find
what public prior art exists, under what terms, and what deserved adoption.
Two techniques were subsequently adopted and are promoted to
`UPSTREAM_SOURCES.md` / `CREDITS.md`; everything else on this list was
inspected and not used.

### Open The Windows (jptrrs)
`github.com/jptrrs/OpenTheWindows` — **MIT**, © 2020 jptrrs. 1.1–1.6, alive.
**Levels: audit completed · source traced · adaptation designed · technique
incorporated** (the daylight model — see `UPSTREAM_SOURCES.md`; no code or
assets copied). Traced and NOT taken: the `CompWindow : CompFlickable`
light/air dual-channel open state (CA state is the def swap); the facing-voted
`ScanLine` light footprint (CA uses a bounded inward fan); `CoverUtility`
and `CanBeSeenOver` postfixes (CA's defs get both natively from fillPercent);
beauty-from-view `StatPart_Landscape`; the `Need_Outdoors` interval rewrite;
path-cost patches; the runtime `def.blockLight` mutation (identified as the
wart CA's def-swap exists to avoid).

### Open The Windows — Owlchemist performance fork
`github.com/Owlchemist/OpenTheWindows` — **MIT** (jptrrs's notice retained).
1.3/1.4 only; author inactive since 2023.
**Levels: audit completed · source traced.** Nothing taken directly: its
performance ideas were merged back into jptrrs's 1.5/1.6 mainline upstream
(seven files there carry "fetched from Owlchemist's" comments), so for 1.6 the
mainline supersedes it.

### ED-Embrasures (jaxxa)
`github.com/jaxxa/ED-Embrasures` — **MIT**, © 2025 Jaxxa. 1.0–1.6, current.
**Levels: audit completed · source traced — validation only, independent
convergence.** CA's defensive slit (`CA_ApertureSlit`, fillPercent 0.85 +
Impassable) predates the audit and stands on the same vanilla facts their
embrasure demonstrates (fillPercent < 1 ⇒ seen-over/shoot-through, fill as
cover). The audit CONFIRMED the pattern is sound and needs no code; nothing
was changed in CA because of it and nothing was taken from it. Recorded so
the resemblance is never mistaken for an unacknowledged port.

### Skylights (Dubwise56 / Dubs Skylights)
`github.com/Dubwise56/Skylights` — **no licence, no C# source** (version
folders ship compiled assemblies + defs only). Default all rights reserved.
**Levels: audit completed · assembly traced — blocked for any reuse.** Its
observable shape (a skylight bool-grid map component + GameGlowAt and
lighting-overlay patches, visible through OpenTheWindows' integration shims)
is the roof-side ancestor of the technique CA adopted from the MIT sources.
Nothing was or may be taken from this repository itself.

### Skylights (archdukejim)
`github.com/archdukejim/rimworld-skylights` — **MIT**, © 2026 archdukejim.
**Levels: audit completed · source traced · adaptation designed · technique
incorporated** (the 1.6 `GroundGlowAt` postfix form — see
`UPSTREAM_SOURCES.md`). Traced and NOT taken: the `SectionLayer_IndoorMask`
prefix reimplementation and the lighting-overlay transpiler (visual-only; CA
states the overlay gap instead), and the `CompGlower`-driven glow-bucket mode.

### Pass It Through The Window! (Continued) (Mlie)
`github.com/emipa606/PassItThroughTheWindow` — **MIT**, © 2020 Mlie. 1.5/1.6.
**Levels: audit completed · source traced.** A two-sided pass-through wall
container transferring items without breaching climate. Nothing taken; noted
as the reusable donor if through-aperture item interaction is ever wanted.

### Vanilla Furniture Expanded — Security
`github.com/Vanilla-Expanded/VanillaFurnitureExpanded-Security` — public
source, **no licence file in the repo** ⇒ treated all rights reserved.
**Levels: audit completed · source traced — blocked for reuse.** Finding of
note: its 1.6 def set contains no embrasures; its defensive positions are
trenches/dugouts applying cover through a hediff-on-building extension. That
concept was not taken.

### (Dirty) Windows (ANumeric)
Steam workshop `3220152649`, ≤1.5. Continuation of Owlchemist's fork; links
only its MIT parent as source, **no public repository of its own changes**.
**Levels: audit completed.** Nothing to take beyond what its MIT parents
already provide.

### Searched for and not found
"Windows (Continued) by Mlie" (no `emipa606/Windows` repo — 404 — and no
workshop hit) and "Simple Windows" (no RimWorld mod under that exact name on
workshop or GitHub) — both marked nonexistent as far as public sources show,
recorded so the search is not repeated from zero.
