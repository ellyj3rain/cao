# B14 Map Preview page-transition receipt

Recorded 2026-08-14 21:04 UTC / 14:04 PDT.

## Runtime finding

The operator's first and second Generate clicks both completed RimWorld world
generation. The failure occurred afterward, in the main-thread completion action
that opens `Page_SelectStartingSite` and then closes
`Page_CreateWorldParams`.

| Evidence | Result |
|---|---|
| `Player.log` lines 690-719 | Biome generation finishes; `Page_CreateWorldParams.<CanDoNext>b__20_1` calls `WindowStack.Add(next)`; Map Preview's `Page_SelectStartingSite.PreOpen` postfix throws `TypeInitializationException`; execution never reaches the vanilla page close. |
| `Player.log` lines 1148-1164 | The completed generation cycle and identical exception recur after the second Generate click. |
| Earlier load log | `RerollWorldSeedMP` is requested from the play-load worker, then CA reports Map Preview compatibility failure; Verse later reports the same poisoned toolbar static constructor. |
| Decompiled RimWorld 1.6 | `WorldGenerator.GenerateWorld(...)` assigns `Current.Game.World` before the completion action. The completion action adds the next page, unloads unused assets, regenerates world layers, and only then closes the creation page. An exception from `WindowStack.Add(next)` leaves the generated world assigned and the creation page open. |
| Installed Map Preview 1.12.26 | `MapPreviewToolbar` constructs texture-backed buttons in its static constructor. `ButtonOpenSettings.Icon` reads `OptionCategoryDefOf.General.texPath`. Its `Page_SelectStartingSite.PreOpen` postfix calls `WorldInterfaceManager.RefreshInterface`, which accesses the poisoned toolbar type. |

## Causal boundary

Map Preview owns the throwing postfix and unsafe toolbar static constructor. CA
caused the edge case by reflecting and Harmony-patching that toolbar from
`AwarenessMod` while RimWorld was still on the play-load worker and before DefOf
binding. The caught first failure permanently poisoned the external type for the
rest of the process. Logging the compatibility failure did not contain its later
effect on vanilla page navigation.

## Repair

`CARegionalCompatibility.TryInstallMapPreviewCompatibility` now returns before
any Map Preview type reflection unless it is running on Unity's main thread and
`OptionCategoryDefOf.General` is bound. `ModEntry` already schedules the same
installer with `LongEventHandler.ExecuteWhenFinished`; that safe retry remains
the sole external-type installation point during play-data load.

## Static and build verification

| Gate | Result |
|---|---|
| Release compile | **PASS** - 0 warnings, 0 errors. |
| Regional geography receipt | **PASS** - 11/11, including the initialization-order contract before the first Map Preview type touch. |
| Generation and fixture receipt | **PASS** - 12/12; paired schema-12 fixture remains byte-identical with 3 factions, 4 settlements, and 9 population groups. |
| Deterministic rebuild | **PASS** - two no-incremental Release builds emitted 3,999,232 byte assemblies at SHA-256 `8FE532A0F9A92581000C42F05DA577C7ADA9406D919E157F92B535C3D0399D25`. |

## Deployment

Deployed 2026-08-14 21:17 UTC / 14:17 PDT after confirming that
`RimWorldWin64` was closed. The worktree assembly and installed assembly are
byte-identical:

| Artifact | Bytes | SHA-256 |
|---|---:|---|
| Worktree `Assemblies/ColonistAwareness.dll` | 3,999,232 | `8FE532A0F9A92581000C42F05DA577C7ADA9406D919E157F92B535C3D0399D25` |
| Installed `Mods/ColonistAwareness/Assemblies/ColonistAwareness.dll` | 3,999,232 | `8FE532A0F9A92581000C42F05DA577C7ADA9406D919E157F92B535C3D0399D25` |

The prior 3,998,720-byte assembly was preserved at
`ColonistAwareness.dll.pre-B14-map-preview.7C56DC62E267CD39.20260814-141731.bak`
with SHA-256
`7C56DC62E267CD39FACA64AFDCC378529A53669165B3B58B533A0A81A83CD9A1`.
`RimWorldWin64` remained closed after replacement; Codex did not launch the
game. The next action is the operator's full-restart runtime test.
