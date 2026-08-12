# Credits

**Licence.** This mod is GPL-3.0; the full text is in `LICENSE`. It adapts
GPL-covered work (Empire) and aligns with community norms supporting open
inspection and reuse under reciprocal terms. Components below retain their own
terms where those are more specific.

**Machine-checkable provenance** — upstream URLs, licences, versions, what was
taken from each source and the state of each incorporation — is in
`UPSTREAM_SOURCES.md`. External implementations that were investigated but not
incorporated are recorded in `PRIOR_ART_TRACES.md`. Each entry below states its
own integration status; an entry describing a design is not a claim that the
code exists.


Colonist Awareness uses work by other creators. Thanks to all of them.

## Camping Stuff
Nandonalt, rewritten and maintained by Alias44, with jfredric, Orange
Peel Assassin, and the RimWorld community.
https://github.com/Alias44/Nandonalts-Camping-Stuff - GPL-2.0.
Public redistribution of this mod must be GPL-2.0-compatible with
source available.

## Medieval Overhaul
SirLalaPyon. Removed immediately on request from the author.

## Dubs Bad Hygiene
Dubwise56 (Dubwise). https://github.com/Dubwise56/Dubs-Bad-Hygiene -
no license stated. Removed immediately on request from the author.

How and why: the mod's compiled assembly is not published, so none of
its code could be ported. What was taken is its art, its sounds, and
the defs that stand on vanilla machinery - the hygiene hediffs, the
filth, the thoughts, the water bottle graphics. Thirst, hygiene and
bladder are re-implemented on RimWorld's own Need and think-tree
seams because there was no source to port. The reason for using it at
all is that sanitation is a settlement-level logistics problem in this
mod, not a colony chore, and Dubs Bad Hygiene already solved what the
fixtures should be and what they should look like.

## Processor Framework
Syrchalis. https://github.com/Syrchalis/ProcessorFramework - no
license stated. Removed immediately on request from the author.

*Status:* **not yet integrated. Nothing is ported.** No ProcessDef,
CompProcessor or ProcessorFramework symbol exists in this mod.

How and why, as designed: its C# IS published, so this is intended as
a real port rather than an asset lift. Its ProcessDef already carries
every axis this project needs for food preservation - elapsed time,
safe and ideal temperature bands, sun, rain, snow and wind factors,
fuel and power dependence, quality by processing skill, yield
efficiency, spoilage chance and byproducts. Writing that again would
produce a worse copy of it. The plan is to bring the 1.3 source to
1.6 and drop its multiplayer compatibility file, since this project
does not support multiplayer. Drying, smoking, salting and pickling
would then be DATA on that framework rather than four separate
systems, with the framework sitting underneath this mod's own layer
deciding who owns the smokehouse and who the output belongs to.

## More Furniture (Continued)
Anonemous2 and wofl, continued by Mlie (emipa606).
https://github.com/emipa606/MoreFurniture - MIT.

## Rimshare
Sera (seraphile), with per-folder contributor credits.
https://github.com/seraphile/rimshare - MIT.
The world-map icons are Sera's edits of icons from **game-icons.net**,
which are **CC BY 3.0** - attribution to game-icons.net and its
artists travels with them.

## Boats
Smash Phil. https://github.com/zenith408/Boats - MIT, (c) 2019.

## Vehicle Framework
SmashPhil. https://github.com/SmashPhil/Vehicle-Framework - MIT,
(c) 2019-2026. Bundled with its SmashTools, CoreLib, DevTools and
UpdateLogTool submodules, each separately MIT.

## Economic stack

### Hospitality
Orion (OrionFive). https://github.com/OrionFive/Hospitality
GPLv3 for code, CC BY-SA 4.0 for original artwork.
*Used for:* visitors and their spending behaviour - the demand side
that CA has no model of.
*How it fits:* CA simulates the institutions a settlement holds; it
does not simulate why an individual wants to buy something.
Hospitality supplies the customer, CA supplies the body that owns the
premises and answers for what happens there.
*Status:* not yet integrated.

### Hospitality: Storefront
tomvd, continuing Orion's original.
https://github.com/tomvd/Storefront
*Used for:* the point-of-sale surface - a staffed shop, a sale area
around a register, item transfer and silver payment.
*How it fits:* Storefront runs the shop as an operation. CA records
who owns it, who operates it, on what terms, and who the proceeds are
for. The transaction is theirs; the ownership and its consequences
are CA's.
*Status:* not yet integrated.

