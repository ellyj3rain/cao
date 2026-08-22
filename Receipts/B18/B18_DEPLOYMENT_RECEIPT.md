# B18 Deployment Receipt

Timestamp: 2026-08-19 19:06 UTC / 12:06 PDT

| Fact | Value |
|---|---|
| Installed mod path | `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\ColonistAwareness` |
| Link kind | NTFS junction resolving to this worktree, so the tracked `Assemblies\ColonistAwareness.dll` and the installed assembly are one file identity |
| Deployed assembly | `Assemblies\ColonistAwareness.dll` |
| SHA-256 | `0F8B16C2C862372211BD52F4D62D477923D9263A570566619C96836BA365E50D` |
| Size | 4,525,056 bytes |
| Identity check | Fresh SHA-256 of the installed path equals the verified reproducible build (see `B18_REPRODUCIBLE_BUILD_RECEIPT.md`) |
| Game state | The deployment copy was made with no RimWorld process running |

The shipped assembly differs from the build the final 400x6 evidence runs
executed (`799AB941...`, `Performance/target-400x6-final-run1` and the
passive final) only in combat-topology capture code outside the generation
path; the deployed identity above is the verified reproducible candidate.
