# Upstream sources

Provenance for every third-party work incorporated into Colonist Awareness.
`CREDITS.md` carries the public attribution and the how-and-why; this file
carries the machine-checkable facts: where it came from, under what terms, what
was taken, and what state the incorporation is in.

The local reference archive is NOT in this repository. It was excluded
deliberately: it is not a build input (`Source/ColonistAwareness.csproj` resolves
every compile-time reference from NuGet — `Krafs.Rimworld.Ref` and
`Lib.Harmony` — so CA builds on any machine with the .NET SDK and no game
install). Vehicle Framework's MIT runtime assemblies are shipping content, not
compile-time references. A source archive may be retained outside Git for
reference.

Commit hashes are recorded where an exact upstream checkout is available. The
older local archives carry no `.git` metadata; for those sources, recoverable
version evidence is stated and absent evidence remains absent rather than being
guessed.

## Incorporated content

| Source | Author(s) | Upstream | Licence | Version evidence | What was taken | State |
|---|---|---|---|---|---|---|
| Camping Stuff | Nandonalt; rewritten and maintained by Alias44, with jfredric and Orange Peel Assassin | `github.com/Alias44/Nandonalts-Camping-Stuff` | **GPL-2.0** (full text in the local archive root) | not determinable from the archive | C# (`Source/CampingStuff/`: Comps, Jobs, Patches, BackCompatibility, `PlaceWorker_Tent`, `SketchRoof`, `LayoutCache`, `LayoutSpawn`), defs, textures, patches | code + content imported; CA integration and runtime behaviour unverified |
| Boats | Smash Phil | `github.com/zenith408/Boats` | **MIT**, © 2019 Smash Phil | imported archive not determinable | boat defs, textures, and sounds | content imported on the bundled Vehicle Framework runtime; CA does not fork its runtime classes |
| Vehicle Framework | SmashPhil | `github.com/SmashPhil/Vehicle-Framework` | **MIT**, © 2019–2026; bundled SmashTools, CoreLib, DevTools, UpdateLogTool each separately MIT | shipping `Vehicles.dll` 1.6.0 identifies upstream commit `fd5ed722ce214836d4c283832865dfa5966f1e4e`; the 62 base Def files, 7 base patches, assemblies, shader bundles, animations, Burst libraries, legacy textures, English keys, and conditionally loaded DLC patches are verified against that same checkout | runtime assemblies and their complete required content surface; CA's three boat definitions use the framework's current XML and graphic contracts | bundled runtime and content are one version-coherent shipping surface; CA consumes the framework through its public Def contracts rather than a CA C# fork |
| More Furniture (Continued) | Anonemous2 and wofl; continued by Mlie (emipa606) | `github.com/emipa606/MoreFurniture`; Steam `2565302299` | **MIT**, © 2020 Mlie | archive carries 1.0 / 1.1 / 1.2 trees | defs, textures, patches | content imported; integration unverified |
| Rimshare | Sera (seraphile), with per-folder contributor credits | `github.com/seraphile/rimshare` | **MIT**, © 2017 Sera | not determinable | textures | content imported. World-map icons are Sera's edits of **game-icons.net** icons, **CC BY 3.0** — that attribution travels with them |
| Medieval Overhaul | SirLalaPyon | — | no public licence; removable on request | n/a | defs, textures | content imported; integration unverified |
| Dubs Bad Hygiene | Dubwise56 (Dubwise) | `github.com/Dubwise56/Dubs-Bad-Hygiene` | no licence stated; removable on request | n/a | art, sounds, and defs standing on vanilla machinery. **No code** — the compiled assembly is not published, so nothing could be ported | live CA-native thirst, hygiene, bladder, water-source, vessel, well, and disease behavior uses RimWorld Need, StatDef, JobGiver, and health seams; upstream content supplies credited art, sounds, and compatible def evidence |
| Processor Framework | Syrchalis | `github.com/Syrchalis/ProcessorFramework` | no licence stated | n/a | intended: `ProcessDef` as the basis for drying, smoking, salting, pickling | **nothing ported.** No `ProcessDef` / `CompProcessor` / `ProcessorFramework` symbol exists in `Source/`, `Defs/`, `Patches/` or the local archive |
| Open The Windows | jptrrs | `github.com/jptrrs/OpenTheWindows` | **MIT**, © 2020 jptrrs (LICENSE fetched and read verbatim, 2026-08-08) | repo release v2.2.4; `About.xml` supportedVersions 1.1–1.6 (read 2026-08-08) | **technique only, no code, no assets**: the wall-daylight model — map-held window-lit cell set consulted by a `GlowGrid.GroundGlowAt` postfix answering max(result, skyGlow × transmission) — reimplemented with CA's own footprint in `Source/ApertureLightModule.cs`; plus a negative design lesson (upstream mutates `def.blockLight` on the shared def at runtime; CA's shutter states are two defs swapped per instance in `Source/ApertureModule.cs` to avoid exactly that) | technique adapted; runtime unverified |
| Skylights (archdukejim) | archdukejim | `github.com/archdukejim/rimworld-skylights` | **MIT**, © 2026 archdukejim (LICENSE fetched and read verbatim, 2026-08-08) | repo created 2026-07, active at audit | **technique only, no code**: the 1.6 form of the `GroundGlowAt` postfix (O(1) registry probe, max-with-sky-glow), followed by CA's postfix in `Source/ApertureLightModule.cs`. Their `SectionLayer_IndoorMask` prefix and lighting-overlay transpiler (the visual half) deliberately not taken | technique adapted; runtime unverified |

## Reference-only, not incorporated

| Source | Position |
|---|---|
| Vanilla Expanded Framework, Vanilla Furniture Expanded, Vanilla Trading Expanded | **CC BY-NC-ND.** Nothing used. VTE is a compatibility target only — CA influences price through the game's own virtual `Tradeable.GetPriceFor`, never by touching their code. |
| Hospitality, Storefront, Gastronomy, RimBank, Empire, Simple Warrants, Law and Order, Yayo's Bank | Audited and traced, not incorporated. See `PRIOR_ART_TRACES.md`. |

## Known defects in this surface

1. **The Processor Framework credit describes an integration that does not
   exist.** `CREDITS.md` narrates the 1.3→1.6 port in the past tense, including
   five specific API renames, while no ported code is present anywhere. Its own
   stated policy is that entries are "filled as the work lands rather than in
   advance." Corrected in `CREDITS.md`; recorded here so the discrepancy is not
   lost.
