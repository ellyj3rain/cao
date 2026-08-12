# Economic function audit — first pass

> ## STATUS: SCOPING INVENTORY. NOT A BASIS FOR ARCHITECTURAL ACTION.
>
> **The implementation audit has not happened.** No mod's classes, stored
> state, mutation sites, consumers, save/load behaviour, ownership transfer,
> destruction handling or transaction flow have been traced. Feature
> descriptions below are the operator's account; a Workshop description can
> establish what a mod *claims* to do and cannot establish whether its
> implementation fits CA's model.
>
> **Every reuse recommendation in this document is therefore withdrawn** and
> marked `PENDING`. In particular: "stop building X", "adopt RimBank
> outright", "do not reimplement point-of-sale", and "use the quest machinery
> as the obligation system" all outran their evidence and are not decisions.
>
> **Permission is not architectural fit.** Licence and permission determine
> what MAY be used. They are recorded in their own section and must not
> influence what SHOULD be used. The two questions are answered separately.
>
> What this document is good for: knowing which native systems exist, which
> mods occupy which functions, and what CA currently implements. Nothing more
> until the second pass lands.

**2026-08-05 15:30 UTC / 08:30 PDT.** Organised by economic function, not by
mod, per DR-87. Six columns per function: **native object → existing mod
implementation → CA requirement → source/license → reuse strategy → missing
model support.**

**Provenance of each column.** Native objects are read from the decompiled
1.6.4871 source. CA requirements are read from this codebase. **Repositories
and licences are verified against the live public repositories** (see the
licence table below); mod *feature* descriptions remain the operator's, since
I inspected metadata and licence files rather than reading every implementation
in full. Reuse strategy and model gaps are my recommendations.

## Verified repository and licence status

