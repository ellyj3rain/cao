# Upstream sources

Provenance for every third-party work incorporated into Colonist Awareness.
`CREDITS.md` carries the public attribution and the how-and-why; this file
carries the machine-checkable facts: where it came from, under what terms, what
was taken, and what state the incorporation is in.

The local reference archive is NOT in this repository. It was excluded
deliberately: it is not a build input (`Source/ColonistAwareness.csproj` resolves
every reference from NuGet — `Krafs.Rimworld.Ref` and `Lib.Harmony` — so the mod
builds on any machine with the .NET SDK, with no game install and no vendored
assemblies). A copy may be retained outside Git for reference.

**Commit hashes are not recorded below because they are not recoverable.** The
local archive carries no `.git` metadata for any vendored source. Where a
version is determinable from the archive or from published metadata it is given;
where it is not, the field says so rather than guessing.

## Incorporated content

| Source | Author(s) | Upstream | Licence | Version evidence | What was taken | State |
|---|---|---|---|---|---|---|
| Camping Stuff | Nandonalt; rewritten and maintained by Alias44, with jfredric and Orange Peel Assassin | `github.com/Alias44/Nandonalts-Camping-Stuff` | **GPL-2.0** (full text in the local archive root) | not determinable from the archive | C# (`Source/CampingStuff/`: Comps, Jobs, Patches, BackCompatibility, `PlaceWorker_Tent`, `SketchRoof`, `LayoutCache`, `LayoutSpawn`), defs, textures, patches | code + content imported; CA integration and runtime behaviour unverified |
| Boats | Smash Phil | `github.com/zenith408/Boats` | **MIT**, © 2019 Smash Phil | not determinable from the archive | defs and assets only | content imported; **no CA code binds it** |
| Vehicle Framework | SmashPhil | `github.com/SmashPhil/Vehicle-Framework` | **MIT**, © 2019–2026; bundled SmashTools, CoreLib, DevTools, UpdateLogTool each separately MIT | not determinable | defs, shaders (`AssetBundles/`), `Animations/`, `Burst/` | content imported; **no CA code binds it**; see defect below |
| More Furniture (Continued) | Anonemous2 and wofl; continued by Mlie (emipa606) | `github.com/emipa606/MoreFurniture`; Steam `2565302299` | **MIT**, © 2020 Mlie | archive carries 1.0 / 1.1 / 1.2 trees | defs, textures, patches | content imported; integration unverified |
| Rimshare | Sera (seraphile), with per-folder contributor credits | `github.com/seraphile/rimshare` | **MIT**, © 2017 Sera | not determinable | textures | content imported. World-map icons are Sera's edits of **game-icons.net** icons, **CC BY 3.0** — that attribution travels with them |
| Medieval Overhaul | SirLalaPyon | — | no public licence; **verbal permission to the project owner, 2026-08-04**, removable on request | n/a | defs, textures | content imported; integration unverified |
| Dubs Bad Hygiene | Dubwise56 (Dubwise) | `github.com/Dubwise56/Dubs-Bad-Hygiene` | no licence stated; **verbal permission, 2026-08-04**, removable on request | n/a | art, sounds, and defs standing on vanilla machinery. **No code** — the compiled assembly is not published, so nothing could be ported | defs quarantined from live; needs re-implemented on native seams |
| Processor Framework | Syrchalis | `github.com/Syrchalis/ProcessorFramework` | no licence stated; **verbal permission, 2026-08-04**, conditional on attribution and an explanation of the integration | n/a | intended: `ProcessDef` as the basis for drying, smoking, salting, pickling | **nothing ported.** No `ProcessDef` / `CompProcessor` / `ProcessorFramework` symbol exists in `Source/`, `Defs/`, `Patches/` or the local archive |
| Open The Windows | jptrrs | `github.com/jptrrs/OpenTheWindows` | **MIT**, © 2020 jptrrs (LICENSE fetched and read verbatim, 2026-08-08) | repo release v2.2.4; `About.xml` supportedVersions 1.1–1.6 (read 2026-08-08) | **technique only, no code, no assets**: the wall-daylight model — map-held window-lit cell set consulted by a `GlowGrid.GroundGlowAt` postfix answering max(result, skyGlow × transmission) — reimplemented with CA's own footprint in `Source/ApertureLightModule.cs`; plus a negative design lesson (upstream mutates `def.blockLight` on the shared def at runtime; CA's shutter states are two defs swapped per instance in `Source/ApertureModule.cs` to avoid exactly that) | technique adapted; runtime unverified |
| Skylights (archdukejim) | archdukejim | `github.com/archdukejim/rimworld-skylights` | **MIT**, © 2026 archdukejim (LICENSE fetched and read verbatim, 2026-08-08) | repo created 2026-07, active at audit | **technique only, no code**: the 1.6 form of the `GroundGlowAt` postfix (O(1) registry probe, max-with-sky-glow), followed by CA's postfix in `Source/ApertureLightModule.cs`. Their `SectionLayer_IndoorMask` prefix and lighting-overlay transpiler (the visual half) deliberately not taken | technique adapted; runtime unverified |

## Reference-only, not incorporated

| Source | Position |
|---|---|
| Vanilla Expanded Framework, Vanilla Furniture Expanded, Vanilla Trading Expanded | **CC BY-NC-ND.** Nothing used. VTE is a compatibility target only — CA influences price through the game's own virtual `Tradeable.GetPriceFor`, never by touching their code. |
| Hospitality, Storefront, Gastronomy, RimBank, Empire, Simple Warrants, Law and Order, Yayo's Bank | Audited and traced, not incorporated. See `PRIOR_ART_TRACES.md`. |

## Known defects in this surface

1. **Vehicle Framework is not declared as a dependency.** `About/About.xml`
   declares Harmony, Biotech and Ideology only. Boats and Vehicle Framework defs
   and assets ship with no declared dependency on the framework they require.
2. **The Processor Framework credit describes an integration that does not
   exist.** `CREDITS.md` narrates the 1.3→1.6 port in the past tense, including
   five specific API renames, while no ported code is present anywhere. Its own
   stated policy is that entries are "filled as the work lands rather than in
   advance." Corrected in `CREDITS.md`; recorded here so the discrepancy is not
   lost.
