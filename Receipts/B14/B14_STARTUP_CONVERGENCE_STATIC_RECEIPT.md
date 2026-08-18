# B14 startup convergence static receipt

Timestamp: 2026-08-17 10:06 UTC / 03:06 PDT

Status correction: the first repair below was superseded for runtime package
closure by the fresh 2026-08-17 10:12 UTC / 03:12 PDT launch. The historical
evidence remains accurate for that repair, but its startup-ready conclusion no
longer governs. The current correction is recorded at the end of this receipt.

## Runtime evidence boundary

The operator's fresh RimWorld process was PID `36604`, started 2026-08-17
02:30:54 PDT against RimWorld `1.6.4871 rev591` and the then-deployed
4,134,400-byte CA assembly at SHA-256
`4BFAF1ED509AF0D3913A55A51779B5D8B85457B3A29CA14096B2FD5FD740AFA7`.
The supplied startup record is preserved at
`C:\Users\jleyv\.codex\attachments\4fa6ebb2-703c-41d8-8349-9637c6dee47b\pasted-text.txt`.
The operator closed RimWorld before repository or deployment mutation. No fresh
runtime acceptance is claimed by this receipt.

## Causal boundary

The installed tree contained Vehicle Framework and Boats Defs but only the CA
assembly. The absent Vehicle Framework runtime assemblies made the XML type
names unresolvable. RimWorld consequently aborted affected `FactionDef` loads,
then reported a broad cross-reference cascade against definitions that had not
been admitted. This was a packaged-runtime defect in CA's self-contained
distribution, not evidence that every downstream definition was independently
missing.

Separate CA-owned defects were also present:

- the native Bad Hygiene port referenced thirst and bladder rate stats that CA
  had not defined or consumed;
- retained Medieval Overhaul texture paths omitted their current namespace;
- Vehicle Framework and Boats sound/graphic paths named absent or incomplete
  assets;
- the aperture base omitted the explicit shot-over configuration required for
  an impassable aperture;
- two stuff-built hygiene structures redundantly declared a construction
  effect.

## Runtime assembly closure

The active shipping tree now contains CA plus the six runtime assemblies that
own the bundled Vehicle Framework Defs.

| Assembly | Bytes | SHA-256 |
|---|---:|---|
| `ColonistAwareness.dll` | 4,134,400 | `344EA9248BC2A286EF9AAB16CB7A2AAE2BDB9ECD85AC69D1EB2B4CAE29E6D3D7` |
| `CoreLib.dll` | 91,648 | `8D28277573C6E4034F2F4FFAA445F36757AC833D76DFC2A67D2BD48542EF0688` |
| `DevTools.dll` | 206,848 | `A63DA6513AFE6FF9D461D7B8568642FE1C97A5CE8998D726F764400F078A4903` |
| `SmashTools.dll` | 542,720 | `3FA198514644E58CF4C23A016B25F55D573DD5612BF4ABFD17DC275E855E5690` |
| `SmashToolsBurst.dll` | 55,808 | `9C51F8E0A45A8BBD68AC6A92874616D966F0DB09E8D46B6E3A21C862CD7FC217` |
| `UpdateLogTool.dll` | 90,112 | `EEE88740C7A3D881F161EFC8D380FF90D3382B9E92CD14A65BAEEEE67E000F23` |
| `Vehicles.dll` | 1,963,520 | `4A06190DBEC31B7B25599142920E2B98632E5BA049D72DA7B9948B23B860A72F` |

The bundled Vehicle Framework assembly identifies product version
`1.6.0+fd5ed722ce214836d4c283832865dfa5966f1e4e`; its SmashTools/CoreLib
runtime identifies `4d0faf5fa8d6f149b858808ab938f47092a9d784`.

All 12 distinct `Vehicles.*` types absent in the fresh startup record now
resolve from `Vehicles.dll`: the eight vehicle caravan/order job givers,
`PatchOperationFeature`, `ReduceExplosionOnWater`,
`ThinkNode_ConditionalVehicle`, and `VehicleRaiderDefModExtension`.

## Owned-reference and asset closure

| Gate | Result | Evidence |
|---|---|---|
| Shipping XML parses | **PASS** | 143 XML documents across `About`, `Defs`, `Languages`, and `Patches`; 0 parse failures. |
| Bad Hygiene rate stats | **PASS** | Two CA-prefixed `StatDef`s own 13 hediff modifier uses; both have explicit runtime consumers. |
| Medieval Overhaul textures | **PASS** | 97 current custom references, 96 unique paths, 0 unresolved texture families. |
| Vehicle/Boats assets | **PASS** | All eight corrected or newly supplied texture/sound targets exist. |
| Vehicle type closure | **PASS** | 12/12 fresh-log missing runtime types resolve; 0 remain absent. |
| Installed Def tree | **PASS** | Worktree and installed junction each expose the same 122 relative Def paths. |