| Mod | Repository | Licence — VERIFIED | Reuse position |
|---|---|---|---|
| Hospitality | `github.com/OrionFive/Hospitality` | **GPLv3** for code, **CC BY-SA 4.0** for original artwork (dual, in LICENSE) | Freely reusable under GPLv3 copyleft |
| Gastronomy | `github.com/OrionFive/Gastronomy` | **CC BY-NC-ND 4.0** in README vs a **GPL-3.0** repo marker — **CONFLICT CONFIRMED** | README appears operative → no derivatives under the published terms |
| Storefront | `github.com/tomvd/Storefront` | **NO LICENCE FILE AT ALL** (API `license: null`) | **Corrects a prior assumption:** published source is not a licence grant. All rights reserved by default |
| RimBank | `github.com/emipa606/RimBank` | **MIT** (LICENSE.md) | Freely reusable. Original author user19990313; continued by emipa606 / Bar0th |
| Empire | `github.com/RadsuitRandy/Empire-Mod` (origin; `BigBadE/Empire-Mod` is a fork of it) | **NO LICENCE** on either (API `license: null`) | All rights reserved by default. Study only without permission |
| Vanilla Trading Expanded | Vanilla Expanded team | **CC BY-NC-ND** (operator-stated; consistent with the VE team's standard terms) | Reference / compatibility target only |

**What the published licences say.**
Three of six — Storefront, Empire, and effectively Gastronomy — carry no usable
*public* licence: absent a LICENSE file the default is all rights reserved.
The licence column below is recorded for accuracy.

The one thing that still shapes engineering: Hospitality's GPLv3 is
**copyleft**, so incorporating its code carries obligations for the combined
work, while RimBank's MIT does not. That decides what is copied and what is
depended upon.

Note on Hospitality's GPLv3: it is **copyleft**. Incorporating its code into CA
carries licence obligations for the combined work. RimBank's MIT does not.
That difference should decide which is copied and which is depended upon.

---

## 1. Payment settlement

**Native.** A full system exists: `TradeDeal`, `Tradeable`,
`Tradeable_Pawn`, `Tradeable_RoyalFavor`, `TradeAction`, `TradeSession`,
`TradeabilityUtility`. Settlement is per-deal, two-party, and resolves by
moving real Things.
**Mod.** Storefront implements point-of-sale settlement — a register, a sale
area, item transfer, silver payment — for a *shop*, which vanilla has no
concept of. Cash Register underpins it.
**CA requirement.** `DestinationTradeModule` already settles by destroying
goods and spawning real Silver at the boundary (F-122). CA needs settlement
between organizations, not only between a player and a trader.
**Licence.** Storefront: source published. Hospitality stack: GPLv3.
**Reuse — PENDING implementation audit.** Native `TradeDeal` covers
caravan/trader settlement; Storefront covers premises-based sale. CA records
the resulting organization transaction.
**Missing.** Settlement where neither party is the player, and settlement of
obligations rather than goods.

## 2. Business premises

**Native.** None. `Room`, `RoomRoleDef` and zones exist; a *business* does not.
**Mod.** Storefront defines a sale area around a register with staffing;
Gastronomy defines a restaurant with menus, hours and waiters.
**CA state.** `CAFacilityHolding` records a facility's owner, operator, capital
source, allocation rule, oversight, liability, and beneficiaries. Starting
materialization uses those records to assign stores and staffed posts (F-122).
**Licence.** Storefront source published; Gastronomy source published with
conflicting README/GPL signals to resolve.
**Reuse — PENDING implementation audit.** These mods run the shop. CA records
which organization owns and operates it.
**Missing.** Destruction and transfer reconciliation, plus continued operation
after starting materialization.

## 3. Staffing

**Native.** `WorkTypeDef`, `WorkGiverDef`, `Pawn_WorkSettings`, priorities.
Work is assigned, but RimWorld has no wage, contract, or employer.
**Mod.** Storefront and Gastronomy both staff a premises (shopkeeper, waiter).
**CA state.** CA has offices and direct organization relations that record work
responsibility, compensation kind, and compensation rate. Starting
materialization assigns provider-group residents to staffed posts.
**Licence.** GPLv3 stack.
**Reuse — PENDING implementation audit.** A premises role assigned to a pawn is
the relevant staffing pattern in the mod stack.
**Missing.** Persistent employment contracts, scheduled payment, and enforcement
when work or payment is refused.

## 4. Inventory ownership

**Native.** `Thing.Faction` is the only ownership relation, and it is faction-
level. `ThingOwner` holds containment; `CompForbiddable` gates access.
**Mod.** Storefront moves items on sale; RimBank moves currency.
**CA requirement.** CA needs ownership within a faction — stores held by one
organization versus a person's holdings — which `Thing.Faction` cannot express.
**Licence.** RimBank MIT.
**Reuse — PENDING implementation audit.** None available; this gap is genuinely CA's.
**Missing.** Sub-faction ownership. This is a legitimate CA-native record, and
must reconcile against destruction and transfer the way `seededAssets`
consumers already validate (F-124).

## 5. Customer demand

**Native.** `Need`s drive pawn behaviour; `IncidentWorker_VisitorGroup` brings
visitors. No purchasing preference exists.
**Mod.** Hospitality supplies visitors *and their spending behaviour*;
Storefront adds customer preferences and personal budgets; Gastronomy adds
menu choice.
**CA requirement.** CA has no demand model at all.
**Licence.** Hospitality GPLv3, art CC BY-SA.
**Reuse — PENDING implementation audit.** Strong candidate for direct dependency rather than reimplementation.
Demand is expensive to model and already solved here.
**Missing.** Settlement demand — a settlement needing medicine, not a pawn
wanting a meal.

## 6 & 12. Pricing and market prices

**Native.** `Tradeable.GetPriceFor(TradeAction)` (virtual — a real extension
point), `PriceType`, `StatDefOf.MarketValue`, `StockGenerator_*` for trader
stock. Prices are static per trader; there is no supply-and-demand movement.
**Mod.** Vanilla Trading Expanded implements fluctuating prices, buy/sell
pressure, news shocks and a stock market — self-described as arcade rather than
simulation.
**CA requirement.** `DestinationTradeModule` prices at
`t.MarketValue * stackCount * 0.85f` — a flat markdown.
**Licence.** **VTE is CC BY-NC-ND. No porting, no derivative incorporation
without separate permission.**
**Reuse — PENDING implementation audit.** **Reference and compatibility target only.** `GetPriceFor` being
virtual means CA can influence price without touching VTE's code, and CA should
ensure it does not fight VTE when both are present.
**Missing.** Prices grounded in regional production and scarcity rather than a
planetary abstraction. This is where CA's material grounding beats an arcade
market, and is worth CA owning.

## 7 & 8. Banking and currency

**Native.** `TradeCurrency` is an enum of exactly two: `Silver` and `Favor`.
No banknotes, denominations, change, credit, or deposit.
**Mod.** RimBank implements banknotes, denominations, ATM exchange, mixed
banknote/silver settlement, trader acceptance and giving change.
**CA requirement.** CA has `treasury` as a float, no physical media.
**Licence.** **RimBank is MIT with public source — the most freely reusable
item in the stack.**
**Reuse — PENDING implementation audit.** Adopt outright for physical money. Do not invent alternative
currency handling.
**Missing.** Accounting. RimBank moves media; it does not record who owes what
to whom.

## 9. Treasury

**Native.** Nothing. RimWorld has no institutional balance.
**Mod.** Empire Refactored holds settlement-level economics; VTE has banks.
**CA requirement.** `org.treasury` — legitimate CA-native, partially conserved:
endowed from nothing at two sites, conserved on the trade path, destroyed by
the frontier levy (F-123).
**Licence.** Empire's source and licence need inspection.
**Reuse — PENDING implementation audit.** Empire is the closest prior art for settlement treasuries and levies;
audit it before extending CA's.
**Missing.** Conservation, and an endowment sourced from persisted economic
history rather than a score (F-124).

## 10. Taxation

**Native.** Effectively none. The closest is
`IncidentWorker_CaravanArrivalTributeCollector` — a Royalty incident, not a
system.
**Mod.** Empire Refactored implements taxes paid in silver or goods, plus
faction-wide tax, social and military edicts, and unrest.
**CA state.** Organizations can set a tax rate. `CATaxation` collects from a
delegated party or its treasury and records a taxation act. Political-belief
effects judge that recorded act.
**Licence.** To be inspected.
**Reuse — PENDING implementation audit.** Empire remains the strongest audit
candidate for broader settlement tax administration.
**Missing.** Assessment, exemptions, arrears, off-map accounting, and a
conserved regional tax base.

## 11. Contracts

**Native.** **RimWorld has a real contract system already** — `Quest`,
`QuestPart`, `QuestManager`, `QuestGen`, `QuestScriptDef`. Obligations,
outcomes, expiry and rewards are all modelled.
**Mod.** VTE adds financial contracts.
**CA requirement.** CA has compacts, suzerainty and obligations in the
organization layer.
**Licence.** Native — no constraint.
**Reuse — PENDING implementation audit.** **This is the largest under-used native system in the audit.** CA's
compacts should be expressed as quests or share their vocabulary before CA
grows a parallel obligation engine. Per DR-86, this is exactly the case the
governing rule covers.
**Missing.** Inter-NPC obligations — quests are player-facing.

## 13. Conservation

**Native.** Structural: Things exist or do not. There is no ledger to
un-balance.
**Mod.** Storefront and RimBank conserve by moving real Things.
**CA requirement.** CA's abstract quantities — treasury above all — must
reconcile to material facts.
**Licence.** n/a.
**Reuse — PENDING implementation audit.** The pattern is already in CA and already correct in one place:
`DestinationTradeModule` spawns real Silver at the boundary.
**Missing.** The same discipline at every other boundary — endowment, levy,
and any future wage or tax path.

---

## What this pass concludes — downgraded

**Nothing architectural.** The observations that survive are factual:

- The native systems verified above exist and are extension points:
  `Tradeable.GetPriceFor` is virtual; `TradeCurrency` has exactly two members;
  there is no native taxation; the quest machinery exists and models
  obligations, outcomes, expiry and rewards.
- Six mods occupy identifiable economic functions.
- CA's own primitives are as recorded in the parallel-ontology audit.

**On the quest system specifically** — the earlier conclusion is withdrawn.
This document itself notes that quests are player-facing and that inter-NPC
obligations are missing, which means the quest machinery has *not* been shown
to replace compacts, suzerainty, employment contracts, taxation obligations or
persistent institutional relations. The live possibility is narrower and more
interesting: quests may be a **presentation, scheduling or enforcement surface
over a CA obligation**, rather than the obligation's source of truth. That is a
question for the second pass, not a finding.

**On "stop building"** — also withdrawn. Prior art admits at least six
responses, and the document collapsed them into one. The available verdicts are
listed in the second-pass schema below; surrendering an economic function is
only one of them, and the least likely to be right.

---

# The target chain, and why it comes before reuse

A mod fits only if it supplies a known link in CA's economic chain. Each link
below must be a real relation rather than a summary:

> **business or public organization → premises → work → ownership of goods
> → production or service → customer or organization demand → price or
> contract → payment →
> treasury and accounting → taxation and political-belief consequence**

Every gap named in this document sits on that chain: firm identity at the head,
organization ownership of goods, employment at work, settlement
demand in the middle, accounting and regional prices toward the end, and the
link into political-belief effects at the tail. Taxation now emits the act those
effects judge; the wider accounting chain remains incomplete.

Conservation is a property **of the whole chain**, not of any link. External
mods supply transaction mechanics at the payment link; they do not by
themselves resolve CA's initial endowment, off-map accounting, wages,
procurement, or the destruction and transfer of value elsewhere on the chain.

---

# Second-pass schema — what a sufficient audit must produce

Per economic function, in this order:

1. native RimWorld state and lifecycle
2. exact mod classes and data flow
3. exact CA state and data flow
4. overlap
5. semantic incompatibilities
6. on-map behaviour
7. off-map representation
8. save/load and reconciliation
9. permission scope
10. recommended verdict

Each recommendation takes exactly one verdict:

- **reuse substantially**
- **adapt behind a CA institutional layer**
- **depend on optionally**
- **compatibility only**
- **study as prior art**
- **retain CA implementation**
- **replace CA implementation**

Questions that must be answered before any verdict, none of which are answered
today: is the implementation current and stable; does it assume the player
colony; does it support NPC-to-NPC transactions; does it preserve institutional
ownership; can it operate off-map; does it integrate with regional simulation;
is dependency or incorporation preferable; and do its save structures survive
promotion between off-map and on-map state.
