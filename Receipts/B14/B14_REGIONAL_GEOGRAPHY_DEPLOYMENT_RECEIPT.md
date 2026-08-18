# B14 deployment receipt

Timestamp: 2026-08-17 10:06 UTC / 03:06 PDT

Status note: this receipt retains the exact first closed-process deployment,
the fresh-launch correction that superseded its startup-ready conclusion, and
the final corrected deployment. The latest dated section is the current state.

| Gate | Result | Evidence |
|---|---|---|
| Process boundary | **PASS** | `RimWorldWin64` was absent immediately before the installed junction changed and remained absent throughout deployment verification. Codex did not launch the game afterward. |
| Release build | **PASS** | Two `dotnet build Source/ColonistAwareness.csproj -c Release --no-incremental` runs each completed with 0 warnings and 0 errors. |
| Reproducible assembly | **PASS** | Both final builds emitted 4,134,400 byte-identical bytes at SHA-256 `344EA9248BC2A286EF9AAB16CB7A2AAE2BDB9ECD85AC69D1EB2B4CAE29E6D3D7`. |
| Governed fixture | **PASS** | Active and mirror schema-13 files are byte-identical at 262,563 bytes and SHA-256 `4098228D6ABE61171BDCD05EE6B309D8C320A5906C6DECE62E509346EE3B51A4`; identity, geography, 3 factions, 4 settlements, and 9 population groups remain intact. |
| Authoring convergence | **PASS** | `B14_AUTHORING_CONVERGENCE_STATIC_RECEIPT.md` records 21/21 passing checks against the final source and fixture. |
| Society preset execution | **PASS** | `B14_SOCIETY_PRESET_EXECUTION_RECEIPT.md` records all 22 coordinated presets applying and surviving copy/readback; invalid application leaves state unchanged and replacement changes both Culture and Political Order. |
| Culture acceptance | **PASS** | `tools/B13CultureCompletionReceipts` passed 67/67 against the same schema-13 fixture. |
| Regional geography | **PASS** | `B14_REGIONAL_GEOGRAPHY_STATIC_RECEIPT.md` records 12/12 passing contracts against the final assembly and installed Map Preview 1.6 API. |
| Generation | **PASS** | `B14_GENERATION_STATIC_RECEIPT.md` records 12/12 passing contracts against the paired fixture, live RimWorld Defs, decompiled engine path, and final assembly. |
| Settlement environment and autonomous construction | **PASS** | `B14_SETTLEMENT_ENVIRONMENT_STATIC_RECEIPT.md` records 21/21 passing causal checks. Exact environment derives habitat requirements; established-settlement potential and frontier material proof remain distinct; one task-owned evidence vector ranks viable autonomous construction without style modes or copied layouts. |
| Retained acceptance | **PASS** | `tools/B11AcceptanceReceipts` passed 78/78; the census identifies 252 persistence carriers, 86 catalog schemas, 4 explicit non-campaign exclusions, and 0 invalid routes. |
| Startup convergence | **PASS (static/build/deployment)** | `B14_STARTUP_CONVERGENCE_STATIC_RECEIPT.md` records the fresh startup boundary, restores the six assemblies that own bundled Vehicle Framework Defs, closes CA's Bad Hygiene rate-stat references, and verifies retained asset paths. Runtime acceptance remains the operator's next test. |
| Installed mod target | **PASS** | `Mods/ColonistAwareness` is a junction to the complete active B14 worktree `.claude/worktrees/rimworld-regional-multithreading-47e9ec`. Its former target, the stale parent checkout, remains present and untouched but is no longer the installed mod. |
| Whole-tree identity | **PASS** | Worktree and installed views each expose the same 122 relative Def paths with 0 differences. Obsolete `Defs/PoliticalDefs/TerritorialBands_CA.xml` is absent; current relation-role and native Bad Hygiene integration Defs are visible. |
| Deployment identity | **PASS** | Worktree and installed views contain the same seven assemblies: CA at 4,134,400 bytes and SHA-256 `344EA9248BC2A286EF9AAB16CB7A2AAE2BDB9ECD85AC69D1EB2B4CAE29E6D3D7`, plus byte-verified `CoreLib`, `DevTools`, `SmashTools`, `SmashToolsBurst`, `UpdateLogTool`, and `Vehicles`. |
| Prior deployment preservation | **PASS** | The former junction target and its rollback assemblies remain on disk. Retargeting removed only the installed junction and recreated it against the active worktree; it did not delete or rewrite either checkout. |

Static and deployment evidence do not establish visual or gameplay acceptance.
The next evidence is the operator's fresh runtime test.

## Post-deployment correction | 2026-08-17 10:40 UTC / 03:40 PST

Fresh PID `11248` loaded the assembly recorded above and demonstrated that the
missing runtime assemblies were fixed, then exposed incomplete Vehicle
Framework content and CA boat contracts. Those sources and assets are corrected
and two clean staging builds emitted the same 4,136,960-byte Release assembly at SHA-256
`D16FC4F21244EA065FA8C0C61E8D263A28C124C10EEB2352E811BEC2C7782F50`.
The running process still owns the prior 4,134,400-byte `344EA924...` assembly,
which remains byte-identical between the worktree and installed junction.
Accordingly this receipt does not claim deployment of `D16FC4F2...`; a closed-
process deployment and fresh operator launch are the next evidence boundary.