## Build and retained acceptance

Two final no-incremental Release builds completed with 0 warnings and 0 errors
and emitted identical 4,134,400-byte assemblies at SHA-256
`344EA9248BC2A286EF9AAB16CB7A2AAE2BDB9ECD85AC69D1EB2B4CAE29E6D3D7`.
The current receipt suites close at:

- B13 Culture completion: 67/67;
- B14 authoring convergence: 21/21;
- B14 society presets: 22/22 plus rejection and replacement probes;
- B14 regional geography: 12/12;
- B14 generation: 12/12;
- B14 settlement environment: 21/21;
- retained B11 acceptance: 78/78.

`Mods/ColonistAwareness` remains a junction to the active B14 worktree, so the
installed and worktree assembly/Def views are the same verified bytes. RimWorld
remained closed after repair. The next evidence is the operator's fresh startup
and creation-flow test.

## Fresh-launch correction | 2026-08-17 10:40 UTC / 03:40 PST

The operator's next process was PID `11248`, started 2026-08-17 03:12:36 PDT
against RimWorld `1.6.4871 rev591` and the deployed 4,134,400-byte CA assembly
at SHA-256
`344EA9248BC2A286EF9AAB16CB7A2AAE2BDB9ECD85AC69D1EB2B4CAE29E6D3D7`.
The full supplied startup record is preserved at
`C:\Users\jleyv\.codex\attachments\9c9dbe70-0cf1-47c6-8fa3-f9e527f98ae5\pasted-text.txt`.
The prior missing-assembly/type cascade did not recur. The launch instead
exposed the remaining bundled-content boundary:

- CA's three boat `VehicleDef`s used list-shaped vehicle stats, stat-category
  names in component stat slots, and vanilla graphic classes where the current
  Vehicle Framework requires named `vehicleStats`, `VehicleStatDef` component
  categories, and `Vehicles.Graphic_Vehicle`;
- the flattened Vehicle Framework surface omitted 102 of 105 legacy textures,
  all 13 English keyed files, and eight conditionally loaded compatibility
  files;
- six shaders and nine UI textures were present in byte-identical upstream
  bundles but retained `SmashPhil.VehicleFramework` as their internal package
  path, while RimWorld correctly searched under CA's package identity;
- the bundled update handler lacked its required non-notifying runtime record;
- CA's RimWorld metadata omitted the governed `1.4.1.0-alpha` version, leaving
  the bundled framework's package-version log and version checks blank;
- CA touched `PlanetLayerDefOf.Surface` from its mod constructor before DefOf
  initialization.

The active source and content tree now corrects those causes as one bundled
runtime surface. `LoadFolders.xml` owns conditional compatibility loading;
Vehicle Framework's 105 legacy textures, 13 English files, 14 bundle/source
files, and eight active compatibility files are byte-exact against upstream
commit `fd5ed722ce214836d4c283832865dfa5966f1e4e`. Normal shader and texture
lookup retains priority; a CA adapter resolves only otherwise-missing assets
through the bundle's retained upstream identity. The three boat and three
blueprint definitions pass their distinct current graphic/stat contracts, all
166 shipping About, Def, language, patch, compatibility, loader, and update XML
files parse, and all six shader plus nine bundle-only texture paths are present
in their manifests. `About.xml` and `VERSION` now both identify
`1.4.1.0-alpha`. Surface-layer verification uses the Def database directly and
defers cleanly before play data is ready.

Two no-incremental Release staging builds completed with 0 warnings and 0
errors and emitted byte-identical 4,136,960-byte assemblies at SHA-256
`D16FC4F21244EA065FA8C0C61E8D263A28C124C10EEB2352E811BEC2C7782F50`.
The B14 regional suite passed 12/12 against that staged assembly; authoring,
generation, environment, Culture, and retained suites remained 21/21, 12/12,
21/21, 67/67, and 78/78 respectively.

PID `11248` remained responsive during diagnosis. To preserve operator control,
the staged assembly was not copied over the live installed DLL. Worktree and
installed DLL therefore remain the prior byte-identical `344EA924...` build.
Deployment and fresh runtime acceptance of the corrected `D16FC4F2...` build
remain pending until RimWorld is closed.

## Closed-process deployment | 2026-08-17 10:47 UTC / 03:47 PST

