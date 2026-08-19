# B17 Reproducible Build Receipt

Timestamp: 2026-08-19 00:10 UTC / 17:10 PST

| Gate | Result | Evidence |
|---|---|---|
| Compiler baseline | **PASS** | Repository-pinned .NET SDK `8.0.423`; production references resolve from the installed RimWorld 1.6 managed assemblies |
| Clean build 1 | **PASS** | Clean Release rebuild in an isolated output; 0 warnings and 0 errors |
| Clean build 2 | **PASS** | Second clean Release rebuild in a separate isolated output; 0 warnings and 0 errors |
| Reproducibility | **PASS** | Both clean candidates are byte-identical |
| Production assembly | **PASS** | 4,461,056 bytes; SHA-256 `A62E23F78CE97831575D9E5C5FCC90B80D721D8212724D897FB11B82AF68C218` |
| Tracked assembly | **PASS** | `Assemblies/ColonistAwareness.dll` is byte-identical to both clean candidates |
| Support projects | **PASS** | All 20 declared executable support projects compile with warnings treated as errors |
| Portable CI | **PASS** | Two-build reproducibility, tracked-assembly identity, support-project compilation, generated B10 synthetic-state sweep, and persistence census complete without drift |
| Repository verification | **PASS** | Version replay, 25 tracked C# projects, 20 executable support projects, and locked production dependencies agree |
| Result | **PASS** | The B17 Release assembly is reproducible and verified for deployment |

The isolated final-build root was
`C:\Users\jleyv\AppData\Local\Temp\cao-b17-final-154dfe540c264da6b4d152c4374ea3e6`.
Build evidence does not establish operator visual or gameplay acceptance.

Warnings: **0**

Errors: **0**

Result: **PASS**