## Corrected deployment completion | 2026-08-17 10:47 UTC / 03:47 PST

PID `11248` closed before mutation. The prior `344EA924...` DLL was preserved
in the local temporary rollback file named in the startup convergence receipt,
and the verified `D16FC4F2...` assembly was promoted. Staged, worktree, and
installed junction views are byte-identical at 4,136,960 bytes and SHA-256
`D16FC4F21244EA065FA8C0C61E8D263A28C124C10EEB2352E811BEC2C7782F50`.
Regional geography passed 12/12 against those deployed bytes; generation,
settlement environment, Culture, and society-preset execution also passed.
RimWorld remains closed for the operator's fresh startup test.

## Bundle-identity deployment | 2026-08-17 20:41 UTC / 13:41 PST

PIDs `34064` and `5084` loaded `D16FC4F2...` and proved that its generic
`ContentFinder<T>` bundle fallback disturbed the shared official-audio resolver.
That adapter is removed. One scoped, non-generic bundle-identity alias now owns
the vendored Vehicle Framework boundary without changing native content lookup.

After PID `5084` closed, the prior DLL was preserved at
`C:\Users\jleyv\AppData\Local\Temp\cao-b14-rollback-D16FC4F21244EA06.dll`.
The final staged, worktree, and installed junction views are byte-identical at
4,135,936 bytes and SHA-256
`3A39702250A3E3C5EAEDACF786BB2D2F7E8D5ABAC6AF5A4D8163229BCCA7A7B2`.
Regional geography passed 12/12, generation 12/12, authoring convergence
21/21, settlement environment 21/21, Culture 67/67, retained B11 78/78, and
all 22 society-preset execution probes passed against the deployed assembly.
RimWorld remains closed for the operator's clean startup test.

## Society preset convergence deployment | 2026-08-17 23:25 UTC / 16:25 PDT

RimWorld was absent before final verification and remained absent. The installed
`Mods/ColonistAwareness` junction still targets this active B14 worktree, so the
verified worktree assembly is also the installed assembly; the game was not
launched.

Two clean `Release` builds using `--no-restore --no-incremental` each completed
with 0 warnings and 0 errors and emitted byte-identical 4,148,224-byte assemblies
at SHA-256
`0EC258CA223CE830B7448D7E8E5CCB88DBDF4C1B2C8D996F0463BFC24834DD0C`.
The staged, worktree, and installed DLL views agree at that identity.

The current schema-13 keyed runtime plan and governed mirror are byte-identical
at 397,275 bytes and SHA-256
`6AE604BB8157FC5F12C58932A2476700A336F3C057F11D85441D09BFF151EF04`.
They preserve the current world, region, candidate, arrival area, and map scale,
with 3 factions, 4 settlements, 4 current population assignments, 19 settlement
program facts, 4 complete Political Orders, and 8 represented Cultures.

Society preset execution passed for all 22 built-in Society presets. Each owns a
separate identity and frozen complete 26-question Political Order snapshot,
copies Culture and Political Order together into canonical faction state, and
does not leave preset ownership behind. The receipt also proves full-causal
Culture matching, invalid-input and exceptional-commit rollback, component
isolation, and actual Scribe serialization/readback for a saved Society snapshot.

Final proportional verification passed: authoring convergence 24/24, Culture
completion 67/67, generation 12/12, settlement environment 21/21, regional
geography 12/12, retained B11 acceptance 78/78, and persistence census 253
carriers / 86 schemas / 5 explicit non-campaign exclusions / 0 invalid routes.
The reviewer and coherence follow-up passes found no remaining Critical or High
defects in the Society convergence.

This establishes source, persistence, build, and deployment readiness. It does
not claim visual or gameplay acceptance; the next boundary is the operator's
fresh RimWorld runtime test.

## Governed B14 closure deployment | 2026-08-17 23:58 UTC / 16:58 PDT

RimWorld was absent throughout final verification. The installed
`Mods/ColonistAwareness` junction remains pointed at the active B14 worktree.
Two clean, no-incremental Release builds completed with 0 warnings and 0 errors
and emitted byte-identical 4,149,248-byte assemblies at SHA-256
`A5A3D89A516B08156D0163E1F49D8DEC97FCFFED799B60531715DB9C9D495FC9`.
The worktree and installed DLL views are byte-identical at that identity.

The current schema-13 runtime plan and governed mirror remain byte-identical at
397,275 bytes and SHA-256
`6AE604BB8157FC5F12C58932A2476700A336F3C057F11D85441D09BFF151EF04`.
They retain the selected world and regional identity, 3 factions, 4
settlements, 4 current population assignments, 19 program facts, 4 complete
Political Orders, and 8 Culture records.

Final verification passed: Society preset execution across all 22 built-ins,
atomic rejection and rollback, component isolation, and Scribe readback;
authoring convergence 24/24; regional geography 12/12; generation 12/12;
settlement environment 21/21; retained Culture completion 67/67; retained B11
acceptance 78/78; and persistence census 253 carriers / 86 schemas / 5 explicit
non-campaign exclusions / 0 invalid routes. Neo's floor and ceiling checks
passed and classified the aggregate B14 change as a minor capability.

This supersedes the earlier B14 assembly identities while preserving them as
chronological deployment evidence. It establishes the closed source, fixture,
build, and installed-tree boundary only. Operator visual and gameplay
acceptance remain the next evidence.