PID `11248` closed before deployment. The prior `344EA924...` DLL was preserved
at
`C:\Users\jleyv\AppData\Local\Temp\cao-b14-rollback-344EA9248BC2A286.dll`,
then the verified staged assembly was promoted through the installed worktree
junction. Staged, worktree, and installed views are now byte-identical at
4,136,960 bytes and SHA-256
`D16FC4F21244EA065FA8C0C61E8D263A28C124C10EEB2352E811BEC2C7782F50`.

Assembly-backed verification against the deployed bytes passed: regional
geography 12/12, generation 12/12, settlement environment 21/21, Culture
67/67, and all 22 society presets plus rejection and replacement probes. No
RimWorld process was launched by Codex. Static/build/deployment closure is
therefore current; fresh startup and gameplay acceptance remain the operator's
next evidence.

## Bundle-identity correction and deployment | 2026-08-17 20:41 UTC / 13:41 PST

Fresh PIDs `34064` and `5084` both loaded the deployed `D16FC4F2...` assembly.
Their startup records established that the per-type Harmony fallbacks on
`ContentFinder<Shader>` and `ContentFinder<Texture2D>` were not a valid bundle
boundary: Mono shares the generic resolver implementation, so the fallback
interfered with RimWorld's official bundled `AudioClip` lookup while still
leaving the six Vehicle Framework shaders unresolved. The repeated audio and
shader cascades were one resolver conflict, not separate missing-content
families.

The generic resolver patches are removed. CA now leaves `ContentFinder<T>`
untouched and aliases identity once at `AssetBundle.LoadAsset(string, Type)`:
only a failed request against a CA-owned vendored bundle is retried with the
same relative asset path under the bundle's retained
`SmashPhil.VehicleFramework` identity. Normal CA, base-game, DLC, and unrelated
mod bundle lookups remain unchanged.

Two no-incremental Release builds completed with 0 warnings and 0 errors and
emitted byte-identical 4,135,936-byte assemblies at SHA-256
`3A39702250A3E3C5EAEDACF786BB2D2F7E8D5ABAC6AF5A4D8163229BCCA7A7B2`.
After PID `5084` closed, the prior `D16FC4F2...` DLL was preserved at
`C:\Users\jleyv\AppData\Local\Temp\cao-b14-rollback-D16FC4F21244EA06.dll`
and the verified assembly was promoted through the installed worktree
junction. Staged, worktree, and installed files are byte-identical.

Assembly-backed verification against the deployed bytes passed: regional
geography 12/12, generation 12/12, authoring convergence 21/21, settlement
environment 21/21, Culture 67/67, retained B11 78/78, and all 22 society
presets plus rejection and replacement probes. All 166 shipping XML files
remain parseable. RimWorld remains closed; a fresh startup is the remaining
runtime evidence.

## Fresh need initialization failure and staged correction | 2026-08-17 20:58 UTC / 13:58 PST

Fresh PID `16084`, started 2026-08-17 13:45:34 PDT, loaded the deployed
4,135,936-byte `3A397022...` assembly. The supplied run is preserved at
`C:\Users\jleyv\.codex\attachments\0e27cf66-ba3d-4b69-b1ee-e45ab1ff8ca1\pasted-text.txt`.
It proves the bundle-identity repair at runtime: the retained Vehicle
Framework shaders and textures resolve through the upstream bundle identity,
with no unresolved cross-references, asset-load failures, toolbar exception,
or null-texture flood. World generation completes, `Page_SelectStartingSite`
opens, the governed 3-faction/4-settlement plan restores, and the regional
preview renders.

The run exposes one later CA-owned failure. Pawn generation reports 195
construction failures for each of `CA_Thirst`, `CA_Hygiene`, and
`CA_Bladder`. Every failure originates at
`CANeed_Body.SetInitialLevel()` reading `def.baseLevel`. RimWorld's `Need`
constructor calls the virtual method before `Pawn_NeedsTracker.AddNeed`
assigns the `NeedDef`, then deliberately calls it again after assignment.
CA now returns from the pre-assignment call and performs the existing
`baseLevel` initialization on the tracker's post-assignment call. The fix is
at the shared need owner; no exception suppression or per-need patch was
added.

Two isolated no-incremental Release builds completed with 0 warnings and 0
errors and emitted byte-identical 4,135,936-byte assemblies at SHA-256
`1613157FA9E6E6597045F882BD2D3076FA7B3E21C2025FB8134BAFF01F064F82`.
PID `16084` remains responsive, so those bytes are staged only. Worktree and
installed assembly remain byte-identical at `3A397022...`; deployment and a
fresh runtime confirmation of body-need construction wait for the operator to
close RimWorld.
