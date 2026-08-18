# B16 Reproducible Build Receipt

Timestamp: 2026-08-18 13:48 UTC / 2026-08-18 06:48 PDT

| Gate | Result | Evidence |
|---|---|---|
| Build 1 | **PASS** | Project-native `dotnet clean` followed by `dotnet build Source/ColonistAwareness.csproj -c Release -t:Rebuild --no-restore` to an isolated output; 0 warnings, 0 errors |
| Build 2 | **PASS** | A second project-native clean and Release rebuild to a separate isolated output; 0 warnings, 0 errors |
| Reproducibility | **PASS** | Both assemblies are byte-identical |
| Assembly size | **PASS** | 4,369,920 bytes |
| SHA-256 | **PASS** | `EF56D9BADAAF82DB5E8269A3996F2F56A565BC8E564DB9A970C97F5B72C94342` |
| B16 executable suite | **PASS** | The final assembly passed all 54 B16 semantic, occurrence, emitter, migration, rollback, and fixture checks |
| Retained suite | **PASS** | B10-B15 executable contracts pass against the current fixture and final assembly |
| Result | **PASS** | The B16 Release assembly is reproducible and verified for deployment |

The build receipt proves compiler output and executable checks. Runtime behavior
and presentation remain the operator's next evidence boundary.

Warnings: **0**

Errors: **0**

Result: **PASS**
