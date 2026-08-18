# B15 Reproducible Build Receipt

Timestamp: 2026-08-18 06:27 UTC / 2026-08-17 23:27 PDT

| Gate | Result | Evidence |
|---|---|---|
| Build 1 | **PASS** | Project-native `dotnet clean` followed by `dotnet build Source/ColonistAwareness.csproj -c Release -t:Rebuild` to an isolated output; 0 warnings, 0 errors |
| Build 2 | **PASS** | A second project-native clean and Release rebuild to a separate isolated output; 0 warnings, 0 errors |
| Reproducibility | **PASS** | Both assemblies are byte-identical |
| Assembly size | **PASS** | 4,233,216 bytes |
| SHA-256 | **PASS** | `DE6312FF9CF2F4B052AACDE82E93C113AF231D36494F93667266FB2D415F0764` |
| B15 executable suite | **PASS** | The final assembly passed all B15 Society, knowledge, distribution, exact mapping, consumer, persistence, and fixture checks |
| Result | **PASS** | The B15 Release assembly is reproducible and verified for deployment |

The build receipt proves compiler output and executable checks. Runtime behavior
and presentation remain the operator's next evidence boundary.

Warnings: **0**

Errors: **0**

Result: **PASS**
