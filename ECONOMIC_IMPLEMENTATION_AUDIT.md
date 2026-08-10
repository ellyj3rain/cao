# Economic implementation audit — second pass

**Started 2026-08-05 16:20 UTC / 09:20 PDT.** Per-mod source tracing to the
ten-field schema. Repositories read read-only over the GitHub API; nothing
installed, no remotes, no worktree, CA unmodified.

**Licence and permission are recorded in field 9 only.** They determine what
MAY be used and are deliberately kept out of fields 1–8, so that architectural
fit is judged on its own evidence.

**Progress: 1 of 6 mods traced.** RimBank complete. Hospitality, Storefront,
Gastronomy, Empire and Vanilla Trading Expanded not yet started. No
cross-function conclusion is drawn until more than one is done.

---

# RimBank — currency and payment settlement

Repository `github.com/emipa606/RimBank`, branch `main`, last pushed
**2025-06-30**. Original author user19990313; continued by emipa606 and Bar0th.

## 1. Native state and lifecycle

`TradeDeal` holds a private `List<Tradeable> tradeables`; `Tradeable` carries
per-item counts and resolves via `ResolveTrade()`. `Transactor` is a two-member
enum — `Colony` and `Trader`. `TradeCurrency` is likewise two members, `Silver`
and `Favor`. Prices resolve through `TradeUtility.GetPricePlayerBuy` and
`GetPricePlayerSell`. **The entire native trade lifecycle is defined around a
player-colony session.**

## 2. Exact mod classes and data flow

- `RimBank.Trade/Methods.cs` — `DoExecute(this TradeDeal deal)` reaches into
  the native deal by reflection, `AccessTools.Field(typeof(TradeDeal),
  "tradeables")`, iterates the tradeables, calls `ResolveTrade()` on each and
  resets the deal.
- `Methods.CanColonyAffordTrade(TradeDeal)` computes
  `deal.CurrencyTradeable.CountPostDealFor(Transactor.Colony)` plus
  `Utility.GetNotesBalanceAvailable(Transactor.Colony)`.
- `Methods.UpdateCurrencyCount` forces `deal.CurrencyTradeable` to a silver
  figure and consumes cached notes via `CountHeldBy(transactor)`, splitting
  between `Transactor.Trader` and `Transactor.Colony` by sign.
- `Methods.CacheNotes` is a `static List<Tradeable>` — a per-session cache,
  reset by `Utility.ResetCacheNotes()`.
- Harmony patches: `TradeUtility_GetPricePlayerBuy`,
  `TradeUtility_GetPricePlayerSell`,
  `Settlement_TraderTracker_RegenerateStock`, `TradeShip_GenerateThings`.
- Currency media: `Defs/Item_BankNote.xml` (banknotes as real Things),
  `Defs/Building_RimBankTerminal.xml` (the ATM), `Building_Terminal.cs`,
  `JobDriver_UseTerminal.cs`, `Trader_BankNoteExchange.cs`, `VirtualTrader.cs`.

**Data flow:** banknotes are ordinary Things in inventories; at trade time they
are gathered into `CacheNotes`, valued, and netted against the deal's silver
currency tradeable, with change resolved at settlement.

## 3. Exact CA state and data flow

`org.treasury` is a `float` on `CAOrganization`. `DestinationTradeModule`
settles by pricing at `MarketValue * stackCount * 0.85f`, decrementing
`treasury`, destroying the player's goods and spawning a real Silver Thing.
`FrontierModule` levies against `treasury` without a recipient. No physical
currency other than vanilla Silver.

## 4. Overlap

Both convert an abstract balance into physical media at a transaction boundary.
CA already does this correctly for Silver; RimBank does it for banknotes with
denominations and change.

## 5. Semantic incompatibilities — the decisive field

**RimBank's settlement machinery is structurally player-colony-bound.** Every
entry point takes a `TradeDeal`; every balance query passes
`Transactor.Colony`; all four Harmony patches target player-facing trade
methods (`GetPricePlayerBuy`, `GetPricePlayerSell`, trader stock generation).
`TradeDeal` exists only for a player trade session.

CA's requirement is settlement where **neither party is the player** —
institution to institution, frequently off-map. There is no `TradeDeal` in that
case and no `Transactor` that means "the other settlement". RimBank's
transaction layer therefore cannot be pointed at CA's problem without being
rewritten around a different session concept.

Its **currency layer** has no such coupling: banknotes are Things with
denominations, and the exchange building is an ordinary building with a job
driver.

## 6. On-map behaviour

Fully on-map and pawn-mediated — a pawn walks to a terminal and runs a job.
Compatible with how CA materialises settlements.

## 7. Off-map representation

**None.** RimBank has no off-map concept whatsoever. Banknotes exist only as
spawned Things or inventory contents. CA's off-map settlements hold a `float`
treasury with no media.

## 8. Save/load and reconciliation

Banknotes save as ordinary Things, so they survive natively and cannot desync —
a genuine strength. `CacheNotes` is `static` and session-scoped, so it is not
persisted and must not be relied on across a save. There is no off-map
aggregate to reconcile, which means **the promotion problem CA has is simply
absent here** rather than solved: nothing in RimBank tells CA how a settlement's
treasury becomes physical notes when its map loads.

## 9. Permission scope

MIT, verified. Freely reusable, including incorporation, with attribution.
Operator additionally holds verbal permission across the stack with the credit
conditions recorded in `CREDITS.md`. *This field does not affect fields 1–8.*

## 10. Recommended verdict — **split by layer**

- **Currency media: reuse substantially.** Banknote ThingDefs, denomination
  handling, change-making, and the terminal/job-driver pattern. MIT permits it,
  and it is the piece CA would otherwise invent badly.
- **Transaction settlement: study as prior art only.** The `TradeDeal` /
  `Transactor.Colony` coupling makes it inapplicable to institution-to-
  institution settlement. Its useful lesson is the *netting* pattern — media
  valued and offset against a balance, change returned — not its plumbing.
- **Off-map: retain CA implementation.** RimBank offers nothing here.

**Open questions this trace did not answer:** whether the four Harmony patches
conflict with Vanilla Trading Expanded's price patches when both are present;
and whether a 2025-06-30 codebase is current against 1.6.4871.

---

## Remaining, not started

Hospitality, Storefront, Gastronomy, Empire, Vanilla Trading Expanded. Each
needs the same ten fields before any function-level verdict is issued.