### Gastronomy
Orion (OrionFive). https://github.com/OrionFive/Gastronomy
*Used for:* restaurants - menus, prices, opening hours, waiters, paid
meals.
*How it fits:* the same division as Storefront. Gastronomy is the
service business running; CA is the institutional order under it -
whose labour, whose capital, whose rules.
*Status:* not yet integrated.

### RimBank
Original author user19990313; continued by emipa606 and Bar0th.
https://github.com/emipa606/RimBank - MIT.
*Used for:* physical currency - banknotes, denominations, ATM
exchange, mixed banknote and silver settlement, trader acceptance and
giving change.
*How it fits:* RimBank is the money's medium. CA's ledger records
obligation and account; RimBank is what actually changes hands, so
CA's abstract balances resolve into things a pawn can carry.
*Status:* not yet integrated.

### Empire (Empire Refactored / Empire-1_6-Continued)
Saakra originally; refactored and continued by Matathias, Epistatic
and others. https://github.com/matathias/Empire-1_6-Continued
**GPL-3.0.** This mod is GPL-3.0 because it adapts this work.
*Used for:* the off-map settlement economy - world-scoped settlement
state, taxation and levy flow, settlement production and upgrades.
`FactionFC` is a `WorldComponent` carrying a tax ledger and settlement
comps, which is the only existing implementation that runs a
settlement economy while its map is unloaded.
*How it fits:* Empire's flow is adapted behind CA's organization model, not
surfaced as-is. Empire collects taxes from the player's
dependent colonies to the player's home map; CA needs organizations
that hold treasuries, levy, and pay each other with no player
involved. The machinery for off-map settlement economy is taken; the
player-centric political shape is replaced by CA's organizations, customs,
and political-belief effects, so a levy becomes an act judged as taxation or
requisition.
*Status:* adaptation ratified; not yet begun.

## Open The Windows
jptrrs. https://github.com/jptrrs/OpenTheWindows - **MIT**,
(c) 2020 jptrrs.
*Used for:* the wall-daylighting technique only - no code, no assets.
CA's apertures (`Source/ApertureModule.cs`) were built natively as
def-driven wall openings; what came from Open The Windows is its
daylight model: a map-held set of window-lit interior cells consulted
by a `GlowGrid.GroundGlowAt` postfix that answers
max(result, sky glow x transmission). CA reimplements that model in
`Source/ApertureLightModule.cs` with its own bounded inward fan in
place of OTW's facing-voted scanline footprint.
*How and why:* the engine only daylights unroofed cells, so a glazed
window in a roofed room changes nothing by def alone; OTW is the
originating public solution for wall windows and its patch point is
the correct one. A second contribution was taken in the negative: OTW
toggles `def.blockLight` on the SHARED def at runtime, and CA's
shutter open/closed states are two defs swapped per instance
specifically to avoid that wart.
*Status:* technique adapted; no upstream code or assets copied;
runtime unverified.

## Skylights (archdukejim)
archdukejim. https://github.com/archdukejim/rimworld-skylights -
**MIT**, (c) 2026 archdukejim.
*Used for:* the current-1.6 form of the same daylight patch point - a
`GroundGlowAt` postfix that costs one registry probe per query and
takes the max with sky glow. CA's postfix in
`Source/ApertureLightModule.cs` follows that shape. Their
lighting-overlay transpiler and indoor-mask prefix - the visual half -
were deliberately not taken; CA lights cells for gameplay and states
the overlay gap instead of patching the render mesh.
*Status:* technique adapted (shared patch point with Open The
Windows, credited per what each contributed); no upstream code
copied; runtime unverified.

Windows and aperture prior art that was audited but NOT used -
Owlchemist's Open The Windows fork, ED-Embrasures (whose def-only
slit pattern CA had independently converged on before the audit),
Pass It Through The Window, Dubs Skylights (no licence, no source),
and VFE-Security (no licence) - is recorded with per-source levels in
`PRIOR_ART_TRACES.md`. Inspection is not use, and none of those are
claimed here.

## Not included
Vanilla Expanded Framework, Vanilla Furniture Expanded and Vanilla
Trading Expanded (Oskar Potocki and the Vanilla Expanded team) are
CC BY-NC-ND. Nothing of theirs is used. Vanilla Trading Expanded is
treated as a compatibility target only - CA influences price through
the game's own virtual `Tradeable.GetPriceFor`, never by touching
their code.

Anything else this mod does that resembles another mod's work is
written from scratch.
