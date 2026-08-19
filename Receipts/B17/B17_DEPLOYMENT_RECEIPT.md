# B17 Deployment Receipt

Timestamp: 2026-08-19 00:10 UTC / 17:10 PST

| Gate | Result | Evidence |
|---|---|---|
| Runtime process | **PASS** | `RimWorldWin64` and `UnityCrashHandler` were not running during deployment verification |
| Worktree candidate | **PASS** | `Assemblies/ColonistAwareness.dll`; 4,461,056 bytes; SHA-256 `A62E23F78CE97831575D9E5C5FCC90B80D721D8212724D897FB11B82AF68C218` |
| Installed topology | **PASS** | The installed `ColonistAwareness` mod root is a junction to the active B17 worktree; the installed assembly view resolves to the tracked worktree assembly rather than a second divergent copy |
| Installed assembly | **PASS** | `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\ColonistAwareness\Assemblies\ColonistAwareness.dll`; 4,461,056 bytes; identical SHA-256 |
| Byte identity | **PASS** | Worktree candidate and installed runtime view are the same verified file identity |
| Operator boundary | **PASS** | RimWorld was not launched or manipulated after deployment verification |
| Result | **PASS** | B17 is deployed and ready for the operator runtime test |

The junction topology is intentional: promoting the tracked assembly promotes
the installed runtime view atomically, so no separate deployment copy is kept.
